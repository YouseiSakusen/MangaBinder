using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MangaBinder.Behaviors;

/// <summary>
/// 素材ファイルのドラッグアンドドロップを処理する添付ビヘイビアです。
/// DragOver/DragLeave/Drop イベントを監視し、ドラッグ状態を通知し、ドロップ時にコマンドを実行します。
/// </summary>
public static class MaterialFileDropBehavior
{
	/// <summary>DragOver タイムアウト時間（ミリ秒）。</summary>
	private const int DragOverTimeoutMs = 150;

	/// <summary>DragOver 監視タイマーの更新間隔（ミリ秒）。</summary>
	private const int DragOverTimerIntervalMs = 100;

	/// <summary>要素ごとの最終 DragOver 時刻を記録する辞書。</summary>
	private static readonly Dictionary<UIElement, DateTime> LastDragOverTime = new();

	/// <summary>要素ごとの DragOver 監視タイマーを管理する辞書。</summary>
	private static readonly Dictionary<UIElement, DispatcherTimer> DragOverTimers = new();

	/// <summary>要素ごとの DragOver 監視タイマーのイベントハンドラを管理する辞書。</summary>
	private static readonly Dictionary<UIElement, EventHandler> DragOverTimerHandlers = new();
	/// <summary>
	/// ドロップ時に実行するコマンドの添付プロパティです。
	/// ドロップされたファイル/フォルダパスの文字列配列を CommandParameter として渡します。
	/// </summary>
	public static readonly DependencyProperty DropCommandProperty =
		DependencyProperty.RegisterAttached(
			"DropCommand",
			typeof(ICommand),
			typeof(MaterialFileDropBehavior),
			new PropertyMetadata(null, OnDropCommandChanged));

	/// <summary>
	/// ドラッグ中状態を示す添付プロパティです。
	/// ドラッグ中は true、DragLeave/Drop 後は false になります。
	/// 双方向バインド対応。
	/// </summary>
	public static readonly DependencyProperty IsDragOverProperty =
		DependencyProperty.RegisterAttached(
			"IsDragOver",
			typeof(bool),
			typeof(MaterialFileDropBehavior),
			new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

	/// <summary><see cref="DropCommandProperty"/> の getter です。</summary>
	public static ICommand GetDropCommand(DependencyObject obj)
		=> (ICommand)obj.GetValue(DropCommandProperty);

	/// <summary><see cref="DropCommandProperty"/> の setter です。</summary>
	public static void SetDropCommand(DependencyObject obj, ICommand value)
		=> obj.SetValue(DropCommandProperty, value);

	/// <summary><see cref="IsDragOverProperty"/> の getter です。</summary>
	public static bool GetIsDragOver(DependencyObject obj)
		=> (bool)obj.GetValue(IsDragOverProperty);

	/// <summary><see cref="IsDragOverProperty"/> の setter です。</summary>
	public static void SetIsDragOver(DependencyObject obj, bool value)
		=> obj.SetValue(IsDragOverProperty, value);

	private static void OnDropCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not UIElement element)
			return;

