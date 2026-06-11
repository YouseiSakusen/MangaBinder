using System.Windows;
using System.Windows.Controls;

namespace MangaBinder.Bindings;

/// <summary>
/// SeriesInspectionPage.xaml の相互作用ロジック
/// </summary>
public partial class SeriesInspectionPage : Page
{
	/// <summary>
	/// <see cref="SeriesInspectionPage"/> の新しいインスタンスを初期化します。
	/// </summary>
	public SeriesInspectionPage()
	{
		this.InitializeComponent();
	}

	/// <summary>
	/// 巻カードの3点リーダボタンがクリックされた際にコンテキストメニューを表示します。
	/// </summary>
	private void VolumeMenuButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is not FrameworkElement button)
			return;

		var menu = button.Resources["VolumeContextMenu"] as ContextMenu;
		if (menu is null)
			return;

		menu.PlacementTarget = button;
		menu.DataContext = button;
		menu.IsOpen = true;
	}
}

