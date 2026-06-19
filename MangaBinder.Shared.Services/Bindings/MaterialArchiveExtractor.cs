using System.IO;
using System.Text.RegularExpressions;
using MangaBinder.Settings;
using SharpCompress.Archives;

namespace MangaBinder.Bindings;

/// <summary>
/// アーカイブファイルの内部フォルダ構造を解析するサービスです。
/// UI/Worker 共通で利用するための DTO を返します。
/// </summary>
public class MaterialArchiveExtractor
{
	/// <summary>
	/// アーカイブファイルを解析し、内部フォルダ構造と画像ファイル数を返します。
	/// </summary>
	/// <param name="archivePath">解析対象のアーカイブファイルパス。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>解析済みのアーカイブ情報。</returns>
	public ValueTask<MaterialArchiveFile> ExtractAsync(
		string archivePath,
		CancellationToken cancellationToken)
	{
		return new ValueTask<MaterialArchiveFile>(
			Task.Run(
				() => this.Extract(archivePath, cancellationToken),
				cancellationToken));
	}

	/// <summary>
	/// アーカイブファイルを解析します（内部用）。
	/// </summary>
	private MaterialArchiveFile Extract(string archivePath, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var fileInfo = new FileInfo(archivePath);
		bool isNestedArchive = false;

		try
		{
			using var archive = ArchiveFactory.OpenArchive(fileInfo);
			// Nested Archive 判定：アーカイブ内に対応圧縮ファイルが1件以上あるかチェック
			var entries = archive.Entries.ToList();
			isNestedArchive = entries
				.Where(e => !e.IsDirectory && e.Key != null)
				.Any(e => SupportedExtensionHelper.IsArchive(Path.GetExtension(e.Key!)));
		}
		catch
		{
			// アーカイブが開けない場合は isNestedArchive は false のままにする
		}

		var result = new MaterialArchiveFile
		{
			ArchivePath = archivePath,
			FileSize = fileInfo.Length,
			LastWriteTime = fileInfo.LastWriteTime,
			IsNestedArchive = isNestedArchive,
		};

		try
		{
			using var archive = ArchiveFactory.OpenArchive(fileInfo);
			this.populateArchive(result, archivePath, archive, cancellationToken);
		}
		catch
		{
			// アーカイブが開けない場合は Folders は空のままにする
		}

		return result;
	}

