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
}
