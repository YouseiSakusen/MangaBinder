namespace MangaBinder.Bindings;

/// <summary>
/// StartPage で使用する製本開始キュー付き作品エンティティです。
/// </summary>
public sealed class BindingSeries
{
	/// <summary>漫画作品情報。</summary>
	public MangaSeries Series { get; init; } = default!;

	/// <summary>製本開始キューの進行状態。</summary>
	public BindingStartStatus Status { get; init; }

	/// <summary>現在の製本工程番号。</summary>
	public int CurrentStep { get; init; }

	/// <summary>キューに追加した日時。</summary>
	public DateTime AddedAt { get; init; }

	/// <summary>最終更新日時。</summary>
	public DateTime UpdatedAt { get; init; }

	/// <summary>あらすじが存在するかどうかを示します。</summary>
	public bool HasSynopsis
		=> !string.IsNullOrWhiteSpace(this.Series.Description);

	/// <summary>
	/// StartPage 用のタグ表示テキストを取得します。
	/// タグが未設定の場合は「🏷 タグ無し」を表示し、設定済みの場合は Series.TagDisplayText を返します。
	/// </summary>
	public string TagDisplayText
		=> this.Series.Tags.Count == 0 ? "🏷 タグ無し" : this.Series.TagDisplayText;
}
