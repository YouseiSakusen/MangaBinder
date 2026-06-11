using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using MangaBinder.Settings;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// EPUB ファイルからページを列挙する <see cref="IVolumeExtractor"/> 実装です。
/// </summary>
public sealed partial class EpubVolumeExtractor : IVolumeExtractor
{
	/// <inheritdoc />
	public async ValueTask ExtractPagesAsync(
		BindingSourceVolume volume,
		Func<BindingPageSource, ValueTask> onPageAsync,
		CancellationToken cancellationToken = default)
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"__epub_{Guid.NewGuid():N}");
		try
		{
			ZipFile.ExtractToDirectory(volume.SourcePath, tempDir);

			var opfPath = this.ResolveOpfPath(tempDir);
			var (manifest, spine) = this.ReadOpf(tempDir, opfPath);
			var coverImagePath = this.ResolveCoverImagePath(tempDir, opfPath, manifest);

			var bodyImages = this.CollectBodyImages(tempDir, opfPath, manifest, spine);

			var allImages = this.BuildImageList(bodyImages, coverImagePath);

			await this.EmitPagesAsync(allImages, bodyImages, coverImagePath, onPageAsync, cancellationToken);
		}
		finally
		{
			if (Directory.Exists(tempDir))
				Directory.Delete(tempDir, true);
		}
	}

	/// <summary>
	/// 一時展開フォルダ内の META-INF/container.xml を読み込み、OPF ファイルの相対パスを返します。
	/// </summary>
	/// <param name="tempDir">EPUB を展開した一時フォルダの絶対パス。</param>
	/// <returns>OPF ファイルの相対パス（OS パス区切り文字に正規化済み）。</returns>
	private string ResolveOpfPath(string tempDir)
	{
		var containerPath = Path.Combine(tempDir, "META-INF", "container.xml");
		if (!File.Exists(containerPath))
			throw new InvalidOperationException("META-INF/container.xml が見つかりません。");

		var xdoc = XDocument.Load(containerPath);
		XNamespace ns = "urn:oasis:names:tc:opendocument:xmlns:container";
		var fullPath = xdoc.Descendants(ns + "rootfile")
			.Select(e => (string?)e.Attribute("full-path"))
			.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p))
			?? throw new InvalidOperationException("container.xml に rootfile full-path が見つかりません。");

		return fullPath.Replace('/', Path.DirectorySeparatorChar);
	}

	/// <summary>OPF manifest の各 item を表す内部レコードです。</summary>
	/// <param name="Id">manifest item の id 属性値。</param>
	/// <param name="Href">manifest item の href 属性値（URL エンコードのまま）。</param>
	/// <param name="MediaType">manifest item の media-type 属性値。</param>
	/// <param name="Properties">manifest item の properties 属性値（スペース区切りの複数値を含む）。</param>
	private record ManifestItem(string Id, string Href, string MediaType, string Properties);

	/// <summary>
	/// OPF ファイルを読み込み、manifest アイテム一覧と spine の idref 順リストを返します。
	/// </summary>
	/// <param name="tempDir">EPUB を展開した一時フォルダの絶対パス。</param>
	/// <param name="opfRelPath">OPF ファイルの相対パス。</param>
	/// <returns>manifest アイテム一覧と spine idref 順リストのタプル。</returns>
	private (IReadOnlyList<ManifestItem> Manifest, IReadOnlyList<string> Spine) ReadOpf(string tempDir, string opfRelPath)
	{
		var opfFullPath = Path.Combine(tempDir, opfRelPath);
		if (!File.Exists(opfFullPath))
			throw new InvalidOperationException($"OPF ファイルが見つかりません: {opfRelPath}");

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
			throw new InvalidOperationException("OPF spine が空です。");

		return (manifest, spine);
	}

	/// <summary>
	/// OPF manifest からカバー画像の絶対パスを解決します。
	/// カバー画像が特定できない場合は <see langword="null"/> を返します。
	/// </summary>
	/// <param name="tempDir">EPUB を展開した一時フォルダの絶対パス。</param>
	/// <param name="opfRelPath">OPF ファイルの相対パス。</param>
	/// <param name="manifest">OPF manifest アイテム一覧。</param>
	/// <returns>カバー画像の絶対パス。特定できない場合は <see langword="null"/>。</returns>
	private string? ResolveCoverImagePath(string tempDir, string opfRelPath, IReadOnlyList<ManifestItem> manifest)
	{
		var opfDir = Path.GetDirectoryName(opfRelPath) ?? string.Empty;

		// 優先1: properties 属性に cover-image を含む item
		var coverItem = manifest.FirstOrDefault(m =>
			m.Properties.Split(' ').Contains("cover-image", StringComparer.OrdinalIgnoreCase));

		// 優先2: meta name="cover" の content が指す manifest item
		if (coverItem is null)
		{
			var opfFullPath = Path.Combine(tempDir, opfRelPath);
			var xdoc = XDocument.Load(opfFullPath);
			XNamespace opf = "http://www.idpf.org/2007/opf";

			var coverId = xdoc.Descendants(opf + "meta")
				.Where(e => string.Equals((string?)e.Attribute("name"), "cover", StringComparison.OrdinalIgnoreCase))
				.Select(e => (string?)e.Attribute("content"))
				.FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

			if (coverId is not null)
				coverItem = manifest.FirstOrDefault(m => m.Id == coverId);
		}

		if (coverItem is null)
			return null;

		var href = HttpUtility.UrlDecode(coverItem.Href).Replace('/', Path.DirectorySeparatorChar);
		var fullPath = Path.GetFullPath(Path.Combine(tempDir, opfDir, href));
		return File.Exists(fullPath) ? fullPath : null;
	}

	/// <summary>
	/// spine 順に XHTML を走査し、img src に出現する画像の絶対パスを本文順で返します。
	/// 同一画像の重複は除外します。
	/// </summary>
	/// <param name="tempDir">EPUB を展開した一時フォルダの絶対パス。</param>
	/// <param name="opfRelPath">OPF ファイルの相対パス。</param>
	/// <param name="manifest">OPF manifest アイテム一覧。</param>
	/// <param name="spine">spine の idref 順リスト。</param>
	/// <returns>本文順に並んだ画像の絶対パス一覧。</returns>
	private List<string> CollectBodyImages(
		string tempDir,
		string opfRelPath,
		IReadOnlyList<ManifestItem> manifest,
		IReadOnlyList<string> spine)
	{
		var opfDir = Path.GetDirectoryName(opfRelPath) ?? string.Empty;
		var manifestById = manifest.ToDictionary(m => m.Id, m => m);
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var result = new List<string>();

		foreach (var idref in spine)
		{
			if (!manifestById.TryGetValue(idref, out var item))
				continue;

			var xhtmlHref = HttpUtility.UrlDecode(item.Href).Replace('/', Path.DirectorySeparatorChar);
			var xhtmlFullPath = Path.GetFullPath(Path.Combine(tempDir, opfDir, xhtmlHref));
			if (!File.Exists(xhtmlFullPath))
				continue;

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

				if (!SupportedExtensionHelper.IsImage(Path.GetExtension(imgFullPath)))
					continue;
				if (!File.Exists(imgFullPath))
					continue;
				if (!seen.Add(imgFullPath))
					continue;

				result.Add(imgFullPath);
			}
		}

		if (result.Count == 0)
			throw new InvalidOperationException("本文から画像が1件も見つかりませんでした。");

		return result;
	}

	/// <summary>XHTML 内の img タグの src 属性値を抽出する正規表現です。</summary>
	/// <remarks>
	/// <see cref="GeneratedRegex"/> の制約により static partial が必要です。
	/// </remarks>
	[GeneratedRegex(@"<img\b[^>]*\bsrc\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase)]
	private partial Regex ImgSrcRegex();

	/// <summary>XHTML 内の SVG image タグの xlink:href / href 属性値を抽出する正規表現です。</summary>
	/// <remarks>
	/// 固定レイアウト EPUB で使われる <c>&lt;image xlink:href="..."&gt;</c> 形式に対応します。
	/// <see cref="GeneratedRegex"/> の制約により static partial が必要です。
	/// </remarks>
	[GeneratedRegex(@"<image\b[^>]*\b(?:xlink:href|href)\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase)]
	private partial Regex SvgImageHrefRegex();

	/// <summary>
	/// 本文画像リストの先頭にカバー画像を差し込んだ全画像リストを返します。
	/// カバー画像が既に本文に含まれている場合は本文リストをそのまま返します。
	/// </summary>
	/// <param name="bodyImages">本文画像の絶対パス一覧。</param>
	/// <param name="coverImagePath">カバー画像の絶対パス。不明な場合は <see langword="null"/>。</param>
	/// <returns>出力順に並んだ全画像の絶対パス一覧。</returns>
	private List<string> BuildImageList(List<string> bodyImages, string? coverImagePath)
	{
		if (coverImagePath is null)
			return bodyImages;

		var coverNorm = Path.GetFullPath(coverImagePath);
		if (bodyImages.Any(p => string.Equals(Path.GetFullPath(p), coverNorm, StringComparison.OrdinalIgnoreCase)))
			return bodyImages;

		var result = new List<string>(bodyImages.Count + 1) { coverImagePath };
		result.AddRange(bodyImages);
		return result;
	}

	/// <summary>
	/// カバー画像の SourceName を生成します。
	/// 本文先頭画像のファイル名末尾連番を <c>!000…</c> に置き換えた名前を返します。
	/// 末尾連番が見つからない場合は <c>!000{拡張子}</c> を返します。
	/// </summary>
	/// <param name="bodyImages">本文画像の絶対パス一覧。</param>
	/// <param name="coverImagePath">カバー画像の絶対パス。</param>
	/// <returns>カバー画像の SourceName。</returns>
	private string BuildCoverSourceName(List<string> bodyImages, string coverImagePath)
	{
		var coverExt = Path.GetExtension(coverImagePath);

		if (bodyImages.Count == 0)
			return $"!000{coverExt}";

		var firstBodyName = Path.GetFileNameWithoutExtension(bodyImages[0]);
		var match = this.TrailingDigitsRegex().Match(firstBodyName);
		if (!match.Success)
			return $"!000{coverExt}";

		var digits = match.Value;
		var prefix = firstBodyName[..^digits.Length];
		var zeros = new string('0', digits.Length);
		return $"{prefix}!{zeros}{coverExt}";
	}

	/// <summary>ファイル名末尾の連続する数字を抽出する正規表現です。</summary>
	/// <remarks>
	/// <see cref="GeneratedRegex"/> の制約により static partial が必要です。
	/// </remarks>
	[GeneratedRegex(@"\d+$")]
	private partial Regex TrailingDigitsRegex();

	/// <summary>
	/// 全画像リストを順に走査し、<see cref="BindingPageSource"/> を生成してコールバックへ渡します。
	/// カバー画像は <see cref="BuildCoverSourceName"/> のルールで命名し、本文画像は 001 から連番にします。
	/// </summary>
	/// <param name="allImages">出力順に並んだ全画像の絶対パス一覧。</param>
	/// <param name="bodyImages">本文画像の絶対パス一覧（カバー判定に使用）。</param>
	/// <param name="coverImagePath">カバー画像の絶対パス。不明な場合は <see langword="null"/>。</param>
	/// <param name="onPageAsync">ページごとに呼び出されるコールバック。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	private async ValueTask EmitPagesAsync(
		List<string> allImages,
		List<string> bodyImages,
		string? coverImagePath,
		Func<BindingPageSource, ValueTask> onPageAsync,
		CancellationToken cancellationToken)
	{
		int bodySeq = 1;

		foreach (var imgPath in allImages)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var isCover = coverImagePath is not null
				&& string.Equals(Path.GetFullPath(imgPath), Path.GetFullPath(coverImagePath), StringComparison.OrdinalIgnoreCase)
				&& !bodyImages.Any(p => string.Equals(Path.GetFullPath(p), Path.GetFullPath(coverImagePath), StringComparison.OrdinalIgnoreCase));

			string sourceName;

			if (isCover)
			{
				sourceName = this.BuildCoverSourceName(bodyImages, imgPath);
			}
			else
			{
				var ext = Path.GetExtension(imgPath);
				sourceName = $"{bodySeq:D3}{ext}";
				bodySeq++;
			}

			var captured = imgPath;
			var page = new BindingPageSource
			{
				SourceName = sourceName,
				Extension = Path.GetExtension(sourceName),
				OpenStreamAsync = (ct) => ValueTask.FromResult<Stream>(File.OpenRead(captured)),
			};

			await onPageAsync(page);
		}
	}
}
