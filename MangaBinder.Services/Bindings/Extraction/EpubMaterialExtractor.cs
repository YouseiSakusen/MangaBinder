using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using MangaBinder.Helpers;
using MangaBinder.Bindings.Inspection;

namespace MangaBinder.Bindings.Extraction;

/// <summary>
/// EPUB 素材の展開を実行する Extractor です。
/// EPUB ファイルを Work フォルダ直下の .epub 一時フォルダへ展開し、
/// OPF spine の読書順から BindingImage を生成します。
/// </summary>
public partial class EpubMaterialExtractor : IMaterialExtractor
{
	/// <summary>
	/// EPUB 解析時に既知エラー条件を検出した際に throw される内部例外。
	/// EpubExtractionError が設定されており、呼び出し元で分類判定に使用します。
	/// </summary>
	private sealed class EpubKnownErrorException : Exception
	{
		public EpubExtractionError Error { get; }

		public EpubKnownErrorException(EpubExtractionError error, string? message = null)
			: base(message)
		{
			this.Error = error;
		}
	}

	private readonly ILogger<EpubMaterialExtractor> logger;

	/// <summary>
	/// <see cref="EpubMaterialExtractor"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="logger">ロガー。</param>
	public EpubMaterialExtractor(ILogger<EpubMaterialExtractor> logger)
	{
		this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// EPUB グループの素材を展開します。
	/// group.Key は EPUB ファイルのフルパスです。
	/// group 内の各 BindingVolume に対して、EPUB から抽出した BindingImage を生成・追加します。
	/// 2段階処理の第1段階として、素材分析と BindingImage 生成を行います。
	/// </summary>
	/// <param name="group">
	/// EffectiveSourcePath が同一の EPUB ファイルパスである BindingVolume グループ。
	/// </param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	public async ValueTask PrepareAsync(
		IGrouping<string, BindingVolume> group,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var epubFilePath = group.Key;

		// group 内の各 BindingVolume を処理
		foreach (var volume in group)
		{
			await this.ExtractForVolumeAsync(epubFilePath, volume, cancellationToken);
		}
	}

