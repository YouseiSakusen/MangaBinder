using System.ComponentModel;
using System.Runtime.CompilerServices;
using MangaBinder.Bindings;
using MangaBinder.Controls;
using MangaBinder.Core.Formatters;
using R3;

namespace MangaBinder;

/// <summary>
/// Home 画面の ListView アイテム表示用 ViewModel です。MangaSeries をラップしています。
/// </summary>
public class SeriesCardViewModel : INotifyPropertyChanged
{
	private SeriesVolumeStatusViewModel volumeStatus;
	private DisposableBag disposableBag = new();

	/// <summary>
	/// 基になった MangaSeries です。
	/// </summary>
	public MangaSeries Series { get; }

	/// <summary>
	/// 巻情報表示用の ViewModel です。
	/// </summary>
	public SeriesVolumeStatusViewModel VolumeStatus
	{
		get => this.volumeStatus;
		private set
		{
			if (this.volumeStatus == value)
				return;
			this.volumeStatus = value;
			this.OnPropertyChanged();
		}
	}

	/// <summary>
	/// 製本対象として選択されているかどうかを示します。
	/// UI 状態のため、SeriesCardViewModel が保持します。
	/// </summary>
	public BindableReactiveProperty<bool> IsSelected { get; }

	/// <summary>
	/// タグ表示用テキスト。
	/// Series.Tags の変更に応じて自動更新されます。
	/// </summary>
	public BindableReactiveProperty<string> TagDisplayText { get; }

	/// <summary>
	/// <see cref="SeriesCardViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="series">ラップする MangaSeries。</param>
	/// <param name="bindingQueueStore">製本開始キュー ストア。初期値決定用。</param>
	public SeriesCardViewModel(MangaSeries series, BindingQueueStore? bindingQueueStore = null)
	{
		this.Series = series;
		this.volumeStatus = SeriesVolumeStatusViewModel.FromSeries(series);

		// IsSelected の初期値を BindingQueueStore から決定
		var isInQueue = bindingQueueStore?.Contains(series.SeriesId) ?? false;
		this.IsSelected = new BindableReactiveProperty<bool>(isInQueue);

		// TagDisplayText の初期化と Tags.CollectionChanged 購読
		this.TagDisplayText = new BindableReactiveProperty<string>(
			SeriesTagDisplayFormatter.Format(series.Tags)
		);

		series.Tags.CollectionChanged += (_, _) =>
		{
			this.TagDisplayText.Value = SeriesTagDisplayFormatter.Format(series.Tags);
		};
	}

	/// <summary>
	/// 現在の Series インスタンスから VolumeStatus を再生成します。
	/// </summary>
	public void RefreshVolumeStatus()
	{
		this.VolumeStatus = SeriesVolumeStatusViewModel.FromSeries(this.Series);
	}

	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>
	/// PropertyChanged イベントを発火させます。
	/// </summary>
	/// <param name="propertyName">プロパティ名。省略時は呼び出し元のプロパティ名。</param>
	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		=> this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
