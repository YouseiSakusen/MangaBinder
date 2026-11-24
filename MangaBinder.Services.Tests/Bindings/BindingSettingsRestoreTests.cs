using System.Data.SQLite;
using System.Reflection;
using System.Text.Json;
using MangaBinder.Bindings;
using MangaBinder.Helpers;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MangaBinder.Services.Tests.Bindings;

/// <summary>
/// 製本設定の復元（<c>VolumeSelectionManager.restoreBindingSettingsAsync</c>）のテストです。
/// 保存済み JSON を一時 SQLite へ保存し、ExpandedVolumes と Materials を基準に復元されることを検証します。
/// </summary>
public class BindingSettingsRestoreTests : IDisposable
{
	private const long seriesId = 1;

	private readonly string seriesFolder;
	private readonly string dbPath;
	private readonly BindingStore store = new();
	private readonly VolumeSelectionManager manager;

	public BindingSettingsRestoreTests()
	{
		SupportedExtensionHelper.Initialize(
		[
			new SupportedFileExtension { Extension = ".jpg", FileType = (int)FileType.Image },
		]);

		var root = Path.Combine(Path.GetTempPath(), "MangaBinderTests_" + Guid.NewGuid().ToString("N"));
		this.seriesFolder = Path.Combine(root, "series");
		this.dbPath = Path.Combine(root, "test.db");
		Directory.CreateDirectory(this.seriesFolder);
		this.store.WorkSeriesFolderPath.Value = this.seriesFolder;

		var settings = new AppSettings { ConnectionString = $"Data Source={this.dbPath}" };
		using (var connection = new SQLiteConnection(settings.ConnectionString))
		{
			connection.Open();
			using var command = connection.CreateCommand();
			command.CommandText = "CREATE TABLE BindingSettings (SeriesId INTEGER PRIMARY KEY, HistoryId INTEGER, SettingsJson TEXT, UpdatedAt TEXT);";
			command.ExecuteNonQuery();
		}

		var services = new ServiceCollection();
		services.AddScoped(_ => new BindingRepository(settings));
		var repository = new MaterialArchiveRepository(":memory:");
		this.manager = new VolumeSelectionManager(
			this.store,
			new SeriesMaterialFolderLoader(new MaterialArchiveExtractor(), repository),
			settings,
			services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
			repository,
			NullLogger<VolumeSelectionManager>.Instance);
	}

	public void Dispose()
	{
		this.store.Dispose();
		SQLiteConnection.ClearAllPools();
		Directory.Delete(Path.GetDirectoryName(this.dbPath)!, recursive: true);
	}

	private string createVolumeFolder(string name)
	{
		var path = Path.Combine(this.seriesFolder, name);
		Directory.CreateDirectory(path);
		File.WriteAllBytes(Path.Combine(path, "1.jpg"), new byte[10]);
		return path;
	}

