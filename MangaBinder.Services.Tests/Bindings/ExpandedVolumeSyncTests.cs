using MangaBinder.Bindings;
using MangaBinder.Helpers;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MangaBinder.Services.Tests.Bindings;

/// <summary>
/// <see cref="VolumeSelectionManager.SyncWorkVolumesAsync"/> による ExpandedVolumes / WorkVolumes 同期のテストです。
/// </summary>
public class ExpandedVolumeSyncTests : IDisposable
{
	private readonly string seriesFolder;
	private readonly BindingStore store = new();
	private readonly VolumeSelectionManager manager;

	public ExpandedVolumeSyncTests()
	{
		SupportedExtensionHelper.Initialize(
		[
			new SupportedFileExtension { Extension = ".jpg", FileType = (int)FileType.Image },
			new SupportedFileExtension { Extension = ".png", FileType = (int)FileType.Image },
		]);

		this.seriesFolder = Path.Combine(Path.GetTempPath(), "MangaBinderTests_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(this.seriesFolder);
		this.store.WorkSeriesFolderPath.Value = this.seriesFolder;

		var repository = new MaterialArchiveRepository(":memory:");
		this.manager = new VolumeSelectionManager(
			this.store,
			new SeriesMaterialFolderLoader(new MaterialArchiveExtractor(), repository),
			new AppSettings { ConnectionString = string.Empty },
			new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
			repository,
			NullLogger<VolumeSelectionManager>.Instance);
	}

	public void Dispose()
	{
		this.store.Dispose();
		Directory.Delete(this.seriesFolder, recursive: true);
	}

	private string createVolume(string name, params (string File, int Bytes)[] files)
	{
		var path = Path.Combine(this.seriesFolder, name);
		Directory.CreateDirectory(path);
		foreach (var (file, bytes) in files)
		{
			File.WriteAllBytes(Path.Combine(path, file), new byte[bytes]);
		}
		return path;
	}

	[Fact]
	public async Task 同期_単巻かつ画像ありのフォルダだけが巻番号順にExpandedVolumesへ登録される()
	{
		this.createVolume("Title 02", ("1.jpg", 10));
		this.createVolume("Title 01", ("1.jpg", 10), ("2.png", 20), ("memo.txt", 5));
		this.createVolume("Title 03");
		this.createVolume("Title 04", ("memo.txt", 5));

		await this.manager.SyncWorkVolumesAsync();

		Assert.Equal(new[] { 1m, 2m }, this.store.ExpandedVolumes.Select(v => v.VolumeNumber));
		var first = this.store.ExpandedVolumes[0];
		Assert.Equal("Title 01", first.Name);
		Assert.Equal(2, first.FileCount);
		Assert.Equal(30L, first.TotalImageBytes);
		Assert.False(first.IsChecked.Value);
		Assert.Null(first.Source);
		Assert.Equal(2, this.store.WorkVolumes.Count);
	}

	[Fact]
	public async Task 再スキャン_同一パスはインスタンスを維持し画像数とサイズを更新しIsCheckedとSourceを保持する()
	{
		var path = this.createVolume("Title 01", ("1.jpg", 10));
		await this.manager.SyncWorkVolumesAsync();
		var volume = this.store.ExpandedVolumes.Single();
		var workVolume = this.store.WorkVolumes.Single();
		using var source = new MaterialItem(MaterialItemType.Folder, MaterialSourceType.Folder, "src", @"C:\src");
		volume.Source = source;
		volume.IsChecked.Value = true;

		File.WriteAllBytes(Path.Combine(path, "2.jpg"), new byte[40]);
		await this.manager.SyncWorkVolumesAsync();

		Assert.Same(volume, this.store.ExpandedVolumes.Single());
		Assert.Equal(2, volume.FileCount);
		Assert.Equal(50L, volume.TotalImageBytes);
		Assert.True(volume.IsChecked.Value);
		Assert.Same(source, volume.Source);
		Assert.Same(workVolume, this.store.WorkVolumes.Single());
		Assert.Equal(2, workVolume.FileCount);
	}

	[Fact]
	public async Task 再スキャン_新規フォルダは巻番号順に挿入される()
	{
		this.createVolume("Title 01", ("1.jpg", 10));
		this.createVolume("Title 03", ("1.jpg", 10));
		await this.manager.SyncWorkVolumesAsync();
		var vol1 = this.store.ExpandedVolumes[0];

		this.createVolume("Title 02", ("1.jpg", 10));
		await this.manager.SyncWorkVolumesAsync();

		Assert.Equal(new[] { 1m, 2m, 3m }, this.store.ExpandedVolumes.Select(v => v.VolumeNumber));
		Assert.Same(vol1, this.store.ExpandedVolumes[0]);
	}

	[Fact]
	public async Task 再スキャン_フォルダ消失と画像0件になった巻は除外されDisposeされる()
	{
		var path1 = this.createVolume("Title 01", ("1.jpg", 10));
		var path2 = this.createVolume("Title 02", ("1.jpg", 10));
		this.createVolume("Title 03", ("1.jpg", 10));
		await this.manager.SyncWorkVolumesAsync();
		var removedByDelete = this.store.ExpandedVolumes[0];
		var removedByEmpty = this.store.ExpandedVolumes[1];
		using var source = new MaterialItem(MaterialItemType.Folder, MaterialSourceType.Folder, "src", @"C:\src");
		removedByDelete.Source = source;

		Directory.Delete(path1, recursive: true);
		File.Delete(Path.Combine(path2, "1.jpg"));
		await this.manager.SyncWorkVolumesAsync();

		Assert.Equal(3m, this.store.ExpandedVolumes.Single().VolumeNumber);
		Assert.True(removedByDelete.IsChecked.IsDisposed);
		Assert.True(removedByEmpty.IsChecked.IsDisposed);
		Assert.False(source.IsChecked.IsDisposed);
	}

	[Fact]
	public async Task BindingStoreDispose_ExpandedVolumesを破棄しSourceは破棄しない()
	{
		this.createVolume("Title 01", ("1.jpg", 10));
		await this.manager.SyncWorkVolumesAsync();
		var volume = this.store.ExpandedVolumes.Single();
		using var source = new MaterialItem(MaterialItemType.Folder, MaterialSourceType.Folder, "src", @"C:\src");
		volume.Source = source;

		this.store.Dispose();

		Assert.Empty(this.store.ExpandedVolumes);
		Assert.True(volume.IsChecked.IsDisposed);
		Assert.False(source.IsChecked.IsDisposed);
	}
}
