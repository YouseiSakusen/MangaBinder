using System.Threading.Channels;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 製本前確認工程の入口を統括するマネージャーです。
/// BindingStore から製本状態を取得し、Work側フォルダを準備した後、
/// 素材展開対象のボリュームを Material.SourcePath 単位でグループ化し、
/// 素材形式ごとの IMaterialExtractor へ渡します。
/// </summary>
public class SeriesInspectionManager
{
	private readonly BindingStore bindingStore;
	private readonly AppSettings appSettings;
	private readonly IServiceScopeFactory serviceScopeFactory;
	private readonly ILogger<SeriesInspectionManager> logger;
	private readonly BindingManager bindingManager;

	/// <summary>
	/// 1つの BindingVolume の Inspect() が完了したことを通知するイベント。
	/// UI / ViewModel 側で、この巻の最終検査結果を画面へ反映するためのトリガーとして使用できます。
	/// 通知されるのは BindingVolume の同一インスタンスです。DTOやコピーは作成されません。
	/// 通知は Inspect() が同期的に完了した直後に発生します。
	/// </summary>
	public event Action<BindingVolume>? VolumeInspected;

	/// <summary>
	/// <see cref="SeriesInspectionManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingStore">製本工程の正本状態ストア。</param>
	/// <param name="appSettings">アプリケーション設定。Workフォルダパス生成に使用します。</param>
	/// <param name="serviceScopeFactory">DI スコープを作成するファクトリー。Extractor と VolumeFileNameNormalizer の解決に使用します。</param>
	/// <param name="logger">ロガー。</param>
	/// <param name="bindingManager">製本工程の共通処理マネージャー。</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="bindingStore"/>, <paramref name="appSettings"/>, <paramref name="serviceScopeFactory"/>, <paramref name="logger"/>, または <paramref name="bindingManager"/> が null の場合。
	/// </exception>
	public SeriesInspectionManager(
		BindingStore bindingStore,
		AppSettings appSettings,
		IServiceScopeFactory serviceScopeFactory,
		ILogger<SeriesInspectionManager> logger,
		BindingManager bindingManager)
	{
		this.bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
		this.appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
		this.serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
		this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
		this.bindingManager = bindingManager ?? throw new ArgumentNullException(nameof(bindingManager));
	}

