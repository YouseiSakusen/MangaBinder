using System.Windows.Controls;

namespace MangaBinder.Settings;

/// <summary>
/// SettingsPage.xaml の相互作用ロジック
/// </summary>
public partial class SettingsPage : Page
{
	/// <summary>
	/// <see cref="SettingsPage"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="viewModel">設定ページの ViewModel。</param>
	public SettingsPage(SettingsPageViewModel viewModel)
	{
		InitializeComponent();
		this.DataContext = viewModel;
	}
}

