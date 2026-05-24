using System.Text;
using System.Text.RegularExpressions;
using MangaBinder;

namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// 漫画タイトルの正規化・パースを担うヘルパークラスです。
/// </summary>
public static class MangaTitleHelper
{
    /// <summary>
    /// タイトルの表記ゆれを吸収するための内部用正規化を行います。
    /// <list type="bullet">
    ///   <item>全角英数字・記号の半角化</item>
    ///   <item>波ダッシュ（U+301C / U+FF5E）を U+301C に統一</item>
    ///   <item>文字列前後の空白（全角・半角）の除去</item>
    /// </list>
    /// </summary>
    /// <param name="title">正規化前のタイトル文字列。</param>
    /// <returns>正規化後のタイトル文字列。</returns>
    public static string NormalizeTitleInternal(string title)
    {
        // macOS由来のNFD（濁点分離）文字をNFC（合成済み）に変換し、DB上での名寄せを保証する
        var nfc = title.Normalize(NormalizationForm.FormC);

        // 波ダッシュの統一：全角チルダ(U+FF5E)を波ダッシュ(U+301C)に置換
        // ※全角変換ループより先に処理することで、U+FF5Eが半角チルダに変換されるのを防ぐ
        var unified = nfc.Replace('\uFF5E', '\u301C');

        // 全角英数字・記号（U+FF01〜U+FF5E）を半角（U+0021〜U+007E）に変換
        var sb = new StringBuilder(unified.Length);
        foreach (var c in unified)
        {
            if (c >= '\uFF01' && c <= '\uFF5E')
                sb.Append((char)(c - 0xFEE0));
            else
                sb.Append(c);
        }

        // 前後の空白除去（char.IsWhiteSpace が全角スペース U+3000 も対象とするため Trim() で統一処理）
        return sb.ToString().Trim();
    }

    /// <summary>
    /// 製本済みファイル名を解析し、<see cref="MangaSeries"/> を生成します。
    /// <para>対応形式例: <c>[著者名] タイトル 第01-10巻.zip</c>、<c>[著者名] タイトル 第01-全10巻.zip</c></para>
    /// </summary>
    /// <param name="rawName">拡張子を含む元のファイル名。</param>
    /// <returns>解析結果を格納した <see cref="MangaSeries"/>。</returns>
    public static MangaSeries ParseAsBinding(string rawName, string separatorChars = "")
    {
        // 拡張子を除去
        var stem = Path.GetFileNameWithoutExtension(rawName);

        // 先頭の [作者] を抽出
        var authorMatch = AuthorPattern.Match(stem);
        var author = authorMatch.Success ? authorMatch.Groups["author"].Value.Trim() : string.Empty;
        var remaining = authorMatch.Success ? stem[authorMatch.Length..].Trim() : stem.Trim();

        // 巻数表記を抽出（第n-全m巻 / 第n-m巻 / 全n巻 / 第n巻）
        var volMatch = BindingVolumePattern.Match(remaining);

        var startVolume = 0;
        var boundEndVolume = 0;
        var endVolume = 0;
        var seriesCompleted = false;

        if (volMatch.Success)
        {
            // マッチ文字列に「全」が含まれる場合は完結扱い
            seriesCompleted = volMatch.Value.Contains('全');

            if (volMatch.Groups["start"].Value is { Length: > 0 } startStr)
                startVolume = int.Parse(startStr);

            if (volMatch.Groups["bound"].Value is { Length: > 0 } boundStr)
                boundEndVolume = int.Parse(boundStr);
            else
                // 単巻（第n巻）の場合は start をそのまま BoundEndVolume にも格納
                boundEndVolume = startVolume;

            // 「全」が含まれる場合は EndVolume にも総巻数を格納
            if (seriesCompleted)
                endVolume = boundEndVolume;
        }

        // 巻数表記より前をタイトルとして切り出す
        var titleRaw = volMatch.Success
            ? remaining[..volMatch.Index].Trim()
            : remaining.Trim();

        return new MangaSeries
        {
            Title = titleRaw,
            NormalizedTitleInternal = NormalizeTitleInternal(titleRaw),
            ShortTitle = GetShortTitle(titleRaw, separatorChars),
            Author = author,
            SeriesCompleted = seriesCompleted,
            StartVolume = startVolume,
            EndVolume = endVolume,
            BoundEndVolume = boundEndVolume,
        };
    }

