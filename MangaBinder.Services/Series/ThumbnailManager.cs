using MangaBinder.Settings;
using Microsoft.Extensions.Logging;

namespace MangaBinder.Series;

/// <summary>
/// サムネイル画像ファイルの操作を統一管理するクラスです。
/// WorkThumbnail および正式 Thumbnail の保存・削除・コピーを担当します。
/// </summary>
public class ThumbnailManager
{
	/// <summary>アプリケーション設定。</summary>
	private readonly AppSettings appSettings;

	/// <summary>アプリケーション設定インターフェース（正式サムネイル用）。</summary>
	private readonly IMangaBinderConfig config;

	/// <summary>ログ出力用の Logger。</summary>
	private readonly ILogger<ThumbnailManager>? logger;

	/// <summary>
	/// <see cref="ThumbnailManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="config">アプリケーション設定インターフェース。</param>
	/// <param name="logger">ログ出力用の Logger。オプション。</param>
	public ThumbnailManager(AppSettings appSettings, IMangaBinderConfig config, ILogger<ThumbnailManager>? logger = null)
	{
		this.appSettings = appSettings;
		this.config = config;
		this.logger = logger;
	}

	/// <summary>
	/// サムネイル byte[] を WorkThumbnail フォルダへ JPEG ファイルとして保存します。
	/// 保存先ディレクトリが存在しない場合は作成します。
	/// </summary>
	/// <param name="fileName">保存するファイル名（拡張子含む）。</param>
	/// <param name="thumbnailBytes">JPEG byte[] データ。</param>
	/// <returns>完了を表す ValueTask。</returns>
	/// <exception cref="ArgumentException">fileName が null または空の場合にスローされます。</exception>
	/// <exception cref="ArgumentNullException">thumbnailBytes が null の場合にスローされます。</exception>
	public async ValueTask SaveWorkThumbnailAsync(string fileName, byte[] thumbnailBytes)
	{
		ArgumentException.ThrowIfNullOrEmpty(fileName);
		ArgumentNullException.ThrowIfNull(thumbnailBytes);

		try
		{
			// 保存先パスを取得
			var filePath = this.appSettings.GetWorkThumbnailFullPath(fileName);

			// 保存先ディレクトリが存在しない場合は作成
			var directory = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			// JPEG を保存
			await File.WriteAllBytesAsync(filePath, thumbnailBytes);

			this.logger?.LogInformation("WorkThumbnail を保存しました。FileName={FileName}", fileName);
		}
		catch (Exception ex)
		{
			this.logger?.LogError(ex, "WorkThumbnail 保存に失敗しました。FileName={FileName}", fileName);
			throw;
		}
	}

	/// <summary>
	/// サムネイル byte[] を正式 Thumbnail フォルダへ JPEG ファイルとして保存します。
	/// 保存先ディレクトリが存在しない場合は作成します。
	/// </summary>
	/// <param name="fileName">保存するファイル名（拡張子含む）。</param>
	/// <param name="thumbnailBytes">JPEG byte[] データ。</param>
	/// <returns>完了を表す ValueTask。</returns>
	/// <exception cref="ArgumentException">fileName が null または空の場合にスローされます。</exception>
	/// <exception cref="ArgumentNullException">thumbnailBytes が null の場合にスローされます。</exception>
	public async ValueTask SaveThumbnailAsync(string fileName, byte[] thumbnailBytes)
	{
		ArgumentException.ThrowIfNullOrEmpty(fileName);
		ArgumentNullException.ThrowIfNull(thumbnailBytes);

		try
		{
			// 保存先パスを取得（正式 Thumbnail フォルダ）
			var filePath = this.config.GetThumbnailFullPath(fileName);

			// 保存先ディレクトリが存在しない場合は作成
			var directory = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			// JPEG を保存
			await File.WriteAllBytesAsync(filePath, thumbnailBytes);

			this.logger?.LogInformation("正式サムネイルを保存しました。FileName={FileName}", fileName);
		}
		catch (Exception ex)
		{
			this.logger?.LogError(ex, "正式サムネイル保存に失敗しました。FileName={FileName}", fileName);
			throw;
		}
	}

