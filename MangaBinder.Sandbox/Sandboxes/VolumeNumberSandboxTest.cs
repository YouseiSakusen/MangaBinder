using System.Text;
using MangaBinder.Binding.Inspection;

namespace MangaBinder.Sandboxes;

/// <summary>
/// フォルダ名からの巻番号抽出パターンを実データで観測するための Sandbox テストクラスです。
/// 結果は UTF-8 BOM 付き CSV として出力されます。
/// </summary>
public class VolumeNumberSandboxTest
{
	/// <summary>
	/// 観測対象の素材ルートフォルダパスの一覧です。
	/// 実際のパスに合わせて変更してください。
	/// </summary>
	private static readonly IReadOnlyList<string> SourceRoots =
	[
		// 例: @"D:\Manga\Sources",
		// 複数指定可能です。
		@"D:\My Comic\!src",
	];

	/// <summary>CSV の出力先フォルダパスです。空の場合はアプリケーションの実行フォルダを使用します。</summary>
	private const string OutputFolder = @"D:\GitBares\MangaBinder\MangaBinder.Sandbox\bin\Debug\net10.0-windows\SandBoxOutputs";

	/// <summary>CSV の出力先パスです。</summary>
	private static readonly string OutputCsvPath =
		Path.Combine(
			string.IsNullOrEmpty(OutputFolder) ? AppContext.BaseDirectory : OutputFolder,
			$"VolumeNumberExtractResults_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

	/// <summary>
	/// 素材ルート配下の巻候補フォルダを走査し、巻番号抽出結果を CSV に出力します。
	/// </summary>
	[Fact]
	public void ExtractAndExportCsv()
	{
		var extractor = new VolumeNumberExtractor();
		var rows = new List<CsvRow>();

		foreach (var sourceRoot in SourceRoots)
		{
			if (!Directory.Exists(sourceRoot))
				continue;

			// 素材ルート直下の作品フォルダを列挙
			foreach (var seriesDir in Directory.GetDirectories(sourceRoot).OrderBy(d => d))
			{
				var parentFolderName = Path.GetFileName(seriesDir);

				// 作品フォルダ直下の巻候補フォルダを列挙
				foreach (var volumeDir in Directory.GetDirectories(seriesDir).OrderBy(d => d))
				{
					var folderName = Path.GetFileName(volumeDir);
					var result = extractor.Extract(folderName);

					rows.Add(new CsvRow
					{
						SourceRoot = sourceRoot,
						ParentFolderName = parentFolderName,
						FolderName = folderName,
						Success = result.Success,
						VolumeNumber = result.VolumeNumber,
						PatternName = result.PatternName,
						Message = result.Message,
					});
				}
			}
		}

		WriteCsv(OutputCsvPath, rows);

		// 出力先を出力して確認しやすくする
		Assert.True(File.Exists(OutputCsvPath), $"CSV が出力されていません: {OutputCsvPath}");
	}

	/// <summary>
	/// 行リストを UTF-8 BOM 付き CSV として書き出します。
	/// </summary>
	private static void WriteCsv(string path, IReadOnlyList<CsvRow> rows)
	{
		using var writer = new StreamWriter(path, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

		// ヘッダー
		writer.WriteLine("SourceRoot,ParentFolderName,FolderName,Success,VolumeNumber,PatternName,Message");

		foreach (var row in rows)
		{
			writer.WriteLine(string.Join(",",
				Escape(row.SourceRoot),
				Escape(row.ParentFolderName),
				Escape(row.FolderName),
				Escape(row.Success.ToString()),
				Escape(row.VolumeNumber?.ToString("G29") ?? string.Empty),
				Escape(row.PatternName),
				Escape(row.Message)));
		}
	}

	/// <summary>
	/// CSV フィールド値を RFC 4180 に従ってエスケープします。
	/// </summary>
	private static string Escape(string value)
	{
		if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
			return $"\"{value.Replace("\"", "\"\"")}\"";

		return value;
	}

	/// <summary>CSV 出力用の行データです。</summary>
	private sealed class CsvRow
	{
		public string SourceRoot { get; set; } = string.Empty;
		public string ParentFolderName { get; set; } = string.Empty;
		public string FolderName { get; set; } = string.Empty;
		public bool Success { get; set; }
		public decimal? VolumeNumber { get; set; }
		public string PatternName { get; set; } = string.Empty;
		public string Message { get; set; } = string.Empty;
	}
}
