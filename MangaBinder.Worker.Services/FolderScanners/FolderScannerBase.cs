using MangaBinder;
using MangaBinder.Bindings;
using MangaBinder.Jobs.Contexts;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using ZLogger;

namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// フォルダスキャンジョブの共通基底クラスです。
/// テンプレートメソッドパターンにより、共通フローを提供します。
/// </summary>
public abstract class FolderScannerBase : IJob
{
	/// <inheritdoc/>
	public bool SkipThumbnailSizeLimit { get; set; }

	/// <summary>スコープファクトリ。</summary>
	protected readonly IServiceScopeFactory scopeFactory;

	/// <summary>ロガー。</summary>
	protected readonly ILogger logger;

	/// <summary>このスキャナーが担当するフォルダの役割。</summary>
	protected readonly FolderRole role;

	/// <summary>Worker 実行コンテキスト。</summary>
	protected readonly WorkerContext workerContext;

	/// <summary>
	/// <see cref="FolderScannerBase"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="scopeFactory">スコープファクトリ。</param>
	/// <param name="logger">ロガー。</param>
	/// <param name="role">このスキャナーが担当するフォルダの役割。</param>
	/// <param name="workerContext">Worker 実行コンテキスト。</param>
	protected FolderScannerBase(IServiceScopeFactory scopeFactory, ILogger logger, FolderRole role, WorkerContext workerContext)
	{
		this.scopeFactory = scopeFactory;
		this.logger = logger;
		this.role = role;
		this.workerContext = workerContext;
	}

	/// <summary>
	/// フォルダスキャンを非同期で実行します。
	/// リポジトリからルートパスを取得し、走査・保存フローを派生クラスに委譲します。
	/// </summary>
	/// <param name="ct">キャンセルトークン。</param>
	public virtual async ValueTask ExecuteAsync(CancellationToken ct)
	{
		this.logger.ZLogInformation($"フォルダスキャン開始: Role={this.role}");

		using var scope = this.scopeFactory.CreateScope();
		var repository = scope.ServiceProvider.GetRequiredService<IFolderScannerRepository>();
		var rootPaths = await repository.GetSourceFoldersAsync((int)this.role, ct);
		var savedCount = await this.ScanAndSaveAsync(rootPaths, ct);

		this.logger.ZLogInformation($"フォルダスキャン完了: {savedCount} 件保存");
	}

    /// <summary>
    /// 作品のサムネイルが作成済みであり、かつ実ファイルが存在するかどうかを確認します。
    /// </summary>
    /// <param name="series">確認対象の作品。</param>
    /// <returns>
    /// <see cref="ThumbnailStatus.Completed"/> かつ <see cref="MangaSeries.ThumbnailFileName"/> が空でなく、
    /// かつサムネイルファイルが実在する場合は <c>true</c>。
    /// </returns>
    protected bool HasCompletedThumbnail(MangaSeries series)
    {
        if (series.ThumbnailStatus != ThumbnailStatus.Completed)
            return false;
        if (string.IsNullOrEmpty(series.ThumbnailFileName))
            return false;
        var fullPath = ((IMangaBinderConfig)this.workerContext).GetThumbnailFullPath(series.ThumbnailFileName);
        return File.Exists(fullPath);
    }

    /// <summary>
    /// ファイル／フォルダ名がNFC正規化済みでない場合、物理リネームを行います。
    /// <para>
    /// WindowsのNTFSはNFDとNFCを同一視するため、
    /// Guid経由の一時名を挟んだ2段階リネームでNFC名に強制変換します。
    /// リネームに失敗した場合は元の <paramref name="item"/> をそのまま返して処理を継続します。
    /// </para>
    /// </summary>
    /// <param name="item">検証対象のファイルまたはフォルダ情報。</param>
    /// <returns>NFC正規化済みの名前を持つ <see cref="FileSystemInfo"/>。</returns>
    protected FileSystemInfo EnsurePhysicalNormalization(FileSystemInfo item)
    {
        if (item.Name.IsNormalized(NormalizationForm.FormC))
            return item;

        var parentDir = Path.GetDirectoryName(item.FullName)!;
        var nfcName = item.Name.Normalize(NormalizationForm.FormC);
        var tempPath = Path.Combine(parentDir, Guid.NewGuid().ToString("N"));
        var nfcPath = Path.Combine(parentDir, nfcName);

        try
        {
            // NTFSのNFD/NFC同一視を回避するため、一時名を経由した2段階リネームを行う
            if (item is FileInfo file)
            {
                file.MoveTo(tempPath);
                File.Move(tempPath, nfcPath);
                this.logger.ZLogInformation($"物理リネーム（ファイル）: {item.Name} -> {nfcName}");
                return new FileInfo(nfcPath);
            }
            else
            {
                Directory.Move(item.FullName, tempPath);
                Directory.Move(tempPath, nfcPath);
                this.logger.ZLogInformation($"物理リネーム（フォルダ）: {item.Name} -> {nfcName}");
                return new DirectoryInfo(nfcPath);
            }
        }
        catch (IOException ex)
        {
            this.logger.ZLogWarning(ex, $"物理リネーム失敗（IOException）。元の名前で処理継続: {item.Name}");
            return item;
        }
        catch (UnauthorizedAccessException ex)
        {
            this.logger.ZLogWarning(ex, $"物理リネーム失敗（UnauthorizedAccessException）。元の名前で処理継続: {item.Name}");
            return item;
        }
    }

    /// <summary>
    /// ルートパス一覧を走査し、作品を保存する処理を実装します。
    /// 派生クラスで走査・集約・保存の順序を制御します。
    /// </summary>
    /// <param name="rootPaths">スキャン対象のルートフォルダパス一覧。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>保存件数。</returns>
    protected abstract ValueTask<int> ScanAndSaveAsync(IEnumerable<string> rootPaths, CancellationToken ct);

    /// <summary>
    /// 解析済みの作品 1 件をリポジトリ経由で保存し、DB最新状態の <see cref="MangaSeries"/> を返します。
    /// 派生クラスで保存先（素材 / 製本）に応じた実装を行います。
    /// </summary>
    /// <param name="series">保存対象の作品。</param>
    /// <param name="repository">保存先リポジトリ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>DB上でマージ済みの最新 <see cref="MangaSeries"/>。</returns>
    protected abstract ValueTask<MangaSeries> SaveResultsAsync(MangaSeries series, IFolderScannerRepository repository, CancellationToken ct);

    /// <summary>
    /// 指定されたルートパス配下の走査対象を列挙します。
    /// </summary>
    /// <param name="rootPath">スキャン対象のルートフォルダパス。</param>
    /// <returns>走査対象の <see cref="FileSystemInfo"/> 一覧。</returns>
    protected abstract IEnumerable<FileSystemInfo> GetScanItems(string rootPath);

    /// <summary>
    /// <see cref="FileSystemInfo"/> を解析して <see cref="MangaSeries"/> を生成します。
    /// </summary>
    /// <param name="info">解析対象のファイルまたはフォルダ情報。</param>
    /// <returns>解析結果の <see cref="MangaSeries"/>。</returns>
    protected abstract MangaSeries ParseToSeries(FileSystemInfo info);
}