	/// <summary>
	/// WorkThumbnail フォルダのファイルを正式 Thumbnail フォルダへコピーします。
	/// WorkThumbnail 側にファイルが存在する場合のみコピーを行います。
	/// コピー先ディレクトリが存在しない場合は作成します。
	/// </summary>
	/// <param name="workFileName">WorkThumbnail フォルダ内のファイル名。</param>
	/// <param name="thumbnailFileName">正式 Thumbnail フォルダへのファイル名。</param>
	/// <returns>コピーが成功した場合は true、WorkThumbnail ファイルが存在しない場合は false。</returns>
	/// <exception cref="ArgumentException">workFileName または thumbnailFileName が null または空の場合にスローされます。</exception>
	public async ValueTask<bool> CopyWorkThumbnailToThumbnailAsync(string workFileName, string thumbnailFileName)
	{
		ArgumentException.ThrowIfNullOrEmpty(workFileName);
		ArgumentException.ThrowIfNullOrEmpty(thumbnailFileName);

		try
		{
			// WorkThumbnail ファイルのパスを取得
			var sourceFilePath = this.appSettings.GetWorkThumbnailFullPath(workFileName);

			// WorkThumbnail ファイルが存在しない場合は false を返す
			if (!File.Exists(sourceFilePath))
			{
				this.logger?.LogInformation("WorkThumbnail ファイルが存在しません。FileName={FileName}", workFileName);
				return false;
			}

			// 正式 Thumbnail ファイルのパスを取得
			var destFilePath = this.config.GetThumbnailFullPath(thumbnailFileName);

			// 保存先ディレクトリが存在しない場合は作成
			var directory = Path.GetDirectoryName(destFilePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			// ファイルをコピー（上書き可）
			File.Copy(sourceFilePath, destFilePath, overwrite: true);

			this.logger?.LogInformation(
				"WorkThumbnail から正式サムネイルへコピーしました。From={From}, To={To}",
				workFileName,
				thumbnailFileName);

			return true;
		}
		catch (Exception ex)
		{
			this.logger?.LogError(
				ex,
				"WorkThumbnail から正式サムネイルへのコピーに失敗しました。From={From}, To={To}",
				workFileName,
				thumbnailFileName);
			throw;
		}
	}

	/// <summary>
	/// 正式 Thumbnail フォルダの対象ファイルを削除します。
	/// ファイルが存在しない場合は何もしません。
	/// </summary>
	/// <param name="fileName">削除するファイル名。</param>
	public void DeleteThumbnailIfExists(string fileName)
	{
		ArgumentException.ThrowIfNullOrEmpty(fileName);

		try
		{
			var filePath = this.config.GetThumbnailFullPath(fileName);

			if (File.Exists(filePath))
			{
				File.Delete(filePath);
				this.logger?.LogInformation("正式サムネイルを削除しました。FileName={FileName}", fileName);
			}
		}
		catch (Exception ex)
		{
			this.logger?.LogError(ex, "正式サムネイル削除に失敗しました。FileName={FileName}", fileName);
			throw;
		}
	}

	/// <summary>
	/// 正式 Thumbnail フォルダの対象ファイルを非同期で削除します。
	/// ファイルが存在しない場合は何もしません。
	/// 削除処理はバックグラウンドスレッドで実行されます。
	/// </summary>
	/// <param name="fileName">削除するファイル名。</param>
	/// <returns>完了を表す ValueTask。</returns>
	/// <exception cref="ArgumentException">fileName が null または空の場合にスローされます。</exception>
	public ValueTask DeleteThumbnailIfExistsAsync(string fileName)
	{
		ArgumentException.ThrowIfNullOrEmpty(fileName);

		// バックグラウンドスレッドで実行
		return new ValueTask(
			Task.Run(() => this.deleteThumbnailIfExistsSync(fileName)));
	}

	/// <summary>
	/// 正式サムネイルファイルを削除する実際の処理をバックグラウンドで実行します。
	/// UIスレッドをブロックしないよう、Task.Run内で呼び出されることを想定しています。
	/// </summary>
	private void deleteThumbnailIfExistsSync(string fileName)
	{
		try
		{
			var filePath = this.config.GetThumbnailFullPath(fileName);

			if (File.Exists(filePath))
			{
				File.Delete(filePath);
				this.logger?.LogInformation("正式サムネイルを削除しました。FileName={FileName}", fileName);
			}
		}
		catch (Exception ex)
		{
			this.logger?.LogError(ex, "正式サムネイル削除に失敗しました。FileName={FileName}", fileName);
			throw;
		}
	}

	/// <summary>
	/// WorkThumbnail フォルダの対象ファイルを削除します。
	/// ファイルが存在しない場合は何もしません。
	/// </summary>
	/// <param name="fileName">削除するファイル名。</param>
	public void DeleteWorkThumbnailIfExists(string fileName)
	{
		ArgumentException.ThrowIfNullOrEmpty(fileName);

		try
		{
			var filePath = this.appSettings.GetWorkThumbnailFullPath(fileName);

			if (File.Exists(filePath))
			{
				File.Delete(filePath);
				this.logger?.LogInformation("WorkThumbnail を削除しました。FileName={FileName}", fileName);
			}
		}
		catch (Exception ex)
		{
			this.logger?.LogError(ex, "WorkThumbnail 削除に失敗しました。FileName={FileName}", fileName);
			throw;
		}
	}
}
