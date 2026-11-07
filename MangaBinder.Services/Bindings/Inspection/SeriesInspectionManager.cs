using System.Collections.Concurrent;
using System.Threading.Channels;
using MangaBinder.Helpers;
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

		// ⑤ ZIP ファイル名の初期生成を実行
		this.bindingManager.InitializeZipOutputFileName();

		// ⑥ BindingStore.WorkSeriesFolderPath から対象作品の作品Workフォルダパスを取得する
		var seriesFolderPath = this.bindingStore.WorkSeriesFolderPath.Value;

		// ⑦ 作品Workフォルダが存在しない場合のみ作成する（既存の作品Workフォルダは削除しない）
		await Task.Run(
			() => Directory.CreateDirectory(seriesFolderPath),
			cancellationToken).ConfigureAwait(false);

		// ⑧ BindingStore.BindingVolumes の全件を順番に処理して WorkFolderPath を設定する
		// 素材由来で画像情報を持たない巻のみ該当巻フォルダを削除・再作成する
		// 画像情報を持つ巻は既存の Work 巻フォルダを新しい巻フォルダ名へ引き継ぐ
		// BindingVolumes に存在しない巻フォルダには触れない
		var volumeFolderDigits = this.bindingStore.VolumeFolderDigits.Value;
		foreach (var bindingVolume in this.bindingStore.BindingVolumes.ToArray())
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

			// 展開先由来の巻は、スキャン済みの実フォルダをそのまま使用する
			if (bindingVolume.Material.OriginalSourceType == MaterialSourceType.WorkFolder)
			{
				bindingVolume.WorkFolderPath = bindingVolume.Material.FullPath;
				continue;
			}

			// 巻フォルダ名を生成
			var volumeFolderName = this.appSettings.CreateWorkVolumeFolderName(
				volumeNumber.Value,
				volumeFolderDigits);

			var targetFolderPath = Path.Combine(seriesFolderPath, volumeFolderName);

			await Task.Run(
				() => this.prepareMaterialVolumeFolder(bindingVolume, targetFolderPath),
				cancellationToken).ConfigureAwait(false);
		}

		// ⑪ 全 BindingVolume を EffectiveSourcePath でGroupBy する
		// 文字列比較には StringComparer.OrdinalIgnoreCase を使用
		var groupsBySourcePath = this.bindingStore.BindingVolumes
			.GroupBy(v => v.EffectiveSourcePath, StringComparer.OrdinalIgnoreCase)
			.ToList();

		// ⑫ System.Threading.Channels を使用した Producer/Consumer パターンを構築
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

		// 画像処理パイプラインへ流した BindingImage（最終状態確定後に Work 実ファイル情報を記録する対象）
		var pipelineImages = new ConcurrentBag<BindingImage>();

		// Producer Task を起動
		var producerTask = RunProducerAsync(
			groupsBySourcePath,
			writer,
			pipelineImages,
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

		// 展開先由来の巻は、同期の結果画像が0件になった場合に BindingVolumes から論理的に除外する
		var emptyWorkVolumes = this.bindingStore.BindingVolumes
			.Where(v => v.Material.OriginalSourceType == MaterialSourceType.WorkFolder && v.Images.Count == 0)
			.ToList();

		foreach (var emptyVolume in emptyWorkVolumes)
		{
			await this.bindingManager.UnselectMaterialAsync(emptyVolume.Material, invalidateCompletedState: false)
				.ConfigureAwait(false);
		}

		// ⑪ ファイル名正規化処理を実行
		// EPUB エラー巻
		var normalizationTargets = this.bindingStore.BindingVolumes
			.Where(v => !(v.Material.OriginalSourceType == MaterialSourceType.Epub && v.EpubExtractionError != EpubExtractionError.None))
			.ToList();

		await this.ExecuteFileNameNormalizationAsync(normalizationTargets, pipelineImages, pipelineCancellationToken)
			.ConfigureAwait(false);

		// ⑫ EPUB エラー巻について、後段処理前に検査を実行
		var epubErrorVolumes = this.bindingStore.BindingVolumes
			.Where(v => v.Material.OriginalSourceType == MaterialSourceType.Epub && v.EpubExtractionError != EpubExtractionError.None)
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

		// ⑬ 展開先作品フォルダ直下の巻フォルダ数を1回だけ取得し、Store へ保持
		this.bindingStore.WorkVolumeFolderCount.Value = Directory.GetDirectories(this.bindingStore.WorkSeriesFolderPath.Value).Length;
	}

	/// <summary>
	/// 素材由来の巻の Work 巻フォルダを準備し、WorkFolderPath を設定します。
	/// 画像情報を持たない巻は巻フォルダを削除して作り直します。
	/// 画像情報を持つ巻は既存の Work 巻フォルダを維持し、巻フォルダ名が変わる場合はフォルダ名と画像パスを引き継ぎます。
	/// </summary>
	/// <param name="volume">対象の素材由来の BindingVolume。</param>
	/// <param name="targetFolderPath">今回の巻番号・桁数に対応する Work 巻フォルダのフルパス。</param>
	/// <exception cref="InvalidOperationException">リネーム先の巻フォルダが既に存在する場合。</exception>
	private void prepareMaterialVolumeFolder(BindingVolume volume, string targetFolderPath)
	{
		if (volume.Images.Count == 0)
		{
			volume.WorkFolderPath = targetFolderPath;

			if (Directory.Exists(targetFolderPath))
			{
				Directory.Delete(targetFolderPath, recursive: true);
			}

			Directory.CreateDirectory(targetFolderPath);
			return;
		}

		// 製本設定から復元した直後は WorkFolderPath が未設定のため、保存済みの画像パスから現在の巻フォルダを特定する
		var currentFolderPath = volume.WorkFolderPath
			?? volume.Images
				.Where(image => image.FilePath is not null)
				.Select(image => Path.GetDirectoryName(image.FilePath!))
				.FirstOrDefault()
			?? targetFolderPath;

		if (!string.Equals(currentFolderPath, targetFolderPath, StringComparison.Ordinal))
		{
			if (Directory.Exists(targetFolderPath)
				&& !string.Equals(currentFolderPath, targetFolderPath, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException(
					$"巻フォルダ名の変更先が既に存在するため、Work 巻フォルダを引き継げません。Current={currentFolderPath}, Target={targetFolderPath}");
			}

			if (Directory.Exists(currentFolderPath))
			{
				Directory.Move(currentFolderPath, targetFolderPath);
			}

			foreach (var image in volume.Images)
			{
				if (image.FilePath is not null)
				{
					image.FilePath = Path.Combine(targetFolderPath, Path.GetFileName(image.FilePath));
				}
			}
		}

		volume.WorkFolderPath = targetFolderPath;
		Directory.CreateDirectory(targetFolderPath);
	}

	/// <summary>
	/// 複数 BindingVolume のファイル名正規化を最大4巻並列で実行します。
	/// 各巻の処理単位でスコープを作成し、Scoped なサービスのライフサイクルを管理します。
	/// </summary>
	/// <param name="volumes">正規化対象の BindingVolume リスト。</param>
	/// <param name="pipelineImages">今回の画像処理パイプラインへ投入された画像。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	private async ValueTask ExecuteFileNameNormalizationAsync(List<BindingVolume> volumes, ConcurrentBag<BindingImage> pipelineImages, CancellationToken cancellationToken)
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

				// 正規化完了後、今回処理した画像の Work 実ファイルのサイズ・最終更新日時を記録
				foreach (var image in pipelineImages)
				{
					ct.ThrowIfCancellationRequested();

					if (!ReferenceEquals(image.BindingVolume, volume)
						|| image.ProcessStatus != BindingImageProcessStatus.Succeeded
						|| image.FilePath is null)
					{
						continue;
					}

					image.RecordWorkFileState(new FileInfo(image.FilePath));
				}

				// 記録後、巻の検査を実行して集計結果を保持
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
		ConcurrentBag<BindingImage> pipelineImages,
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
					var sourceType = firstVolume.Material.OriginalSourceType;

					// DI Scope を作成し、入力元種別に対応する IMaterialExtractor を解決
					using (var scope = this.serviceScopeFactory.CreateScope())
					{
						var extractor = scope.ServiceProvider.GetRequiredKeyedService<IMaterialExtractor>(sourceType);

						// 第1段階：処理対象の BindingImage を確定する
						// 画像情報を持つ巻（展開先由来を含む）：Work の実ファイルと既存 BindingImage を照合し、新規・変更分のみを対象にする
						// 画像情報を持たない素材由来の巻（照合後に0件になった巻を含む）：Extractor が元素材から生成した BindingImage を対象にする
						var imagesToProcess = new Dictionary<BindingVolume, List<BindingImage>>();
						var extractTargets = new List<BindingVolume>();
						foreach (var volume in group)
						{
							ct.ThrowIfCancellationRequested();

							if (sourceType != MaterialSourceType.WorkFolder && volume.Images.Count == 0)
							{
								extractTargets.Add(volume);
								continue;
							}

							imagesToProcess[volume] = ReconcileWorkFolderImages(volume, ct);

							if (sourceType != MaterialSourceType.WorkFolder && volume.Images.Count == 0)
							{
								imagesToProcess.Remove(volume);
								extractTargets.Add(volume);
							}
						}

						if (extractTargets.Count > 0)
						{
							var extractGroup = extractTargets
								.GroupBy(v => v.EffectiveSourcePath, StringComparer.OrdinalIgnoreCase)
								.Single();

							await extractor.PrepareAsync(extractGroup, ct).ConfigureAwait(false);

							foreach (var volume in extractTargets)
							{
								imagesToProcess[volume] = volume.Images.ToList();
							}
						}

						// PrepareAsync 完了直後に、同じ素材グループ内の BindingVolume に対して Simulation を実行する
						var bindingImageProcessor = scope.ServiceProvider.GetRequiredService<BindingImageProcessor>();

						// 素材グループの各 BindingVolume に対して処理を実行
						foreach (var volume in group)
						{
							ct.ThrowIfCancellationRequested();

							// EPUB エラー巻は後段処理をスキップ
							var isEpubError = volume.Material.OriginalSourceType == MaterialSourceType.Epub
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
							// 新規・変更された BindingImage のみを対象にする
							var images = imagesToProcess[volume];

							foreach (var image in images)
							{
								ct.ThrowIfCancellationRequested();

								// 第2段階：1つの BindingImage を処理可能な状態へ準備
								// (Archive の場合のみ MemoryStream 化。Folder/EPUB は no-op)
								// Work の実画像を入力元とする画像は元素材から取得しない
								if (extractTargets.Contains(volume))
								{
									await extractor.PrepareImageAsync(image, ct).ConfigureAwait(false);
								}

								pipelineImages.Add(image);

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
	/// Work 巻フォルダの実ファイルと既存 BindingImage を1ファイルずつ照合し、処理対象を返します。
	/// 今回存在を確認できた BindingImage（未変更を含む）以外は BindingVolume 側で削除・Dispose されます。
	/// </summary>
	/// <param name="volume">対象の BindingVolume。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>新規または変更された、画像処理対象の BindingImage。</returns>
	private static List<BindingImage> ReconcileWorkFolderImages(BindingVolume volume, CancellationToken cancellationToken)
	{
		var confirmedImages = new List<BindingImage>();
		var imagesToProcess = new List<BindingImage>();

		var workFiles = new DirectoryInfo(volume.WorkFolderPath!)
			.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
			.Where(file => SupportedExtensionHelper.IsImage(file.Extension))
			.OrderBy(file => file.FullName, StringComparer.OrdinalIgnoreCase);

		foreach (var workFile in workFiles)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var image = volume.FindImageByFilePath(workFile.FullName);
			if (image is null)
			{
				image = volume.AddImage(workFile.FullName, workFile.Name);
				image.FilePath = workFile.FullName;
				imagesToProcess.Add(image);
			}
			else if (image.IsChanged(workFile))
			{
				imagesToProcess.Add(image);
			}

			confirmedImages.Add(image);
		}

		volume.RemoveUnconfirmedImages(confirmedImages);

		return imagesToProcess;
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
	/// 削除対象は、Material.OriginalSourceType == MaterialSourceType.Epub かつ
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
					v.Material.OriginalSourceType == MaterialSourceType.Epub &&
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
