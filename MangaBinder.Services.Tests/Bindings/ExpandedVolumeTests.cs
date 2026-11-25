using MangaBinder.Bindings;

namespace MangaBinder.Services.Tests.Bindings;

/// <summary>
/// <see cref="ExpandedVolume"/> のテストです。
/// </summary>
public class ExpandedVolumeTests
{
	[Fact]
	public void コンストラクタ_指定値が設定されIsCheckedは初期falseでSourceはnull()
	{
		using var volume = new ExpandedVolume("Vol_01", @"C:\work\Vol_01", 1m, 10, 1000L);

		Assert.Equal("Vol_01", volume.Name);
		Assert.Equal(@"C:\work\Vol_01", volume.FullPath);
		Assert.Equal(1m, volume.VolumeNumber);
		Assert.Equal(10, volume.FileCount);
		Assert.Equal(1000L, volume.TotalImageBytes);
		Assert.False(volume.IsChecked.Value);
		Assert.Null(volume.Source);
	}

	[Fact]
	public void CreateMaterial_ExpandedVolumeの情報から新しいMaterialItemを生成する()
	{
		using var source = new MaterialItem(MaterialItemType.Folder, MaterialSourceType.Folder, "src", @"C:\src");
		using var volume = new ExpandedVolume("Vol_01", @"C:\work\Vol_01", 1m, 10, 1000L, source);

		using var material = volume.CreateMaterial();

		Assert.Equal(MaterialItemType.Folder, material.ItemType);
		Assert.Equal(MaterialSourceType.ExpandedFolder, material.Type);
		Assert.Equal("Vol_01", material.Name);
		Assert.Equal(@"C:\work\Vol_01", material.FullPath);
		Assert.Equal(@"C:\work\Vol_01", material.SourcePath);
		Assert.Equal(10, material.FileCount);
		Assert.Equal(1000L, material.TotalImageBytes);
		Assert.Same(source, material.Source);
	}

	[Fact]
	public void CreateMaterial_呼び出すたびに新しいインスタンスを返す()
	{
		using var volume = new ExpandedVolume("Vol_01", @"C:\work\Vol_01", 1m, 10, 1000L);

		using var first = volume.CreateMaterial();
		using var second = volume.CreateMaterial();

		Assert.NotSame(first, second);
		Assert.Null(first.Source);
	}

	[Fact]
	public void UpdateFolderState_画像数とサイズのみ更新しIsCheckedとSourceは維持する()
	{
		using var source = new MaterialItem(MaterialItemType.Folder, MaterialSourceType.Folder, "src", @"C:\src");
		using var volume = new ExpandedVolume("Vol_01", @"C:\work\Vol_01", 1m, 10, 1000L, source);
		volume.IsChecked.Value = true;

		volume.UpdateFolderState(20, 2000L);

		Assert.Equal(20, volume.FileCount);
		Assert.Equal(2000L, volume.TotalImageBytes);
		Assert.True(volume.IsChecked.Value);
		Assert.Same(source, volume.Source);
	}

	[Fact]
	public void Dispose_Sourceは破棄しない()
	{
		using var source = new MaterialItem(MaterialItemType.Folder, MaterialSourceType.Folder, "src", @"C:\src");
		var volume = new ExpandedVolume("Vol_01", @"C:\work\Vol_01", 1m, 10, 1000L, source);

		volume.Dispose();

		Assert.False(source.IsChecked.IsDisposed);
		Assert.True(volume.IsChecked.IsDisposed);
	}

	[Fact]
	public void MaterialItemDispose_Sourceは破棄しない()
	{
		using var source = new MaterialItem(MaterialItemType.Folder, MaterialSourceType.Folder, "src", @"C:\src");
		using var volume = new ExpandedVolume("Vol_01", @"C:\work\Vol_01", 1m, 10, 1000L, source);

		volume.CreateMaterial().Dispose();

		Assert.False(source.IsChecked.IsDisposed);
	}
}
