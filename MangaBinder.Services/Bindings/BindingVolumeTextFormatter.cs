using MangaBinder.Settings;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本後 zip ファイル名に使用する巻範囲表記を生成します。
/// </summary>
public class BindingVolumeTextFormatter
{
	/// <summary>アプリケーション設定。</summary>
	private readonly AppSettings appSettings;

	/// <summary>
	/// <see cref="BindingVolumeTextFormatter"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定。</param>
	public BindingVolumeTextFormatter(AppSettings appSettings)
		=> this.appSettings = appSettings;

	/// <summary>
	/// 選択巻情報をもとに巻範囲表記文字列を生成します。
	/// </summary>
	/// <param name="selectedStartVolume">選択開始巻番号。</param>
	/// <param name="selectedEndVolume">選択終了巻番号。</param>
	/// <param name="seriesEndVolume">シリーズ最終巻番号（ゼロ埋め桁数計算に使用）。</param>
	/// <param name="isSeriesCompleted">作品が完結しているかどうか。</param>
	/// <param name="isOwnedCompleted">全巻所持扱いかどうか。</param>
	/// <returns>巻範囲表記文字列。</returns>
	public string Format(
		decimal selectedStartVolume,
		decimal selectedEndVolume,
		int seriesEndVolume,
		bool isSeriesCompleted,
		bool isOwnedCompleted)
	{
		var settings = this.appSettings;
		var digits = seriesEndVolume.ToString().Length;

		// 完結判定: 1巻から最終巻まで選択 かつ 全巻所持扱い
		var isFullComplete =
			selectedStartVolume == 1
			&& selectedEndVolume == seriesEndVolume
			&& isOwnedCompleted;

		// 途中から完結判定: 開始が1巻以外 かつ 最終巻まで選択 かつ 全巻所持扱い
		var isPartialComplete =
			selectedStartVolume != 1
			&& selectedEndVolume == seriesEndVolume
			&& isOwnedCompleted;

		if (isFullComplete)
		{
			// 全9巻
			return $"{settings.BindingZipCompleteVolumePrefix.Value}{seriesEndVolume}{settings.BindingZipCompleteVolumeSuffix.Value}";
		}

		if (isPartialComplete)
		{
			// 第015-全105巻
			var startStr = this.formatVolumeNumber(selectedStartVolume, digits);
			return $"{settings.BindingZipPartialCompleteVolumePrefix.Value}{startStr}{settings.BindingZipPartialCompleteVolumeSeparator.Value}{seriesEndVolume}{settings.BindingZipPartialCompleteVolumeSuffix.Value}";
		}

		// 通常: 第01-12巻
		var start = this.formatVolumeNumber(selectedStartVolume, digits);
		var end = this.formatVolumeNumber(selectedEndVolume, digits);
		return $"{settings.BindingZipNormalVolumePrefix.Value}{start}{settings.BindingZipNormalVolumeSeparator.Value}{end}{settings.BindingZipNormalVolumeSuffix.Value}";
	}

	/// <summary>
	/// 巻番号を指定桁数でゼロ埋めした文字列に変換します。
	/// 小数の場合はゼロ埋めせずそのまま返します。
	/// </summary>
	/// <param name="volumeNumber">巻番号。</param>
	/// <param name="digits">整数部のゼロ埋め桁数。</param>
	/// <returns>書式化された巻番号文字列。</returns>
	private string formatVolumeNumber(decimal volumeNumber, int digits)
	{
		if (volumeNumber != Math.Floor(volumeNumber))
			return volumeNumber.ToString("G29");

		return ((long)volumeNumber).ToString().PadLeft(digits, '0');
	}
}