	/// <summary>
	/// アーカイブ内のフォルダ構造を走査して <paramref name="result"/> に追加します。
	/// </summary>
	private void populateArchive(MaterialArchiveFile result, string archivePath, IArchive archive, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Entries は逐次読み取り専用アーカイブ（RAR 等）では1回しか列挙できないため、先にリスト化する
		var entries = archive.Entries.ToList();

		var folderPaths = entries
			.Where(e => e.IsDirectory || (e.Key != null && e.Key.Contains('/')))
			.SelectMany(e => this.getAncestorFolders(e.Key, e.IsDirectory))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(p => p)
			.ToList();

		var nodeMap = new Dictionary<string, ArchiveFolderItem>(StringComparer.OrdinalIgnoreCase);

		foreach (var folderPath in folderPaths)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var parts = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
			var parentFolder = result;
			string? currentKey = null;

			for (var i = 0; i < parts.Length; i++)
			{
				var key = string.Join("/", parts.Take(i + 1));
				if (!nodeMap.TryGetValue(key, out var node))
				{
					var selectable = this.evaluateFolderSelectability(key);
					var reason = selectable ? string.Empty : this.buildDisabledReason(parts[i], key);
					var fileCount = this.countArchiveFolderImages(key, entries);
					var hasArchiveFile = this.countArchiveFiles(key, entries) > 0;

					node = new ArchiveFolderItem
					{
						EntryPath = key,
						ParentEntryPath = currentKey ?? string.Empty,
						FileCount = fileCount,
						IsSelectable = selectable,
						SelectionDisabledReason = reason,
						HasArchiveFile = hasArchiveFile,
					};

					nodeMap[key] = node;

					// ルートレベルの場合は result.Folders に追加
					if (i == 0)
					{
						result.Folders.Add(node);
					}
				}

				if (i + 1 < parts.Length)
				{
					var nextKey = string.Join("/", parts.Take(i + 2));
					if (!nodeMap.TryGetValue(nextKey, out var nextNode))
					{
						var selectable = this.evaluateFolderSelectability(nextKey);
						var reason = selectable ? string.Empty : this.buildDisabledReason(parts[i + 1], nextKey);
						var fileCount = this.countArchiveFolderImages(nextKey, entries);
						var hasArchiveFile = this.countArchiveFiles(nextKey, entries) > 0;

						nextNode = new ArchiveFolderItem
						{
							EntryPath = nextKey,
							ParentEntryPath = key,
							FileCount = fileCount,
							IsSelectable = selectable,
							SelectionDisabledReason = reason,
							HasArchiveFile = hasArchiveFile,
						};
						nodeMap[nextKey] = nextNode;
					}

					if (!node.Children.Contains(nextNode))
					{
						node.Children.Add(nextNode);
					}
				}

				currentKey = key;
			}
		}
	}

	/// <summary>
	/// Archive 内フォルダ配下の画像ファイル数を返します。
	/// </summary>
	private int countArchiveFolderImages(string folderKey, IEnumerable<IArchiveEntry> entries)
	{
		var normalizedFolderKey = folderKey.Replace('\\', '/').TrimEnd('/');
		var prefix = normalizedFolderKey + "/";

		return entries
			.Where(e => !e.IsDirectory && e.Key != null)
			.Count(e =>
			{
				var key = e.Key!.Replace('\\', '/');
				return key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
					&& SupportedExtensionHelper.IsImage(Path.GetExtension(key));
			});
	}

	/// <summary>
	/// Archive 内フォルダ配下のアーカイブファイル数を返します。
	/// </summary>
	private int countArchiveFiles(string folderKey, IEnumerable<IArchiveEntry> entries)
	{
		var normalizedFolderKey = folderKey.Replace('\\', '/').TrimEnd('/');
		var prefix = normalizedFolderKey + "/";

		return entries
			.Where(e => !e.IsDirectory && e.Key != null)
			.Count(e =>
			{
				var key = e.Key!.Replace('\\', '/');
				return key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
					&& SupportedExtensionHelper.IsArchive(Path.GetExtension(key));
			});
	}

	/// <summary>
	/// 選択不可の理由メッセージを生成します。
	/// </summary>
	private string buildDisabledReason(string folderName, string key)
	{
		if (this.isMultiVolumeRange(folderName))
			return "複数巻範囲と判定されました";

		return string.Empty;
	}

	/// <summary>
	/// フォルダ名が複数巻範囲を表しているかどうかを判定します。
	/// </summary>
	private bool isMultiVolumeRange(string folderName)
	{
		var pattern = new Regex(
			@"(?:\d+\s*[-–—~～〜]\s*\d+)",
			RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase);
		return pattern.IsMatch(folderName);
	}

	/// <summary>
	/// Archive 内フォルダが製本対象として選択可能かどうかを評価します。
	/// </summary>
	private bool evaluateFolderSelectability(string folderKey)
	{
		var folderName = folderKey.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
			.LastOrDefault() ?? string.Empty;

		if (this.isMultiVolumeRange(folderName))
			return false;

		return true;
	}

	/// <summary>
	/// エントリキーから祖先フォルダのパスをすべて返します。
	/// </summary>
	private IEnumerable<string> getAncestorFolders(string? key, bool isDirectory)
	{
		if (string.IsNullOrEmpty(key))
			yield break;

		var normalized = key.Replace('\\', '/').TrimEnd('/');
		var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

		// IsDirectory == true ならエントリ自身もフォルダとして扱う、false ならファイル名を除外する
		var depth = isDirectory ? parts.Length : parts.Length - 1;

		for (var i = 1; i <= depth; i++)
			yield return string.Join("/", parts.Take(i));
	}
}
