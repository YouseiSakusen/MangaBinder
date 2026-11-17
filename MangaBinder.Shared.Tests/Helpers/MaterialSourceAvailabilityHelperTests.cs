using MangaBinder.Helpers;
using MangaBinder.Settings;

namespace MangaBinder.Tests.Helpers;

/// <summary>
/// MaterialSourceAvailabilityHelper の実在素材フォルダ判定の単体テストです。
/// DB の登録件数ではなく、物理フォルダの実在数で判定されることを検証します。
/// </summary>
public sealed class MaterialSourceAvailabilityHelperTests : IDisposable
{
	/// <summary>テスト用の一時ルートフォルダ。</summary>
	private readonly string rootPath;

	/// <summary>
	/// 一時ルートフォルダを作成します。
	/// </summary>
	public MaterialSourceAvailabilityHelperTests()
	{
		this.rootPath = Path.Combine(Path.GetTempPath(), "MangaBinderTests_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(this.rootPath);
	}

	/// <summary>
	/// 一時ルートフォルダを削除します。
	/// </summary>
	public void Dispose()
	{
		Directory.Delete(this.rootPath, true);
	}

	/// <summary>
	/// 実在する一時フォルダを作成してそのパスを返します。
	/// </summary>
	private string createFolder(string name)
	{
		var path = Path.Combine(this.rootPath, name);
		Directory.CreateDirectory(path);
		return path;
	}

	/// <summary>
	/// 存在しないフォルダのパスを返します。
	/// </summary>
	private string missingFolder(string name) => Path.Combine(this.rootPath, name);

	/// <summary>
	/// 指定のソースを持つ作品を作成します。
	/// </summary>
	private static MangaSeries createSeries(params MangaSource[] sources)
		=> new() { Sources = sources.ToList() };

	/// <summary>
	/// テスト用の MangaSource を作成します。
	/// </summary>
	private static MangaSource source(long id, string path, FolderRole role = FolderRole.Material)
		=> new() { SourceId = id, Path = path, Role = role };

	/// <summary>
	/// 実在する素材フォルダが 0 件の場合は複数フォルダエラーにならないことを確認します。
	/// </summary>
	[Fact]
	public void HasMultipleExistingMaterialFolders_NoExistingFolder_ReturnsFalse()
	{
		var series = createSeries(source(1, this.missingFolder("a")), source(2, this.missingFolder("b")));

		Assert.False(MaterialSourceAvailabilityHelper.HasMultipleExistingMaterialFolders(series));
		Assert.Empty(MaterialSourceAvailabilityHelper.GetExistingMaterialSources(series));
	}

	/// <summary>
	/// 実在 1 件は正常で、登録が 2 件あっても実在する 1 件が対象になることを確認します（古いレコードが先頭に残るケース）。
	/// </summary>
	[Fact]
	public void ResolveEditingMaterialSource_StaleRecordFirst_ReturnsExistingSource()
	{
		var series = createSeries(source(1, this.missingFolder("stale")), source(2, this.createFolder("alive")));

		Assert.False(MaterialSourceAvailabilityHelper.HasMultipleExistingMaterialFolders(series));
		var resolved = MaterialSourceAvailabilityHelper.ResolveEditingMaterialSource(series);

		Assert.NotNull(resolved);
		Assert.Equal(2, resolved.SourceId);
	}

	/// <summary>
	/// 実在 2 件以上は複数フォルダと判定され、編集対象の特定は例外になることを確認します。
	/// </summary>
	[Fact]
	public void HasMultipleExistingMaterialFolders_TwoExistingFolders_ReturnsTrue()
	{
		var series = createSeries(source(1, this.createFolder("a")), source(2, this.createFolder("b")));

		Assert.True(MaterialSourceAvailabilityHelper.HasMultipleExistingMaterialFolders(series));
		Assert.Throws<InvalidOperationException>(() => MaterialSourceAvailabilityHelper.ResolveEditingMaterialSource(series));
	}

	/// <summary>
	/// 同じ物理フォルダが複数レコードに登録されている場合（末尾区切り・大文字小文字違い）は 1 件として数えることを確認します。
	/// </summary>
	[Fact]
	public void GetExistingMaterialSources_SamePhysicalFolder_CountedOnce()
	{
		var path = this.createFolder("same");
		var series = createSeries(
			source(1, path),
			source(2, path + Path.DirectorySeparatorChar),
			source(3, path.ToUpperInvariant()));

		var existing = MaterialSourceAvailabilityHelper.GetExistingMaterialSources(series);

		Assert.Single(existing);
		Assert.Equal(1, existing[0].SourceId);
		Assert.False(MaterialSourceAvailabilityHelper.HasMultipleExistingMaterialFolders(series));
	}

	/// <summary>
	/// Binding ロールのフォルダは判定対象に含まれないことを確認します。
	/// </summary>
	[Fact]
	public void GetExistingMaterialSources_BindingRoleIgnored()
	{
		var series = createSeries(
			source(1, this.createFolder("material")),
			source(2, this.createFolder("binding"), FolderRole.Binding));

		var existing = MaterialSourceAvailabilityHelper.GetExistingMaterialSources(series);

		Assert.Single(existing);
		Assert.Equal(1, existing[0].SourceId);
	}

	/// <summary>
	/// 実在 0 件の場合は、古い Material レコードが 1 件残っていても物理フォルダとして扱わないことを確認します。
	/// </summary>
	[Fact]
	public void ResolveEditingMaterialSource_NoExistingFolderSingleStaleRecord_ReturnsNull()
	{
		var series = createSeries(source(5, this.missingFolder("missing")));

		Assert.Null(MaterialSourceAvailabilityHelper.ResolveEditingMaterialSource(series));
	}

	/// <summary>
	/// 実在 0 件で古い Material レコードが 1 件の場合は、その SourceId を再利用対象にすることを確認します。
	/// </summary>
	[Fact]
	public void FindReusableStaleMaterialSource_NoExistingFolderSingleStaleRecord_ReturnsRecord()
	{
		var series = createSeries(source(5, this.missingFolder("missing")), source(6, this.createFolder("binding"), FolderRole.Binding));

		var reusable = MaterialSourceAvailabilityHelper.FindReusableStaleMaterialSource(series);

		Assert.Equal(5, reusable?.SourceId);
	}

	/// <summary>
	/// Material レコードが 1 件もない場合、または実在する素材フォルダがある場合は、再利用対象がないことを確認します。
	/// </summary>
	[Fact]
	public void FindReusableStaleMaterialSource_NoRecordOrExistingFolder_ReturnsNull()
	{
		Assert.Null(MaterialSourceAvailabilityHelper.FindReusableStaleMaterialSource(createSeries()));
		Assert.Null(MaterialSourceAvailabilityHelper.FindReusableStaleMaterialSource(
			createSeries(source(1, this.missingFolder("stale")), source(2, this.createFolder("alive")))));
	}

	/// <summary>
	/// 実在 0 件で古い Material レコードが複数ある場合は、対象を一意に決められず例外になることを確認します。
	/// </summary>
	[Fact]
	public void FindReusableStaleMaterialSource_MultipleStaleRecords_Throws()
	{
		var series = createSeries(source(1, this.missingFolder("a")), source(2, this.missingFolder("b")));

		Assert.Throws<InvalidOperationException>(() => MaterialSourceAvailabilityHelper.FindReusableStaleMaterialSource(series));
	}
}
