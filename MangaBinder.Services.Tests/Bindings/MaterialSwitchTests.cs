using MangaBinder.Bindings;
using R3;

namespace MangaBinder.Services.Tests.Bindings;

/// <summary>
/// BindingVolume.Material の Reactive 化、展開完了後の Material 切替、生成 MaterialItem の所有管理のテストです。
/// </summary>
public class MaterialSwitchTests : IDisposable
{
	private readonly BindingStore store = new();

	public void Dispose()
	{
		this.store.Dispose();
	}

	private static MaterialItem createSourceMaterial(string name = "src", int fileCount = 3, long bytes = 300L)
		=> new(
			MaterialItemType.Folder,
			MaterialSourceType.Folder,
			name,
			@"C:\src\" + name,
			fileCount: fileCount,
			totalImageBytes: bytes,
			sourcePath: @"C:\src\" + name);

	private (BindingVolume Volume, MaterialItem Source, ExpandedVolume Expanded) createSelectedSourceVolume()
	{
		var source = createSourceMaterial();
		this.store.Materials.Add(source);
		source.IsChecked.Value = true;

		var volume = new BindingVolume(source);
		volume.VolumeNumber.Value = 1m;
		this.store.InsertBindingVolumeInOrder(volume);

		var expanded = this.store.AddOrUpdateExpandedVolume("Vol_01", @"C:\work\Vol_01", 1m, 2, 20L);
		return (volume, source, expanded);
	}

	[Fact]
	public void SetMaterial_切り替えるとMaterialとCurrentMaterialが更新され通知される()
	{
		using var first = createSourceMaterial("a");
		using var second = createSourceMaterial("b");
		using var volume = new BindingVolume(first);
		var notified = new List<MaterialItem>();
		using var subscription = volume.CurrentMaterial.AsObservable().Skip(1).Subscribe(notified.Add);

		volume.SetMaterial(second);

		Assert.Same(second, volume.Material);
		Assert.Same(second, volume.CurrentMaterial.Value);
		Assert.Same(second, Assert.Single(notified));
	}

	[Fact]
	public void SetMaterial_同一Materialの再設定では通知されない()
	{
		using var material = createSourceMaterial();
		using var volume = new BindingVolume(material);
		var count = 0;
		using var subscription = volume.CurrentMaterial.AsObservable().Skip(1).Subscribe(_ => count++);

		volume.SetMaterial(material);

		Assert.Equal(0, count);
	}

	[Fact]
	public void SetMaterial_nullは例外()
	{
		using var material = createSourceMaterial();
		using var volume = new BindingVolume(material);

		Assert.Throws<ArgumentNullException>(() => volume.SetMaterial(null!));
	}

	[Fact]
	public void SwitchToExpandedVolume_BindingVolumeとBindingImageのインスタンスを維持しMaterialだけ切り替わる()
	{
		var (volume, source, expanded) = this.createSelectedSourceVolume();
		var image = volume.AddImage("a.jpg", "a.jpg");
		image.FilePath = @"C:\work\Vol_01\a.jpg";
		image.ExpandedFileSize = 1234;

		this.store.SwitchToExpandedVolume(volume, expanded);

		Assert.Same(volume, Assert.Single(this.store.BindingVolumes));
		Assert.Same(image, Assert.Single(volume.Images));
		Assert.Equal(@"C:\work\Vol_01\a.jpg", image.FilePath);
		Assert.Equal(1234, image.ExpandedFileSize);
		Assert.Equal(MaterialSourceType.ExpandedFolder, volume.Material.Type);
		Assert.Equal(@"C:\work\Vol_01", volume.Material.FullPath);
		Assert.Same(source, volume.Material.Source);
		Assert.Same(source, expanded.Source);
		Assert.False(source.IsChecked.Value);
		Assert.True(expanded.IsChecked.Value);
		Assert.True(volume.Material.IsChecked.Value);
		Assert.Same(source, Assert.Single(this.store.Materials));
		Assert.False(source.IsChecked.IsDisposed);
	}

	[Fact]
	public void SwitchToExpandedVolume_合計画像サイズが切替後のMaterialに追従する()
	{
		var (volume, _, expanded) = this.createSelectedSourceVolume();
		Assert.Equal(300L, this.store.SelectedVolumeTotalImageBytes.Value);

		this.store.SwitchToExpandedVolume(volume, expanded);

		Assert.Equal(20L, this.store.SelectedVolumeTotalImageBytes.Value);
	}

	[Fact]
	public void NotifyMaterialChanged_Material内容の変更後に合計画像サイズが再計算される()
	{
		var (volume, _, expanded) = this.createSelectedSourceVolume();
		this.store.SwitchToExpandedVolume(volume, expanded);

		volume.Material.TotalImageBytes = 999L;
		volume.NotifyMaterialChanged();

		Assert.Equal(999L, this.store.SelectedVolumeTotalImageBytes.Value);
	}

	[Fact]
	public void 削除したBindingVolumeのMaterial変更は合計画像サイズへ影響しない()
	{
		var (volume, source, expanded) = this.createSelectedSourceVolume();
		this.store.BindingVolumes.Remove(volume);
		Assert.Equal(0L, this.store.SelectedVolumeTotalImageBytes.Value);

		using var other = expanded.CreateMaterial();
		other.TotalImageBytes = 500L;
		volume.SetMaterial(other);

		Assert.Equal(0L, this.store.SelectedVolumeTotalImageBytes.Value);
		volume.Dispose();
		Assert.False(source.IsChecked.IsDisposed);
	}

	[Fact]
	public void SwitchToExpandedVolume_BindingVolumesに無い巻や既に展開済みの巻は例外()
	{
		var (volume, _, expanded) = this.createSelectedSourceVolume();
		using var stranger = new BindingVolume(createSourceMaterial("x"));

		Assert.Throws<InvalidOperationException>(() => this.store.SwitchToExpandedVolume(stranger, expanded));

		this.store.SwitchToExpandedVolume(volume, expanded);
		Assert.Throws<InvalidOperationException>(() => this.store.SwitchToExpandedVolume(volume, expanded));
	}

	[Fact]
	public void ReleaseExpandedMaterial_使用中は破棄せず除外後に破棄し元素材は破棄しない()
	{
		var (volume, source, expanded) = this.createSelectedSourceVolume();
		this.store.SwitchToExpandedVolume(volume, expanded);
		var generated = volume.Material;

		Assert.False(this.store.ReleaseExpandedMaterial(generated));
		Assert.False(generated.IsChecked.IsDisposed);

		this.store.BindingVolumes.Remove(volume);
		volume.Dispose();

		Assert.True(this.store.ReleaseExpandedMaterial(generated));
		Assert.True(generated.IsChecked.IsDisposed);
		Assert.False(expanded.IsChecked.Value);
		Assert.False(source.IsChecked.IsDisposed);
		Assert.False(this.store.ReleaseExpandedMaterial(generated));
	}

	[Fact]
	public void ReleaseExpandedMaterial_Storeが所有しない元素材は破棄しない()
	{
		var source = createSourceMaterial();
		this.store.Materials.Add(source);

		Assert.False(this.store.ReleaseExpandedMaterial(source));
		Assert.False(source.IsChecked.IsDisposed);
	}

	[Fact]
	public void ClearSourceReferences_削除される元素材を指すSourceのみnullへ戻し破棄はしない()
	{
		var (volume, source, expanded) = this.createSelectedSourceVolume();
		this.store.SwitchToExpandedVolume(volume, expanded);
		var other = this.store.AddOrUpdateExpandedVolume("Vol_02", @"C:\work\Vol_02", 2m, 1, 1L);
		using var otherSource = createSourceMaterial("other");
		other.Source = otherSource;

		this.store.ClearSourceReferences(source);

		Assert.Null(expanded.Source);
		Assert.Null(volume.Material.Source);
		Assert.Same(otherSource, other.Source);
		Assert.False(source.IsChecked.IsDisposed);
	}

	[Fact]
	public void StoreDispose_生成MaterialとExpandedVolumeを破棄しSourceを二重破棄しない()
	{
		var (volume, source, expanded) = this.createSelectedSourceVolume();
		this.store.SwitchToExpandedVolume(volume, expanded);
		var generated = volume.Material;

		this.store.Dispose();

		Assert.True(generated.IsChecked.IsDisposed);
		Assert.True(expanded.IsChecked.IsDisposed);
		Assert.True(source.IsChecked.IsDisposed);
	}

	[Fact]
	public void CloneForImageSplitter_共有Materialを破棄しない()
	{
		var (volume, _, expanded) = this.createSelectedSourceVolume();
		this.store.SwitchToExpandedVolume(volume, expanded);
		var clone = volume.CloneForImageSplitter();

		Assert.Same(volume.Material, clone.Material);
		clone.Dispose();

		Assert.False(volume.Material.IsChecked.IsDisposed);
	}

	[Fact]
	public void HasBeenUsedForExpansion_新しいMaterialItemの初期値はfalse()
	{
		using var material = createSourceMaterial();

		Assert.False(material.HasBeenUsedForExpansion.Value);
	}

	[Fact]
	public void HasBeenUsedForExpansion_Material切替成功後に元素材がtrueになりチェックはfalseのまま()
	{
		var (volume, source, expanded) = this.createSelectedSourceVolume();
		Assert.False(source.HasBeenUsedForExpansion.Value);

		this.store.SwitchToExpandedVolume(volume, expanded);

		Assert.True(source.HasBeenUsedForExpansion.Value);
		Assert.False(source.IsChecked.Value);
		Assert.False(volume.Material.HasBeenUsedForExpansion.Value);
	}

	[Fact]
	public void HasBeenUsedForExpansion_切替の通知は一度だけ流れる()
	{
		var (volume, source, expanded) = this.createSelectedSourceVolume();
		var notified = new List<bool>();
		using var subscription = source.HasBeenUsedForExpansion.AsObservable().Skip(1).Subscribe(notified.Add);

		this.store.SwitchToExpandedVolume(volume, expanded);

		Assert.True(Assert.Single(notified));
	}

	[Fact]
	public void HasBeenUsedForExpansion_切替が成立しない場合はマークされない()
	{
		var (volume, source, expanded) = this.createSelectedSourceVolume();
		using var stranger = new BindingVolume(createSourceMaterial("x"));
		var strangerMaterial = stranger.Material;

		Assert.Throws<InvalidOperationException>(() => this.store.SwitchToExpandedVolume(stranger, expanded));

		Assert.False(strangerMaterial.HasBeenUsedForExpansion.Value);
		Assert.False(source.HasBeenUsedForExpansion.Value);

		this.store.SwitchToExpandedVolume(volume, expanded);
		var generated = volume.Material;
		Assert.Throws<InvalidOperationException>(() => this.store.SwitchToExpandedVolume(volume, expanded));
		Assert.False(generated.HasBeenUsedForExpansion.Value);
	}

	[Fact]
	public void HasBeenUsedForExpansion_別素材へ再展開しても先に使用した素材のマークが残る()
	{
		var (volumeA, sourceA, expanded) = this.createSelectedSourceVolume();
		this.store.SwitchToExpandedVolume(volumeA, expanded);

		this.store.BindingVolumes.Remove(volumeA);
		volumeA.Dispose();
		var sourceB = createSourceMaterial("b");
		this.store.Materials.Add(sourceB);
		sourceB.IsChecked.Value = true;
		var volumeB = new BindingVolume(sourceB);
		volumeB.VolumeNumber.Value = 1m;
		this.store.InsertBindingVolumeInOrder(volumeB);

		this.store.SwitchToExpandedVolume(volumeB, expanded);

		Assert.Same(sourceB, expanded.Source);
		Assert.True(sourceA.HasBeenUsedForExpansion.Value);
		Assert.True(sourceB.HasBeenUsedForExpansion.Value);
	}

	[Fact]
	public void HasBeenUsedForExpansion_チェックの切替や展開済み巻の論理除外では戻らない()
	{
		var (volume, source, expanded) = this.createSelectedSourceVolume();
		this.store.SwitchToExpandedVolume(volume, expanded);

		source.IsChecked.Value = true;
		source.IsChecked.Value = false;
		this.store.BindingVolumes.Remove(volume);
		expanded.IsChecked.Value = false;

		Assert.True(source.HasBeenUsedForExpansion.Value);
	}

	[Fact]
	public void HasBeenUsedForExpansion_新しいBindingStoreのMaterialItemには過去セッションのマークが残らない()
	{
		var (volume, source, expanded) = this.createSelectedSourceVolume();
		this.store.SwitchToExpandedVolume(volume, expanded);
		Assert.True(source.HasBeenUsedForExpansion.Value);

		using var nextStore = new BindingStore();
		var nextSource = createSourceMaterial();
		nextStore.Materials.Add(nextSource);

		Assert.False(nextSource.HasBeenUsedForExpansion.Value);
	}

	[Fact]
	public void HasBeenUsedForExpansion_MaterialItemのDisposeで破棄される()
	{
		var material = createSourceMaterial();

		material.Dispose();

		Assert.True(material.HasBeenUsedForExpansion.IsDisposed);
	}

	[Fact]
	public void AddOrUpdateExpandedVolume_同一パスは大文字小文字を区別せず再利用する()
	{
		var first = this.store.AddOrUpdateExpandedVolume("Vol_01", @"C:\work\Vol_01", 1m, 2, 20L);

		var again = this.store.AddOrUpdateExpandedVolume("Vol_01", @"c:\WORK\vol_01", 1m, 5, 50L);

		Assert.Same(first, again);
		Assert.Single(this.store.ExpandedVolumes);
		Assert.Equal(5, first.FileCount);
		Assert.Equal(50L, first.TotalImageBytes);
	}
}
