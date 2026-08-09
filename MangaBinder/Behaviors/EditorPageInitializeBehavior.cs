using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MangaBinder.Behaviors;

/// <summary>
/// EditorPage 専用の初期化ビヘイビアです。
/// EditorPage の初回表示時、または編集対象が切り替わった時に、
/// View の状態（フォーカス位置、スクロール位置など）を初期状態へリセットします。
/// </summary>
public static class EditorPageInitializeBehavior
{
	/// <summary>
	/// EditorPage 全体の初期化要求を受け取る添付プロパティです。
	/// 値が変更されたタイミングで初期化処理を実行します。
	/// bool 値そのものには意味を持たせず、変更を通知トリガーとして利用します。
	/// </summary>
	public static readonly DependencyProperty RequestProperty =
		DependencyProperty.RegisterAttached(
			"Request",
			typeof(bool),
			typeof(EditorPageInitializeBehavior),
			new PropertyMetadata(false, OnRequestChanged));

	/// <summary>Request 添付プロパティの値を取得します。</summary>
	/// <param name="obj">値を取得する対象の <see cref="DependencyObject"/>。</param>
	public static bool GetRequest(DependencyObject obj)
		=> (bool)obj.GetValue(RequestProperty);

	/// <summary>Request 添付プロパティの値を設定します。</summary>
	/// <param name="obj">値を設定する対象の <see cref="DependencyObject"/>。</param>
	/// <param name="value">設定する値。</param>
	public static void SetRequest(DependencyObject obj, bool value)
		=> obj.SetValue(RequestProperty, value);

	/// <summary>
	/// Request 添付プロパティが変更されたときに呼び出されます。
	/// EditorPage の初期化処理を実行します。
	/// </summary>
	private static void OnRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not Page page)
			return;

		// Dispatcher でレイアウト完了後に実行
		page.Dispatcher.BeginInvoke(
			DispatcherPriority.ContextIdle,
			new Action(() =>
			{
				InitializeEditorPage(page);
			}));
	}

	/// <summary>
	/// EditorPage 全体の初期化処理を実行します。
	/// </summary>
	private static void InitializeEditorPage(Page page)
	{
		// 各コントロールを x:Name から取得
		// タイトル入力欄
		if (page.FindName("TitleTextBox") is TextBox titleTextBox)
		{
			titleTextBox.Dispatcher.BeginInvoke(
				DispatcherPriority.Input,
				new Action(() =>
				{
					titleTextBox.Focus();
					Keyboard.Focus(titleTextBox);
					titleTextBox.SelectAll();
				}));
		}

		// あらすじ入力欄
		if (page.FindName("DescriptionTextBox") is TextBox descriptionTextBox)
		{
			ResetTextBoxScroll(descriptionTextBox);
		}

		// メモ入力欄
		if (page.FindName("MemoTextBox") is TextBox memoTextBox)
		{
			ResetTextBoxScroll(memoTextBox);
		}

		// 素材ファイル一覧 ListView
		if (page.FindName("MaterialFilesListView") is ListView materialFilesListView)
		{
			ResetListViewScroll(materialFilesListView);
		}
	}

	/// <summary>
	/// TextBox のスクロール位置を先頭へリセットします。
	/// </summary>
	private static void ResetTextBoxScroll(TextBox textBox)
	{
		textBox.Dispatcher.BeginInvoke(
			DispatcherPriority.ContextIdle,
			new Action(() =>
			{
				// TextBox の内部 ScrollViewer を取得
				var scrollViewer = FindScrollViewer(textBox);
				if (scrollViewer != null)
				{
					scrollViewer.ScrollToHome();
				}
			}));
	}

	/// <summary>
	/// ListView のスクロール位置を先頭へリセットします。
	/// </summary>
	private static void ResetListViewScroll(ListView listView)
	{
		listView.Dispatcher.BeginInvoke(
			DispatcherPriority.ContextIdle,
			new Action(() =>
			{
				// ListView の内部 ScrollViewer を取得
				var scrollViewer = FindScrollViewer(listView);
				if (scrollViewer != null)
				{
					scrollViewer.ScrollToHome();
				}
			}));
	}

	/// <summary>
	/// VisualTree の子孫から最初の <see cref="ScrollViewer"/> を返します。
	/// </summary>
	private static ScrollViewer? FindScrollViewer(DependencyObject parent)
	{
		var count = VisualTreeHelper.GetChildrenCount(parent);
		for (var i = 0; i < count; i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);
			if (child is ScrollViewer scrollViewer)
				return scrollViewer;

			var found = FindScrollViewer(child);
			if (found != null)
				return found;
		}
		return null;
	}
}
