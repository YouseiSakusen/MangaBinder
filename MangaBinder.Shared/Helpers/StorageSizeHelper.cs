using MangaBinder.Helpers;

namespace MangaBinder.Helpers;

/// <summary>
/// ファイルシステムのパスサイズ計算を担うヘルパークラスです。
/// </summary>
public static class StorageSizeHelper
{
	/// <summary>
	/// 指定されたパスのサイズを取得します。
	/// ファイルの場合は FileInfo.Length を返し、フォルダの場合は配下を再帰的に集計します。
	/// 存在しない場合は 0 を返します。
	/// </summary>
	/// <param name="path">対象パス（ファイルまたはフォルダ）。</param>
	/// <returns>パスのサイズ（バイト）。存在しない場合は 0。</returns>
	public static long GetPathSize(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return 0;
		}

		if (File.Exists(path))
		{
			return new FileInfo(path).Length;
		}

		if (Directory.Exists(path))
		{
			return calculateDirectorySize(path);
		}

		return 0;
	}

	/// <summary>
	/// 指定されたパス一覧の合計サイズを非同期で取得します。
	/// フォルダ・圧縮ファイル・EPUB・その他対応ファイルを含め、全て集計対象とします。
	/// UI スレッドをブロックしないよう非同期で実行されます。
	/// </summary>
	/// <param name="paths">対象パスの列挙。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>合計サイズ（バイト）。</returns>
	public static async ValueTask<long> GetTotalAllAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
	{
		var pathList = paths?.ToList() ?? [];

		if (pathList.Count == 0)
		{
			return 0;
		}

		return await Task.Run(() =>
		{
			long totalSize = 0;

			foreach (var path in pathList)
			{
				cancellationToken.ThrowIfCancellationRequested();
				totalSize += GetPathSize(path);
			}

			return totalSize;
		}, cancellationToken);
	}

	/// <summary>
	/// 指定されたパス一覧のうち、MangaBinder がアーカイブとして扱うファイルのみの合計サイズを非同期で取得します。
	/// SupportedExtensionHelper を使用してアーカイブ判定を行い、マッチしたファイルのみ集計対象とします。
	/// UI スレッドをブロックしないよう非同期で実行されます。
	/// </summary>
	/// <param name="paths">対象パスの列挙。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>アーカイブファイルの合計サイズ（バイト）。</returns>
	public static async ValueTask<long> GetArchiveOnlyAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
	{
		var pathList = paths?.ToList() ?? [];

		if (pathList.Count == 0)
		{
			return 0;
		}

		return await Task.Run(() =>
		{
			long totalSize = 0;

			foreach (var path in pathList)
			{
				cancellationToken.ThrowIfCancellationRequested();

				if (isArchivePath(path))
				{
					totalSize += GetPathSize(path);
				}
			}

			return totalSize;
		}, cancellationToken);
	}

	/// <summary>
	/// 容量をフォーマット済み文字列に変換します。
	/// 1GB 未満は MB 表示（小数第1位）、1GB 以上は GB 表示（小数第1位）で返します。
	/// 0 バイトの場合は「0 MB」を返します。
	/// </summary>
	/// <param name="bytes">バイト数。</param>
	/// <returns>フォーマット済みサイズ文字列（例: "256.5 MB", "1.2 GB"）。</returns>
	public static string FormatSize(long bytes)
	{
		if (bytes == 0)
		{
			return "0 MB";
		}

		const long oneGB = 1024L * 1024 * 1024;
		const long oneMB = 1024L * 1024;

		if (bytes >= oneGB)
		{
			var sizeGB = bytes / (1024.0 * 1024 * 1024);
			return $"{sizeGB:F1} GB";
		}
		else
		{
			var sizeMB = bytes / (1024.0 * 1024);
			return $"{sizeMB:F1} MB";
		}
	}

	/// <summary>
	/// 指定されたフォルダ配下のサイズを再帰的に集計します。
	/// </summary>
	/// <param name="folderPath">フォルダパス。</param>
	/// <returns>フォルダ配下の全ファイルの合計サイズ（バイト）。</returns>
	private static long calculateDirectorySize(string folderPath)
	{
		long totalSize = 0;

		var directoryInfo = new DirectoryInfo(folderPath);

		// 配下のすべてのファイルサイズを集計
		foreach (var file in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
		{
			totalSize += file.Length;
		}

		return totalSize;
	}

	/// <summary>
	/// 指定されたパスがアーカイブファイルかどうかを判定します。
	/// </summary>
	/// <param name="path">判定対象のパス。</param>
	/// <returns>アーカイブファイルの場合は true。</returns>
	private static bool isArchivePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}

		if (!File.Exists(path))
		{
			return false;
		}

		var extension = Path.GetExtension(path);
		return SupportedExtensionHelper.IsArchive(extension);
	}
}
