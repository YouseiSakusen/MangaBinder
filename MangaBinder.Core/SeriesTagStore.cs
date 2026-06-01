namespace MangaBinder;

/// <summary>
/// タグ変更された <see cref="MangaSeries"/> を保持する Singleton ストアです。
/// </summary>
public sealed class SeriesTagStore
{
	private readonly Dictionary<long, MangaSeries> dirtyMap = new();

	/// <summary>
	/// 指定した作品をタグ変更済みとしてマークします。
	/// 同一 SeriesId は重複登録しません。
	/// </summary>
	/// <param name="series">変更された作品。</param>
	public void MarkDirty(MangaSeries series)
		=> this.dirtyMap[series.SeriesId] = series;

	/// <summary>保存対象の <see cref="MangaSeries"/> 一覧を返します。</summary>
	public IReadOnlyCollection<MangaSeries> GetDirtyItems()
		=> this.dirtyMap.Values;

	/// <summary>変更がある場合 true を返します。</summary>
	public bool HasChanges => this.dirtyMap.Count > 0;

	/// <summary>ストアをクリアします。</summary>
	public void Clear()
		=> this.dirtyMap.Clear();
}
