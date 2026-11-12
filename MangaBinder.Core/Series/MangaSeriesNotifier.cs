using MangaBinder.Tags;
using ObservableCollections;
using R3;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace MangaBinder.Series;

/// <summary>
/// MangaSeries の Reactive な共有 ViewModel です。
/// アプリケーション全体で MangaSeries を参照する際に使用される薄いラッパーです。
/// 状態は最小限に抑え、MangaSeries インスタンス自体と Series.ForceNotify() による通知機構を活用します。
/// 作品に付与されたタグは、<see cref="TagStore"/> が所有する共有 <see cref="TagNotifier"/> を
/// <see cref="Tags"/> として公開し、<c>MangaSeries.Tags</c> の変更に自動追従します。
/// </summary>
public class MangaSeriesNotifier : IDisposable
{
	private DisposableBag disposableBag;
	private readonly TagStore tagStore;
	private readonly MangaSeries trackedSeries;
	private readonly ObservableList<TagNotifier> tags = new();

	/// <summary>
	/// ViewModel が保持する MangaSeries インスタンスを取得します。
	/// 同一インスタンスを各表示先で参照し、ForceNotify() で内容変更を通知します。
	/// </summary>
	public BindableReactiveProperty<MangaSeries> Series { get; }

	/// <summary>
	/// この作品に付与されたタグの表示・通知用派生コレクションを取得します。
	/// 要素は <see cref="TagStore"/> が所有する共有 <see cref="TagNotifier"/> であり、
	/// <c>MangaSeries.Tags</c> の変更に連動して更新されます。要素を Dispose してはいけません。
	/// </summary>
	public ObservableList<TagNotifier> Tags => this.tags;

	/// <summary>
	/// <see cref="MangaSeriesNotifier"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="series">保持する MangaSeries インスタンス。</param>
	/// <param name="tagStore">共有 <see cref="TagNotifier"/> を所有するタグストア。</param>
	/// <exception cref="InvalidOperationException">作品のタグが <paramref name="tagStore"/> に存在しない場合。</exception>
	public MangaSeriesNotifier(MangaSeries series, TagStore tagStore)
	{
		this.tagStore = tagStore;
		this.trackedSeries = series;

		this.Series = new BindableReactiveProperty<MangaSeries>(series)
			.AddTo(ref this.disposableBag);

		this.synchronizeSeriesTagInstances();
		this.rebuildTags();

		series.Tags.CollectionChanged += this.onSeriesTagsChanged;
	}

	/// <summary>
	/// 作品の <c>MangaSeries.Tags</c> に含まれる各 <see cref="MangaTag"/> を、
	/// <see cref="TagStore"/> が保持する正本インスタンスへ整合させます。
	/// DB から読み込んだ作品など、別インスタンスのタグを持つ場合に置き換えます。
	/// </summary>
	private void synchronizeSeriesTagInstances()
	{
		var seriesTags = this.trackedSeries.Tags;

		for (var i = 0; i < seriesTags.Count; i++)
		{
			var canonical = this.tagStore.GetTagNotifier(seriesTags[i].TagId).Tag.Value;

			if (!ReferenceEquals(seriesTags[i], canonical))
			{
				seriesTags[i] = canonical;
			}
		}
	}

	/// <summary>
	/// <see cref="Tags"/> を、この作品の <c>MangaSeries.Tags</c> に対応する共有 <see cref="TagNotifier"/> で再構築します。
	/// この作品のタグのみが対象で、<see cref="TagStore"/> 全体のタグは再生成しません。
	/// </summary>
	private void rebuildTags()
	{
		this.tags.Clear();

		foreach (var tag in this.trackedSeries.Tags)
		{
			this.tags.Add(this.resolveTagNotifier(tag));
		}
	}

	/// <summary>
	/// 指定した <see cref="MangaTag"/> に対応する共有 <see cref="TagNotifier"/> を取得します。
	/// 渡されたインスタンスが <see cref="TagStore"/> の正本インスタンスでない場合は例外とします。
	/// </summary>
	/// <param name="tag">作品に付与されたタグ。</param>
	/// <returns>共有 <see cref="TagNotifier"/>。</returns>
	/// <exception cref="InvalidOperationException">タグが存在しない、または正本インスタンスでない場合。</exception>
	private TagNotifier resolveTagNotifier(MangaTag tag)
	{
		var notifier = this.tagStore.GetTagNotifier(tag.TagId);

		if (!ReferenceEquals(notifier.Tag.Value, tag))
		{
			throw new InvalidOperationException($"TagId {tag.TagId} の MangaTag が TagStore の正本インスタンスではありません。");
		}

		return notifier;
	}

	/// <summary>
	/// <c>MangaSeries.Tags</c> の変更を <see cref="Tags"/> へ同期します。
	/// Add / Remove / Replace / Move は差分で反映し、Reset 系（Clear / Sort / Reverse）は
	/// この作品のタグのみで再構築します。
	/// </summary>
	private void onSeriesTagsChanged(in NotifyCollectionChangedEventArgs<MangaTag> e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add:
				{
					var index = e.NewStartingIndex;

					if (e.IsSingleItem)
					{
						this.tags.Insert(index, this.resolveTagNotifier(e.NewItem));
					}
					else
					{
						foreach (var added in e.NewItems)
						{
							this.tags.Insert(index++, this.resolveTagNotifier(added));
						}
					}

					break;
				}

			case NotifyCollectionChangedAction.Remove:
				{
					var count = e.IsSingleItem ? 1 : e.OldItems.Length;
					for (var i = 0; i < count; i++)
					{
						this.tags.RemoveAt(e.OldStartingIndex);
					}

					break;
				}

			case NotifyCollectionChangedAction.Replace:
				this.tags[e.NewStartingIndex] = this.resolveTagNotifier(e.NewItem);
				break;

			case NotifyCollectionChangedAction.Move:
				this.tags.Move(e.OldStartingIndex, e.NewStartingIndex);
				break;

			default:
				this.rebuildTags();
				break;
		}
	}

	/// <summary>
	/// このビューモデルが保持するリソースを解放します。
	/// <c>MangaSeries.Tags</c> のイベント購読を解除します。共有 <see cref="TagNotifier"/> は <see cref="TagStore"/> の所有物のため破棄しません。
	/// </summary>
	public void Dispose()
	{
		this.trackedSeries.Tags.CollectionChanged -= this.onSeriesTagsChanged;
		this.disposableBag.Dispose();
	}
}
