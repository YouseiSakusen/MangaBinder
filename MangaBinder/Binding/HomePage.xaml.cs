using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MangaBinder.Binding;

/// <summary>
/// HomePage.xaml の相互作用ロジック
/// </summary>
public partial class HomePage : Page
{
	public HomePage()
	{
		InitializeComponent();
	}

	/// <summary>
	/// タグ ToggleButton がチェックされた際に ViewModel へ対象作品を通知します。
	/// </summary>
	private void TagToggleButton_Checked(object sender, RoutedEventArgs e)
	{
		if (sender is not ToggleButton btn)
			return;

		if (btn.DataContext is not MangaSeries series)
			return;

		if (this.DataContext is not HomePageViewModel vm)
			return;

		vm.PrepareTagPopupCommand.Execute(series);
	}
}
