using MangaBinder.Bindings;
using MangaBinder.Series;
using MangaBinder.Settings;
using MangaBinder.Tags;

namespace MangaBinder.Sandboxes;

/// <summary>
/// 素材一覧の並び順、巻入力値の変換、MangaSeries.UpdateValues の単体テストです。
/// </summary>
public sealed class EditorCoreTest
{
	/// <summary>
	/// 素材は Folder → Archive/EPUB → その他の順に並び、同じ分類内では名前の Ordinal 昇順になることを確認します。
	/// </summary>
	[Fact]
	public void MaterialFileItemComparer_OrdersByTypeThenOrdinalName()
	{
		var items = new List<MaterialFileItem>
		{
			new() { Name = "b.zip", FullPath = "b.zip", ItemType = MaterialItemType.Archive },
			new() { Name = "z", FullPath = "z", ItemType = MaterialItemType.Folder },
			new() { Name = "a.zip", FullPath = "a.zip", ItemType = MaterialItemType.Archive },
			new() { Name = "a", FullPath = "a", ItemType = MaterialItemType.Folder },
		};

		items.Sort(MaterialFileItemComparer.Default);

		Assert.Equal(["a", "z", "a.zip", "b.zip"], items.Select(item => item.Name));
	}

	/// <summary>
	/// 巻の入力値（未入力・数値）が MangaSeries の int へ、int が入力値へ変換されることを確認します。
	/// </summary>
	[Fact]
	public void VolumeInputConverter_ConvertsBetweenInputAndSeriesVolume()
	{
		Assert.Equal(0, VolumeInputConverter.ToSeriesVolume(null));
		Assert.Equal(0, VolumeInputConverter.ToSeriesVolume(double.NaN));
		Assert.Equal(12, VolumeInputConverter.ToSeriesVolume(12));
		Assert.Null(VolumeInputConverter.ToInputValue(0));
		Assert.Equal(5d, VolumeInputConverter.ToInputValue(5));
	}

	/// <summary>
	/// UpdateValues は編集内容を反映しても SeriesId・WorkId・Sources を変更せず、タグの共有インスタンスを維持することを確認します。
	/// </summary>
	[Fact]
	public void UpdateValues_CopiesEditableValuesAndKeepsIdentityAndSources()
	{
		var sharedTag = new MangaTag { TagId = 1, Name = "tag" };
		var source = new MangaSource { SourceId = 7, Path = @"C:\material", Role = FolderRole.Material };
		var target = new MangaSeries { SeriesId = 3, Title = "旧", Sources = [source] };
		var edited = new MangaSeries { SeriesId = 3, Title = "新", Author = "作者", EndVolume = 5, SeriesCompleted = true };
		edited.Tags.Add(sharedTag);

		target.UpdateValues(edited);

		Assert.Equal("新", target.Title);
		Assert.Equal("作者", target.Author);
		Assert.Equal(5, target.EndVolume);
		Assert.True(target.SeriesCompleted);
		Assert.Equal(3, target.SeriesId);
		Assert.Same(source, Assert.Single(target.Sources));
		Assert.Same(sharedTag, Assert.Single(target.Tags));
	}
}