		if (e.NewValue is not null)
		{
			element.AllowDrop = true;
			element.PreviewDragOver += OnPreviewDragOver;
			element.PreviewDragLeave += OnPreviewDragLeave;
			element.PreviewDrop += OnPreviewDrop;
			element.PreviewQueryContinueDrag += OnPreviewQueryContinueDrag;
		}
		else
		{
			element.PreviewDragOver -= OnPreviewDragOver;
			element.PreviewDragLeave -= OnPreviewDragLeave;
			element.PreviewDrop -= OnPreviewDrop;
			element.PreviewQueryContinueDrag -= OnPreviewQueryContinueDrag;

			// クリーンアップ：タイマーと記録を削除
			StopDragOverTimer(element);
			LastDragOverTime.Remove(element);

			element.AllowDrop = false;
		}
	}

	private static void OnPreviewDragOver(object sender, DragEventArgs e)
	{
		if (sender is not UIElement element)
			return;

		if (sender is not DependencyObject obj)
			return;

		// ファイル/フォルダのドラッグかチェック
		if (e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			e.Effects = DragDropEffects.Copy;
			e.Handled = true;

			// ドラッグ中状態を true に設定
			SetIsDragOver(obj, true);

			// 最終 DragOver 時刻を更新
			LastDragOverTime[element] = DateTime.UtcNow;

			// タイマーが起動していなければ開始
			if (!DragOverTimers.ContainsKey(element))
			{
				StartDragOverTimer(element);
			}
		}
		else
		{
			e.Effects = DragDropEffects.None;
		}
	}

	private static void OnPreviewDragLeave(object sender, DragEventArgs e)
	{
		if (sender is not UIElement element)
			return;

		if (sender is not DependencyObject obj)
			return;

		// マウス位置を取得（対象要素を基準）
		var position = e.GetPosition(element);

		// 対象要素の Bounds を取得
		var bounds = new Rect(0, 0, element.RenderSize.Width, element.RenderSize.Height);

		// マウス位置が対象要素の範囲外へ出た場合のみ、ドラッグ中状態を false に設定
		// 子要素間の移動では false にしない
		if (!bounds.Contains(position))
		{
			SetIsDragOver(obj, false);
			StopDragOverTimer(element);
			LastDragOverTime.Remove(element);
		}
	}

	private static void OnPreviewDrop(object sender, DragEventArgs e)
	{
		e.Handled = true;

		if (sender is not UIElement element)
			return;

		if (sender is not DependencyObject obj)
			return;

		// ドラッグ中状態を false に設定
		SetIsDragOver(obj, false);

		// タイマーを停止
		StopDragOverTimer(element);

		// ドラッグ情報を削除
		LastDragOverTime.Remove(element);

		// ファイルパスを取得し、コマンドを実行
		if (e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			var filePaths = e.Data.GetData(DataFormats.FileDrop) as string[];
			var command = GetDropCommand(obj);

			if (filePaths != null && command != null && command.CanExecute(filePaths))
			{
				command.Execute(filePaths);
			}
		}
	}

	private static void OnPreviewQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
	{
		if (sender is not DependencyObject obj)
			return;

		// Esc キーでキャンセルされた場合
		if (e.EscapePressed)
		{
			SetIsDragOver(obj, false);
			return;
		}

		// DragAction が Cancel の場合
		if (e.Action == DragAction.Cancel)
		{
			SetIsDragOver(obj, false);
			return;
		}

		// DragAction が Drop の場合
		if (e.Action == DragAction.Drop)
		{
			SetIsDragOver(obj, false);
			return;
		}
	}

	/// <summary>DragOver 監視タイマーを開始します。</summary>
	private static void StartDragOverTimer(UIElement element)
	{
		var timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(DragOverTimerIntervalMs)
		};

		// イベントハンドラを作成して保持
		EventHandler handler = (s, e) => OnDragOverTimerTick(element);
		timer.Tick += handler;
		timer.Start();

		DragOverTimers[element] = timer;
		DragOverTimerHandlers[element] = handler;
	}

	/// <summary>DragOver 監視タイマーを停止します。</summary>
	private static void StopDragOverTimer(UIElement element)
	{
		if (DragOverTimers.TryGetValue(element, out var timer))
		{
			// タイマーを停止
			timer.Stop();

			// 保持しているハンドラ参照を取得して削除
			if (DragOverTimerHandlers.TryGetValue(element, out var handler))
			{
				timer.Tick -= handler;
				DragOverTimerHandlers.Remove(element);
			}

			// タイマーの参照を削除
			timer = null;
			DragOverTimers.Remove(element);
		}
	}

	/// <summary>DragOver タイムアウトをチェックするタイマー Tick ハンドラです。</summary>
	private static void OnDragOverTimerTick(UIElement element)
	{
		if (!LastDragOverTime.TryGetValue(element, out var lastTime))
		{
			return;
		}

		var elapsed = DateTime.UtcNow - lastTime;

		// タイムアウト時間を超えたら DragOver を終了したものとして IsDragOver を false にする
		if (elapsed.TotalMilliseconds >= DragOverTimeoutMs)
		{
			SetIsDragOver(element, false);
			StopDragOverTimer(element);
			LastDragOverTime.Remove(element);
		}
	}
}
