using MangaBinder.Bindings;
using MangaBinder.Series;
using MangaBinder.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace MangaBinder.Sandboxes;

/// <summary>
/// MangaSeriesManager の編集準備・タイトル検証・所持最大巻数の手動変更判定の単体テストです。
/// 使用しない依存はテスト対象のメソッドから参照されないため null を渡します。
/// </summary>
public sealed class MangaSeriesManagerEditTest : IDisposable
{
	/// <summary>テスト用の一時ルートフォルダ。</summary>
	private readonly string rootPath;

	/// <summary>テスト用の編集セッション状態。</summary>
	private readonly EditorStore editorStore = new();

	/// <summary>テスト用のタグストア。</summary>
	private readonly TagStore tagStore = new();

	/// <summary>
	/// 一時ルートフォルダを作成します。
	/// </summary>
	public MangaSeriesManagerEditTest()
	{
		this.rootPath = Path.Combine(Path.GetTempPath(), "MangaBinderManagerTests_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(this.rootPath);
	}

	/// <summary>
	/// 一時フォルダと編集セッション状態を破棄します。
	/// </summary>
	public void Dispose()
	{
		this.editorStore.Dispose();
		Directory.Delete(this.rootPath, true);
	}

	/// <summary>
	/// テスト対象の Manager を作成します。
	/// </summary>
	private MangaSeriesManager createManager()
		=> new(
			this.editorStore,
			new AppSettings { ConnectionString = string.Empty },
			null!,
			null!,
			this.tagStore,
			null!,
			new MaterialManager(),
			new OwnedVolumeEstimator(),
			null!,
			NullLogger<MangaSeriesManager>.Instance);

	/// <summary>
	/// 編集セッションを手動で初期化します（元作品と、そのクローン）。
	/// </summary>
	private (MangaSeries Original, MangaSeries Clone) startSession(string title, int ownedMaxVolume, bool manuallyEdited = false)
	{
		var original = new MangaSeries
		{
			SeriesId = 10,
			Title = title,
			OwnedMaxVolume = ownedMaxVolume,
			IsOwnedMaxVolumeManuallyEdited = manuallyEdited,
		};
		var clone = new MangaSeries
		{
			SeriesId = 10,
			Title = title,
			OwnedMaxVolume = ownedMaxVolume,
			IsOwnedMaxVolumeManuallyEdited = manuallyEdited,
		};

		this.editorStore.Initialize(original, clone, new MangaSeriesNotifier(clone, this.tagStore), [], [], null, false, false, 0, null);
		return (original, clone);
	}

	/// <summary>
	/// 画面入力値を作成します。
	/// </summary>
	private static EditorInputValues createInput(string title, double? ownedMaxVolume)
		=> new() { Title = title, OwnedMaxVolume = ownedMaxVolume, StartVolume = 1 };

	/// <summary>
	/// 実在する素材フォルダが複数ある作品は編集準備が中止され、EditorStore が初期化されないことを確認します。
	/// </summary>
	[Fact]
	public async Task PrepareEditAsync_MultipleExistingMaterialFolders_ReturnsErrorAndKeepsStoreEmpty()
	{
		var a = Directory.CreateDirectory(Path.Combine(this.rootPath, "a")).FullName;
		var b = Directory.CreateDirectory(Path.Combine(this.rootPath, "b")).FullName;
		var series = new MangaSeries
		{
			SeriesId = 1,
			Title = "テスト",
			Sources =
			[
				new MangaSource { SourceId = 1, Path = a, Role = FolderRole.Material },
				new MangaSource { SourceId = 2, Path = b, Role = FolderRole.Material },
			],
		};

		var result = await this.createManager().PrepareEditAsync(series);

		Assert.Equal(EditPrepareResult.MultipleMaterialFolders, result);
		Assert.Null(this.editorStore.EditTarget.Value);
		Assert.Null(this.editorStore.OriginalSeries);
	}

	/// <summary>
	/// 古い（実在しない）Material レコードが先頭に残っていても、実在する 1 件が編集対象になることを確認します。
	/// </summary>
	[Fact]
	public async Task PrepareEditAsync_StaleRecordFirst_UsesExistingMaterialSource()
	{
		var alive = Directory.CreateDirectory(Path.Combine(this.rootPath, "alive")).FullName;
		var series = new MangaSeries
		{
			SeriesId = 0,
			Title = "テスト",
			Sources =
			[
				new MangaSource { SourceId = 1, Path = Path.Combine(this.rootPath, "stale"), Role = FolderRole.Material },
				new MangaSource { SourceId = 2, Path = alive, Role = FolderRole.Material },
			],
		};

		var result = await this.createManager().PrepareEditAsync(series);

		Assert.Equal(EditPrepareResult.Ready, result);
		Assert.Equal(2, this.editorStore.MaterialSource?.SourceId);
		Assert.Same(series, this.editorStore.OriginalSeries);
		Assert.NotSame(series, this.editorStore.EditTarget.Value);
	}

	/// <summary>
	/// 既存正式作品のタイトルが同一作品と判定できる表記修正（全角・半角、スペース、大文字小文字）なら検証を通過することを確認します。
	/// </summary>
	[Theory]
	[InlineData("ドラゴン ボール", "ドラゴンボール")]
	[InlineData("１４歳", "14歳")]
	[InlineData("Fate", "FATE")]
	public void ValidateForRegister_ExistingSeriesNotationFix_ReturnsNone(string originalTitle, string editedTitle)
	{
		this.startSession(originalTitle, 0);

		var error = this.createManager().ValidateForRegister(createInput(editedTitle, 0));

		Assert.Equal(EditorValidationError.None, error);
	}

	/// <summary>
	/// 既存正式作品のタイトルが別作品と判定される場合は TitleMismatch になり、正本が変更されないことを確認します。
	/// </summary>
	[Fact]
	public void ValidateForRegister_ExistingSeriesDifferentTitle_ReturnsTitleMismatchWithoutChangingOriginal()
	{
		var (original, _) = this.startSession("ドラゴンボール", 0);

		var error = this.createManager().ValidateForRegister(createInput("ワンピース", 0));

		Assert.Equal(EditorValidationError.TitleMismatch, error);
		Assert.Equal("ドラゴンボール", original.Title);
	}

	/// <summary>
	/// タイトルを元に戻すと、編集用クローンだけが元のタイトルになることを確認します。
	/// </summary>
	[Fact]
	public void RestoreOriginalTitle_RestoresCloneTitle()
	{
		var (original, clone) = this.startSession("ドラゴンボール", 0);
		var manager = this.createManager();
		manager.ValidateForRegister(createInput("ワンピース", 0));

		var restored = manager.RestoreOriginalTitle();

		Assert.Equal("ドラゴンボール", restored);
		Assert.Equal("ドラゴンボール", clone.Title);
		Assert.Equal("ドラゴンボール", original.Title);
	}

	/// <summary>
	/// 所持最大巻数が編集用クローンの値と同じ入力では、手動変更フラグが立たないことを確認します。
	/// </summary>
	[Fact]
	public void ApplyInput_OwnedMaxVolumeUnchanged_DoesNotSetManualFlag()
	{
		var (_, clone) = this.startSession("テスト", 3);

		this.createManager().ApplyInput(createInput("テスト", 3));

		Assert.False(clone.IsOwnedMaxVolumeManuallyEdited);
	}

	/// <summary>
	/// ユーザーが所持最大巻数を変更した入力では、手動変更フラグが立つことを確認します。
	/// </summary>
	[Fact]
	public void ApplyInput_OwnedMaxVolumeChangedByUser_SetsManualFlag()
	{
		var (_, clone) = this.startSession("テスト", 3);

		this.createManager().ApplyInput(createInput("テスト", 5));

		Assert.True(clone.IsOwnedMaxVolumeManuallyEdited);
		Assert.Equal(5, clone.OwnedMaxVolume);
	}

	/// <summary>
	/// 既に手動変更フラグが立っている場合は、値が同じでも false に戻らないことを確認します。
	/// </summary>
	[Fact]
	public void ApplyInput_ManualFlagAlreadyTrue_StaysTrue()
	{
		var (_, clone) = this.startSession("テスト", 3, manuallyEdited: true);

		this.createManager().ApplyInput(createInput("テスト", 3));

		Assert.True(clone.IsOwnedMaxVolumeManuallyEdited);
	}

	/// <summary>
	/// 素材の削除による自動推定は、所持最大巻数を更新しても手動変更フラグを立てないことを確認します。
	/// 推定できない（素材が 0 件になった）場合は現在の値を維持します。
	/// </summary>
	[Fact]
	public void RemoveMaterialFile_AutoEstimate_UpdatesVolumeWithoutManualFlagAndKeepsValueWhenEmpty()
	{
		var (original, clone) = this.startSession("テスト", 9);
		this.editorStore.MaterialFiles.Add(new MaterialFileItem { Name = "テスト 第1巻.zip", FullPath = @"C:\x\テスト 第1巻.zip", ItemType = MaterialItemType.Archive, CanRemove = true });
		this.editorStore.MaterialFiles.Add(new MaterialFileItem { Name = "テスト 第2巻.zip", FullPath = @"C:\x\テスト 第2巻.zip", ItemType = MaterialItemType.Archive, CanRemove = true });
		this.editorStore.MaterialFiles.Add(new MaterialFileItem { Name = "テスト 第3巻.zip", FullPath = @"C:\x\テスト 第3巻.zip", ItemType = MaterialItemType.Archive, CanRemove = true });
		var manager = this.createManager();

		var removed = manager.RemoveMaterialFile(@"C:\x\テスト 第3巻.zip");

		Assert.True(removed.Removed);
		Assert.True(removed.OwnedMaxVolumeUpdated);
		Assert.Equal(2, clone.OwnedMaxVolume);
		Assert.False(clone.IsOwnedMaxVolumeManuallyEdited);
		Assert.Equal(9, original.OwnedMaxVolume);

		manager.RemoveMaterialFile(@"C:\x\テスト 第1巻.zip");
		var valueBeforeLastRemoval = clone.OwnedMaxVolume;
		var last = manager.RemoveMaterialFile(@"C:\x\テスト 第2巻.zip");

		// 素材が 0 件になっても、編集開始時の値（9）へは戻さず現在の値を維持する
		Assert.False(last.OwnedMaxVolumeUpdated);
		Assert.Empty(this.editorStore.MaterialFiles);
		Assert.Equal(valueBeforeLastRemoval, clone.OwnedMaxVolume);
		Assert.NotEqual(9, clone.OwnedMaxVolume);
		Assert.False(clone.IsOwnedMaxVolumeManuallyEdited);
	}
}
