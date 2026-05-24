using MangaBinder.Settings;
using NetVips;

namespace MangaBinder.Bindings;

/// <summary>
/// NetVips を使用したサムネイル生成実装です。
/// </summary>
public sealed class ThumbnailImageProcessor : IThumbnailImageProcessor
{
    /// <inheritdoc />
    public ValueTask<Stream> ProcessThumbnailAsync(
        Stream input,
        ThumbnailOptions options,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Stream から直接 NetVips にロード
        using var source = Image.NewFromStream(input);

        int targetW = options.Width;
        int targetH = options.Height;

        // アスペクト比を維持して fit (contain) スケール計算
        double scaleX = (double)targetW / source.Width;
        double scaleY = (double)targetH / source.Height;
        double scale  = Math.Min(scaleX, scaleY);

        int scaledW = (int)Math.Round(source.Width  * scale);
        int scaledH = (int)Math.Round(source.Height * scale);

        // リサイズ
        using var resizedRaw = source.Resize(scale, kernel: Enums.Kernel.Lanczos3);

        // アルファチャンネルを持つ場合は Flatten して RGB に統一（band mismatch 防止）
        using var resized = resizedRaw.HasAlpha() ? resizedRaw.Flatten() : resizedRaw;

        // 背景色のキャンバスを作成（RGB）
        var bg = ParseColor(options.BackgroundColor);
        using var canvas = Image.Black(targetW, targetH).NewFromImage(bg).Copy(interpretation: Enums.Interpretation.Srgb);

        // 中央配置オフセット
        int offsetX = (targetW - scaledW) / 2;
        int offsetY = (targetH - scaledH) / 2;

        // 合成
        using var composite = canvas.Insert(resized, offsetX, offsetY);

        // JPEG 出力
        var jpegBytes = composite.WriteToBuffer(".jpg", new VOption
        {
            { "Q", options.JpegQuality }
        });

        Stream result = new MemoryStream(jpegBytes);
        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// HTML カラーコード（例: "#FFFFFF" / "FFFFFF"）を NetVips 用の double[] に変換します。
    /// </summary>
    private static double[] ParseColor(string color)
    {
        var hex = color.TrimStart('#');
        if (hex.Length == 6)
        {
            return
            [
                Convert.ToInt32(hex[..2], 16),
                Convert.ToInt32(hex[2..4], 16),
                Convert.ToInt32(hex[4..6], 16)
            ];
        }
        // パース失敗時は白
        return [255.0, 255.0, 255.0];
    }
}