	/// <summary>
	/// 製本前確認処理の入口を実行します。
	/// BindingStore から製本状態を取得し、Work側フォルダを準備した後、
	/// 素材展開対象のボリュームを Material.SourcePath 単位でグループ化します。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	/// <exception cref="OperationCanceledException">キャンセルトークンがキャンセルされた場合。</exception>
	/// <exception cref="InvalidOperationException">BindingTarget が設定されていない、または BindingVolume の VolumeNumber が null の場合。</exception>
	public async ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
	{
		// ① CancellationToken のキャンセル要求を確認する
		cancellationToken.ThrowIfCancellationRequested();

		// Linked CancellationTokenSource を作成
		// 外部から渡された cancellationToken と、Producer/Consumer パイプライン内部の
		// linkedCancellationToken を統合し、相互の異常終了で停止信号を伝播できる構造にする
		using var linkedCancellationTokenSource =
			CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var pipelineCancellationToken = linkedCancellationTokenSource.Token;

		// ② BindingStore.BindingTarget から対象作品を取得する
		var bindingTarget = this.bindingStore.BindingTarget.Value;
		if (bindingTarget is null)
		{
			throw new InvalidOperationException(
				"製本前確認処理には BindingStore.BindingTarget が設定されている必要があります。");
		}

		// ③ 巻選択工程を完了し、製本前確認工程へ到達したことを記録
		this.bindingStore.VolumeSelectionCompleted.Value = true;

		// ④ 製本前確認工程の処理が既に完了している場合は再実行せずに終了
		if (this.bindingStore.SeriesInspectionCompleted.Value)
		{
			// 既存の処理結果を保持したまま、正常終了
			return;
		}

		// ⑤ ZIP ファイル名の初期生成を実行
		this.bindingManager.InitializeZipOutputFileName();

		// ⑥ AppSettings.CreateWorkSeriesFolderPath() を使用して、対象作品の作品Workフォルダパスを取得する
		var seriesFolderPath = this.appSettings.CreateWorkSeriesFolderPath(bindingTarget.Series.Title);

		// ⑤ BindingStore.ImageExpansionMethod が Recreate の場合、作品Workフォルダが存在すれば削除する
		// ファイルシステムの重い処理でUIスレッドをブロックしないようにする
		if (this.bindingStore.ImageExpansionMethod.Value == global::MangaBinder.Bindings.ImageExpansionMethod.Recreate
			&& Directory.Exists(seriesFolderPath))
		{
			await Task.Run(
				() => Directory.Delete(seriesFolderPath, recursive: true),
				cancellationToken).ConfigureAwait(false);
		}

		// ⑥ 作品Workフォルダが存在しない場合を含め、以降の処理で使用できるよう作品Workフォルダを作成する
		Directory.CreateDirectory(seriesFolderPath);

		// ⑦ 作品Workフォルダ直下に既に存在するフォルダの一覧を取得
		// 各BindingVolumeの既存Work巻フォルダ判定に使用するため、フォルダ名の集合を構築する
		// StringComparer.OrdinalIgnoreCase で大文字小文字非依存に比較できる集合とする
		var existingVolumeFolderNames = new HashSet<string>(
			Directory.GetDirectories(seriesFolderPath)
				.Select(path => Path.GetFileName(path)),
			StringComparer.OrdinalIgnoreCase);

		// ⑧ BindingStore.BindingVolumes を順番に処理して WorkFolderPath を設定し、
		// 既存巻フォルダの判定と作成を行う
		// 全 BindingVolume を製本前確認工程の対象とする
		var volumeFolderDigits = this.bindingStore.VolumeFolderDigits.Value;

		foreach (var bindingVolume in this.bindingStore.BindingVolumes)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// VolumeNumber が null の場合は不正
			var volumeNumber = bindingVolume.VolumeNumber.Value;
			if (volumeNumber is null)
			{
				throw new InvalidOperationException(
					$"BindingVolume (Material: {bindingVolume.Material.Name}) の VolumeNumber が設定されていません。" +
					"巻選択工程を通過した状態として不正です。");
			}

			// 巻フォルダ名を生成
			var volumeFolderName = this.appSettings.CreateWorkVolumeFolderName(
				volumeNumber.Value,
				volumeFolderDigits);

			// 巻WorkフォルダのフルパスをBindingVolumeに設定
			bindingVolume.WorkFolderPath = Path.Combine(seriesFolderPath, volumeFolderName);

			// 巻フォルダが既存フォルダ一覧に存在するか判定
			var workFolderExists = existingVolumeFolderNames.Contains(volumeFolderName);

			// UseOriginalMaterial を毎回明示的に設定
			// 既存巻フォルダが存在する場合は元素材を使用しない
			// 存在しない場合は元素材を使用する
			bindingVolume.Material.UseOriginalMaterial = !workFolderExists;

			// Work巻フォルダが存在しない場合は新規作成
			if (!workFolderExists)
			{
				Directory.CreateDirectory(bindingVolume.WorkFolderPath);
			}
		}

		// ⑨ 全 BindingVolume を EffectiveSourcePath でGroupBy する
		// 文字列比較には StringComparer.OrdinalIgnoreCase を使用
		var groupsBySourcePath = this.bindingStore.BindingVolumes
			.GroupBy(v => v.EffectiveSourcePath, StringComparer.OrdinalIgnoreCase)
			.ToList();

		// ⑩ System.Threading.Channels を使用した Producer/Consumer パターンを構築
		// 異常終了時にProducer/Consumer間で相互停止信号を伝播させる構造

