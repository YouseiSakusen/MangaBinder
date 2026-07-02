using ObservableCollections;
using R3;
using System.Data.SQLite;
using System.IO;

namespace MangaBinder.Settings;

/// <summary>
/// アプリケーション全体の設定を保持するリアクティブModelクラスです。
/// </summary>
public class AppSettings : IDisposable, IMangaBinderConfig
{
	private DisposableBag disposableBag;

	/// <summary>変更検知の基準となる初期フォルダ一覧を保持するフィールドです。</summary>
	private List<SourceFolderRecord> initialFolders = [];

	/// <summary>SQLite データベースへの接続文字列を取得します。</summary>
	public required string ConnectionString { get; init; }

	/// <summary>DBファイルの物理パスを取得します。</summary>
	public string DatabasePath => new SQLiteConnectionStringBuilder(this.ConnectionString).DataSource;

	/// <summary>作業用一時フォルダのパスを取得します。</summary>
	public BindableReactiveProperty<string> WorkFolderPath { get; }

	/// <summary>サムネイル画像の保存先フォルダパスを取得します。</summary>
	public BindableReactiveProperty<string> ThumbnailFolderPath { get; }

	/// <summary>サムネイル画像の幅（ピクセル）を取得します。</summary>
	public BindableReactiveProperty<int> ThumbnailWidth { get; }

	/// <summary>サムネイル画像の高さ（ピクセル）を取得します。</summary>
	public BindableReactiveProperty<int> ThumbnailHeight { get; }

	/// <summary>製本工程で出力する画像ファイルの既定拡張子を取得します。</summary>
	public BindableReactiveProperty<string> BindingDefaultImageExtension { get; }

	/// <summary>製本工程で出力するアーカイブファイルの既定拡張子を取得します。</summary>
	public BindableReactiveProperty<string> BindingDefaultArchiveExtension { get; }

	/// <summary>製本工程で画像を既定画像形式へ統一変換するかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> BindingConvertImagesToDefaultFormat { get; }

	/// <summary>ワークフォルダ内に作成する巻フォルダ名の接頭辞を取得します。</summary>
	public BindableReactiveProperty<string> WorkVolumeFolderNamePrefix { get; }

	/// <summary>ワークフォルダ内に作成する巻フォルダ名の接尾辞を取得します。</summary>
	public BindableReactiveProperty<string> WorkVolumeFolderNameSuffix { get; }

	/// <summary>作品一覧のスクロール位置を取得します。</summary>
	public BindableReactiveProperty<double> SeriesListVerticalOffset { get; }

	/// <summary>製本後 zip ファイル名の著者名左括弧を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipAuthorLeftBracket { get; }

	/// <summary>製本後 zip ファイル名の著者名右括弧を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipAuthorRightBracket { get; }

	/// <summary>製本後 zip ファイル名のタイトルと著者名の区切り文字を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipNameSeparator { get; }

	/// <summary>製本後 zip ファイル名の通常巻数の接頭辞を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipNormalVolumePrefix { get; }

	/// <summary>製本後 zip ファイル名の通常巻数の区切り文字を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipNormalVolumeSeparator { get; }

	/// <summary>製本後 zip ファイル名の通常巻数の接尾辞を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipNormalVolumeSuffix { get; }

	/// <summary>製本後 zip ファイル名の完結巻数の接頭辞を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipCompleteVolumePrefix { get; }

	/// <summary>製本後 zip ファイル名の完結巻数の接尾辞を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipCompleteVolumeSuffix { get; }

	/// <summary>製本後 zip ファイル名の一部完結巻数の接頭辞を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipPartialCompleteVolumePrefix { get; }

	/// <summary>製本後 zip ファイル名の一部完結巻数の区切り文字を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipPartialCompleteVolumeSeparator { get; }

	/// <summary>製本後 zip ファイル名の一部完結巻数の接尾辞を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipPartialCompleteVolumeSuffix { get; }

	/// <summary>スキャン対象フォルダの一覧を取得します。</summary>
	public ObservableList<SourceFolder> SourceFolders { get; }

	/// <summary>サポート対象の拡張子一覧の実体リストです。</summary>
	private readonly List<SupportedFileExtension> supportedExtensions = [];

	/// <summary>サポート対象の拡張子一覧を取得します。</summary>
	public IReadOnlyList<SupportedFileExtension> SupportedExtensions => this.supportedExtensions;

	/// <inheritdoc/>
	ThumbnailOptions IMangaBinderConfig.ThumbnailOptions =>
		new() { Width = this.ThumbnailWidth.Value, Height = this.ThumbnailHeight.Value };

	/// <inheritdoc/>
	string IMangaBinderConfig.ThumbnailFolderPath => this.ThumbnailFolderPath.Value;

