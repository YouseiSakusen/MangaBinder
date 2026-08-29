using System.Text;
using HalationGhost.Utilities;

namespace MangaBinder.Helpers;

/// <summary>
/// 漫画タイトルの正規化・パースを担うヘルパークラスです。
/// </summary>
public static class MangaTitleHelper
{
    /// <summary>
    /// タイトルの表記ゆれを吸収するための内部用正規化を行います。
    /// 表現の違いは吸収しますが、情報の違いは吸収しません。
    /// <list type="bullet">
    ///   <item>Unicode NFD を NFC に正規化</item>
    ///   <item>全角英数字・記号の半角化</item>
    ///   <item>U+2010 HYPHEN を U+002D HYPHEN-MINUS に統一</item>
    ///   <item>合字的記号を標準表記へ統一（U+203C → !! / U+2047 → ?? / U+2048 → ?! / U+2049 → !?）</item>
    ///   <item>タイトル内のすべての空白（char.IsWhiteSpace 判定）を除去</item>
    ///   <item>ASCII英字 A-Z / a-z の大小文字を統一（大文字へ）</item>
    ///   <item>波ダッシュ（U+007E / U+301C / U+FF5E）を U+301C に統一</item>
    /// </list>
    /// </summary>
    /// <param name="title">正規化前のタイトル文字列。</param>
    /// <returns>正規化後のタイトル文字列。</returns>
    public static string NormalizeTitleInternal(string title)
    {
        // macOS由来のNFD（濁点分離）文字をNFC（合成済み）に変換し、DB上での名寄せを保証する
        var nfc = title.Normalize(NormalizationForm.FormC);

        // U+2010 HYPHEN を U+002D HYPHEN-MINUS に統一
        var hyphenUnified = nfc.Replace('\u2010', '\u002D');

        // 波ダッシュの統一：ASCII TILDE (U+007E) と FULLWIDTH TILDE (U+FF5E) を WAVE DASH (U+301C) に統一
        // ※全角変換ループより先に処理することで、U+FF5E が半角チルダに変換されるのを防ぐ
        var unified = hyphenUnified.Replace('\u007E', '\u301C').Replace('\uFF5E', '\u301C');

        // 合字的な記号を標準表記へ統一
        // U+203C (‼) → !! / U+2047 (⁇) → ?? / U+2048 (⁈) → ?! / U+2049 (⁉) → !?
        var symbolUnified = unified
            .Replace("\u203C", "!!")
            .Replace("\u2047", "??")
            .Replace("\u2048", "?!")
            .Replace("\u2049", "!?");

        // 全角英数字・記号（U+FF01〜U+FF5E）を半角（U+0021〜U+007E）に変換
        var sb = new StringBuilder(symbolUnified.Length);
        foreach (var c in symbolUnified)
        {
            // 全角から半角への変換
            if (c >= '\uFF01' && c <= '\uFF5E')
            {
                var halfWidthChar = (char)(c - 0xFEE0);
                // 半角に変換された英字を大文字へ統一
                sb.Append(char.IsAsciiLetter(halfWidthChar) ? char.ToUpperInvariant(halfWidthChar) : halfWidthChar);
            }
            else
            {
                // 空白文字を除去、ASCII英字を大文字へ統一
                if (char.IsWhiteSpace(c))
                {
                    // 空白は除外（sb に追加しない）
                    continue;
                }
                else if (char.IsAsciiLetter(c))
                {
                    sb.Append(char.ToUpperInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>略称タイトルの最大文字数。</summary>
    private const int MaxShortTitleLength = 30;

    /// <summary>
    /// 30文字以内の略称タイトルを生成します。
    /// 戻り値はファイル名として安全にサニタイズ済みの ShortTitle です。
    /// サムネイルファイル名に直接使用可能です。
    /// </summary>
    /// <remarks>
    /// <para>短縮ルール（実行順）:</para>
    /// <list type="number">
    ///   <item>最大文字数（30文字）以下ならそのまま使用</item>
    ///   <item>separatorChars で分割し、先頭要素が30文字以下なら使用</item>
    ///   <item>全角/半角スペース（U+3000/U+0020）で分割し、先頭要素が30文字以下なら使用</item>
    ///   <item>先頭30文字で短縮</item>
    /// </list>
    /// <para>すべての戻り値は FileSystemCharSanitizer によってサニタイズ済みであり、
    /// Windows ファイルシステムの禁則文字を含みません。</para>
    /// </remarks>
    /// <param name="title">元のタイトル文字列。</param>
    /// <param name="separatorChars">DBから取得した区切り文字群。</param>
    /// <returns>30文字以内に収めた、ファイル名として安全なサニタイズ済み略称タイトル。</returns>
    public static string GetShortTitle(string title, string separatorChars)
    {
        // 短縮ルールに基づいて候補を決定
        string candidate;

        // タイトルが最大文字数以下ならそのまま使用
        if (title.Length <= MaxShortTitleLength)
        {
            candidate = title;
        }
        // ① separatorChars による Split（先頭要素）
        else if (!string.IsNullOrEmpty(separatorChars))
        {
            var part = title.Split(separatorChars.ToCharArray())[0].Trim();
            if (part.Length <= MaxShortTitleLength)
            {
                candidate = part;
            }
            // ② 全角/半角スペースによる Split（先頭要素）
            else
            {
                var spacePart = title.Split([' ', '\u3000'])[0].Trim();
                if (spacePart.Length <= MaxShortTitleLength)
                {
                    candidate = spacePart;
                }
                // ③ 先頭30文字での Substring
                else
                {
                    candidate = title[..MaxShortTitleLength].Trim();
                }
            }
        }
        // ② 全角/半角スペースによる Split（先頭要素）
        else
        {
            var part = title.Split([' ', '\u3000'])[0].Trim();
            if (part.Length <= MaxShortTitleLength)
            {
                candidate = part;
            }
            // ③ 先頭30文字での Substring
            else
            {
                candidate = title[..MaxShortTitleLength].Trim();
            }
        }

        // 最後に1か所でサニタイズを適用し、ファイル名として安全な値を返す
        return FileSystemCharSanitizer.Sanitize(candidate);
    }
}