    /// <summary>
    /// 素材フォルダ名を解析し、<see cref="MangaSeries"/> を生成します。
    /// <para>対応形式例: <c>タイトル 全4巻</c>、<c>#タイトル （全3巻）</c></para>
    /// </summary>
    /// <param name="rawName">素材フォルダの元の名前。</param>
    /// <returns>解析結果を格納した <see cref="MangaSeries"/>。</returns>
    public static MangaSeries ParseAsMaterial(string rawName, string separatorChars = "")
    {
        // 括弧あり「 （全n巻）」: SeriesCompleted=true, IsOwnedCompleted=false
        var parenMatch = MaterialParenPattern.Match(rawName);
        if (parenMatch.Success)
        {
            var titleRaw = rawName[..parenMatch.Index].Trim();
            return new MangaSeries
            {
                Title = titleRaw,
                NormalizedTitleInternal = NormalizeTitleInternal(titleRaw),
                ShortTitle = GetShortTitle(titleRaw, separatorChars),
                Author = string.Empty,
                SeriesCompleted = true,
                IsOwnedCompleted = false,
                StartVolume = 0,
                EndVolume = int.Parse(parenMatch.Groups["vol"].Value),
            };
        }

        // 括弧なし「 全n巻」: SeriesCompleted=true, IsOwnedCompleted=true
        var bareMatch = MaterialBarePattern.Match(rawName);
        if (bareMatch.Success)
        {
            var titleRaw = rawName[..bareMatch.Index].Trim();
            return new MangaSeries
            {
                Title = titleRaw,
                NormalizedTitleInternal = NormalizeTitleInternal(titleRaw),
                ShortTitle = GetShortTitle(titleRaw, separatorChars),
                Author = string.Empty,
                SeriesCompleted = true,
                IsOwnedCompleted = true,
                StartVolume = 0,
                EndVolume = int.Parse(bareMatch.Groups["vol"].Value),
            };
        }

        // 表記なし
        var title = rawName.Trim();
        return new MangaSeries
        {
            Title = title,
            NormalizedTitleInternal = NormalizeTitleInternal(title),
            ShortTitle = GetShortTitle(title, separatorChars),
            Author = string.Empty,
            SeriesCompleted = false,
            IsOwnedCompleted = false,
            StartVolume = 0,
            EndVolume = 0,
        };
    }

    /// <summary>略称タイトルの最大文字数。</summary>
    private const int MaxShortTitleLength = 30;

    /// <summary>30文字以内の略称タイトルを生成します。</summary>
    /// <param name="title">元のタイトル文字列。</param>
    /// <param name="separatorChars">DBから取得した区切り文字群。</param>
    /// <returns>30文字以内に収めた略称タイトル。</returns>
    public static string GetShortTitle(string title, string separatorChars)
    {
        // タイトルが最大文字数以下ならそのまま返す
        if (title.Length <= MaxShortTitleLength)
            return title;
        // ① separatorChars による Split（先頭要素）
        if (!string.IsNullOrEmpty(separatorChars))
        {
            var part = title.Split(separatorChars.ToCharArray())[0].Trim();
            if (part.Length <= MaxShortTitleLength)
                return part;
        }

        // ② 全角/半角スペースによる Split（先頭要素）
        {
            var part = title.Split([' ', '\u3000'])[0].Trim();
            if (part.Length <= MaxShortTitleLength)
                return part;
        }

        // ③ 先頭30文字での Substring
        return title[..MaxShortTitleLength].Trim();
    }

    /// <summary>先頭の [作者名] を抽出する正規表現です。</summary>
    private static readonly Regex AuthorPattern =
        new(@"^\[(?<author>[^\]]+)\]\s*", RegexOptions.Compiled);

    /// <summary>
    /// 製本済みファイル名の巻数表記を抽出する正規表現です。
    /// <list type="bullet">
    ///   <item><c>第n-全m巻</c>: start=n, bound=m, 完結あり</item>
    ///   <item><c>第n-m巻</c>: start=n, bound=m</item>
    ///   <item><c>全n巻</c>: bound=n, 完結あり（start なし）</item>
    ///   <item><c>第n巻</c>: start=n（単巻）</item>
    /// </list>
    /// </summary>
    private static readonly Regex BindingVolumePattern = new(
        @"第(?<start>\d+)[-－]\s*全(?<bound>\d+)巻" +
        @"|第(?<start>\d+)[-－]\s*(?<bound>\d+)巻" +
        @"|全(?<bound>\d+)巻" +
        @"|第(?<start>\d+)巻",
        RegexOptions.Compiled);

    /// <summary>括弧あり「 （全n巻）」を末尾で捉える正規表現です。</summary>
    private static readonly Regex MaterialParenPattern =
        new(@" （全(?<vol>\d+)巻）$", RegexOptions.Compiled);

    /// <summary>括弧なし「 全n巻」を末尾で捉える正規表現です。</summary>
    private static readonly Regex MaterialBarePattern =
        new(@" 全(?<vol>\d+)巻$", RegexOptions.Compiled);
}
