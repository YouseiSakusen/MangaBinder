using MangaBinder.Settings;

namespace MangaBinder.Binding;

/// <summary>
/// 製本後 zip ファイル名を生成します。
/// </summary>
public class BindingZipFileNameFormatter
{
	/// <summary>Windows のファイル名に使用できない文字のセット。</summary>
	private static readonly char[] InvalidFileNameChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

	/// <summary>アプリケーション設定。</summary>
	private readonly AppSettings appSettings;

	/// <summary>巻範囲表記フォーマッタ。</summary>
	private readonly BindingVolumeTextFormatter volumeTextFormatter;

	/// <summary>
	/// <see cref="BindingZipFileNameFormatter"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="volumeTextFormatter">巻範囲表記フォーマッタ。</param>
	public BindingZipFileNameFormatter(AppSettings appSettings, BindingVolumeTextFormatter volumeTextFormatter)
	{
		this.appSettings = appSettings;
		this.volumeTextFormatter = volumeTextFormatter;
	}

	/// <summary>
	/// 製本後 zip ファイル名を生成します。
	/// </summary>
	/// <param name="author">著者名。</param>
	/// <param name="title">作品タイトル。</param>
	/// <param name="selectedStartVolume">選択開始巻番号。</param>
	/// <param name="selectedEndVolume">選択終了巻番号。</param>
	/// <param name="seriesEndVolume">シリーズ最終巻番号。</param>
	/// <param name="isSeriesCompleted">作品が完結しているかどうか。</param>
	/// <param name="isOwnedCompleted">全巻所持扱いかどうか。</param>
	/// <returns>ファイル名として使用可能な zip ファイル名文字列。</returns>
	public string Format(
		string author,
		string title,
		decimal selectedStartVolume,
		decimal selectedEndVolume,
		int seriesEndVolume,
		bool isSeriesCompleted,
		bool isOwnedCompleted)
	{
		var settings = this.appSettings;

		var authorPart = $"{settings.BindingZipAuthorLeftBracket.Value}{author}{settings.BindingZipAuthorRightBracket.Value}";
		var sep = settings.BindingZipNameSeparator.Value;
		var volumeText = this.volumeTextFormatter.Format(
			selectedStartVolume,
			selectedEndVolume,
			seriesEndVolume,
			isSeriesCompleted,
			isOwnedCompleted);
		var extension = settings.BindingDefaultArchiveExtension.Value;

		var rawName = $"{authorPart}{sep}{title}{sep}{volumeText}{extension}";
		return sanitize(rawName);
	}

	/// <summary>
	/// ファイル名に使用できない文字を _ に置換します。
	/// </summary>
	/// <param name="fileName">変換対象のファイル名文字列。</param>
	/// <returns>Windows ファイル名として使用可能な文字列。</returns>
	private static string sanitize(string fileName)
	{
		foreach (var c in InvalidFileNameChars)
			fileName = fileName.Replace(c, '_');

		return fileName;
	}
}
