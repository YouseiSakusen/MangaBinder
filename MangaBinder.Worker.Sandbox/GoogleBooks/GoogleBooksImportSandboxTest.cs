using MangaBinder.Jobs.Contexts;
using MangaBinder.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace MangaBinder.Jobs.GoogleBooks;

/// <summary>
/// GoogleBooksImport の実データ確認用 Sandbox テストクラスです。
/// 実 DB の MangaSeries を対象に Google Books API を呼び出し、
/// GoogleBooksVolumeFilter の採用結果を CSV に出力します。
/// DB UPDATE は行いません。
/// </summary>
public class GoogleBooksImportSandboxTest
{
	/// <summary>DB ファイルの絶対パス。環境に合わせて変更してください。</summary>
	private const string DbPath = @"D:\GitBares\MangaBinder\MangaBinder\bin\Debug\net10.0-windows\db\manga.db";

	/// <summary>Sandbox 用設定ファイルの絶対パス。</summary>
	private static readonly string SandboxSettingsFilePath =
		Path.Combine(AppContext.BaseDirectory, "google-books-settings.sandbox.json");

	/// <summary>1回の Sandbox 実行で取得する開始 SeriesId（この値以上を対象にする）。</summary>
	private const long StartSeriesId = 350;

	/// <summary>1回の Sandbox 実行で処理する最大件数。</summary>
	private const int TargetCount = 100;

	/// <summary>CSV 出力先フォルダのパスです。</summary>
	private const string OutputFolder =
		@"D:\GitBares\MangaBinder\MangaBinder.Worker.Sandbox\bin\Debug\net10.0-windows\ScanResults";

	/// <summary>生 JSON 保存先フォルダのパスです。</summary>
	private const string RawResponseFolder =
		@"D:\GitBares\MangaBinder\MangaBinder.Worker.Sandbox\bin\Debug\net10.0-windows\ScanResults\RawResponses";

