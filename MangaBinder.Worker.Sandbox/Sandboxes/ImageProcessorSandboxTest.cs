using System.Text;
using MangaBinder.Settings;
using NetVips;

namespace MangaBinder.Jobs.Sandboxes;

/// <summary>
/// <see cref="ThumbnailImageProcessor"/> を使用した画像検査用 Sandbox テストクラスです。
/// band mismatch 系エラーを発生させる画像ファイルを特定するために使用します。
/// </summary>
public class ImageProcessorSandboxTest
{
	/// <summary>サムネイル生成に使用する <see cref="ThumbnailImageProcessor"/> インスタンスです。</summary>
	private readonly ThumbnailImageProcessor imageProcessor = new();

	/// <summary>
	/// 画像スキャン処理の1件分の結果を表すクラスです。
	/// </summary>
	private sealed class ScanResultRow
	{
		public string FilePath { get; set; } = string.Empty;
		public string FileName { get; set; } = string.Empty;
		public string Extension { get; set; } = string.Empty;
		public bool Success { get; set; }
		public int? Width { get; set; }
		public int? Height { get; set; }
		public int? Bands { get; set; }
		public string? Interpretation { get; set; }
		public bool? HasAlpha { get; set; }
		public string? ErrorType { get; set; }
		public string? ErrorMessage { get; set; }
	}

	/// <summary>対象とする画像ファイル拡張子のセットです。</summary>
	private readonly HashSet<string> targetExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".jpg", ".jpeg", ".png", ".webp", ".avif"
	};

	/// <summary>
	/// 指定フォルダ配下の画像ファイルを全件走査し、<see cref="ThumbnailImageProcessor"/> でサムネイル生成を試みます。
	/// 処理結果を CSV に出力し、エラー発生ファイルを特定します。
	/// </summary>
	[Fact]
	public async Task ExecuteAsync_画像ファイルを走査してImageProcessor処理結果をCSVに出力する()
	{
		var rootPath = @"D:\GitBares\MangaBinder\MangaBinder.Worker.Sandbox\bin\Debug\net10.0-windows\ImageProcessorTest\テラフォーマーズ\images";

		var thumbnailOptions = new ThumbnailOptions
		{
			Width = 300,
			Height = 400,
			JpegQuality = 85,
			BackgroundColor = "#FFFFFF"
		};

		var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		var baseOutputDir = Path.Combine(
			AppContext.BaseDirectory,
			"ScanResults");
		var csvPath = Path.Combine(baseOutputDir, $"ImageProcessor_Sandbox_Result_{timestamp}.csv");
		var thumbnailOutputDir = Path.Combine(baseOutputDir, "ImageProcessorSandboxOutput");

		Directory.CreateDirectory(baseOutputDir);
		Directory.CreateDirectory(thumbnailOutputDir);

		var imageFiles = Directory
			.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
			.Where(f => this.targetExtensions.Contains(Path.GetExtension(f)))
			.OrderBy(f => f)
			.ToList();

		var rows = new List<ScanResultRow>();

		foreach (var filePath in imageFiles)
		{
			var row = this.ProcessFile(filePath, thumbnailOptions, thumbnailOutputDir);
			rows.Add(row);
		}

		await this.WriteCsvAsync(csvPath, rows, CancellationToken.None);

		Console.WriteLine($"走査ファイル数: {imageFiles.Count}");
		Console.WriteLine($"成功: {rows.Count(r => r.Success)}");
		Console.WriteLine($"失敗: {rows.Count(r => !r.Success)}");
		Console.WriteLine($"CSV出力先: {csvPath}");
	}

	/// <summary>
	/// 1ファイルに対して画像情報取得および <see cref="ThumbnailImageProcessor"/> によるサムネイル生成を実行します。
	/// </summary>
	/// <param name="filePath">処理対象のファイルパスです。</param>
	/// <param name="options">サムネイル生成オプションです。</param>
	/// <param name="thumbnailOutputDir">サムネイル出力先ディレクトリです。</param>
	/// <returns>処理結果を表す <see cref="ScanResultRow"/> です。</returns>
	private ScanResultRow ProcessFile(string filePath, ThumbnailOptions options, string thumbnailOutputDir)
	{
		var row = new ScanResultRow
		{
			FilePath = filePath,
			FileName = Path.GetFileName(filePath),
			Extension = Path.GetExtension(filePath)
		};

		try
		{
			using var vipsImage = NetVips.Image.NewFromFile(filePath);
			row.Width = vipsImage.Width;
			row.Height = vipsImage.Height;
			row.Bands = vipsImage.Bands;
			row.Interpretation = vipsImage.Interpretation.ToString();
			row.HasAlpha = vipsImage.HasAlpha();
		}
		catch (Exception ex)
		{
			row.Success = false;
			row.ErrorType = ex.GetType().Name;
			row.ErrorMessage = ex.Message;
			return row;
		}

		try
		{
			using var input = File.OpenRead(filePath);
			var resultStream = this.imageProcessor.ProcessThumbnailAsync(input, options, CancellationToken.None).GetAwaiter().GetResult();

			var outputFileName = Path.GetFileNameWithoutExtension(filePath) + ".jpg";
			var outputPath = Path.Combine(thumbnailOutputDir, outputFileName);

			using (var fs = File.Create(outputPath))
			{
				resultStream.CopyTo(fs);
			}

			row.Success = true;
		}
		catch (Exception ex)
		{
			row.Success = false;
			row.ErrorType = ex.GetType().Name;
			row.ErrorMessage = ex.Message;
		}

		return row;
	}

	/// <summary>
	/// スキャン結果を CSV ファイルに書き出します。
	/// </summary>
	/// <param name="csvPath">出力先の CSV ファイルパスです。</param>
	/// <param name="rows">出力対象の結果行リストです。</param>
	/// <param name="ct">キャンセルトークンです。</param>
	private async ValueTask WriteCsvAsync(string csvPath, List<ScanResultRow> rows, CancellationToken ct)
	{
		var sb = new StringBuilder();
		sb.AppendLine("FilePath,FileName,Extension,Success,Width,Height,Bands,Interpretation,HasAlpha,ErrorType,ErrorMessage");

		foreach (var row in rows)
		{
			sb.AppendLine(string.Join(",",
				Escape(row.FilePath),
				Escape(row.FileName),
				Escape(row.Extension),
				row.Success,
				row.Width?.ToString() ?? string.Empty,
				row.Height?.ToString() ?? string.Empty,
				row.Bands?.ToString() ?? string.Empty,
				Escape(row.Interpretation ?? string.Empty),
				row.HasAlpha?.ToString() ?? string.Empty,
				Escape(row.ErrorType ?? string.Empty),
				Escape(row.ErrorMessage ?? string.Empty)));
		}

		await File.WriteAllTextAsync(csvPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct);
	}

	/// <summary>
	/// CSV 用にフィールド値をエスケープします。カンマ・改行・ダブルクォートを含む場合はダブルクォートで囲みます。
	/// </summary>
	/// <param name="value">エスケープ対象の文字列です。</param>
	/// <returns>エスケープ済みの文字列です。</returns>
	private static string Escape(string value)
	{
		if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
			return $"\"{value.Replace("\"", "\"\"")}\"";
		return value;
	}
}


