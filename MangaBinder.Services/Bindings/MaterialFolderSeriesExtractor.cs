using MangaBinder.Settings;
using SharpCompress.Archives;

namespace MangaBinder.Bindings;

/// <summary>
/// 素材フォルダを再帰走査して <see cref="MaterialVolumeNode"/> ツリーを作成するサービスです。
/// </summary>
public class MaterialFolderSeriesExtractor : ISeriesExtractor
{
    /// <inheritdoc/>
    public async ValueTask<MaterialVolumeNode> ExtractAsync(string path, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var root = new MaterialVolumeNode(Path.GetFileName(path), path, MaterialItemType.Root);
            this.populateFolder(root, path, ct);
            return root;
        }, ct);
    }

    /// <summary>
    /// 指定フォルダを再帰走査してノードを <paramref name="parent"/> に追加します。
    /// </summary>
    /// <param name="parent">追加先の親ノード。</param>
    /// <param name="folderPath">走査対象フォルダのパス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    private void populateFolder(MaterialVolumeNode parent, string folderPath, CancellationToken ct)
    {
        if (!Directory.Exists(folderPath))
            return;

        foreach (var dir in Directory.EnumerateDirectories(folderPath).OrderBy(d => d))
        {
            ct.ThrowIfCancellationRequested();
            var isSelectable = this.containsDirectImages(dir);
            var fileCount = this.countDirectImages(dir);
            var dirNode = new MaterialVolumeNode(
                Path.GetFileName(dir),
                dir,
                MaterialItemType.Folder,
                isSelectableByDefault: isSelectable,
                selectionDisabledReason: isSelectable ? null : "直下に画像ファイルが存在しません")
            {
                SourcePath = dir,
                FileCount = fileCount,
            };
            dirNode.SetParent(parent);
            this.populateFolder(dirNode, dir, ct);
            parent.Children.Add(dirNode);
        }

        foreach (var file in Directory.EnumerateFiles(folderPath).OrderBy(f => f))
        {
            ct.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(file);
            var fileType = SupportedExtensionHelper.GetFileType(ext);

            if (fileType == FileType.Archive)
            {
                var fileInfo = new FileInfo(file);
                long bytes = fileInfo.Length;
                var sizeText = bytes >= 1024L * 1024 * 1024
                    ? $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
                    : $"{bytes / (1024.0 * 1024):F1} MB";
                var archiveNode = new MaterialVolumeNode(Path.GetFileName(file), file, MaterialItemType.Archive)
                {
                    FileSizeText = sizeText,
                    SourcePath = file,
                };
                this.populateArchive(archiveNode, file);
                archiveNode.SetParent(parent);
                parent.Children.Add(archiveNode);
            }
            else if (fileType == FileType.Epub)
            {
                var epubNode = new MaterialVolumeNode(Path.GetFileName(file), file, MaterialItemType.Epub)
                {
                    SourcePath = file,
                };
                epubNode.SetParent(parent);
                parent.Children.Add(epubNode);
            }
        }
    }

    /// <summary>
    /// Archive ファイル内部のフォルダ構造を走査してノードを <paramref name="archiveNode"/> に追加します。
    /// </summary>
    /// <param name="archiveNode">追加先の Archive ノード。</param>
    /// <param name="archivePath">Archive ファイルのパス。</param>
    private void populateArchive(MaterialVolumeNode archiveNode, string archivePath)
    {
        using var archive = ArchiveFactory.OpenArchive(new FileInfo(archivePath));

        // Entries は逐次読み取り専用アーカイブ（RAR 等）では1回しか列挙できないため、先にリスト化する
        var entries = archive.Entries.ToList();

        var folderPaths = entries
            .Where(e => e.IsDirectory || (e.Key != null && e.Key.Contains('/')))
            .SelectMany(e => this.getAncestorFolders(e.Key, e.IsDirectory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p)
            .ToList();

        var nodeMap = new Dictionary<string, MaterialVolumeNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var folderPath in folderPaths)
        {
            var parts = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var parentNode = archiveNode;

            for (var i = 0; i < parts.Length; i++)
            {
                var key = string.Join("/", parts.Take(i + 1));
                if (!nodeMap.TryGetValue(key, out var node))
                {
                    var fileCount = this.countArchiveFolderImages(key, entries);
                    var selectable = fileCount > 0;
                    var reason = selectable ? null : this.buildDisabledReason(fileCount);
                    node = new MaterialVolumeNode(
                        parts[i],
                        $"{archivePath}/{key}",
                        MaterialItemType.Folder,
                        isSelectableByDefault: selectable,
                        selectionDisabledReason: reason)
                    {
                        SourcePath = archivePath,
                        ArchiveEntryPrefix = key,
                        FileCount = fileCount,
                    };
                    nodeMap[key] = node;
                    node.SetParent(parentNode);
                    parentNode.Children.Add(node);
                }
                parentNode = node;
            }
        }
    }

    /// <summary>
    /// 指定フォルダの直下に画像ファイルが存在するかどうかを返します。
    /// </summary>
    /// <param name="folderPath">判定対象のフォルダパス。</param>
    private bool containsDirectImages(string folderPath)
        => Directory.EnumerateFiles(folderPath)
            .Any(file => SupportedExtensionHelper.IsImage(Path.GetExtension(file)));

    /// <summary>
    /// 指定フォルダの直下にある画像ファイル数を返します。
    /// </summary>
    /// <param name="folderPath">対象フォルダパス。</param>
    private int countDirectImages(string folderPath)
        => Directory.EnumerateFiles(folderPath)
            .Count(file => SupportedExtensionHelper.IsImage(Path.GetExtension(file)));

    /// <summary>
    /// Archive 内フォルダ配下の画像ファイル数を返します。
    /// </summary>
    /// <param name="folderKey">フォルダキー（末尾スラッシュなし）。</param>
    /// <param name="entries">事前にリスト化した Archive エントリ一覧。</param>
    private int countArchiveFolderImages(string folderKey, IEnumerable<SharpCompress.Archives.IArchiveEntry> entries)
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
    /// 選択不可の理由メッセージを生成します。
    /// FileCount が 0 の場合のみ理由を返します。
    /// </summary>
    private string buildDisabledReason(int fileCount)
    {
        if (fileCount == 0)
            return "直下に画像ファイルが存在しません";
        return "選択できません";
    }

    /// <summary>
    /// エントリキーから祖先フォルダのパスをすべて返します。
    /// </summary>
    /// <param name="key">Archive エントリのキー。</param>
    /// <param name="isDirectory">エントリがディレクトリかどうか。</param>
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

