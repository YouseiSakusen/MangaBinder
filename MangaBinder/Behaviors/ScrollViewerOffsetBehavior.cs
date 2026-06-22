using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;

namespace MangaBinder.Behaviors;

/// <summary>
/// ListView 等の内部 ScrollViewer の VerticalOffset を ViewModel にバインドする添付ビヘイビアです。
/// </summary>
public static class ScrollViewerOffsetBehavior
{
    /// <summary>保存・復元する VerticalOffset の添付プロパティです。</summary>
    public static readonly DependencyProperty BoundVerticalOffsetProperty =
        DependencyProperty.RegisterAttached(
            "BoundVerticalOffset",
            typeof(double),
            typeof(ScrollViewerOffsetBehavior),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundVerticalOffsetChanged));

    /// <summary>内部 ScrollViewer を保持するキーです。</summary>
    private static readonly DependencyProperty ScrollViewerKey =
        DependencyProperty.RegisterAttached(
            "ScrollViewer",
            typeof(ScrollViewer),
            typeof(ScrollViewerOffsetBehavior));

    /// <summary>ScrollViewer にオーナー要素を保持するキーです。</summary>
    private static readonly DependencyProperty OwnerElementKey =
        DependencyProperty.RegisterAttached(
            "OwnerElement",
            typeof(FrameworkElement),
            typeof(ScrollViewerOffsetBehavior));

    /// <summary>BoundVerticalOffset 添付プロパティの値を取得します。</summary>
    /// <param name="obj">値を取得する対象の <see cref="DependencyObject"/>。</param>
    /// <returns>現在の VerticalOffset の値。</returns>
    public static double GetBoundVerticalOffset(DependencyObject obj)
        => (double)obj.GetValue(BoundVerticalOffsetProperty);

    /// <summary>BoundVerticalOffset 添付プロパティの値を設定します。</summary>
    /// <param name="obj">値を設定する対象の <see cref="DependencyObject"/>。</param>
    /// <param name="value">設定する VerticalOffset の値。</param>
    public static void SetBoundVerticalOffset(DependencyObject obj, double value)
        => obj.SetValue(BoundVerticalOffsetProperty, value);

    /// <summary>
    /// BoundVerticalOffset 添付プロパティが変更されたときに呼び出されます。
    /// Loaded / Unloaded イベントを購読します。
    /// </summary>
    /// <param name="d">プロパティが変更された <see cref="DependencyObject"/>。</param>
    /// <param name="e">変更イベントの引数。</param>
    private static void OnBoundVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // DEBUG: スクロール復元調査用
        // Debug.WriteLine($"[ScrollBehavior] OnBoundVerticalOffsetChanged: target={d.GetType().Name}, old={e.OldValue}, new={e.NewValue}");

        if (d is not FrameworkElement element)
        {
            // DEBUG: スクロール復元調査用
            // Debug.WriteLine($"[ScrollBehavior] OnBoundVerticalOffsetChanged: target は FrameworkElement ではないためスキップします (type={d.GetType().FullName})");
            return;
        }

        element.Loaded -= OnLoaded;
        element.Loaded += OnLoaded;
        element.Unloaded -= OnUnloaded;
        element.Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 要素がロードされたときに呼び出されます。
    /// 内部の ScrollViewer を探し、ScrollChanged イベントを購読してオフセットを復元します。
    /// </summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの引数。</param>
    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        // DEBUG: スクロール復元調査用
        // Debug.WriteLine($"[ScrollBehavior] OnLoaded: target={element.GetType().Name}");

        // DEBUG: スクロール復元調査用
        var boundOffset = GetBoundVerticalOffset(element);
        // Debug.WriteLine($"[ScrollBehavior] OnLoaded: BoundVerticalOffset={boundOffset}");

        // DEBUG: スクロール復元調査用
        var scrollViewers = FindScrollViewers(element);
        // Debug.WriteLine($"[ScrollBehavior] OnLoaded: ScrollViewer count={scrollViewers.Count}");
        // for (int i = 0; i < scrollViewers.Count; i++)
        // {
        //     var sv = scrollViewers[i];
        //     Debug.WriteLine($"[ScrollBehavior] OnLoaded: [{i}] Name={sv.Name}, VerticalOffset={sv.VerticalOffset}, ExtentHeight={sv.ExtentHeight}, ViewportHeight={sv.ViewportHeight}, VerticalScrollBarVisibility={sv.ComputedVerticalScrollBarVisibility}");
        // }

        if (scrollViewers.Count == 0)
            return;

        // スクロール可能（ExtentHeight > ViewportHeight かつ VerticalScrollBarVisibility=Visible）な ScrollViewer を候補とする
        var scrollableViewers = scrollViewers
            .Where(sv => sv.ExtentHeight > sv.ViewportHeight && sv.ComputedVerticalScrollBarVisibility == Visibility.Visible)
            .ToList();

        if (scrollableViewers.Count == 0)
        {
            // スクロール可能な ScrollViewer が見つからない場合は処理を終了
            // Debug.WriteLine($"[ScrollBehavior] OnLoaded: スクロール可能な ScrollViewer が見つかりません");
            return;
        }

        // 候補の中から ExtentHeight が最大の ScrollViewer を選択する
        var primaryScrollViewer = scrollableViewers.MaxBy(sv => sv.ExtentHeight)!;

        // DEBUG: スクロール復元調査用
        // Debug.WriteLine($"[ScrollBehavior] OnLoaded: 選択された ScrollViewer: Name={primaryScrollViewer.Name}, ExtentHeight={primaryScrollViewer.ExtentHeight}, ViewportHeight={primaryScrollViewer.ViewportHeight}, VerticalScrollBarVisibility={primaryScrollViewer.ComputedVerticalScrollBarVisibility}");

        // 既に購読済みの ScrollViewer があれば先に解除する
        var previous = (ScrollViewer?)element.GetValue(ScrollViewerKey);
        if (previous is not null)
        {
            previous.ScrollChanged -= OnScrollChanged;
            previous.SetValue(OwnerElementKey, null);
        }

        element.SetValue(ScrollViewerKey, primaryScrollViewer);
        primaryScrollViewer.SetValue(OwnerElementKey, element);

        // 復元オフセットを退避しておく（初期レイアウトの ScrollChanged=0 で上書きされる前に保存）
        var restoreOffset = GetBoundVerticalOffset(element);

        // 復元処理完了後に ScrollChanged を購読することで、
        // 初期レイアウト時の VerticalOffset=0 による上書きを防ぐ
        element.Dispatcher.InvokeAsync(() =>
        {
            // DEBUG: スクロール復元調査用
            // Debug.WriteLine($"[ScrollBehavior] ContextIdle: restoreOffset={restoreOffset}");

            if (!double.IsNaN(restoreOffset) && restoreOffset > 0)
            {
                // DEBUG: スクロール復元調査用
                // Debug.WriteLine($"[ScrollBehavior] ContextIdle: ScrollToVerticalOffset({restoreOffset}) を実行します");
                primaryScrollViewer.ScrollToVerticalOffset(restoreOffset);
                // Debug.WriteLine($"[ScrollBehavior] ContextIdle: 実行直後 scrollViewer.VerticalOffset={primaryScrollViewer.VerticalOffset}");
            }
            else
            {
                // DEBUG: スクロール復元調査用
                // Debug.WriteLine($"[ScrollBehavior] ContextIdle: restoreOffset={restoreOffset} のため ScrollToVerticalOffset をスキップします");
            }

            // DEBUG: スクロール復元調査用
            // Debug.WriteLine($"[ScrollBehavior] ContextIdle: ScrollChanged 購読を開始します");
            primaryScrollViewer.ScrollChanged += OnScrollChanged;
        }, DispatcherPriority.ContextIdle);
    }

    /// <summary>
    /// 要素がアンロードされたときに呼び出されます。
    /// ScrollChanged イベントの購読を解除してリークを防止します。
    /// </summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの引数。</param>
    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        var scrollViewer = (ScrollViewer?)element.GetValue(ScrollViewerKey);

        // DEBUG: スクロール復元調査用
        // Debug.WriteLine($"[ScrollBehavior] OnUnloaded: BoundVerticalOffset={GetBoundVerticalOffset(element)}, scrollViewer.VerticalOffset={scrollViewer?.VerticalOffset}");

        if (scrollViewer is not null)
        {
            scrollViewer.ScrollChanged -= OnScrollChanged;
            scrollViewer.SetValue(OwnerElementKey, null);
        }

        element.SetValue(ScrollViewerKey, null);
    }

    /// <summary>
    /// ScrollViewer のスクロール位置が変化したときに呼び出されます。
    /// 現在の VerticalOffset を BoundVerticalOffset 添付プロパティに書き戻します。
    /// </summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">スクロール変更イベントの引数。</param>
    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        // DEBUG: スクロール復元調査用
        // Debug.WriteLine($"[ScrollBehavior] ScrollChanged fired: Name={scrollViewer.Name}, offset={e.VerticalOffset}, extent={scrollViewer.ExtentHeight}, viewport={scrollViewer.ViewportHeight}");

        // VisualTree 遡及より安定した、添付プロパティ経由でオーナー要素を取得する
        var element = (FrameworkElement?)scrollViewer.GetValue(OwnerElementKey);
        if (element is not null && !double.IsNaN(scrollViewer.VerticalOffset))
        {
            // DEBUG: スクロール復元調査用
            // Debug.WriteLine($"[ScrollBehavior] OnScrollChanged: BoundVerticalOffset に {scrollViewer.VerticalOffset} を保存します");
            element.SetCurrentValue(BoundVerticalOffsetProperty, scrollViewer.VerticalOffset);
            // TwoWay Binding の Source（ViewModel）へ確実に反映するため UpdateSource() を明示呼び出しする
            BindingOperations.GetBindingExpression(element, BoundVerticalOffsetProperty)?.UpdateSource();
        }
    }

    /// <summary>
    /// VisualTree を再帰的に辿り、子孫の ScrollViewer をすべて返します。
    /// </summary>
    /// <param name="parent">探索を開始する <see cref="DependencyObject"/>。</param>
    /// <returns>見つかった <see cref="ScrollViewer"/> のリスト。</returns>
    private static List<ScrollViewer> FindScrollViewers(DependencyObject parent)
    {
        var result = new List<ScrollViewer>();
        findScrollViewersCore(parent, result);
        return result;
    }

    /// <summary>
    /// VisualTree を再帰的に辿り、見つかった ScrollViewer を result に追加します。
    /// </summary>
    /// <param name="parent">探索する <see cref="DependencyObject"/>。</param>
    /// <param name="result">結果を追加するリスト。</param>
    private static void findScrollViewersCore(DependencyObject parent, List<ScrollViewer> result)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv)
                result.Add(sv);
            findScrollViewersCore(child, result);
        }
    }

    /// <summary>
    /// ScrollViewer の VisualTree を遡り、BoundVerticalOffset 添付プロパティが設定された
    /// <see cref="FrameworkElement"/> を返します。
    /// </summary>
    /// <param name="obj">探索を開始する <see cref="DependencyObject"/>。</param>
    /// <returns>該当する <see cref="FrameworkElement"/>。見つからない場合は <c>null</c>。</returns>
    private static FrameworkElement? FindOwnerElement(DependencyObject obj)
    {
        var current = VisualTreeHelper.GetParent(obj);
        while (current is not null)
        {
            if (current is FrameworkElement fe &&
                fe.ReadLocalValue(BoundVerticalOffsetProperty) != DependencyProperty.UnsetValue)
                return fe;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
