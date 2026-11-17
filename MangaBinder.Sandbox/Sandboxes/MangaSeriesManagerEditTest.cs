using MangaBinder.Bindings;
using MangaBinder.Series;
using MangaBinder.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using R3;

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
		var (_, clone) = this.startSession(originalTitle, 0);
		clone.Title = editedTitle;

		var error = this.createManager().ValidateForRegister();

		Assert.Equal(EditorValidationError.None, error);
	}

	/// <summary>
	/// 既存正式作品のタイトルが別作品と判定される場合は TitleMismatch になり、正本が変更されないことを確認します。
	/// </summary>
	[Fact]
	public void ValidateForRegister_ExistingSeriesDifferentTitle_ReturnsTitleMismatchWithoutChangingOriginal()
	{
		var (original, clone) = this.startSession("ドラゴンボール", 0);
		clone.Title = "ワンピース";

		var error = this.createManager().ValidateForRegister();

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
		clone.Title = "ワンピース";
		var manager = this.createManager();
		manager.ValidateForRegister();

		manager.RestoreOriginalTitle();

		Assert.Equal("ドラゴンボール", clone.Title);
		Assert.Equal("ドラゴンボール", original.Title);
	}

	/// <summary>
	/// ユーザーが所持最大巻数を変更すると、手動変更フラグが立ち、編集用クローンへ反映されることを確認します。
	/// </summary>
	[Fact]
	public void OwnedMaxVolumeInput_ChangedByUser_SetsManualFlag()
	{
		var (_, clone) = this.startSession("テスト", 3);

		this.editorStore.OwnedMaxVolume.Value = 5;

		Assert.True(clone.IsOwnedMaxVolumeManuallyEdited);
		Assert.Equal(5, clone.OwnedMaxVolume);
	}

	/// <summary>
	/// 自動推定による所持最大巻数の設定では、入力値と編集用クローンが一致し、手動変更フラグが立たないことを確認します。
	/// </summary>
	[Fact]
	public void SetEstimatedOwnedMaxVolume_DoesNotSetManualFlagAndSyncsInput()
	{
		var (_, clone) = this.startSession("テスト", 3);

		var updated = this.editorStore.SetEstimatedOwnedMaxVolume(7);

		Assert.True(updated);
		Assert.Equal(7, clone.OwnedMaxVolume);
		Assert.Equal(7, this.editorStore.OwnedMaxVolume.Value);
		Assert.False(clone.IsOwnedMaxVolumeManuallyEdited);
	}

	/// <summary>
	/// 完結巻の入力値に応じて、EndVolume・SeriesCompleted・全巻所持の編集可否が更新されることを確認します。
	/// </summary>
	[Fact]
	public void EndVolumeInput_UpdatesCloneAndCanEditOwnedCompleted()
	{
		var (_, clone) = this.startSession("テスト", 3);

		this.editorStore.EndVolume.Value = 10;

		Assert.Equal(10, clone.EndVolume);
		Assert.True(clone.SeriesCompleted);
		Assert.True(this.editorStore.CanEditOwnedCompleted.Value);

		this.editorStore.IsOwnedCompleted.Value = true;
		this.editorStore.EndVolume.Value = null;

		Assert.Equal(0, clone.EndVolume);
		Assert.False(clone.SeriesCompleted);
		Assert.False(clone.IsOwnedCompleted);
		Assert.False(this.editorStore.IsOwnedCompleted.Value);
		Assert.False(this.editorStore.CanEditOwnedCompleted.Value);
	}

	/// <summary>
	/// 巻情報の入力変更が、編集用 Notifier の Series 通知として発行されることを確認します。
	/// </summary>
	[Fact]
	public void VolumeInput_NotifiesEditTargetNotifier()
	{
		var (_, clone) = this.startSession("テスト", 3);
		var notifyCount = 0;
		using var subscription = this.editorStore.EditTargetNotifier!.Series.Subscribe(_ => notifyCount++);
		var countAfterSubscribe = notifyCount;

		this.editorStore.StartVolume.Value = 2;

		Assert.Equal(2, clone.StartVolume);
		Assert.True(notifyCount > countAfterSubscribe);
	}

	/// <summary>
	/// 素材の削除による自動推定は、所持最大巻数を更新しても手動変更フラグを立てないことを確認します。
	/// 推定できない（素材が 0 件になった）場合は現在の値を維持します。
	/// </summary>
	[Fact]
	public void RemoveMaterialFile_AutoEstimate_UpdatesVolumeWithoutManualFlagAndKeepsValueWhenEmpty()
	{
		var (original, clone) = this.startSession("テスト", 9);
		this.editorStore.MaterialFiles.Add(new MaterialFileItem { Name = "テスト 第1巻.zip", FullPath = @"C:\x\テスト 第1巻.zip", ItemType = MaterialItemType.Archive, CanRemove = true, SizeBytes = 100 });
		this.editorStore.MaterialFiles.Add(new MaterialFileItem { Name = "テスト 第2巻.zip", FullPath = @"C:\x\テスト 第2巻.zip", ItemType = MaterialItemType.Archive, CanRemove = true, SizeBytes = 100 });
		this.editorStore.MaterialFiles.Add(new MaterialFileItem { Name = "テスト 第3巻.zip", FullPath = @"C:\x\テスト 第3巻.zip", ItemType = MaterialItemType.Archive, CanRemove = true, SizeBytes = 100 });
		this.editorStore.MaterialTotalSizeBytes.Value = 300;
		var manager = this.createManager();

		var removed = manager.RemoveMaterialFile(@"C:\x\テスト 第3巻.zip");

		Assert.True(removed.Removed);
		Assert.True(removed.OwnedMaxVolumeUpdated);
		Assert.Equal(200, this.editorStore.MaterialTotalSizeBytes.Value);
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

	/// <summary>
	/// サイズ計算中（未確定）の追加素材は削除できず、計算完了後の結果が現在の編集対象にだけ反映されることを確認します。
	/// </summary>
	[Fact]
	public void ApplyCalculatedMaterialSize_PendingItemIsNotRemovableAndStaleResultIsIgnored()
	{
		var (_, clone) = this.startSession("テスト", 0);
		var path = @"C:\x\テスト 第1巻";
		this.editorStore.MaterialFiles.Add(new MaterialFileItem { Name = "テスト 第1巻", FullPath = path, ItemType = MaterialItemType.Folder, CanRemove = true });
		var manager = this.createManager();

		Assert.False(manager.RemoveMaterialFile(path).Removed);

		// 別の編集対象向けの古い計算結果は反映しない
		this.editorStore.ApplyCalculatedMaterialSize(new MangaSeries(), path, 500);
		Assert.Equal(0, this.editorStore.MaterialTotalSizeBytes.Value);
		Assert.True(this.editorStore.MaterialFiles[0].IsSizePending);

		this.editorStore.ApplyCalculatedMaterialSize(clone, path, 500);
		Assert.Equal(500, this.editorStore.MaterialTotalSizeBytes.Value);
		Assert.False(this.editorStore.MaterialFiles[0].IsSizePending);
		Assert.True(manager.RemoveMaterialFile(path).Removed);
		Assert.Equal(0, this.editorStore.MaterialTotalSizeBytes.Value);
	}
}
