using System.Text.Json;
using MangaBinder.Bindings;
using MangaBinder.Helpers;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MangaBinder.Services.Tests.Bindings;

/// <summary>
/// 展開済み巻追加Dialogの <see cref="VolumeSelectionManager"/> 処理と、製本設定DTOの SourceMaterial のテストです。
/// </summary>
public class ExpandedVolumeDialogTests : IDisposable
{
	private readonly string seriesFolder;
	private readonly BindingStore store = new();
	private readonly VolumeSelectionManager manager;

	public ExpandedVolumeDialogTests()
	{
		SupportedExtensionHelper.Initialize(
		[
			new SupportedFileExtension { Extension = ".jpg", FileType = (int)FileType.Image },
		]);

		this.seriesFolder = Path.Combine(Path.GetTempPath(), "MangaBinderTests_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(this.seriesFolder);
		this.store.ExpandedSeriesFolderPath.Value = this.seriesFolder;

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

	private string createVolume(string name)
	{
		var path = Path.Combine(this.seriesFolder, name);
		Directory.CreateDirectory(path);
		File.WriteAllBytes(Path.Combine(path, "1.jpg"), new byte[10]);
		return path;
	}

	private BindingVolume addSourceVolume(decimal number)
	{
		var source = new MaterialItem(
			MaterialItemType.Folder,
			MaterialSourceType.Folder,
			"src" + number,
			@"C:\src\" + number,
			fileCount: 1,
			totalImageBytes: 5L,
			sourcePath: @"C:\src\" + number);
		this.store.Materials.Add(source);
		source.IsChecked.Value = true;
		var volume = new BindingVolume(source);
		volume.VolumeNumber.Value = number;
		this.store.InsertBindingVolumeInOrder(volume);
		return volume;
	}

	[Fact]
	public async Task 展開済み巻が未選択なら全件チェックされExpandedVolumeの一覧が返る()
	{
		this.createVolume("Title 01");
		this.createVolume("Title 02");
		await this.manager.SyncExpandedVolumesAsync();

		var list = this.manager.PrepareExpandedVolumeSelection();

		Assert.Equal(2, list.Count);
		Assert.All(list, v => Assert.True(v.IsChecked.Value));
		Assert.All(list, v => Assert.Contains(v, this.store.ExpandedVolumes));
	}

	[Fact]
	public async Task 追加_生成Materialを入力としSourceはnullのままで物理フォルダは維持される()
	{
		var path = this.createVolume("Title 01");
		await this.manager.SyncExpandedVolumesAsync();
		var expanded = this.store.ExpandedVolumes.Single();
		expanded.IsChecked.Value = true;

		this.manager.ApplyExpandedVolumeSelection();
		this.manager.ApplyExpandedVolumeSelection();

		var volume = Assert.Single(this.store.BindingVolumes);
		Assert.Equal(1m, volume.VolumeNumber.Value);
		Assert.Equal(MaterialSourceType.ExpandedFolder, volume.Material.Type);
		Assert.Equal(path, volume.Material.FullPath, ignoreCase: true);
		Assert.Null(volume.Material.Source);
		Assert.Null(expanded.Source);
	}

	[Fact]
	public async Task 追加_生成MaterialItemとExpandedVolumeがともにチェック済みになり二重追加されない()
	{
		this.createVolume("Title 01");
		await this.manager.SyncExpandedVolumesAsync();
		var expanded = this.store.ExpandedVolumes.Single();
		expanded.IsChecked.Value = true;

		this.manager.ApplyExpandedVolumeSelection();
		var volume = Assert.Single(this.store.BindingVolumes);
		var material = volume.Material;
		this.manager.ApplyExpandedVolumeSelection();

		Assert.True(material.IsChecked.Value);
		Assert.True(expanded.IsChecked.Value);
		Assert.Same(volume, Assert.Single(this.store.BindingVolumes));
		Assert.Same(material, volume.Material);
	}

	[Fact]
	public async Task 置換_追加された展開済み巻のMaterialItemはチェック済みで元素材のチェックは外れる()
	{
		this.createVolume("Title 01");
		await this.manager.SyncExpandedVolumesAsync();
		var original = this.addSourceVolume(1m);
		var originalMaterial = original.Material;
		var expanded = this.store.ExpandedVolumes.Single();
		expanded.IsChecked.Value = true;

		this.manager.ApplyExpandedVolumeSelection();

		var volume = Assert.Single(this.store.BindingVolumes);
		Assert.True(volume.Material.IsChecked.Value);
		Assert.True(expanded.IsChecked.Value);
		Assert.False(originalMaterial.IsChecked.Value);
	}

	[Fact]
	public async Task 置換_同じ巻番号の元素材は確認対象となり適用後に除外されSourceは設定されない()
	{
		this.createVolume("Title 01");
		await this.manager.SyncExpandedVolumesAsync();
		var original = this.addSourceVolume(1m);
		var expanded = this.store.ExpandedVolumes.Single();
		expanded.IsChecked.Value = true;

		var replacements = this.manager.FindExpandedVolumeReplacements();

		var replacement = Assert.Single(replacements);
		Assert.Same(expanded, replacement.ExpandedVolume);
		Assert.Same(original.Material, Assert.Single(replacement.ReplacedMaterials));
		Assert.Same(original, Assert.Single(this.store.BindingVolumes));

		this.manager.ApplyExpandedVolumeSelection();

		var volume = Assert.Single(this.store.BindingVolumes);
		Assert.NotSame(original, volume);
		Assert.Equal(MaterialSourceType.ExpandedFolder, volume.Material.Type);
		Assert.Null(volume.Material.Source);
		Assert.Null(expanded.Source);
		Assert.Single(this.store.Materials);
	}

	[Fact]
	public async Task 選択済みの巻はチェック状態が反映され置換対象にもならない()
	{
		this.createVolume("Title 01");
		this.createVolume("Title 02");
		await this.manager.SyncExpandedVolumesAsync();
		this.store.ExpandedVolumes[0].IsChecked.Value = true;
		this.store.ExpandedVolumes[1].IsChecked.Value = false;
		this.manager.ApplyExpandedVolumeSelection();

		this.manager.PrepareExpandedVolumeSelection();

		Assert.True(this.store.ExpandedVolumes[0].IsChecked.Value);
		Assert.False(this.store.ExpandedVolumes[1].IsChecked.Value);
		Assert.Empty(this.manager.FindExpandedVolumeReplacements());
	}

	[Fact]
	public async Task 解除_論理的に除外されフォルダとExpandedVolumeとSourceは維持される()
	{
		var path = this.createVolume("Title 01");
		await this.manager.SyncExpandedVolumesAsync();
		var expanded = this.store.ExpandedVolumes.Single();
		var source = new MaterialItem(MaterialItemType.Folder, MaterialSourceType.Folder, "s", @"C:\s");
		this.store.Materials.Add(source);
		expanded.Source = source;
		expanded.IsChecked.Value = true;
		this.manager.ApplyExpandedVolumeSelection();
		var generated = this.store.BindingVolumes.Single().Material;
		Assert.True(generated.IsChecked.Value);

		expanded.IsChecked.Value = false;
		this.manager.ApplyExpandedVolumeSelection();

		Assert.False(generated.IsChecked.Value);
		Assert.False(expanded.IsChecked.Value);
		Assert.Empty(this.store.BindingVolumes);
		Assert.True(Directory.Exists(path));
		Assert.Same(expanded, Assert.Single(this.store.ExpandedVolumes));
		Assert.Same(source, expanded.Source);
		Assert.False(source.IsChecked.IsDisposed);
		Assert.True(generated.IsChecked.IsDisposed);
	}

	[Fact]
	public void HasBeenUsedForExpansion_元素材の再選択と展開済み巻の選択解除でもマークが残る()
	{
		var volume = this.addSourceVolume(1m);
		var source = volume.Material;
		var expanded = this.store.AddOrUpdateExpandedVolume("Title 01", Path.Combine(this.seriesFolder, "Title 01"), 1m, 1, 10L);
		this.store.SwitchToExpandedVolume(volume, expanded);
		Assert.True(source.HasBeenUsedForExpansion.Value);

		this.manager.UnselectMaterial(volume.Material);

		Assert.Empty(this.store.BindingVolumes);
		Assert.True(source.HasBeenUsedForExpansion.Value);

		this.manager.SelectMaterial(source);

		Assert.True(source.IsChecked.Value);
		Assert.True(source.HasBeenUsedForExpansion.Value);
	}

	[Fact]
	public void CreateDto_元素材由来と展開済みSourceなしではSourceMaterialがnullで展開済みSourceありでは識別情報が入る()
	{
		var sourceVolume = this.addSourceVolume(1m);
		Assert.Null(sourceVolume.CreateDto().SourceMaterial);

		var expanded = this.store.AddOrUpdateExpandedVolume("Vol_01", @"C:\work\Vol_01", 1m, 1, 1L);
		this.store.SwitchToExpandedVolume(sourceVolume, expanded);
		var withSource = sourceVolume.CreateDto();

		Assert.Equal(MaterialSourceType.ExpandedFolder, withSource.Type);
		Assert.Equal(@"C:\work\Vol_01", withSource.Material.FullPath);
		Assert.NotNull(withSource.SourceMaterial);
		Assert.Equal(@"C:\src\1", withSource.SourceMaterial!.FullPath);
		Assert.Empty(withSource.SourceMaterial.Children);

		var other = this.store.AddOrUpdateExpandedVolume("Vol_02", @"C:\work\Vol_02", 2m, 1, 1L);
		using var material = other.CreateMaterial();
		using var noSource = new BindingVolume(material);
		Assert.Null(noSource.CreateDto().SourceMaterial);
	}

	[Fact]
	public void DTO_SourceMaterialのない旧JSONはnullとして読め往復できる()
	{
		var volume = this.addSourceVolume(1m);
		var expanded = this.store.AddOrUpdateExpandedVolume("Vol_01", @"C:\work\Vol_01", 1m, 1, 1L);
		this.store.SwitchToExpandedVolume(volume, expanded);
		var dto = volume.CreateDto();

		var json = JsonSerializer.Serialize(dto);
		var restored = JsonSerializer.Deserialize<BindingVolumeDto>(json)!;
		Assert.Equal(@"C:\src\1", restored.SourceMaterial!.FullPath);

		var legacy = JsonSerializer.Deserialize<BindingVolumeDto>(
			json.Replace("\"SourceMaterial\"", "\"Unknown\""))!;
		Assert.Null(legacy.SourceMaterial);
		Assert.Equal(@"C:\work\Vol_01", legacy.Material.FullPath);
	}

	[Fact]
	public void UpdateFolderLocation_ExpandedVolumeとMaterialItemの名前とパスが更新されインスタンスと参照は維持される()
	{
		var source = new MaterialItem(MaterialItemType.Folder, MaterialSourceType.Folder, "s", @"C:\s");
		var expanded = this.store.AddOrUpdateExpandedVolume("Vol_01", @"C:\work\Vol_01", 1m, 1, 1L);
		expanded.Source = source;
		using var material = this.store.CreateExpandedMaterial(expanded);

		expanded.UpdateFolderLocation("Vol_001", @"C:\work\Vol_001");
		material.UpdateFolderLocation("Vol_001", @"C:\work\Vol_001");

		Assert.Equal("Vol_001", expanded.Name);
		Assert.Equal(@"C:\work\Vol_001", expanded.FullPath);
		Assert.Equal("Vol_001", material.Name);
		Assert.Equal(@"C:\work\Vol_001", material.FullPath);
		Assert.Equal(@"C:\work\Vol_001", material.SourcePath);
		Assert.Same(source, expanded.Source);
		Assert.Throws<InvalidOperationException>(() => source.UpdateFolderLocation("x", @"C:\x"));
		source.Dispose();
	}
}
