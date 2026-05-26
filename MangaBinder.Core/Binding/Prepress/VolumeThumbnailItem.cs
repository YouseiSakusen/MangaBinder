using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MangaBinder.Binding.Prepress;

/// <summary>
/// サムネイル一覧の1アイテムを表します。
/// </summary>
public sealed class VolumeThumbnailItem : INotifyPropertyChanged
{
	private bool isChecked;

	/// <summary>ファイルのフルパスを取得します。</summary>
	public string FilePath { get; init; } = string.Empty;

	/// <summary>ファイル名を取得します。</summary>
	public string FileName { get; init; } = string.Empty;

	/// <summary>サムネイル JPEG バイト列を取得します。<see langword="null"/> の場合は読み込み失敗。</summary>
	public byte[]? ThumbnailBytes { get; init; }

	/// <summary>フォールバック画像リソースキーを取得します。対応画像の場合は <see langword="null"/>。</summary>
	public string? FallbackResourceKey { get; init; }

	/// <summary>分割対象としてチェックされているかどうかを取得または設定します。</summary>
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

	/// <summary>このアイテムにエラーがあるかどうかを取得します。</summary>
	public bool HasError { get; init; }

	/// <summary>対応外ファイル形式かどうかを取得します。</summary>
	public bool IsUnsupported { get; init; }

	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		=> this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
