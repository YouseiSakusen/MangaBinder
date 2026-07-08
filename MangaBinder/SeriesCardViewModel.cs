using System.ComponentModel;
using System.Runtime.CompilerServices;
using MangaBinder.Controls;

namespace MangaBinder;

/// <summary>
/// Home 画面の ListView アイテム表示用 ViewModel です。MangaSeries をラップしています。
/// </summary>
public class SeriesCardViewModel : INotifyPropertyChanged
{
	private SeriesVolumeStatusViewModel volumeStatus;

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
	/// <see cref="SeriesCardViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="series">ラップする MangaSeries。</param>
	public SeriesCardViewModel(MangaSeries series)
	{
		this.Series = series;
		this.volumeStatus = SeriesVolumeStatusViewModel.FromSeries(series);
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
