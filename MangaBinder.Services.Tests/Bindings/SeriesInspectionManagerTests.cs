using System.Collections.Concurrent;
using MangaBinder.Bindings;
using MangaBinder.Bindings.Inspection;
using MangaBinder.Helpers;
using MangaBinder.Series;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using R3;

namespace MangaBinder.Services.Tests.Bindings;

/// <summary>
/// <see cref="SeriesInspectionManager"/> の UI コンテキストでの状態確定と、Work フォルダ画像の照合のテストです。
/// </summary>
public class SeriesInspectionManagerTests : IDisposable
{
	private readonly string seriesFolder;
	private readonly BindingStore store = new();
	private readonly TagStore tagStore = new();
	private readonly AppSettings appSettings = new() { ConnectionString = string.Empty };
	private readonly ServiceProvider serviceProvider;
	private readonly SeriesInspectionManager manager;

	public SeriesInspectionManagerTests()
	{
		SupportedExtensionHelper.Initialize(
		[
			new SupportedFileExtension { Extension = ".jpg", FileType = (int)FileType.Image },
		]);

		this.seriesFolder = Path.Combine(Path.GetTempPath(), "MangaBinderTests_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(this.seriesFolder);

		this.appSettings.ExpandedVolumeFolderNamePrefix.Value = "Title ";
		this.appSettings.ExpandedVolumeFolderNameSuffix.Value = string.Empty;

		var services = new ServiceCollection();
		services.AddSingleton(this.appSettings);
		services.AddKeyedScoped<IMaterialExtractor, ExpandedFolderExtractor>(MaterialSourceType.ExpandedFolder);
		services.AddKeyedScoped<IMaterialExtractor, ExpandedFolderExtractor>(MaterialSourceType.Folder);
		services.AddScoped<BindingImageProcessor>();
		services.AddScoped<VolumeFileNameNormalizer>();
		this.serviceProvider = services.BuildServiceProvider();
		var scopeFactory = this.serviceProvider.GetRequiredService<IServiceScopeFactory>();

		var bindingManager = new BindingManager(
			this.store,
			new MangaSeriesStore(this.tagStore, this.store),
			this.appSettings,
			scopeFactory);

		this.manager = new SeriesInspectionManager(
			this.store,
			this.appSettings,
			scopeFactory,
			NullLogger<SeriesInspectionManager>.Instance,
			bindingManager);

		this.store.BindingTarget.Value = new BindingSeries
		{
			SeriesNotifier = new MangaSeriesNotifier(new MangaSeries(), this.tagStore),
		};
		this.store.ExpandedSeriesFolderPath.Value = this.seriesFolder;
		this.store.ZipOutputFileName.Value = "test.zip";
		this.store.VolumeFolderDigits.Value = 2;
	}

	public void Dispose()
	{
		this.serviceProvider.Dispose();
		this.store.Dispose();
		Directory.Delete(this.seriesFolder, recursive: true);
	}

	private string createImageFile(string folderPath, string fileName, int length = 10)
	{
		Directory.CreateDirectory(folderPath);
		var path = Path.Combine(folderPath, fileName);
		File.WriteAllBytes(path, new byte[length]);
		return path;
	}

	private static BindingImage addTrackedImage(BindingVolume volume, string filePath)
	{
		var image = volume.AddImage(filePath, Path.GetFileName(filePath));
		image.FilePath = filePath;
		image.RecordExpandedFileState(new FileInfo(filePath));
		return image;
	}

	private (BindingVolume Volume, ExpandedVolume Expanded, BindingImage Image) createWorkVolume(string folderName, decimal number)
	{
		var folderPath = Path.Combine(this.seriesFolder, folderName);
		var filePath = this.createImageFile(folderPath, "1.jpg");

		var expanded = this.store.AddOrUpdateExpandedVolume(folderName, folderPath, number, 1, 10L);
		var material = this.store.CreateExpandedMaterial(expanded);
		material.IsChecked.Value = true;
		expanded.IsChecked.Value = true;

		var volume = new BindingVolume(material);
		volume.VolumeNumber.Value = number;
		volume.ExpandedVolumeFolderPath = folderPath;
		this.store.InsertBindingVolumeInOrder(volume);

		return (volume, expanded, addTrackedImage(volume, filePath));
	}

	private (BindingVolume Volume, MaterialItem Source, BindingImage Image) createSourceVolumeWithWorkImage(string folderName, decimal number)
	{
		var folderPath = Path.Combine(this.seriesFolder, folderName);
		var filePath = this.createImageFile(folderPath, "1.jpg");

		var source = new MaterialItem(
			MaterialItemType.Folder,
			MaterialSourceType.Folder,
			"src",
			@"C:\src\src",
			fileCount: 1,
			totalImageBytes: 10L,
			sourcePath: @"C:\src\src");
		this.store.Materials.Add(source);
		source.IsChecked.Value = true;

		var volume = new BindingVolume(source);
		volume.VolumeNumber.Value = number;
		this.store.InsertBindingVolumeInOrder(volume);

		return (volume, source, addTrackedImage(volume, filePath));
	}

	private BindingVolume createReconcileVolume(string folderName)
	{
		var folderPath = Path.Combine(this.seriesFolder, folderName);
		Directory.CreateDirectory(folderPath);
		var expanded = this.store.AddOrUpdateExpandedVolume(folderName, folderPath, 1m, 0, 0L);
		var volume = new BindingVolume(this.store.CreateExpandedMaterial(expanded))
		{
			ExpandedVolumeFolderPath = folderPath,
		};
		return volume;
	}

	[Fact]
	public async Task ExecuteAsync_元素材由来の巻の切替とStore派生値の更新がUIコンテキストで確定しインスタンスが維持される()
	{
		var (volume, source, image) = this.createSourceVolumeWithWorkImage("Title 01", 1m);
		using var context = new SingleThreadSynchronizationContext();
		var threads = new ConcurrentQueue<int>();
		using var materialSubscription = volume.CurrentMaterial.AsObservable().Skip(1)
			.Subscribe(_ => threads.Enqueue(Environment.CurrentManagedThreadId));
		using var selectedBytesSubscription = this.store.SelectedVolumeTotalImageBytes.AsObservable().Skip(1)
			.Subscribe(_ => threads.Enqueue(Environment.CurrentManagedThreadId));
		using var totalBytesSubscription = this.store.ImageTotalBytes.AsObservable().Skip(1)
			.Subscribe(_ => threads.Enqueue(Environment.CurrentManagedThreadId));
		using var volumeBytesSubscription = volume.ImageTotalBytes.AsObservable().Skip(1)
			.Subscribe(_ => threads.Enqueue(Environment.CurrentManagedThreadId));
		var inspectedThreads = new ConcurrentQueue<int>();
		this.manager.VolumeInspected += _ => inspectedThreads.Enqueue(Environment.CurrentManagedThreadId);

		await context.RunAsync(() => this.manager.ExecuteAsync());

		Assert.Same(volume, Assert.Single(this.store.BindingVolumes));
		Assert.Same(image, Assert.Single(volume.Images));
		Assert.Equal(MaterialSourceType.ExpandedFolder, volume.Material.Type);
		Assert.Same(source, volume.Material.Source);
		Assert.True(volume.Material.IsChecked.Value);
		Assert.False(source.IsChecked.Value);
		Assert.True(source.HasBeenUsedForExpansion.Value);
		var expanded = Assert.Single(this.store.ExpandedVolumes);
		Assert.True(expanded.IsChecked.Value);
		Assert.Same(source, expanded.Source);
		Assert.Equal(10L, volume.ImageTotalBytes.Value);
		Assert.Equal(1, volume.Material.FileCount);

		Assert.False(threads.IsEmpty);
		Assert.All(threads, id => Assert.Equal(context.ThreadId, id));
		Assert.Single(inspectedThreads);
		Assert.All(inspectedThreads, id => Assert.Equal(context.ThreadId, id));
	}

	[Fact]
	public async Task ExecuteAsync_桁数変更でリネームされた後のパスと通知がUIコンテキストで整合する()
	{
		this.store.VolumeFolderDigits.Value = 3;
		var (volume, expanded, image) = this.createWorkVolume("Title 01", 1m);
		var oldPath = expanded.FullPath;
		var newPath = Path.Combine(this.seriesFolder, "Title 001");
		var originalMaterial = volume.Material;
		using var context = new SingleThreadSynchronizationContext();
		var snapshots = new ConcurrentQueue<(int ThreadId, string MaterialPath, string? WorkFolderPath, string? ImagePath, string ExpandedPath)>();
		using var subscription = volume.CurrentMaterial.AsObservable().Skip(1)
			.Subscribe(_ => snapshots.Enqueue((
				Environment.CurrentManagedThreadId,
				volume.Material.FullPath,
				volume.ExpandedVolumeFolderPath,
				image.FilePath,
				expanded.FullPath)));

		await context.RunAsync(() => this.manager.ExecuteAsync());

		Assert.False(Directory.Exists(oldPath));
		Assert.True(File.Exists(Path.Combine(newPath, "1.jpg")));
		var snapshot = Assert.Single(snapshots);
		Assert.Equal(context.ThreadId, snapshot.ThreadId);
		Assert.Equal(newPath, snapshot.MaterialPath);
		Assert.Equal(newPath, snapshot.WorkFolderPath);
		Assert.Equal(Path.Combine(newPath, "1.jpg"), snapshot.ImagePath);
		Assert.Equal(newPath, snapshot.ExpandedPath);

		// 既存の BindingImage は新規画像と誤判定されず、同一インスタンスのまま維持される
		Assert.Same(originalMaterial, volume.Material);
		Assert.Same(image, Assert.Single(volume.Images));
		Assert.Equal("Title 001", expanded.Name);
		Assert.Equal("Title 001", volume.Material.Name);
		Assert.Equal(newPath, volume.ExpandedVolumeFolderPath);
	}

	[Fact]
	public async Task ExecuteAsync_リネーム先が衝突した場合は例外で何も確定されずMaterialも切り替わらない()
	{
		this.store.VolumeFolderDigits.Value = 3;
		var (volume, expanded, image) = this.createWorkVolume("Title 01", 1m);
		var oldPath = expanded.FullPath;
		Directory.CreateDirectory(Path.Combine(this.seriesFolder, "Title 001"));
		var (sourceVolume, source, _) = this.createSourceVolumeWithWorkImage("Title 02", 2m);
		var originalMaterial = volume.Material;
		using var context = new SingleThreadSynchronizationContext();

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => context.RunAsync(() => this.manager.ExecuteAsync()));

		Assert.True(Directory.Exists(oldPath));
		Assert.Equal(oldPath, expanded.FullPath);
		Assert.Equal(oldPath, volume.Material.FullPath);
		Assert.Equal(Path.Combine(oldPath, "1.jpg"), image.FilePath);
		Assert.Same(originalMaterial, volume.Material);
		Assert.Same(source, sourceVolume.Material);
		Assert.Equal(MaterialSourceType.Folder, sourceVolume.Material.Type);
		Assert.True(source.IsChecked.Value);
		Assert.False(source.HasBeenUsedForExpansion.Value);
	}