	private MaterialItem addSource(string name)
	{
		var source = new MaterialItem(
			MaterialItemType.Folder,
			MaterialSourceType.Folder,
			name,
			@"C:\src\" + name,
			fileCount: 1,
			totalImageBytes: 5L,
			sourcePath: @"C:\src\" + name);
		this.store.Materials.Add(source);
		return source;
	}

	private static BindingVolumeDto createDto(MaterialItem material, decimal number, MaterialItem? source = null)
	{
		using var volume = new BindingVolume(material);
		volume.VolumeNumber.Value = number;
		var image = volume.AddImage("1.jpg", "1.jpg");
		image.FilePath = Path.Combine(material.FullPath, "1.jpg");
		image.WorkFileSize = 10;
		image.FileName = "001.jpg";
		var dto = volume.CreateDto();
		if (source is not null && dto.SourceMaterial is null)
		{
			throw new InvalidOperationException("Source が DTO へ保存されていません。");
		}

		return dto;
	}

	private async Task<string?> restoreAsync(params BindingVolumeDto[] volumes)
	{
		await this.saveAsync(JsonSerializer.Serialize(new BindingSettingsDto { VolumeFolderDigits = 3, Volumes = [.. volumes] }));
		return await this.invokeRestoreAsync();
	}

	private async Task saveAsync(string json)
	{
		using var connection = new SQLiteConnection($"Data Source={this.dbPath}");
		await connection.OpenAsync();
		using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM BindingSettings; INSERT INTO BindingSettings (SeriesId, HistoryId, SettingsJson, UpdatedAt) VALUES (1, 1, @json, '2025-01-01');";
		command.Parameters.AddWithValue("@json", json);
		await command.ExecuteNonQueryAsync();
	}

	private async Task<string?> invokeRestoreAsync()
	{
		var method = typeof(VolumeSelectionManager).GetMethod(
			"restoreBindingSettingsAsync",
			BindingFlags.Instance | BindingFlags.NonPublic)!;
		var result = (ValueTask<string?>)method.Invoke(
			this.manager,
			[new MangaSeries { SeriesId = seriesId }, CancellationToken.None])!;
		return await result;
	}

	[Fact]
	public async Task 元素材由来の巻はMaterialsの同一インスタンスで復元される()
	{
		var source = this.addSource("a");
		var dto = createDto(source, 1m);

		var version = await this.restoreAsync(dto);

		Assert.Null(version);
		var volume = Assert.Single(this.store.BindingVolumes);
		Assert.Same(source, volume.Material);
		Assert.True(source.IsChecked.Value);
		Assert.Equal(3, this.store.VolumeFolderDigits.Value);
		Assert.Single(this.store.Materials);
	}

	[Fact]
	public async Task Source付き展開済み巻は元素材の同一参照をSourceとして復元されBindingImageが維持される()
	{
		this.createVolumeFolder("Title 01");
		await this.manager.SyncWorkVolumesAsync();
		var expanded = this.store.ExpandedVolumes.Single();
		var source = this.addSource("a");
		using var temp = expanded.CreateMaterial();
		temp.Source = source;
		var dto = createDto(temp, 1m, source);

		await this.restoreAsync(dto);

		var volume = Assert.Single(this.store.BindingVolumes);
		Assert.Equal(MaterialSourceType.WorkFolder, volume.Material.Type);
		Assert.Same(source, expanded.Source);
		Assert.Same(source, volume.Material.Source);
		Assert.True(expanded.IsChecked.Value);
		Assert.True(volume.Material.IsChecked.Value);
		Assert.False(source.IsChecked.Value);
		var image = Assert.Single(volume.Images);
		Assert.Equal("001.jpg", image.FileName);
		Assert.Equal(10, image.WorkFileSize);
		Assert.Equal(Path.Combine(expanded.FullPath, "1.jpg"), image.FilePath);
	}

	[Fact]
	public async Task Sourceなし展開済み巻はSourceがnullのまま復元される()
	{
		this.createVolumeFolder("Title 01");
		await this.manager.SyncWorkVolumesAsync();
		var expanded = this.store.ExpandedVolumes.Single();
		using var temp = expanded.CreateMaterial();
		var dto = createDto(temp, 1m);

		await this.restoreAsync(dto);

		var volume = Assert.Single(this.store.BindingVolumes);
		Assert.Null(volume.Material.Source);
		Assert.Null(expanded.Source);
	}

	[Fact]
	public async Task Sourceが見つからなくても展開済み巻は復元されSourceはnullになる()
	{
		this.createVolumeFolder("Title 01");
		await this.manager.SyncWorkVolumesAsync();
		var expanded = this.store.ExpandedVolumes.Single();
		using var temp = expanded.CreateMaterial();
		using var missingSource = new MaterialItem(MaterialItemType.Folder, MaterialSourceType.Folder, "gone", @"C:\gone", sourcePath: @"C:\gone");
		temp.Source = missingSource;
		var dto = createDto(temp, 1m, missingSource);

		await this.restoreAsync(dto);

		var volume = Assert.Single(this.store.BindingVolumes);
		Assert.Null(volume.Material.Source);
		Assert.Null(expanded.Source);
	}

	[Fact]
	public async Task 展開済みフォルダが存在しない巻はスキップされ元素材へ置換も再展開もされない()
	{
		var source = this.addSource("a");
		using var gone = new MaterialItem(
			MaterialItemType.Folder,
			MaterialSourceType.WorkFolder,
			"Title 01",
			Path.Combine(this.seriesFolder, "Title 01"),
			sourcePath: Path.Combine(this.seriesFolder, "Title 01"));
		gone.Source = source;
		var goneDto = createDto(gone, 1m, source);
		var other = this.addSource("b");
		var otherDto = createDto(other, 2m);
		await this.manager.SyncWorkVolumesAsync();

		await this.restoreAsync(goneDto, otherDto);

		var volume = Assert.Single(this.store.BindingVolumes);
		Assert.Same(other, volume.Material);
		Assert.False(source.IsChecked.Value);
		Assert.Empty(this.store.ExpandedVolumes);
	}

	[Fact]
	public async Task SourceMaterial項目がない旧1_0のJSONでも展開済み巻を復元できる()
	{
		this.createVolumeFolder("Title 01");
		await this.manager.SyncWorkVolumesAsync();
		var expanded = this.store.ExpandedVolumes.Single();
		using var temp = expanded.CreateMaterial();
		var dto = createDto(temp, 1m);
		var json = JsonSerializer.Serialize(new BindingSettingsDto { VolumeFolderDigits = 2, Volumes = [dto] })
			.Replace("\"SourceMaterial\":null,", string.Empty);
		Assert.DoesNotContain("SourceMaterial", json);
		await this.saveAsync(json);

		var version = await this.invokeRestoreAsync();

		Assert.Null(version);
		var volume = Assert.Single(this.store.BindingVolumes);
		Assert.Equal(MaterialSourceType.WorkFolder, volume.Material.Type);
		Assert.Null(volume.Material.Source);
	}

	[Fact]
	public async Task バージョン不一致の設定は復元されず保存バージョンが返る()
	{
		await this.saveAsync(JsonSerializer.Serialize(new BindingSettingsDto { Version = "9.9" }));

		var version = await this.invokeRestoreAsync();

		Assert.Equal("9.9", version);
		Assert.Empty(this.store.BindingVolumes);
	}
}
