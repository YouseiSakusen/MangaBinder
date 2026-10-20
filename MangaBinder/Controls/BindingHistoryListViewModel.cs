using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using MangaBinder.Bindings;
using MangaBinder.Series;
using R3;

namespace MangaBinder.Controls;

/// <summary>
/// 製本履歴一覧の1行分の表示データです。
/// </summary>
/// <param name="FileName">表示するファイル名（ZIP 未作成の場合は巻数範囲）。</param>
/// <param name="BoundAtText">表示用の製本日時。</param>
/// <param name="GroupName">グループ見出し用の文字列。</param>
public sealed record BindingHistoryListItem(string FileName, string BoundAtText, string GroupName);

/// <summary>
/// <see cref="BindingHistoryList"/> の ViewModel です。
/// 共有 MangaSeriesViewModel の BindingHistories を、表示用に投影します。
/// </summary>
public class BindingHistoryListViewModel : IDisposable
{
	/// <summary>共有 Series の購読。作品切り替え時に差し替えます。</summary>
	private IDisposable? seriesSubscription;

	/// <summary>リソース管理用の DisposableBag。</summary>
	private DisposableBag disposableBag;

	/// <summary>履歴が1件以上存在するかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> HasHistory { get; }

	/// <summary>グループ化済みの表示用履歴ビューを取得します。履歴がない場合は null です。</summary>
	public BindableReactiveProperty<ICollectionView?> HistoryView { get; }

	/// <summary>
	/// <see cref="BindingHistoryListViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	public BindingHistoryListViewModel()
	{
		this.HasHistory = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.HistoryView = new BindableReactiveProperty<ICollectionView?>(null)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// 対象の共有 MangaSeriesViewModel を設定し、通知の購読を差し替えます。
	/// </summary>
	/// <param name="seriesViewModel">対象の共有VM。未設定の場合は null。</param>
	public void SetSeriesViewModel(MangaSeriesViewModel? seriesViewModel)
	{
		this.seriesSubscription?.Dispose();
		this.seriesSubscription = null;

		if (seriesViewModel is null)
		{
			this.clear();
			return;
		}

		this.seriesSubscription = seriesViewModel.Series
			.Subscribe(series => this.rebuild(series.BindingHistories));
	}

	/// <summary>
	/// 表示をクリアします。
	/// </summary>
	private void clear()
	{
		this.HistoryView.Value = null;
		this.HasHistory.Value = false;
	}

	/// <summary>
	/// 履歴から表示用データを生成します。表示順の決定はここに集約します。
	/// </summary>
	private void rebuild(IReadOnlyCollection<BindingHistory> histories)
	{
		if (histories.Count == 0)
		{
			this.clear();
			return;
		}

		var items = histories
			.OrderBy(h => h.StartVolume)
			.ThenBy(h => h.BoundAt)
			.ThenBy(h => h.HistoryId)
			.Select(h => new BindingHistoryListItem(
				getFileName(h),
				h.BoundAt.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture),
				$"開始巻：{formatVolume(h.StartVolume)}巻"))
			.ToList();

		var view = new ListCollectionView(items);
		if (histories.Select(h => h.StartVolume).Distinct().Count() > 1)
		{
			view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(BindingHistoryListItem.GroupName)));
		}

		this.HistoryView.Value = view;
		this.HasHistory.Value = true;
	}

	/// <summary>
	/// 表示用のファイル名を取得します。ZIP 未作成の場合は巻数範囲を返します。
	/// </summary>
	private static string getFileName(BindingHistory history)
		=> history.OutputFileName.Length > 0
			? history.OutputFileName
			: $"{formatVolume(history.StartVolume)}-{formatVolume(history.EndVolume)}巻";

	/// <summary>
	/// 巻番号を末尾のゼロを除いて文字列化します。
	/// </summary>
	private static string formatVolume(decimal volume)
		=> volume.ToString("0.############################", CultureInfo.InvariantCulture);

	/// <summary>
	/// リソースを破棄します。共有 MangaSeriesViewModel は破棄しません。
	/// </summary>
	public void Dispose()
	{
		this.seriesSubscription?.Dispose();
		this.disposableBag.Dispose();
	}
}