	/// <summary>
	/// 1つの BindingImage を準備します。
	/// EPUB 素材の場合は no-op です。CancellationToken の確認のみを行います。
	/// </summary>
	/// <param name="image">準備対象の BindingImage。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	public ValueTask PrepareImageAsync(
		BindingImage image,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return default;
	}

	/// <summary>
	/// 単一の BindingVolume に対して EPUB を展開・解析し、BindingImage を生成します。
	/// </summary>
	private async ValueTask ExtractForVolumeAsync(string epubFilePath, BindingVolume volume, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// EPUB 処理結果を初期化
		volume.EpubExtractionError = EpubExtractionError.None;
		volume.EpubExtractionWarnings = EpubExtractionWarning.None;
		volume.EpubExtractionErrorMessage = null;

		if (string.IsNullOrEmpty(volume.WorkFolderPath))
		{
			// WorkFolderPath が未設定は状態不整合
			throw new InvalidOperationException("BindingVolume.WorkFolderPath が設定されていません。");
		}

		var epubTempDir = Path.Combine(volume.WorkFolderPath, ".epub");

		try
		{
			// 展開フォルダの準備（前回残骸があれば削除）
			await this.PrepareEpubTempFolderAsync(epubTempDir, cancellationToken);

			// EPUB を ZIP として展開
			await Task.Run(
				() => ZipFile.ExtractToDirectory(epubFilePath, epubTempDir),
				cancellationToken).ConfigureAwait(false);

			cancellationToken.ThrowIfCancellationRequested();

			// EPUB を解析し、BindingImage を生成
			await this.ExtractImagesFromEpubAsync(epubTempDir, epubFilePath, volume, cancellationToken);
		}
		catch (OperationCanceledException)
		{
			// キャンセル時は .epub を削除して上位へ再送出
			await this.DeleteEpubTempFolderAsync(epubTempDir);
			throw;
		}
		catch (EpubKnownErrorException ex)
		{
			// 既知 EPUB エラー (ContainerFileNotFound, RootFileNotFound, OpfFileNotFound, SpineEmpty)
			volume.EpubExtractionError = ex.Error;
			// EpubExtractionErrorMessage は null のままにしておく（既知エラー）
			this.logger.LogDebug("EPUB 既知エラーが発生しました。エラー：{Error} ファイル：{EpubPath} WorkFolder：{WorkFolder}", ex.Error, epubFilePath, volume.WorkFolderPath);
			volume.Images.Clear();
			await this.DeleteEpubTempFolderAsync(epubTempDir);
		}
		catch (InvalidDataException ex)
		{
			// 不正な ZIP / アーカイブ
			volume.EpubExtractionError = EpubExtractionError.InvalidArchive;
			this.logger.LogError(ex, "EPUB を ZIP として展開できません。ファイル：{EpubPath} WorkFolder：{WorkFolder}", epubFilePath, volume.WorkFolderPath);
			volume.Images.Clear();
			await this.DeleteEpubTempFolderAsync(epubTempDir);
		}
		catch (XmlException ex)
		{
			// XML 不正
			volume.EpubExtractionError = EpubExtractionError.InvalidXml;
			this.logger.LogError(ex, "EPUB 内の XML が不正です。ファイル：{EpubPath} WorkFolder：{WorkFolder}", epubFilePath, volume.WorkFolderPath);
			volume.Images.Clear();
			await this.DeleteEpubTempFolderAsync(epubTempDir);
		}
		catch (IOException ex)
		{
			// I/O エラー
			volume.EpubExtractionError = EpubExtractionError.IoError;
			this.logger.LogError(ex, "EPUB 処理中に I/O エラーが発生しました。ファイル：{EpubPath} WorkFolder：{WorkFolder}", epubFilePath, volume.WorkFolderPath);
			volume.Images.Clear();
			await this.DeleteEpubTempFolderAsync(epubTempDir);
		}
		catch (UnauthorizedAccessException ex)
		{
			// アクセス拒否
			volume.EpubExtractionError = EpubExtractionError.AccessDenied;
			this.logger.LogError(ex, "EPUB ファイルへのアクセスが拒否されました。ファイル：{EpubPath} WorkFolder：{WorkFolder}", epubFilePath, volume.WorkFolderPath);
			volume.Images.Clear();
			await this.DeleteEpubTempFolderAsync(epubTempDir);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			// 分類不能な予期しないエラー
			volume.EpubExtractionError = EpubExtractionError.UnexpectedError;
			volume.EpubExtractionErrorMessage = ex.Message;
			this.logger.LogError(ex, "EPUB 展開中に予期しないエラーが発生しました。ファイル：{EpubPath} WorkFolder：{WorkFolder}", epubFilePath, volume.WorkFolderPath);
			volume.Images.Clear();
			await this.DeleteEpubTempFolderAsync(epubTempDir);
		}
	}

	/// <summary>
	/// EPUB 一時展開フォルダを準備します。
	/// 前回プロセス強制終了で残った作業残骸があれば削除します。
	/// </summary>
	private async ValueTask PrepareEpubTempFolderAsync(string epubTempDir, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (Directory.Exists(epubTempDir))
		{
			// 前回残骸を削除
			await Task.Run(() => Directory.Delete(epubTempDir, true), cancellationToken).ConfigureAwait(false);
		}

		// フォルダを作成
		Directory.CreateDirectory(epubTempDir);

		// Hidden 属性を付与（既存属性を壊さないよう OR する）
		try
		{
			var info = new DirectoryInfo(epubTempDir);
			info.Attributes |= FileAttributes.Hidden;
		}
		catch
		{
			// Hidden 属性付与に失敗しても処理を続ける
		}
	}

	/// <summary>
	/// .epub 一時展開フォルダを非同期削除します。
	/// 削除失敗時はログに記録しますが、例外は送出しません。
	/// </summary>
	private async ValueTask DeleteEpubTempFolderAsync(string epubTempDir)
	{
		try
		{
			if (Directory.Exists(epubTempDir))
			{
				await Task.Run(() => Directory.Delete(epubTempDir, true)).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			this.logger.LogWarning(ex, ".epub 一時フォルダの削除に失敗しました。フォルダ：{EpubTempDir}", epubTempDir);
		}
	}

	/// <summary>
	/// 展開済みの EPUB から画像を抽出し、BindingImage を生成・追加します。
	/// </summary>
	private async ValueTask ExtractImagesFromEpubAsync(
		string epubTempDir,
		string epubFilePath,
		BindingVolume volume,
		CancellationToken cancellationToken)
	{
		// container.xml から OPF パスを解決
		var opfPath = this.ResolveOpfPath(epubTempDir);

		// OPF を読み込み、manifest と spine を取得
		var (manifest, spine) = this.ReadOpf(epubTempDir, opfPath);

		cancellationToken.ThrowIfCancellationRequested();

		// カバー画像パスを解決（見つからない場合は null）
		var coverImagePath = this.ResolveCoverImagePath(epubTempDir, opfPath, manifest);
		if (coverImagePath is null)
		{
			volume.EpubExtractionWarnings |= EpubExtractionWarning.CoverImageNotFound;
		}

		// 本文画像を列挙
		var bodyImages = this.CollectBodyImages(epubTempDir, opfPath, manifest, spine, volume);

		cancellationToken.ThrowIfCancellationRequested();

		// 本文画像が0件の場合はエラー（Cover の有無は関係ない）
		if (bodyImages.Count == 0)
		{
			volume.EpubExtractionError = EpubExtractionError.BodyImageNotFound;
			volume.Images.Clear();
			await this.DeleteEpubTempFolderAsync(epubTempDir);
			return;
		}

		// カバーと本文を結合
		var allImages = this.BuildImageList(bodyImages, coverImagePath);

		cancellationToken.ThrowIfCancellationRequested();

		// BindingImage を生成・追加
		await this.EmitBindingImagesAsync(allImages, bodyImages, coverImagePath, volume, cancellationToken);
	}

	/// <summary>
	/// META-INF/container.xml から OPF ファイルの相対パスを解決します。
	/// </summary>
	private string ResolveOpfPath(string epubTempDir)
	{
		var containerPath = Path.Combine(epubTempDir, "META-INF", "container.xml");
		if (!File.Exists(containerPath))
			throw new EpubKnownErrorException(EpubExtractionError.ContainerFileNotFound, "META-INF/container.xml が見つかりません。");

		var xdoc = XDocument.Load(containerPath);
		XNamespace ns = "urn:oasis:names:tc:opendocument:xmlns:container";
		var fullPath = xdoc.Descendants(ns + "rootfile")
			.Select(e => (string?)e.Attribute("full-path"))
			.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));

		if (string.IsNullOrWhiteSpace(fullPath))
			throw new EpubKnownErrorException(EpubExtractionError.RootFileNotFound, "container.xml に rootfile full-path が見つかりません。");

		return fullPath.Replace('/', Path.DirectorySeparatorChar);
	}

	/// <summary>OPF manifest の各 item を表す内部レコードです。</summary>
	private record ManifestItem(string Id, string Href, string MediaType, string Properties);

	/// <summary>
	/// OPF ファイルを読み込み、manifest アイテム一覧と spine の idref 順リストを返します。
	/// </summary>
	private (IReadOnlyList<ManifestItem> Manifest, IReadOnlyList<string> Spine) ReadOpf(string epubTempDir, string opfRelPath)
	{
		var opfFullPath = Path.Combine(epubTempDir, opfRelPath);
		if (!File.Exists(opfFullPath))
			throw new EpubKnownErrorException(EpubExtractionError.OpfFileNotFound, $"OPF ファイルが見つかりません: {opfRelPath}");

		var xdoc = XDocument.Load(opfFullPath);
		XNamespace opf = "http://www.idpf.org/2007/opf";

		var manifest = xdoc.Descendants(opf + "item")
			.Select(e => new ManifestItem(
				Id: (string?)e.Attribute("id") ?? string.Empty,
				Href: (string?)e.Attribute("href") ?? string.Empty,
				MediaType: (string?)e.Attribute("media-type") ?? string.Empty,
				Properties: (string?)e.Attribute("properties") ?? string.Empty))
			.Where(m => !string.IsNullOrEmpty(m.Id))
			.ToList();

		var spine = xdoc.Descendants(opf + "itemref")
			.Select(e => (string?)e.Attribute("idref") ?? string.Empty)
			.Where(id => !string.IsNullOrEmpty(id))
			.ToList();

		if (spine.Count == 0)
			throw new EpubKnownErrorException(EpubExtractionError.SpineEmpty, "OPF spine が空です。");

		return (manifest, spine);
	}

	/// <summary>
	/// OPF manifest からカバー画像の絶対パスを解決します。
	/// カバー画像が特定できない場合は null を返します。
	/// </summary>
	private string? ResolveCoverImagePath(string epubTempDir, string opfRelPath, IReadOnlyList<ManifestItem> manifest)
	{
		var opfDir = Path.GetDirectoryName(opfRelPath) ?? string.Empty;

		// 優先1: properties 属性に cover-image を含む item
		var coverItem = manifest.FirstOrDefault(m =>
			m.Properties.Split(' ').Contains("cover-image", StringComparer.OrdinalIgnoreCase));

		// 優先2: meta name="cover" の content が指す manifest item
		if (coverItem is null)
		{
			var opfFullPath = Path.Combine(epubTempDir, opfRelPath);
			try
			{
				var xdoc = XDocument.Load(opfFullPath);
				XNamespace opf = "http://www.idpf.org/2007/opf";

				var coverId = xdoc.Descendants(opf + "meta")
					.Where(e => string.Equals((string?)e.Attribute("name"), "cover", StringComparison.OrdinalIgnoreCase))
					.Select(e => (string?)e.Attribute("content"))
					.FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

				if (coverId is not null)
					coverItem = manifest.FirstOrDefault(m => m.Id == coverId);
			}
			catch
			{
				// XDocument.Load 失敗時は null で続行
			}
		}

		if (coverItem is null)
			return null;

		var href = HttpUtility.UrlDecode(coverItem.Href).Replace('/', Path.DirectorySeparatorChar);
		var fullPath = Path.GetFullPath(Path.Combine(epubTempDir, opfDir, href));
		return File.Exists(fullPath) ? fullPath : null;
	}

	/// <summary>
	/// spine 順に XHTML を走査し、img / SVG image に出現する画像の絶対パスを本文順で返します。
	/// 同一画像の重複は除外します。
	/// </summary>
	private List<string> CollectBodyImages(
		string epubTempDir,
		string opfRelPath,
		IReadOnlyList<ManifestItem> manifest,
		IReadOnlyList<string> spine,
		BindingVolume volume)
	{
		var opfDir = Path.GetDirectoryName(opfRelPath) ?? string.Empty;
		var manifestById = manifest.ToDictionary(m => m.Id, m => m);
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var result = new List<string>();

		foreach (var idref in spine)
		{
			if (!manifestById.TryGetValue(idref, out var item))
			{
				volume.EpubExtractionWarnings |= EpubExtractionWarning.ManifestItemNotFound;
				continue;
			}

			var xhtmlHref = HttpUtility.UrlDecode(item.Href).Replace('/', Path.DirectorySeparatorChar);
			var xhtmlFullPath = Path.GetFullPath(Path.Combine(epubTempDir, opfDir, xhtmlHref));
			if (!File.Exists(xhtmlFullPath))
			{
				volume.EpubExtractionWarnings |= EpubExtractionWarning.XhtmlFileNotFound;
				continue;
			}

			var xhtmlDir = Path.GetDirectoryName(xhtmlFullPath) ?? string.Empty;
			var xhtmlContent = File.ReadAllText(xhtmlFullPath);

			var imageRefs = this.ImgSrcRegex().Matches(xhtmlContent)
				.Cast<Match>()
				.Concat(this.SvgImageHrefRegex().Matches(xhtmlContent).Cast<Match>())
				.OrderBy(m => m.Index)
				.Select(m => m.Groups[1].Value);

			foreach (var src in imageRefs)
			{
				var rawSrc = HttpUtility.UrlDecode(src);
				var imgFullPath = Path.GetFullPath(Path.Combine(xhtmlDir, rawSrc.Replace('/', Path.DirectorySeparatorChar)));

				if (!File.Exists(imgFullPath))
				{
					volume.EpubExtractionWarnings |= EpubExtractionWarning.ReferencedImageFileNotFound;
					continue;
				}

				if (!SupportedExtensionHelper.IsImage(Path.GetExtension(imgFullPath)))
				{
					volume.EpubExtractionWarnings |= EpubExtractionWarning.UnsupportedImageFormat;
					continue;
				}

				if (!seen.Add(imgFullPath))
				{
					volume.EpubExtractionWarnings |= EpubExtractionWarning.DuplicateImageRemoved;
					continue;
				}

				result.Add(imgFullPath);
			}
		}

		return result;
	}

	/// <summary>XHTML 内の img タグの src 属性値を抽出する正規表現です。</summary>
	/// <remarks>
	/// <see cref="GeneratedRegex"/> の制約により private partial が必要です。
	/// </remarks>
	[GeneratedRegex(@"<img\b[^>]*\bsrc\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase)]
	private partial Regex ImgSrcRegex();

	/// <summary>XHTML 内の SVG image タグの xlink:href / href 属性値を抽出する正規表現です。</summary>
	/// <remarks>
	/// 固定レイアウト EPUB で使われる <c>&lt;image xlink:href="..."&gt;</c> 形式に対応します。
	/// <see cref="GeneratedRegex"/> の制約により private partial が必要です。
	/// </remarks>
	[GeneratedRegex(@"<image\b[^>]*\b(?:xlink:href|href)\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase)]
	private partial Regex SvgImageHrefRegex();

	/// <summary>
	/// 本文画像リストの先頭にカバー画像を差し込んだ全画像リストを返します。
	/// カバー画像が既に本文に含まれている場合は本文リストをそのまま返します。
	/// </summary>
	private List<string> BuildImageList(List<string> bodyImages, string? coverImagePath)
	{
		var result = new List<string>(bodyImages.Count + (coverImagePath is not null ? 1 : 0));

		// カバーが存在する場合は先頭に追加
		if (coverImagePath is not null)
		{
			result.Add(coverImagePath);
		}

		// 本文画像をそのまま追加
		// Cover と同じ物理画像でも重複排除しない
		result.AddRange(bodyImages);

		return result;
	}

	/// <summary>
	/// 全画像リストから BindingImage を生成・追加します。
	/// カバー画像があれば 0 番、本文は 1 から採番します。
	/// </summary>
	private async ValueTask EmitBindingImagesAsync(
		List<string> allImages,
		List<string> bodyImages,
		string? coverImagePath,
		BindingVolume volume,
		CancellationToken cancellationToken)
	{
		// 最終本文番号の最大値から必要な桁数を計算
		var maxBodyIndex = bodyImages.Count;
		var digitCount = maxBodyIndex.ToString().Length;
		if (coverImagePath is not null)
		{
			// カバーがある場合、0 番も考慮
			digitCount = Math.Max(digitCount, 1);
		}

		int bodySeq = 1;
		bool firstImage = true;

		foreach (var imgPath in allImages)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// 先頭要素がカバー画像である場合のみ isCover = true
			// allImages は BuildImageList で [cover, body1, body2, ...] の順に構築される
			var isCover = firstImage && coverImagePath is not null
				&& string.Equals(Path.GetFullPath(imgPath), Path.GetFullPath(coverImagePath), StringComparison.OrdinalIgnoreCase);

			firstImage = false;

			string logicalFileName;

			if (isCover)
			{
				var ext = Path.GetExtension(imgPath);
				logicalFileName = $"{0.ToString($"D{digitCount}")}{ext}";
			}
			else
			{
				var ext = Path.GetExtension(imgPath);
				logicalFileName = $"{bodySeq.ToString($"D{digitCount}")}{ext}";
				bodySeq++;
			}

			// BindingImage を生成・追加
			volume.AddImage(imgPath, logicalFileName);
		}

		await Task.CompletedTask;
	}
}
