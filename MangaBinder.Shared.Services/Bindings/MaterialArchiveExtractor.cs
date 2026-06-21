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
	/// フォルダごとの集計情報を保持する内部クラス。
	/// </summary>
	private sealed class FolderStats
	{
		/// <summary>このフォルダ配下の画像ファイル数。</summary>
		public int FileCount { get; set; }

		/// <summary>アーカイブファイルの有無。</summary>
		public bool HasArchiveFile { get; set; }

		/// <summary>このフォルダ配下の画像ファイルの合計サイズ（展開後）。</summary>
		public long TotalImageBytes { get; set; }
	}
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
		var folders = new List<ArchiveFolderItem>();

		try
		{
			using var archive = ArchiveFactory.OpenArchive(fileInfo);
			// Entries は逐次読み取り専用アーカイブ（RAR 等）では1回しか列挙できないため、先にリスト化する
			var entries = archive.Entries.ToList();

			// 単一走査で NestedArchive 判定 + フォルダ統計を集計
			var folderStats = this.computeFolderStats(entries, out isNestedArchive);

			// ツリー構造を構築
			this.buildArchiveTree(folders, entries, folderStats, cancellationToken);
		}
		catch
		{
			// アーカイブが開けない場合は folders は空のままにする
		}

		var result = new MaterialArchiveFile
		{
			ArchivePath = archivePath,
			FileSize = fileInfo.Length,
			LastWriteTime = fileInfo.LastWriteTime,
			IsNestedArchive = isNestedArchive,
		};

		// 構築したツリーを result に追加
		foreach (var folder in folders)
		{
			result.Folders.Add(folder);
		}

		return result;
	}

	/// <summary>
	/// 全エントリを1回走査して、フォルダごとの統計情報と NestedArchive 判定を実施します。
	/// ファイルは直接の親フォルダのみに統計を加算します。
	/// </summary>
	private Dictionary<string, FolderStats> computeFolderStats(List<IArchiveEntry> entries, out bool isNestedArchive)
	{
		var stats = new Dictionary<string, FolderStats>(StringComparer.OrdinalIgnoreCase);
		isNestedArchive = false;

		// エントリを一度だけ走査
		foreach (var entry in entries)
		{
			if (entry.IsDirectory || entry.Key == null)
				continue;

			var key = entry.Key.Replace('\\', '/');
			var ext = Path.GetExtension(key);
			var isImage = SupportedExtensionHelper.IsImage(ext);
			var isArchive = SupportedExtensionHelper.IsArchive(ext);

			// NestedArchive 判定
			if (isArchive)
				isNestedArchive = true;

			// ファイルの直接の親フォルダパスを取得
			var lastSlashIndex = key.LastIndexOf('/');
			if (lastSlashIndex > 0)
			{
				// 親フォルダが存在する場合のみ統計を更新
				var parentFolderPath = key.Substring(0, lastSlashIndex);

				if (!stats.TryGetValue(parentFolderPath, out var folderStat))
				{
					folderStat = new FolderStats();
					stats[parentFolderPath] = folderStat;
				}

				if (isImage)
				{
					folderStat.FileCount++;
					folderStat.TotalImageBytes += entry.Size;
				}

				if (isArchive)
					folderStat.HasArchiveFile = true;
			}
		}

		return stats;
	}

	/// <summary>
	/// アーカイブ内のフォルダ構造ツリーを構築します。
	/// </summary>
	private void buildArchiveTree(
		List<ArchiveFolderItem> folders,
		List<IArchiveEntry> entries,
		Dictionary<string, FolderStats> folderStats,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// フォルダパスの完全なリストを抽出
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
			string? currentKey = null;

			for (var i = 0; i < parts.Length; i++)
			{
				var key = string.Join("/", parts.Take(i + 1));
				if (!nodeMap.TryGetValue(key, out var node))
				{
					// 事前集計済みの統計情報を使用
					var hasStats = folderStats.TryGetValue(key, out var stat);
					var fileCount = hasStats ? stat!.FileCount : 0;
					var hasArchiveFile = hasStats && stat!.HasArchiveFile;
					var totalImageBytes = hasStats ? stat!.TotalImageBytes : 0L;

					var selectable = this.evaluateFolderSelectability(key);
					var reason = selectable ? string.Empty : this.buildDisabledReason(parts[i], key);

					node = new ArchiveFolderItem
					{
						EntryPath = key,
						ParentEntryPath = currentKey ?? string.Empty,
						FileCount = fileCount,
						IsSelectable = selectable,
						SelectionDisabledReason = reason,
						HasArchiveFile = hasArchiveFile,
						TotalImageBytes = totalImageBytes,
					};

					nodeMap[key] = node;

					// ルートレベルの場合は folders に追加
					if (i == 0)
					{
						folders.Add(node);
					}
				}

				if (i + 1 < parts.Length)
				{
					var nextKey = string.Join("/", parts.Take(i + 2));
					if (!nodeMap.TryGetValue(nextKey, out var nextNode))
					{
						// 事前集計済みの統計情報を使用
						var hasStats = folderStats.TryGetValue(nextKey, out var stat);
						var fileCount = hasStats ? stat!.FileCount : 0;
						var hasArchiveFile = hasStats && stat!.HasArchiveFile;
						var totalImageBytes = hasStats ? stat!.TotalImageBytes : 0L;

						var selectable = this.evaluateFolderSelectability(nextKey);
						var reason = selectable ? string.Empty : this.buildDisabledReason(parts[i + 1], nextKey);

						nextNode = new ArchiveFolderItem
						{
							EntryPath = nextKey,
							ParentEntryPath = key,
							FileCount = fileCount,
							IsSelectable = selectable,
							SelectionDisabledReason = reason,
							HasArchiveFile = hasArchiveFile,
							TotalImageBytes = totalImageBytes,
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
