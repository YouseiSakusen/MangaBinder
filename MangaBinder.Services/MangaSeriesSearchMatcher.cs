using MangaBinder.Core.Series;
using MangaBinder.Helpers;

namespace MangaBinder;

/// <summary>
/// MangaSeries が検索文字列に一致するかを判定するクラスです。
/// 検索文字列は一度だけ分割・正規化され、複数の IsMatch() 呼び出しで再利用されます。
/// </summary>
public class MangaSeriesSearchMatcher
{
	/// <summary>正規化済みの検索ワード一覧。</summary>
	private readonly IReadOnlyList<string> normalizedWords;

	/// <summary>表示用の検索ワード一覧（正規化前）。</summary>
	private readonly IReadOnlyList<string> displayWords;

	/// <summary>検索条件が有効であるかを示します。</summary>
	private readonly bool isValid;

	/// <summary>
	/// <see cref="MangaSeriesSearchMatcher"/> の新しいインスタンスを初期化します。
	/// 検索文字列から検索ワードを抽出し、正規化します。
	/// </summary>
	/// <param name="searchText">検索文字列。null、空文字、または空白のみの場合は無効な検索条件となります。</param>
	public MangaSeriesSearchMatcher(string? searchText)
	{
		// searchText が null、空文字、または空白のみの場合は無効
		if (string.IsNullOrWhiteSpace(searchText))
		{
			this.isValid = false;
			this.normalizedWords = new List<string>();
			this.displayWords = new List<string>();
			return;
		}

		// ワード分割（半角スペース・全角スペース）
		var rawWords = searchText
			.Split(new[] { ' ', '\u3000' }, StringSplitOptions.RemoveEmptyEntries)
			.ToList();

		var words = rawWords
			.Select(word => MangaTitleHelper.NormalizeTitleInternal(word))
			.Where(word => !string.IsNullOrEmpty(word))
			.ToList();

		// ワードが空の場合は無効
		if (words.Count == 0)
		{
			this.isValid = false;
			this.normalizedWords = new List<string>();
			this.displayWords = new List<string>();
		}
		else
		{
			this.isValid = true;
			this.normalizedWords = words.AsReadOnly();
			this.displayWords = rawWords.AsReadOnly();
		}
	}

	/// <summary>
	/// 検索条件が有効であるかを取得します。
	/// </summary>
	public bool IsValid => this.isValid;

	/// <summary>
	/// 正規化済みの検索ワード一覧を取得します。
	/// 検索条件が無効の場合は空のリストを返します。
	/// </summary>
	public IReadOnlyList<string> GetSearchWords() => this.normalizedWords;

	/// <summary>
	/// 表示用の検索ワード一覧（正規化前）を取得します。
	/// ユーザーが入力した表記のまま返します。
	/// 検索条件が無効の場合は空のリストを返します。
	/// </summary>
	public IReadOnlyList<string> GetDisplayWords() => this.displayWords;

	/// <summary>
	/// 指定された MangaSeries が検索条件に一致するかを判定します。
	/// 複数ワードは AND 条件で、各ワードについて NormalizedTitleInternal、Author、Memo のいずれかに含まれていれば一致とします。
	/// </summary>
	/// <param name="series">判定対象の MangaSeries。</param>
	/// <returns>検索条件に一致する場合は true、一致しない場合は false。</returns>
	public bool IsMatch(MangaSeries series)
	{
		// 検索条件が無効な場合は常に false
		if (!this.isValid)
			return false;

		ArgumentNullException.ThrowIfNull(series);

		// AND 検索：すべてのワードが Title OR Author OR Memo に含まれた作品のみ一致
		return this.normalizedWords.All(word =>
			series.NormalizedTitleInternal.Contains(word, StringComparison.OrdinalIgnoreCase) ||
			series.Author.Contains(word, StringComparison.OrdinalIgnoreCase) ||
			series.Memo.Contains(word, StringComparison.OrdinalIgnoreCase)
		);
	}
}
