using System.ComponentModel;
using System.Runtime.CompilerServices;
using MangaBinder.Tags;

namespace MangaBinder;

/// <summary>
/// タグ付けポップアップで使用するタグ選択項目です。
/// </summary>
public sealed class SeriesTagSelectionItem : INotifyPropertyChanged
{
	/// <summary>対象タグを取得します。</summary>
	public MangaTag Tag { get; }

	private bool isChecked;
	private bool isPlaceholder;

	/// <summary>このタグが対象作品に付与されているかどうかを取得または設定します。</summary>
	public bool IsChecked
	{
		get => this.isChecked;
		set
		{
			if (this.isChecked == value)
				return;
			this.isChecked = value;
			this.OnPropertyChanged();
		}
	}

	/// <summary>このアイテムがプレースホルダー（空セル）かどうかを取得または設定します。</summary>
	public bool IsPlaceholder
	{
		get => this.isPlaceholder;
		set
		{
			if (this.isPlaceholder == value)
				return;
			this.isPlaceholder = value;
			this.OnPropertyChanged();
		}
	}

	/// <summary>
	/// <see cref="SeriesTagSelectionItem"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="tag">対象タグ。</param>
	/// <param name="isChecked">チェック初期状態。</param>
	public SeriesTagSelectionItem(MangaTag tag, bool isChecked)
	{
		this.Tag = tag;
		this.isChecked = isChecked;
		this.isPlaceholder = false;
	}

	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		=> this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