	/// <summary>
	/// <see cref="AppSettings"/> の新しいインスタンスを初期化します。
	/// </summary>
	public AppSettings()
	{
		this.WorkFolderPath = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.ThumbnailFolderPath = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.ThumbnailWidth = new BindableReactiveProperty<int>(160)
			.AddTo(ref this.disposableBag);

		this.ThumbnailHeight = new BindableReactiveProperty<int>(224)
			.AddTo(ref this.disposableBag);

		this.BindingDefaultImageExtension = new BindableReactiveProperty<string>(".jpg")
			.AddTo(ref this.disposableBag);

		this.BindingDefaultArchiveExtension = new BindableReactiveProperty<string>(".zip")
			.AddTo(ref this.disposableBag);

		this.BindingConvertImagesToDefaultFormat = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.WorkVolumeFolderNamePrefix = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.WorkVolumeFolderNameSuffix = new BindableReactiveProperty<string>("巻")
			.AddTo(ref this.disposableBag);

		this.SeriesListVerticalOffset = new BindableReactiveProperty<double>(0.0)
			.AddTo(ref this.disposableBag);

		this.BindingZipAuthorLeftBracket = new BindableReactiveProperty<string>("[")
			.AddTo(ref this.disposableBag);

		this.BindingZipAuthorRightBracket = new BindableReactiveProperty<string>("]")
			.AddTo(ref this.disposableBag);

		this.BindingZipNameSeparator = new BindableReactiveProperty<string>(" ")
			.AddTo(ref this.disposableBag);

		this.BindingZipNormalVolumePrefix = new BindableReactiveProperty<string>("第")
			.AddTo(ref this.disposableBag);

		this.BindingZipNormalVolumeSeparator = new BindableReactiveProperty<string>("-")
			.AddTo(ref this.disposableBag);

		this.BindingZipNormalVolumeSuffix = new BindableReactiveProperty<string>("巻")
			.AddTo(ref this.disposableBag);

		this.BindingZipCompleteVolumePrefix = new BindableReactiveProperty<string>("全")
			.AddTo(ref this.disposableBag);

		this.BindingZipCompleteVolumeSuffix = new BindableReactiveProperty<string>("巻")
			.AddTo(ref this.disposableBag);

		this.BindingZipPartialCompleteVolumePrefix = new BindableReactiveProperty<string>("第")
			.AddTo(ref this.disposableBag);

		this.BindingZipPartialCompleteVolumeSeparator = new BindableReactiveProperty<string>("-全")
			.AddTo(ref this.disposableBag);

		this.BindingZipPartialCompleteVolumeSuffix = new BindableReactiveProperty<string>("巻")
			.AddTo(ref this.disposableBag);

		this.SourceFolders = new ObservableList<SourceFolder>();
	}

	/// <summary>
	/// ワークフォルダが有効（設定済み・存在する）かどうかを取得します。
	/// </summary>
	public bool HasValidWorkFolder =>
		!string.IsNullOrWhiteSpace(this.WorkFolderPath.Value)
		&& Directory.Exists(this.WorkFolderPath.Value);

	/// <summary>
	/// 指定した作品タイトルの中間フォルダパスを生成します。
	/// </summary>
	/// <param name="seriesTitle">作品タイトル。</param>
	/// <returns>WorkFolderPath 直下に作品タイトル名のフォルダパス。</returns>
	public string CreateWorkSeriesFolderPath(string seriesTitle)
		=> Path.Combine(this.WorkFolderPath.Value, seriesTitle);

	/// <summary>
	/// 巻番号からワークフォルダ内の巻フォルダ名を生成します。
	/// </summary>
	/// <param name="volumeNumber">巻番号。</param>
	/// <param name="digits">ゼロ埋め桁数。</param>
	/// <returns>生成されたフォルダ名。</returns>
	public string CreateWorkVolumeFolderName(decimal volumeNumber, int digits)
	{
		var prefix = this.WorkVolumeFolderNamePrefix.Value;
		var suffix = this.WorkVolumeFolderNameSuffix.Value;

		// 整数かどうかで書式を切り替える
		var numberPart = volumeNumber == Math.Floor(volumeNumber)
			? ((long)volumeNumber).ToString().PadLeft(digits, '0')
			: volumeNumber.ToString("G29");

		return $"{prefix}{numberPart}{suffix}";
	}

	/// <summary>
	/// 現在の <see cref="SourceFolders"/> の状態をスナップショットとして保存し、初期状態を確定します。
	/// </summary>
	public void UpdateSnapshot()
	{
		this.initialFolders = this.SourceFolders
			.Select(f => new SourceFolderRecord(f.FolderPath.Value, f.DisplayName.Value, f.Role.Value))
			.ToList();
	}

	/// <summary>
	/// <see cref="SourceFolders"/> が初期状態から変更されているかどうかを取得します。
	/// </summary>
	public bool IsSourceFoldersChanged =>
		!this.SourceFolders
			.Select(f => new SourceFolderRecord(f.FolderPath.Value, f.DisplayName.Value, f.Role.Value))
			.SequenceEqual(this.initialFolders);

	/// <summary>
	/// サポート対象拡張子一覧を <paramref name="extensions"/> で置き換えます。
	/// </summary>
	/// <param name="extensions">新しい拡張子一覧。</param>
	public void ReloadSupportedExtensions(IReadOnlyList<SupportedFileExtension> extensions)
	{
		this.supportedExtensions.Clear();
		this.supportedExtensions.AddRange(extensions);
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose()
	{
		foreach (var folder in this.SourceFolders)
			folder.Dispose();

		this.disposableBag.Dispose();
	}
}
