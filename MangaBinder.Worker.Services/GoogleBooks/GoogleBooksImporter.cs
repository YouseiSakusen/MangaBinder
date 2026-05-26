using MangaBinder.Settings;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace MangaBinder.Jobs.GoogleBooks;

/// <summary>
/// Google Books インポート処理本体のクラスです。
/// API 呼び出し・フィルタ・書誌情報反映を集約します。
/// </summary>
public class GoogleBooksImporter
{
	/// <summary>クォータ管理。</summary>
	private GoogleBooksQuotaManager quotaManager = null!;

	/// <summary>リポジトリ。</summary>
	private readonly IGoogleBooksImportRepository repository;

	/// <summary>Google Books API エージェント。</summary>
	private readonly GoogleBooksAgent agent;

	/// <summary>候補フィルタ。</summary>
	private readonly GoogleBooksVolumeFilter filter;

	/// <summary>ロガー。</summary>
	private readonly ILogger<GoogleBooksImporter> logger;

	/// <summary>Google Books API 設定。</summary>
	private GoogleBooksSettings settings = null!;

	/// <summary>
	/// <see cref="GoogleBooksImporter"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="repository">リポジトリ。</param>
	/// <param name="agent">Google Books API エージェント。</param>
	/// <param name="filter">候補フィルタ。</param>
	/// <param name="logger">ロガー。</param>
	public GoogleBooksImporter(
		IGoogleBooksImportRepository repository,
		GoogleBooksAgent agent,
		GoogleBooksVolumeFilter filter,
		ILogger<GoogleBooksImporter> logger)
	{
		this.repository = repository;
		this.agent = agent;
		this.filter = filter;
		this.logger = logger;
	}

	/// <summary>
	/// Job 実行開始時に設定とクォータ管理インスタンスを適用します。
	/// </summary>
	/// <param name="googleBooksSettings">JSON から読み込んだ設定。</param>
	/// <param name="manager">当該 Job インスタンス専用のクォータ管理。</param>
	public void ApplySettings(GoogleBooksSettings googleBooksSettings, GoogleBooksQuotaManager manager)
	{
		this.settings = googleBooksSettings;
		this.quotaManager = manager;
		this.agent.ApplySettings(googleBooksSettings);
		this.filter.ApplySettings(googleBooksSettings);
	}

	/// <summary>
	/// 指定された作品に対して Google Books インポートを非同期で実行します。
	/// </summary>
	/// <param name="series">インポート対象の作品。</param>
	/// <param name="ct">キャンセルトークン。</param>
	public async ValueTask ImportAsync(MangaSeries series, CancellationToken ct)
	{
		if (!this.quotaManager.CanCall)
		{
			this.logger.ZLogInformation($"クォータ上限に達しました。スキップします: SeriesId={series.SeriesId} Title={series.Title}");
			return;
		}

		var allCandidates = new List<NormalizedVolumeInfo>();
		var startIndex    = 0;
		var pagesFetched  = 0;
		var totalItems    = 0;
		GoogleBooksCandidateSelectionResult? selectionResult = null;

		try
		{
			while (this.quotaManager.CanCall)
			{
				var response = await this.agent.SearchAsync(series.Title, startIndex, ct);
				this.quotaManager.Increment();
				pagesFetched++;

				if (response is null)
					break;

				totalItems = response.TotalItems;
				var items = response.Items;

				if (items is null || items.Count == 0)
					break;

				var pageCandidates = this.filter.NormalizeCandidates(response);
				allCandidates.AddRange(pageCandidates);

				selectionResult = this.filter.SelectFirstVolumeDescriptionCandidate(allCandidates, series);

				if (selectionResult.Candidate is not null)
					break;

				startIndex += items.Count;

				if (totalItems > 0 && startIndex >= totalItems)
					break;
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			this.logger.ZLogError(ex, $"Google Books API 呼び出し中にエラーが発生しました: SeriesId={series.SeriesId} Title={series.Title}");
			await this.repository.UpdateImportFailedAsync(series.SeriesId, ex.Message, ct);
			throw;
		}

		selectionResult ??= this.filter.SelectFirstVolumeDescriptionCandidate(allCandidates, series);

		var reason         = selectionResult.Reason;
		var reasonSummary  = selectionResult.ReasonSummary;
		var candidateCount = selectionResult.CandidateCount;
		var acceptedCount  = selectionResult.AcceptedCount;
		var candidate      = selectionResult.Candidate;

		this.logger.ZLogInformation(
			$"Google Books 探索完了: SeriesId={series.SeriesId} Title={series.Title} " +
			$"PagesFetched={pagesFetched} TotalItems={totalItems} CandidateCount={candidateCount} AcceptedCount={acceptedCount} " +
			$"SelectedTitle={candidate?.Title ?? string.Empty} Reason={reason} ReasonSummary={reasonSummary} " +
			$"CurrentCallCount={this.quotaManager.CallCount}");

		if (candidate is not null)
		{
			var author = string.Join(", ", candidate.Authors);
			await this.repository.UpdateImportSuccessAsync(
				series.SeriesId,
				candidate.Description,
				candidate.Publisher,
				author,
				candidate.Title,
				$"Accepted; Summary={reasonSummary}",
				ct);
		}
		else
		{
			var notFoundMessage = string.IsNullOrEmpty(reasonSummary)
				? $"Reason={reason}"
				: $"Reason={reason}; Summary={reasonSummary}";
			await this.repository.UpdateImportNotFoundAsync(series.SeriesId, notFoundMessage, ct);
		}
	}
}