	/// <summary>
	/// GoogleBooks API を呼び出し、採用結果を CSV に出力します。
	/// </summary>
	[Fact]
	public async Task ExecuteAsync_GoogleBooksImport結果をCSVに出力する()
	{
		// ─ WorkerContext 構築 ─
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:MangaDb"] = DbPath,
			})
			.Build();

		var builder = new WorkerContextBuilder(configuration);
		var context = await builder.BuildAsync();

		// ─ GoogleBooks 部品を直接組み立て ─
		var sharedRepo     = new SharedSettingsRepository(context.ConnectionString);
		var excludeWords   = await sharedRepo.GetGoogleBooksExcludeWordsAsync();
		var settingsRepo   = new GoogleBooksSettingsRepository(SandboxSettingsFilePath);
		var settings       = await settingsRepo.LoadAsync(excludeWords);
		var quotaManager   = new GoogleBooksQuotaManager(settings);
		quotaManager.ResetIfNeeded();

		var httpClient     = new HttpClient();
		var agent          = new GoogleBooksAgent(httpClient, NullLogger<GoogleBooksAgent>.Instance);
		agent.ApplySettings(settings);
		var filter         = new GoogleBooksVolumeFilter(NullLogger<GoogleBooksVolumeFilter>.Instance);
		filter.ApplySettings(settings);
		var repository     = new GoogleBooksImportRepository(context);

		// ─ 対象取得（StartSeriesId 以上、TargetCount 件） ─
		var targets = (await repository.GetImportTargetsAsync(CancellationToken.None))
			.Where(s => s.SeriesId >= StartSeriesId)
			.Take(TargetCount)
			.ToList();

		// ─ 各作品を処理 ─
		var rows = new List<CsvRow>();
		var executedAt = DateTime.Now;

		foreach (var series in targets)
		{
			if (!quotaManager.CanCall)
				break;

			var allCandidates = new List<NormalizedVolumeInfo>();
			var startIndex    = 0;
			var pagesFetched  = 0;
			var totalItems    = 0;
			GoogleBooksCandidateSelectionResult? selectionResult = null;
			var safeTitle = string.Concat(series.Title.Split(Path.GetInvalidFileNameChars()));

			while (quotaManager.CanCall)
			{
				var result = await agent.SearchWithRawAsync(series.Title, startIndex, CancellationToken.None);
				quotaManager.Increment();
				pagesFetched++;

				// ─ 生 JSON を startIndex 付きファイル名で保存 ─
				if (result.RawJson is not null)
				{
					Directory.CreateDirectory(RawResponseFolder);
					var rawFileName = $"{series.SeriesId:D6}_{safeTitle}_start{startIndex:D3}.json";
					await File.WriteAllTextAsync(
						Path.Combine(RawResponseFolder, rawFileName),
						result.RawJson,
						new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				}

				var response = result.Response;

				if (response is null)
					break;

				totalItems = response.TotalItems;
				var items = response.Items;

				if (items is null || items.Count == 0)
					break;

				var pageCandidates = filter.NormalizeCandidates(response);
				allCandidates.AddRange(pageCandidates);

				selectionResult = filter.SelectFirstVolumeDescriptionCandidate(allCandidates, series);

				if (selectionResult.Candidate is not null)
					break;

				startIndex += items.Count;

				if (totalItems > 0 && startIndex >= totalItems)
					break;
			}

			selectionResult ??= filter.SelectFirstVolumeDescriptionCandidate(allCandidates, series);

			var candidate      = selectionResult.Candidate;
			var reason         = selectionResult.Reason;
			var reasonSummary  = selectionResult.ReasonSummary;
			var candidateCount = selectionResult.CandidateCount;
			var acceptedCount  = selectionResult.AcceptedCount;
			var queryNorm      = series.Title;

			if (candidate is not null)
			{
				var eval = candidate.Evaluate(filter, filter.NormalizeTitle(queryNorm));
				rows.Add(new CsvRow
				{
					SeriesId          = series.SeriesId,
					Title             = series.Title,
					SearchQuery       = series.Title,
					TotalItems        = totalItems,
					ItemsCount        = allCandidates.Count,
					PagesFetched      = pagesFetched,
					CandidateCount    = candidateCount,
					AcceptedCount     = acceptedCount,
					SelectedTitle     = candidate.Title,
					OrderNumber       = candidate.OrderNumber?.ToString() ?? string.Empty,
					Publisher         = candidate.Publisher,
					DescriptionLength = candidate.Description.Length,
					Status            = "Success",
					Reason            = reason,
					ReasonSummary     = reasonSummary,
					Categories        = string.Join(" | ", candidate.Categories),
					RawCategories     = this.collectRawCategories(allCandidates),
					NormalizedTitle   = filter.NormalizeTitle(series.Title),
					FuzzyScore        = eval.FuzzyScore,
					StrictMatch       = eval.StrictMatch,
					PartialMatch      = eval.PartialMatch,
					DifferentSubtitle = eval.DifferentSubtitle,
					IsExcluded        = eval.IsExcluded,
				});
			}
			else
			{
				rows.Add(new CsvRow
				{
					SeriesId          = series.SeriesId,
					Title             = series.Title,
					SearchQuery       = series.Title,
					TotalItems        = totalItems,
					ItemsCount        = allCandidates.Count,
					PagesFetched      = pagesFetched,
					CandidateCount    = candidateCount,
					AcceptedCount     = acceptedCount,
					SelectedTitle     = string.Empty,
					OrderNumber       = string.Empty,
					Publisher         = string.Empty,
					DescriptionLength = 0,
					Status            = "NotFound",
					Reason            = reason,
					ReasonSummary     = reasonSummary,
					Categories        = string.Empty,
					RawCategories     = this.collectRawCategories(allCandidates),
					NormalizedTitle   = filter.NormalizeTitle(series.Title),
					FuzzyScore        = 0.0,
					StrictMatch       = false,
					PartialMatch      = false,
					DifferentSubtitle = false,
					IsExcluded        = false,
				});
			}
		}

		// ─ CSV 出力 ─
		Directory.CreateDirectory(OutputFolder);
		var csvPath = Path.Combine(OutputFolder, $"google-books-import-check_{executedAt:yyyyMMdd_HHmmss}.csv");
		await this.writeCsvAsync(csvPath, rows, executedAt);

		// ─ quota 保存 ─
		await settingsRepo.SaveAsync(settings, CancellationToken.None);

		Console.WriteLine($"CSV出力先: {csvPath}");
		Console.WriteLine($"処理件数: {rows.Count}");
		Console.WriteLine($"API呼び出し回数: {quotaManager.CallCount}");
	}

	/// <summary>
	/// 候補一覧から全件の categories を | 区切りで返します。
	/// </summary>
	private string collectRawCategories(IReadOnlyList<NormalizedVolumeInfo> candidates)
	{
		if (candidates.Count == 0)
			return string.Empty;

		var all = candidates
			.SelectMany(c => c.Categories)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		return string.Join(" | ", all);
	}

	/// <summary>
	/// CSV ファイルに結果を書き出します。
	/// </summary>
	/// <param name="csvPath">出力先パス。</param>
	/// <param name="rows">出力データ行一覧。</param>
	/// <param name="executedAt">実行日時。</param>
	private async ValueTask writeCsvAsync(
		string csvPath,
		IReadOnlyList<CsvRow> rows,
		DateTime executedAt)
	{
		var sb = new StringBuilder();

		// ヘッダ
		sb.AppendLine(
			"SeriesId,Title,SearchQuery,TotalItems,ItemsCount,PagesFetched,CandidateCount,AcceptedCount," +
			"SelectedTitle,OrderNumber,Publisher,DescriptionLength," +
			"Status,Reason,ReasonSummary,Categories,RawCategories,NormalizedTitle,FuzzyScore,StrictMatch,PartialMatch,DifferentSubtitle,IsExcluded");

		// データ行
		foreach (var r in rows)
		{
			sb.AppendLine(string.Join(",",
				r.SeriesId,
				Escape(r.Title),
				Escape(r.SearchQuery),
				r.TotalItems,
				r.ItemsCount,
				r.PagesFetched,
				r.CandidateCount,
				r.AcceptedCount,
				Escape(r.SelectedTitle),
				r.OrderNumber,
				Escape(r.Publisher),
				r.DescriptionLength,
				r.Status,
				r.Reason,
				Escape(r.ReasonSummary),
				Escape(r.Categories),
				Escape(r.RawCategories),
				Escape(r.NormalizedTitle),
				r.FuzzyScore.ToString("F4"),
				r.StrictMatch,
				r.PartialMatch,
				r.DifferentSubtitle,
				r.IsExcluded));
		}

		await File.WriteAllTextAsync(csvPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
	}

	/// <summary>
	/// CSV セル値をエスケープします。カンマ・ダブルクォート・改行を含む場合はダブルクォートで囲みます。
	/// </summary>
	/// <param name="value">エスケープ対象の文字列。</param>
	/// <returns>エスケープ済み文字列。</returns>
	private static string Escape(string value)
	{
		if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
			return $"\"{value.Replace("\"", "\"\"")}\"";

		return value;
	}

	/// <summary>
	/// CSV 出力用の1行データを表すクラスです。
	/// </summary>
	private sealed class CsvRow
	{
		/// <summary>シリーズ ID を取得または設定します。</summary>
		public long SeriesId { get; set; }

		/// <summary>作品タイトルを取得または設定します。</summary>
		public string Title { get; set; } = string.Empty;

		/// <summary>検索クエリを取得または設定します。</summary>
		public string SearchQuery { get; set; } = string.Empty;

		/// <summary>API 総ヒット件数を取得または設定します。</summary>
		public int TotalItems { get; set; }

		/// <summary>取得アイテム件数（全ページ蓄積後）を取得または設定します。</summary>
		public int ItemsCount { get; set; }

		/// <summary>取得したページ数を取得または設定します。</summary>
		public int PagesFetched { get; set; }

		/// <summary>評価した候補の総数を取得または設定します。</summary>
		public int CandidateCount { get; set; }

		/// <summary>採用条件を満たした候補の件数を取得または設定します。</summary>
		public int AcceptedCount { get; set; }

		/// <summary>採用候補のタイトルを取得または設定します。</summary>
		public string SelectedTitle { get; set; } = string.Empty;

		/// <summary>採用候補の巻番号を取得または設定します。</summary>
		public string OrderNumber { get; set; } = string.Empty;

		/// <summary>採用候補の出版社を取得または設定します。</summary>
		public string Publisher { get; set; } = string.Empty;

		/// <summary>採用候補のあらすじ文字数を取得または設定します。</summary>
		public int DescriptionLength { get; set; }

		/// <summary>採用状態（Success / NotFound）を取得または設定します。</summary>
		public string Status { get; set; } = string.Empty;

		/// <summary>採用理由または不採用理由を取得または設定します。</summary>
		public string Reason { get; set; } = string.Empty;

		/// <summary>全候補の Reason 集計サマリを取得または設定します。</summary>
		public string ReasonSummary { get; set; } = string.Empty;

		/// <summary>カテゴリ一覧を取得または設定します。</summary>
		public string Categories { get; set; } = string.Empty;

		/// <summary>生カテゴリ一覧（候補全件の categories を | 区切り）を取得または設定します。</summary>
		public string RawCategories { get; set; } = string.Empty;

		/// <summary>normalizeForMatch() 後のタイトルを取得または設定します。</summary>
		public string NormalizedTitle { get; set; } = string.Empty;

		/// <summary>バイグラム類似度スコアを取得または設定します。</summary>
		public double FuzzyScore { get; set; }

		/// <summary>厳密タイトル一致フラグを取得または設定します。</summary>
		public bool StrictMatch { get; set; }

		/// <summary>部分タイトル一致フラグを取得または設定します。</summary>
		public bool PartialMatch { get; set; }

		/// <summary>異なるサブタイトルフラグを取得または設定します。</summary>
		public bool DifferentSubtitle { get; set; }

		/// <summary>除外タイトルフラグを取得または設定します。</summary>
		public bool IsExcluded { get; set; }
	}
}