		// Bounded Channel を作成：容量32、FullMode=Wait、SingleWriter/Reader=false
		var channelOptions = new BoundedChannelOptions(32)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = false,
			SingleWriter = false,
		};
		var channel = Channel.CreateBounded<BindingImage>(channelOptions);
		var reader = channel.Reader;
		var writer = channel.Writer;

		// 4本の画像処理 Worker Task を起動
		var workerTasks = new List<Task>();
		for (int i = 0; i < 4; i++)
		{
			workerTasks.Add(RunImageProcessorWorker(reader, linkedCancellationTokenSource, pipelineCancellationToken));
		}

		// Producer Task を起動
		var producerTask = RunProducerAsync(
			groupsBySourcePath,
			writer,
			linkedCancellationTokenSource,
			pipelineCancellationToken);

		// すべてのタスク（Producer + 4 Worker）を同時に待機
		var allTasks = new List<Task> { producerTask };
		allTasks.AddRange(workerTasks);

		try
		{
			await Task.WhenAll(allTasks).ConfigureAwait(false);
		}
		finally
		{
			// キャンセルまたは例外によって Channel 内に未処理 BindingImage が残っている場合、
			// Channel に残った未読み込み BindingImage のみをdrain してクリーンアップ
			// (Workerが処理中の画像は Worker の finally で既に Dispose されている)
			while (reader.TryRead(out var remainingImage))
			{
				remainingImage.TemporaryImageStream?.Dispose();
				remainingImage.TemporaryImageStream = null;
			}

			// EPUB の一時展開フォルダをクリーンアップ
			// Producer / Worker がすべて完了した後に実行
			// すでにキャンセル状態でも cleanup を試みる（best-effort）
			await this.CleanupEpubTemporaryFoldersAsync();
		}

		// ⑪ ファイル名正規化処理を実行
		// EPUB エラー巻（Material.EffectiveSourceType == MaterialSourceType.Epub かつ EpubExtractionError != None）は除外
		var normalizationTargets = this.bindingStore.BindingVolumes
			.Where(v => !(v.Material.EffectiveSourceType == MaterialSourceType.Epub && v.EpubExtractionError != EpubExtractionError.None))
			.ToList();

		await this.ExecuteFileNameNormalizationAsync(normalizationTargets, pipelineCancellationToken)
			.ConfigureAwait(false);

		// ⑫ EPUB エラー巻について、後段処理前に検査を実行
		var epubErrorVolumes = this.bindingStore.BindingVolumes
			.Where(v => v.Material.EffectiveSourceType == MaterialSourceType.Epub && v.EpubExtractionError != EpubExtractionError.None)
			.ToList();

		foreach (var volume in epubErrorVolumes)
		{
			volume.Inspect();

			// Inspect() が完了したことを通知
			this.VolumeInspected?.Invoke(volume);
		}

		// 画像処理パイプライン完了後、TemporaryImageStream 由来の一時 Managed オブジェクトの
		// 回収を促すため、GC を1回だけ明示実行
		GC.Collect(
			GC.MaxGeneration,
			GCCollectionMode.Forced,
			blocking: true,
			compacting: false);

		// ⑬ 製本前確認工程の処理がすべて正常終了したことを記録
		this.bindingStore.SeriesInspectionCompleted.Value = true;
	}

	/// <summary>
	/// 複数 BindingVolume のファイル名正規化を最大4巻並列で実行します。
	/// 各巻の処理単位でスコープを作成し、Scoped なサービスのライフサイクルを管理します。
	/// </summary>
	/// <param name="volumes">正規化対象の BindingVolume リスト。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	private async ValueTask ExecuteFileNameNormalizationAsync(List<BindingVolume> volumes, CancellationToken cancellationToken)
	{
		if (volumes.Count == 0)
			return;

		// 最大4巻並列でファイル名正規化を実行
		await Parallel.ForEachAsync(
			volumes,
			new ParallelOptions
			{
				MaxDegreeOfParallelism = 4,
				CancellationToken = cancellationToken,
			},
			async (volume, ct) =>
			{
				// 1巻の処理単位でスコープを作成
				using var scope = this.serviceScopeFactory.CreateScope();

				// スコープから VolumeFileNameNormalizer を解決
				var normalizer = scope.ServiceProvider.GetRequiredService<VolumeFileNameNormalizer>();

				// 個別 BindingVolume の正規化処理を実行
				// NormalizeAsync は、IOException / UnauthorizedAccessException を通常結果として
				// BindingImage の状態へ保持し、想定外例外のみ伝播させる設計
				await normalizer.NormalizeAsync(volume, ct).ConfigureAwait(false);

				// 正規化完了後、巻の検査を実行して集計結果を保持
				volume.Inspect();

				// Inspect() が完了したことを通知
				this.VolumeInspected?.Invoke(volume);

				// スコープはusingで自動破棄
			}).ConfigureAwait(false);
	}

	/// <summary>
	/// 素材グループの展開・Simulation・Conflict判定・Channel投入を実行する Producer Task です。
	/// </summary>
	/// <param name="groupsByEffectiveSourcePath">EffectiveSourcePath でグループ化された BindingVolume 集合。</param>
	/// <param name="writer">Channel.Writer。</param>
	/// <param name="linkedCancellationTokenSource">Pipeline全体のLinked CancellationTokenSource。</param>
	/// <param name="pipelineCancellationToken">Pipeline用のキャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	private async Task RunProducerAsync(
		List<IGrouping<string, BindingVolume>> groupsByEffectiveSourcePath,
		ChannelWriter<BindingImage> writer,
		CancellationTokenSource linkedCancellationTokenSource,
		CancellationToken pipelineCancellationToken)
	{
		try
		{
			// 素材グループを最大4並列で処理する
			var parallelOptions = new ParallelOptions
			{
				MaxDegreeOfParallelism = 4,
				CancellationToken = pipelineCancellationToken,
			};

			await Parallel.ForEachAsync(
				groupsByEffectiveSourcePath,
				parallelOptions,
				async (group, ct) =>
				{
					// グループ内の最初のボリュームから入力元種別を取得
					var firstVolume = group.First();
					var sourceType = firstVolume.Material.EffectiveSourceType;

					// DI Scope を作成し、入力元種別に対応する IMaterialExtractor を解決
					using (var scope = this.serviceScopeFactory.CreateScope())
					{
						var extractor = scope.ServiceProvider.GetRequiredKeyedService<IMaterialExtractor>(sourceType);

						// 第1段階：素材グループを解析して BindingImage を生成
						await extractor.PrepareAsync(group, ct).ConfigureAwait(false);

						// PrepareAsync 完了直後に、同じ素材グループ内の BindingVolume に対して Simulation を実行する
						var bindingImageProcessor = scope.ServiceProvider.GetRequiredService<BindingImageProcessor>();

						// 素材グループの各 BindingVolume に対して処理を実行
						foreach (var volume in group)
						{
							ct.ThrowIfCancellationRequested();

							// EPUB エラー巻は後段処理をスキップ
							var isEpubError = volume.Material.EffectiveSourceType == MaterialSourceType.Epub
								&& volume.EpubExtractionError != EpubExtractionError.None;

							if (isEpubError)
							{
								// EPUB エラー巻は Simulate / Conflict判定 / Channel投入をスキップ
								continue;
							}

							// 巻単位の状態を初期化
							volume.HasImageFileNameConflict = false;

							// 各 BindingImage の処理状態を初期化
							foreach (var image in volume.Images)
							{
								image.SkipImageProcessing = false;
							}

							// 各 BindingImage を Simulate
							foreach (var image in volume.Images)
							{
								ct.ThrowIfCancellationRequested();
								bindingImageProcessor.Simulate(image);
							}

							// 巻単位のファイル名競合判定
							CheckFileNameConflicts(volume);

							// ファイル名競合判定完了後、該当 BindingVolume の全 BindingImage を Channel へ投入
							// ToArray() で snapshot を取得
							var images = volume.Images.ToArray();

							foreach (var image in images)
							{
								ct.ThrowIfCancellationRequested();

								// 第2段階：1つの BindingImage を処理可能な状態へ準備
								// (Archive の場合のみ MemoryStream 化。Folder/EPUB は no-op)
								await extractor.PrepareImageAsync(image, ct).ConfigureAwait(false);

								try
								{
									// Channel へ投入
									await writer.WriteAsync(image, ct).ConfigureAwait(false);
								}
								catch
								{
									// WriteAsync 失敗時は TemporaryImageStream を明示的に解放
									image.TemporaryImageStream?.Dispose();
									image.TemporaryImageStream = null;
									throw;
								}
							}
						}
					}
				}).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// Producer 処理で例外が発生した場合、
			// LinkedCancellationTokenSource を Cancel して Worker に停止信号を伝播
			linkedCancellationTokenSource.Cancel();
			throw;
		}
		finally
		{
			// Producer 処理の正常終了・異常終了・キャンセルにかかわらず、
			// Channel.Writer を完了させて Workerの読み込み完了を通知
			writer.TryComplete();
		}
	}

	/// <summary>
	/// 画像処理 Worker タスクを実行します。
	/// Channel.Reader から BindingImage を順次取得し、
	/// 独立した DI Scope 内の BindingImageProcessor で ProcessAsync() を実行します。
	/// 
	/// 想定外例外が発生した場合、LinkedCancellationTokenSource を Cancel して
	/// Pipeline全体の停止信号をProducerおよび他Workerへ伝播させます。
	/// </summary>
	/// <param name="reader">Channel.Reader（BindingImage の読み込み元）。</param>
	/// <param name="linkedCancellationTokenSource">Pipeline全体のLinked CancellationTokenSource。</param>
	/// <param name="pipelineCancellationToken">Pipeline用のキャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	private async Task RunImageProcessorWorker(
		ChannelReader<BindingImage> reader,
		CancellationTokenSource linkedCancellationTokenSource,
		CancellationToken pipelineCancellationToken)
	{
		try
		{
			// 独立した DI Scope を作成し、このWorker専用の BindingImageProcessor を取得
			using (var scope = this.serviceScopeFactory.CreateScope())
			{
				var processor = scope.ServiceProvider.GetRequiredService<BindingImageProcessor>();

				// Channel.Reader から BindingImage を順次取得
				// Channel が完了するまで非同期で読み取り継続
				await foreach (var image in reader.ReadAllAsync(pipelineCancellationToken))
				{
					// ProcessAsync() は成功・失敗を ProcessStatus / ProcessErrorMessage に設定してreturnする
					// OperationCanceledException や想定外の例外は透過的に上位へ伝播
					try
					{
						await processor.ProcessAsync(image, pipelineCancellationToken).ConfigureAwait(false);
					}
					finally
					{
						// 画像処理の成否にかかわらず、TemporaryImageStream を必ず解放する
						image.TemporaryImageStream?.Dispose();
						image.TemporaryImageStream = null;
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
			// 外部キャンセルまたは Pipeline 内部キャンセルによる正常な終了
			throw;
		}
		catch (Exception)
		{
			// Worker で想定外例外が発生した場合、
			// LinkedCancellationTokenSource を Cancel して Producerおよび他Workerへ停止信号を伝播
			linkedCancellationTokenSource.Cancel();
			throw;
		}
	}

	/// <summary>
	/// 1つの BindingVolume 内の全 BindingImage について、
	/// ファイル名競合をチェックします。
	/// 
	/// 競合が存在した場合：
	/// - volume.HasImageFileNameConflict = true
	/// - 競合している全 BindingImage について SkipImageProcessing = true
	/// 
	/// 競合していない BindingImage は SkipImageProcessing = false のままです。
	/// </summary>
	/// <param name="volume">チェック対象の BindingVolume。</param>
	private static void CheckFileNameConflicts(BindingVolume volume)
	{
		// SimulatedFileName が null でないもののみを対象に集計
		var fileNames = volume.Images
			.Where(img => !string.IsNullOrEmpty(img.SimulatedFileName))
			.GroupBy(img => img.SimulatedFileName, StringComparer.OrdinalIgnoreCase)
			.ToList();

		// 重複が存在するかチェック
		var hasDuplicate = false;
		var conflictingImages = new HashSet<BindingImage>();

		foreach (var group in fileNames)
		{
			if (group.Count() > 1)
			{
				hasDuplicate = true;
				foreach (var image in group)
				{
					conflictingImages.Add(image);
				}
			}
		}

		// 結果を反映
		volume.HasImageFileNameConflict = hasDuplicate;

		// 競合している BindingImage に SkipImageProcessing = true を設定
		foreach (var image in volume.Images)
		{
			if (conflictingImages.Contains(image))
			{
				image.SkipImageProcessing = true;
			}
			// 競合していない場合は既に false で初期化されているため追加処理不要
		}
	}

	/// <summary>
	/// 今回の製本前確認処理で正常に処理された EPUB の一時展開フォルダを削除します。
	/// 削除対象は、Material.EffectiveSourceType == MaterialSourceType.Epub かつ
	/// EpubExtractionError == EpubExtractionError.None の BindingVolume です。
	/// 
	/// 削除に失敗した場合はログに記録し、例外は再送出しません（best-effort）。
	/// すでにキャンセル状態の CancellationToken に依存せず実行します。
	/// </summary>
	private async ValueTask CleanupEpubTemporaryFoldersAsync()
	{
		try
		{
			// EPUB として処理され、かつエラーなく完了した BindingVolume を対象
			var epubVolumesToCleanup = this.bindingStore.BindingVolumes
				.Where(v =>
					v.Material.EffectiveSourceType == MaterialSourceType.Epub &&
					v.EpubExtractionError == EpubExtractionError.None)
				.ToList();

			if (epubVolumesToCleanup.Count == 0)
			{
				return;
			}

			// CancellationToken に依存しない cleanup を実行（既にキャンセル状態でも実行）
			foreach (var volume in epubVolumesToCleanup)
			{
				if (string.IsNullOrEmpty(volume.WorkFolderPath))
				{
					continue;
				}

				var epubTempDir = Path.Combine(volume.WorkFolderPath, ".epub");

				try
				{
					if (Directory.Exists(epubTempDir))
					{
						// .epub フォルダを再帰削除
						// 背景タスクで実行してもメインスレッドをブロック回避
						await Task.Run(() => Directory.Delete(epubTempDir, recursive: true))
							.ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
					// cleanup 失敗時はログのみ記録（本来の Pipeline 処理を中断しない）
					this.logger.LogWarning(
						ex,
						"EPUB の一時展開フォルダ削除に失敗しました。WorkFolderPath：{WorkFolder}",
						volume.WorkFolderPath);
				}
			}
		}
		catch (Exception ex)
		{
			// 予期しない例外が発生した場合もログのみ記録
			this.logger.LogWarning(ex, "EPUB 一時フォルダの cleanup 処理中に予期しないエラーが発生しました。");
		}
	}
}