	[Fact]
	public async Task ExecuteAsync_キャンセル時は元素材からの切替を行わない()
	{
		var (sourceVolume, source, _) = this.createSourceVolumeWithWorkImage("Title 01", 1m);
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		using var context = new SingleThreadSynchronizationContext();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => context.RunAsync(() => this.manager.ExecuteAsync(cancellation.Token)));

		Assert.Same(source, sourceVolume.Material);
		Assert.True(source.IsChecked.Value);
		Assert.False(source.HasBeenUsedForExpansion.Value);
		Assert.Empty(this.store.ExpandedVolumes);
	}

	[Fact]
	public async Task ExecuteAsync_展開または検査が例外で失敗した場合は元素材をマークしない()
	{
		var (volume, source, image) = this.createSourceVolumeWithWorkImage("Title 01", 1m);
		File.Delete(image.FilePath!);
		using var context = new SingleThreadSynchronizationContext();

		await Assert.ThrowsAnyAsync<IOException>(
			() => context.RunAsync(() => this.manager.ExecuteAsync()));

		Assert.Same(source, volume.Material);
		Assert.True(source.IsChecked.Value);
		Assert.False(source.HasBeenUsedForExpansion.Value);
	}

	[Fact]
	public async Task ExecuteAsync_元素材のマークはUIコンテキストで通知される()
	{
		var (_, source, _) = this.createSourceVolumeWithWorkImage("Title 01", 1m);
		using var context = new SingleThreadSynchronizationContext();
		var threads = new ConcurrentQueue<int>();
		using var subscription = source.HasBeenUsedForExpansion.AsObservable().Skip(1)
			.Subscribe(_ => threads.Enqueue(Environment.CurrentManagedThreadId));

		await context.RunAsync(() => this.manager.ExecuteAsync());

		Assert.True(source.HasBeenUsedForExpansion.Value);
		Assert.Equal(context.ThreadId, Assert.Single(threads));
	}

	[Fact]
	public async Task ExecuteAsync_新規画像は並列画像処理パイプラインで処理される()
	{
		var (volume, _, _) = this.createWorkVolume("Title 01", 1m);
		var newFilePath = Path.Combine(volume.ExpandedVolumeFolderPath!, "2.jpg");
		using (var created = NetVips.Image.Black(8, 8))
		{
			created.WriteToFile(newFilePath);
		}

		using var context = new SingleThreadSynchronizationContext();

		await context.RunAsync(() => this.manager.ExecuteAsync());

		Assert.Equal(2, volume.Images.Count);
		var processed = volume.Images.Single(image => image.FileName == "2.jpg");
		Assert.Equal(BindingImageProcessStatus.Succeeded, processed.ProcessStatus);
		Assert.Equal(2, volume.ImageFileCount);
		Assert.True(volume.ImageTotalBytes.Value > 10L);
	}

	[Fact]
	public void ReconcileWorkFolderImages_既存画像は同一インスタンスで再利用され未変更なら処理対象にならない()
	{
		using var volume = this.createReconcileVolume("Title 01");
		var first = addTrackedImage(volume, this.createImageFile(volume.ExpandedVolumeFolderPath!, "1.jpg"));
		var second = addTrackedImage(volume, this.createImageFile(volume.ExpandedVolumeFolderPath!, "2.jpg"));
		var splitSettings = new SplitSettings();
		first.SplitSettings = splitSettings;

		var toProcess = SeriesInspectionManager.ReconcileExpandedVolumeImages(volume, CancellationToken.None);

		Assert.Empty(toProcess);
		Assert.Equal(new[] { first, second }, volume.Images);
		Assert.Same(splitSettings, first.SplitSettings);
		Assert.False(first.IsSpreadSplitTarget.IsDisposed);
	}

	[Fact]
	public void ReconcileWorkFolderImages_ファイルサイズが変わった画像は再処理対象になる()
	{
		using var volume = this.createReconcileVolume("Title 01");
		var path = this.createImageFile(volume.ExpandedVolumeFolderPath!, "1.jpg");
		var image = addTrackedImage(volume, path);
		var lastWriteTime = File.GetLastWriteTimeUtc(path);
		File.WriteAllBytes(path, new byte[25]);
		File.SetLastWriteTimeUtc(path, lastWriteTime);

		var toProcess = SeriesInspectionManager.ReconcileExpandedVolumeImages(volume, CancellationToken.None);

		Assert.Same(image, Assert.Single(toProcess));
		Assert.Same(image, Assert.Single(volume.Images));
	}

	[Fact]
	public void ReconcileWorkFolderImages_最終更新日時が変わった画像は再処理対象になる()
	{
		using var volume = this.createReconcileVolume("Title 01");
		var path = this.createImageFile(volume.ExpandedVolumeFolderPath!, "1.jpg");
		var image = addTrackedImage(volume, path);
		File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(5));

		var toProcess = SeriesInspectionManager.ReconcileExpandedVolumeImages(volume, CancellationToken.None);

		Assert.Same(image, Assert.Single(toProcess));
	}

	[Fact]
	public void ReconcileWorkFolderImages_新規ファイルは列挙順にBindingImageとして追加される()
	{
		using var volume = this.createReconcileVolume("Title 01");
		var existing = addTrackedImage(volume, this.createImageFile(volume.ExpandedVolumeFolderPath!, "2.jpg"));
		var path3 = this.createImageFile(volume.ExpandedVolumeFolderPath!, "3.jpg");
		var path1 = this.createImageFile(volume.ExpandedVolumeFolderPath!, "1.jpg");

		var toProcess = SeriesInspectionManager.ReconcileExpandedVolumeImages(volume, CancellationToken.None);

		Assert.Equal(new[] { path1, path3 }, toProcess.Select(image => image.FilePath));
		Assert.Equal(new[] { "1.jpg", "3.jpg" }, toProcess.Select(image => image.FileName));
		Assert.Equal(3, volume.Images.Count);
		Assert.Contains(existing, volume.Images);
		Assert.All(toProcess, image => Assert.Contains(image, volume.Images));
	}

	[Fact]
	public void ReconcileWorkFolderImages_消失した画像は削除されDisposeされる()
	{
		using var volume = this.createReconcileVolume("Title 01");
		var kept = addTrackedImage(volume, this.createImageFile(volume.ExpandedVolumeFolderPath!, "1.jpg"));
		var lostPath = this.createImageFile(volume.ExpandedVolumeFolderPath!, "2.jpg");
		var lost = addTrackedImage(volume, lostPath);
		File.Delete(lostPath);

		var toProcess = SeriesInspectionManager.ReconcileExpandedVolumeImages(volume, CancellationToken.None);

		Assert.Empty(toProcess);
		Assert.Same(kept, Assert.Single(volume.Images));
		Assert.False(kept.IsSpreadSplitTarget.IsDisposed);
		Assert.True(lost.IsSpreadSplitTarget.IsDisposed);
	}

	[Fact]
	public void ReconcileWorkFolderImages_FilePathがnullの画像は照合されず削除されDisposeされ実ファイルは新規扱いになる()
	{
		using var volume = this.createReconcileVolume("Title 01");
		var path = this.createImageFile(volume.ExpandedVolumeFolderPath!, "1.jpg");
		var noPath = volume.AddImage("1.jpg", "1.jpg");

		var toProcess = SeriesInspectionManager.ReconcileExpandedVolumeImages(volume, CancellationToken.None);

		var added = Assert.Single(toProcess);
		Assert.NotSame(noPath, added);
		Assert.Equal(path, added.FilePath);
		Assert.Same(added, Assert.Single(volume.Images));
		Assert.True(noPath.IsSpreadSplitTarget.IsDisposed);
	}

	[Fact]
	public void ReconcileWorkFolderImages_パス比較は大文字小文字を区別しない()
	{
		using var volume = this.createReconcileVolume("Title 01");
		var path = this.createImageFile(volume.ExpandedVolumeFolderPath!, "a.jpg");
		var image = addTrackedImage(volume, path);
		image.FilePath = path.ToUpperInvariant();

		var toProcess = SeriesInspectionManager.ReconcileExpandedVolumeImages(volume, CancellationToken.None);

		Assert.Empty(toProcess);
		Assert.Same(image, Assert.Single(volume.Images));
		Assert.False(image.IsSpreadSplitTarget.IsDisposed);
	}

	[Fact]
	public void ReconcileWorkFolderImages_FilePathが重複する場合は最初のBindingImageを採用し残りは削除される()
	{
		using var volume = this.createReconcileVolume("Title 01");
		var path = this.createImageFile(volume.ExpandedVolumeFolderPath!, "1.jpg");
		var first = addTrackedImage(volume, path);
		var duplicate = addTrackedImage(volume, path);

		var toProcess = SeriesInspectionManager.ReconcileExpandedVolumeImages(volume, CancellationToken.None);

		Assert.Empty(toProcess);
		Assert.Same(first, Assert.Single(volume.Images));
		Assert.True(duplicate.IsSpreadSplitTarget.IsDisposed);
	}

	[Fact]
	public void FindImageByFilePath_既存の検索仕様が維持される()
	{
		using var volume = this.createReconcileVolume("Title 01");
		var path = this.createImageFile(volume.ExpandedVolumeFolderPath!, "a.jpg");
		var image = addTrackedImage(volume, path);

		Assert.Same(image, volume.FindImageByFilePath(path.ToUpperInvariant()));
		Assert.Null(volume.FindImageByFilePath(Path.Combine(volume.ExpandedVolumeFolderPath!, "b.jpg")));
	}

	/// <summary>
	/// 専用スレッド上で継続を実行し、WPF の UI スレッドに相当する SynchronizationContext を再現するテスト用コンテキストです。
	/// </summary>
	private sealed class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable
	{
		private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> queue = new();
		private readonly Thread thread;

		public int ThreadId => this.thread.ManagedThreadId;

		public SingleThreadSynchronizationContext()
		{
			this.thread = new Thread(this.run) { IsBackground = true };
			this.thread.Start();
		}

		public override void Post(SendOrPostCallback d, object? state)
			=> this.queue.Add((d, state));

		public override void Send(SendOrPostCallback d, object? state)
		{
			if (Environment.CurrentManagedThreadId == this.ThreadId)
			{
				d(state);
				return;
			}

			using var completed = new ManualResetEventSlim();
			this.Post(_ =>
			{
				d(state);
				completed.Set();
			}, null);
			completed.Wait();
		}

		/// <summary>
		/// このコンテキストのスレッド上で処理を開始し、完了または例外を呼び出し元へ返します。
		/// </summary>
		public Task RunAsync(Func<ValueTask> action)
		{
			var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			this.Post(
				async _ =>
				{
					try
					{
						await action();
						completion.SetResult();
					}
					catch (Exception ex)
					{
						completion.SetException(ex);
					}
				},
				null);

			return completion.Task;
		}

		private void run()
		{
			SetSynchronizationContext(this);
			foreach (var (callback, state) in this.queue.GetConsumingEnumerable())
			{
				callback(state);
			}
		}

		public void Dispose()
		{
			this.queue.CompleteAdding();
			this.thread.Join();
			this.queue.Dispose();
		}
	}
}
