using System.Threading.Channels;
using MangaBinder.Bindings;
using MangaBinder.Bindings.Extraction;
using MangaBinder.Bindings.Inspection;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace MangaBinder.Bindings;

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

	/// <summary>
	/// <see cref="SeriesInspectionManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingStore">製本工程の正本状態ストア。</param>
	/// <param name="appSettings">アプリケーション設定。Workフォルダパス生成に使用します。</param>
	/// <param name="serviceScopeFactory">DI スコープを作成するファクトリー。Extractor 解決に使用します。</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="bindingStore"/>, <paramref name="appSettings"/>, または <paramref name="serviceScopeFactory"/> が null の場合。
	/// </exception>
	public SeriesInspectionManager(
		BindingStore bindingStore,
		AppSettings appSettings,
		IServiceScopeFactory serviceScopeFactory)
	{
		this.bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
		this.appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
		this.serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
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

		// ③ AppSettings.CreateWorkSeriesFolderPath() を使用して、対象作品の作品Workフォルダパスを取得する
		var seriesFolderPath = this.appSettings.CreateWorkSeriesFolderPath(bindingTarget.Series.Title);

		// ④ BindingStore.RecreateWorkFolder.Value が true の場合、作品Workフォルダが存在すれば削除する
		// ファイルシステムの重い処理でUIスレッドをブロックしないようにする
		if (this.bindingStore.RecreateWorkFolder.Value && Directory.Exists(seriesFolderPath))
		{
			await Task.Run(
				() => Directory.Delete(seriesFolderPath, recursive: true),
				cancellationToken).ConfigureAwait(false);
		}

		// ⑤ 作品Workフォルダが存在しない場合を含め、以降の処理で使用できるよう作品Workフォルダを作成する
		Directory.CreateDirectory(seriesFolderPath);

		// ⑥ BindingStore.BindingVolumes を順番に処理して WorkFolderPath を設定し、
		// 新規実体化が必要なボリュームのみをフィルタリングする
		var volumeFolderDigits = this.bindingStore.VolumeFolderDigits.Value;
		var volumesRequiringNewBuild = new List<BindingVolume>();

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

			// ⑦ 各 BindingVolume.WorkFolderPath について Directory.Exists() で存在を確認
			var workFolderExists = Directory.Exists(bindingVolume.WorkFolderPath);

			// 巻フォルダが存在しない場合は新規実体化対象に追加
			if (!workFolderExists)
			{
				volumesRequiringNewBuild.Add(bindingVolume);
			}
		}

		// ⑧ 今回素材展開する BindingVolume について、巻Workフォルダを作成する
		foreach (var volume in volumesRequiringNewBuild)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Directory.CreateDirectory(volume.WorkFolderPath!);
		}

		// ⑨ 新規実体化が必要なボリュームを Material.SourcePath でGroupBy する
		// 文字列比較には StringComparer.OrdinalIgnoreCase を使用
		var groupsBySourcePath = volumesRequiringNewBuild
			.GroupBy(v => v.Material.SourcePath, StringComparer.OrdinalIgnoreCase)
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
		}

		// ⑪ 全 Producer / Worker が正常に完了した場合のみ、
		// 処理対象の各 BindingVolume について HasImageProcessingError を更新
		foreach (var volume in volumesRequiringNewBuild)
		{
			var hasError = volume.Images.Any(img =>
				img.ProcessStatus == BindingImageProcessStatus.ImageOpenFailed ||
				img.ProcessStatus == BindingImageProcessStatus.ConversionFailed ||
				img.ProcessStatus == BindingImageProcessStatus.OutputFailed);

			volume.HasImageProcessingError = hasError;
		}
	}

	/// <summary>
	/// 素材グループの展開・Simulation・Conflict判定・Channel投入を実行する Producer Task です。
	/// </summary>
	/// <param name="groupsBySourcePath">Material.SourcePath でグループ化された BindingVolume 集合。</param>
	/// <param name="writer">Channel.Writer。</param>
	/// <param name="linkedCancellationTokenSource">Pipeline全体のLinked CancellationTokenSource。</param>
	/// <param name="pipelineCancellationToken">Pipeline用のキャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	private async Task RunProducerAsync(
		List<IGrouping<string, BindingVolume>> groupsBySourcePath,
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
				groupsBySourcePath,
				parallelOptions,
				async (group, ct) =>
				{
					// 素材形式を判定
					var materialType = DetermineMaterialType(group);

					// DI Scope を作成し、素材形式に対応する IMaterialExtractor を解決
					using (var scope = this.serviceScopeFactory.CreateScope())
					{
						var extractor = scope.ServiceProvider.GetRequiredKeyedService<IMaterialExtractor>(materialType);
						await extractor.ExtractAsync(group, ct).ConfigureAwait(false);

						// ExtractAsync 完了直後に、同じ素材グループ内の BindingVolume に対して Simulation を実行する
						var bindingImageProcessor = scope.ServiceProvider.GetRequiredService<BindingImageProcessor>();

						// 素材グループの各 BindingVolume に対して処理を実行
						foreach (var volume in group)
						{
							ct.ThrowIfCancellationRequested();

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
							foreach (var image in volume.Images)
							{
								ct.ThrowIfCancellationRequested();
								await writer.WriteAsync(image, ct).ConfigureAwait(false);
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
	/// グループ内の BindingVolume の情報から素材形式を判定します。
	/// </summary>
	/// <param name="group">Material.SourcePath でグループ化された BindingVolume 集合。</param>
	/// <returns>判定された素材形式（MaterialItemType）。</returns>
	private static MaterialItemType DetermineMaterialType(IGrouping<string, BindingVolume> group)
	{
		// グループ内の最初の BindingVolume を取得して判定
		// 同一グループ内の全ボリュームは同じ Material.ItemType と ArchiveEntryPrefix を持つ
		var firstVolume = group.First();
		var material = firstVolume.Material;

		// 1. ArchiveEntryPrefix が設定されている場合は Archive
		if (!string.IsNullOrEmpty(material.ArchiveEntryPrefix))
		{
			return MaterialItemType.Archive;
		}

		// 2. ItemType.Epub の場合
		if (material.ItemType == MaterialItemType.Epub)
		{
			return MaterialItemType.Epub;
		}

		// 3. それ以外は Folder
		return MaterialItemType.Folder;
	}
}
