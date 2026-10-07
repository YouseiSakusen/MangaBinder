using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MangaBinder.Controls;

/// <summary>
/// BitmapSource を表示し、分割位置・トリミング位置のガイドを直接操作できるプレビュー部品。
/// </summary>
public partial class ImageSplitterPreview : UserControl
{
	private enum GuideKind
	{
		TrimLeft,
		TrimRight,
		TrimTop,
		TrimBottom,
		Split,
	}

	private const double HitThickness = 12;
	/// <summary>実画像基準の通常最大倍率。</summary>
	private const double MaxZoom = 2.0;
	/// <summary>ホイール1ノッチあたりの倍率係数。</summary>
	private const double WheelZoomStep = 1.1;
	private static readonly string[] zoomPresets = ["Fit", "100%", "125%", "150%", "200%"];

	/// <summary>Fit 表示かどうか。</summary>
	private bool isFit = true;
	/// <summary>Fit 以外の場合の実画像基準の倍率。</summary>
	private double zoom = 1.0;
	/// <summary>中央配置からの Pan オフセット。</summary>
	private double panX;
	private double panY;
	private bool isPanning;
	private Point panStart;
	private double panStartX;
	private double panStartY;
	private bool updatingZoomCombo;
	/// <summary>通常状態の Trim 領域の不透明度。</summary>
	private const double TrimOverlayOpacity = 0.30;
	/// <summary>選択中の Trim 領域の不透明度。</summary>
	private const double SelectedTrimOverlayOpacity = 0.45;

	public static readonly DependencyProperty SourceProperty =
		DependencyProperty.Register(nameof(Source), typeof(BitmapSource), typeof(ImageSplitterPreview),
			new PropertyMetadata(null, onSourceChanged));

	public static readonly DependencyProperty SplitOffsetProperty = registerPosition(nameof(SplitOffset));
	public static readonly DependencyProperty TrimLeftProperty = registerPosition(nameof(TrimLeft));
	public static readonly DependencyProperty TrimTopProperty = registerPosition(nameof(TrimTop));
	public static readonly DependencyProperty TrimRightProperty = registerPosition(nameof(TrimRight));
	public static readonly DependencyProperty TrimBottomProperty = registerPosition(nameof(TrimBottom));

	/// <summary>ガイド種別ごとのヒット判定域と線（分割線のみ）を保持する辞書。</summary>
	private readonly Dictionary<GuideKind, (Grid Hit, Rectangle? Line)> guides = new();
	/// <summary>Trim 種別ごとの塗り領域を保持する辞書。</summary>
	private readonly Dictionary<GuideKind, Rectangle> overlays = new();
	/// <summary>現在選択中のガイド。</summary>
	private GuideKind? selected;
	/// <summary>現在ドラッグ中のガイド。</summary>
	private GuideKind? dragging;

	public BitmapSource? Source
	{
		get => (BitmapSource?)GetValue(SourceProperty);
		set => SetValue(SourceProperty, value);
	}

	/// <summary>トリミング後の有効領域中央を 0 とした分割位置のずれ（px、右が正）を取得または設定します。</summary>
	public int SplitOffset
	{
		get => (int)GetValue(SplitOffsetProperty);
		set => SetValue(SplitOffsetProperty, value);
	}

	public int TrimLeft
	{
		get => (int)GetValue(TrimLeftProperty);
		set => SetValue(TrimLeftProperty, value);
	}

	public int TrimTop
	{
		get => (int)GetValue(TrimTopProperty);
		set => SetValue(TrimTopProperty, value);
	}

	public int TrimRight
	{
		get => (int)GetValue(TrimRightProperty);
		set => SetValue(TrimRightProperty, value);
	}

	public int TrimBottom
	{
		get => (int)GetValue(TrimBottomProperty);
		set => SetValue(TrimBottomProperty, value);
	}

	public ImageSplitterPreview()
	{
		InitializeComponent();

		foreach (var preset in zoomPresets)
		{
			this.ZoomComboBox.Items.Add(preset);
		}

		foreach (var kind in Enum.GetValues<GuideKind>().Where(k => k != GuideKind.Split))
		{
			var overlay = new Rectangle
			{
				IsHitTestVisible = false,
				Opacity = TrimOverlayOpacity,
			};
			overlay.SetResourceReference(Shape.FillProperty, "AccentFillColorDefaultBrush");
			this.GuideCanvas.Children.Add(overlay);
			this.overlays[kind] = overlay;
		}

		foreach (var kind in Enum.GetValues<GuideKind>())
		{
			var vertical = isVertical(kind);
			Rectangle? line = null;
			if (kind == GuideKind.Split)
			{
				line = new Rectangle
				{
					IsHitTestVisible = false,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Stretch,
					Width = 1,
				};
				line.SetResourceReference(Shape.FillProperty, "SystemFillColorCriticalBrush");
			}

			var hit = new Grid
			{
				Background = Brushes.Transparent,
				Cursor = vertical ? Cursors.SizeWE : Cursors.SizeNS,
				Tag = kind,
			};
			if (line is not null)
			{
				hit.Children.Add(line);
			}
			hit.MouseLeftButtonDown += guideMouseLeftButtonDown;
			hit.MouseMove += guideMouseMove;
			hit.MouseLeftButtonUp += guideMouseLeftButtonUp;
			this.GuideCanvas.Children.Add(hit);
			this.guides[kind] = (hit, line);
		}

		this.updateGuides();
	}

	/// <summary>
	/// 位置を示す DependencyProperty を登録します。
	/// </summary>
	/// <param name="name">プロパティ名。</param>
	/// <returns>登録された DependencyProperty。</returns>
	private static DependencyProperty registerPosition(string name)
		=> DependencyProperty.Register(name, typeof(int), typeof(ImageSplitterPreview),
			new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, onPositionChanged));

	/// <summary>
	/// Source が変更された際のコールバック。
	/// </summary>
	private static void onSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var self = (ImageSplitterPreview)d;
		var src = (BitmapSource?)e.NewValue;
		self.PreviewImage.Source = src;
		self.ImageSizeText.Text = src is null ? string.Empty : $"{src.PixelWidth} × {src.PixelHeight} px";
		self.resetZoomPan();
		self.updateGuides();
	}

	/// <summary>
	/// 新しい画面セッション開始時に、選択中ガイドとドラッグ状態の View 状態を初期状態へ戻します。
	/// SplitOffset / Trim 値は SplitSettings の値のため変更しません。
	/// </summary>
	public void ResetViewState()
	{
		this.selected = null;
		this.dragging = null;
		this.resetZoomPan();
		this.updateGuides();
	}

	/// <summary>
	/// Zoom を Fit、Pan を中央へ戻します。
	/// </summary>
	private void resetZoomPan()
	{
		this.isFit = true;
		this.zoom = 1.0;
		this.panX = 0;
		this.panY = 0;
		this.isPanning = false;
	}

	/// <summary>
	/// 現在の表示倍率をコンボボックスへ反映します。
	/// </summary>
	private void updateZoomCombo(double scale)
	{
		var label = this.isFit ? "Fit" : $"{Math.Round(scale * 100)}%";
		this.updatingZoomCombo = true;
		this.ZoomComboBox.SelectedItem = zoomPresets.Contains(label) ? label : null;
		this.ZoomComboBox.Text = label;
		this.updatingZoomCombo = false;
	}

	/// <summary>
	/// 指定した表示倍率へ変更します。Fit 以下の場合は Fit に戻します。
	/// </summary>
	/// <param name="newScale">実画像基準の新しい倍率。</param>
	/// <param name="anchor">倍率変更の中心となる表示領域内の位置。</param>
	private void applyZoom(double newScale, Point anchor)
	{
		if (!this.tryGetDisplay(out var ox, out var oy, out var scale, out _, out _))
		{
			return;
		}

		var src = this.Source!;
		var aw = this.RootGrid.ActualWidth;
		var ah = this.RootGrid.ActualHeight;
		var fit = Math.Min(aw / src.PixelWidth, ah / src.PixelHeight);
		newScale = Math.Min(newScale, Math.Max(MaxZoom, fit));
		if (newScale <= fit + 1e-6)
		{
			this.resetZoomPan();
			this.updateGuides();
			return;
		}

		var u = (anchor.X - ox) / scale;
		var v = (anchor.Y - oy) / scale;
		this.isFit = false;
		this.zoom = newScale;
		this.panX = anchor.X - u * newScale - (aw - src.PixelWidth * newScale) / 2;
		this.panY = anchor.Y - v * newScale - (ah - src.PixelHeight * newScale) / 2;
		this.updateGuides();
	}

	private void zoomComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (this.updatingZoomCombo || this.ZoomComboBox.SelectedItem is not string item)
		{
			return;
		}

		if (item == "Fit")
		{
			this.resetZoomPan();
			this.updateGuides();
			return;
		}

		var value = double.Parse(item.TrimEnd('%')) / 100;
		this.applyZoom(value, new Point(this.RootGrid.ActualWidth / 2, this.RootGrid.ActualHeight / 2));
	}

	private void rootGridMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (Keyboard.Modifiers != ModifierKeys.Control || !this.tryGetDisplay(out _, out _, out var scale, out _, out _))
		{
			return;
		}

		this.applyZoom(scale * Math.Pow(WheelZoomStep, e.Delta / 120.0), e.GetPosition(this.RootGrid));
		e.Handled = true;
	}

	private void rootGridMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (this.isFit || this.Source is null)
		{
			return;
		}

		this.isPanning = true;
		this.panStart = e.GetPosition(this.RootGrid);
		this.panStartX = this.panX;
		this.panStartY = this.panY;
		this.RootGrid.CaptureMouse();
		e.Handled = true;
	}

	private void rootGridMouseMove(object sender, MouseEventArgs e)
	{
		if (!this.isPanning)
		{
			return;
		}

		var p = e.GetPosition(this.RootGrid);
		this.panX = this.panStartX + p.X - this.panStart.X;
		this.panY = this.panStartY + p.Y - this.panStart.Y;
		this.updateGuides();
	}

	private void rootGridMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (!this.isPanning)
		{
			return;
		}

		this.isPanning = false;
		this.RootGrid.ReleaseMouseCapture();
		e.Handled = true;
	}

	/// <summary>
	/// 位置が変更された際のコールバック。
	/// </summary>
	private static void onPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		=> ((ImageSplitterPreview)d).updateGuides();

	/// <summary>
	/// ガイド種別が縦方向のラインかどうかを判定します。
	/// </summary>
	private static bool isVertical(GuideKind kind) => kind != GuideKind.TrimTop && kind != GuideKind.TrimBottom;

	/// <summary>
	/// コントロールサイズが変更された際のコールバック。
	/// </summary>
	private void userControlSizeChanged(object sender, SizeChangedEventArgs e) => this.updateGuides();

	/// <summary>
	/// Uniform 表示された画像の表示矩形と、元画像 pixel あたりの表示倍率を求めます。
	/// </summary>
	/// <param name="offsetX">表示矩形の左端位置。</param>
	/// <param name="offsetY">表示矩形の上端位置。</param>
	/// <param name="scale">元画像 pixel あたりの表示倍率。</param>
	/// <param name="width">表示矩形の幅。</param>
	/// <param name="height">表示矩形の高さ。</param>
	/// <returns>表示用の有効な値を取得できた場合は true。</returns>
	private bool tryGetDisplay(out double offsetX, out double offsetY, out double scale, out double width, out double height)
	{
		offsetX = offsetY = scale = width = height = 0;
		var src = this.Source;
		var aw = this.RootGrid.ActualWidth;
		var ah = this.RootGrid.ActualHeight;
		if (src is null || src.PixelWidth <= 0 || src.PixelHeight <= 0 || aw <= 0 || ah <= 0)
		{
			return false;
		}

		var fit = Math.Min(aw / src.PixelWidth, ah / src.PixelHeight);
		scale = this.isFit ? fit : Math.Max(this.zoom, fit);
		width = src.PixelWidth * scale;
		height = src.PixelHeight * scale;
		var maxPanX = Math.Max(0, (width - aw) / 2);
		var maxPanY = Math.Max(0, (height - ah) / 2);
		this.panX = Math.Clamp(this.panX, -maxPanX, maxPanX);
		this.panY = Math.Clamp(this.panY, -maxPanY, maxPanY);
		offsetX = (aw - width) / 2 + this.panX;
		offsetY = (ah - height) / 2 + this.panY;
		return true;
	}

	/// <summary>
	/// ガイド種別に対応した値を取得します。
	/// </summary>
	private int getGuideValue(GuideKind kind) => kind switch
	{
		GuideKind.TrimLeft => this.TrimLeft,
		GuideKind.TrimRight => this.TrimRight,
		GuideKind.TrimTop => this.TrimTop,
		GuideKind.TrimBottom => this.TrimBottom,
		_ => this.SplitOffset,
	};

	/// <summary>
	/// ガイド種別に対応した値を設定します。Trim 値は元画像の範囲にクランプされます。SplitOffset はそのまま設定します。
	/// </summary>
	private void setGuideValue(GuideKind kind, int value)
	{
		if (kind == GuideKind.Split)
		{
			this.SplitOffset = value;
			return;
		}

		var src = this.Source!;
		var max = isVertical(kind) ? src.PixelWidth : src.PixelHeight;
		value = Math.Clamp(value, 0, max);
		switch (kind)
		{
			case GuideKind.TrimLeft: this.TrimLeft = value; break;
			case GuideKind.TrimRight: this.TrimRight = value; break;
			case GuideKind.TrimTop: this.TrimTop = value; break;
			case GuideKind.TrimBottom: this.TrimBottom = value; break;
			default: break;
		}
	}

	/// <summary>
	/// 現在画像のトリミング後有効領域の中央（元画像 pixel 座標）を取得します。
	/// </summary>
	private double getEffectiveCenter()
	{
		var left = (double)this.TrimLeft;
		var right = this.Source!.PixelWidth - this.TrimRight;
		return (left + right) / 2;
	}

	/// <summary>
	/// ガイドの元画像座標上の位置を取得します（縦ガイドは x、横ガイドは y）。
	/// </summary>
	private double getGuideCoordinate(GuideKind kind)
	{
		var src = this.Source!;
		return kind switch
		{
			GuideKind.TrimLeft => this.TrimLeft,
			GuideKind.TrimRight => src.PixelWidth - this.TrimRight,
			GuideKind.TrimTop => this.TrimTop,
			GuideKind.TrimBottom => src.PixelHeight - this.TrimBottom,
			_ => this.getEffectiveCenter() + this.SplitOffset,
		};
	}

	/// <summary>
	/// 全ガイドの表示位置と見た目を更新します。
	/// </summary>
	private void updateGuides()
	{
		if (this.guides.Count == 0)
		{
			return;
		}

		var visible = this.tryGetDisplay(out var ox, out var oy, out var scale, out var w, out var h);
		if (visible)
		{
			this.PreviewImage.Width = w;
			this.PreviewImage.Height = h;
			this.PreviewImage.Margin = new Thickness(ox, oy, 0, 0);
			this.updateZoomCombo(scale);
		}

		this.updateOverlays(visible, ox, oy, scale, w, h);
		foreach (var (kind, (hit, _)) in this.guides)
		{
			hit.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

			if (!visible)
			{
				continue;
			}

			var pos = this.getGuideCoordinate(kind) * scale;
			if (isVertical(kind))
			{
				hit.Width = HitThickness;
				hit.Height = h;
				Canvas.SetLeft(hit, ox + pos - HitThickness / 2);
				Canvas.SetTop(hit, oy);
			}
			else
			{
				hit.Width = w;
				hit.Height = HitThickness;
				Canvas.SetLeft(hit, ox);
				Canvas.SetTop(hit, oy + pos - HitThickness / 2);
			}
		}
	}

	/// <summary>
	/// Trim 領域の塗りつぶし矩形の位置・サイズ・不透明度を更新します。
	/// 左右は画像の高さ全体、上下は左右 Trim の内側のみとし、領域が重ならないようにします。
	/// </summary>
	private void updateOverlays(bool visible, double ox, double oy, double scale, double w, double h)
	{
		foreach (var (kind, overlay) in this.overlays)
		{
			overlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
			overlay.Opacity = this.selected == kind ? SelectedTrimOverlayOpacity : TrimOverlayOpacity;
		}

		if (!visible)
		{
			return;
		}

		var left = Math.Min(this.TrimLeft * scale, w);
		var right = Math.Min(this.TrimRight * scale, w - left);
		var innerWidth = w - left - right;
		var top = Math.Min(this.TrimTop * scale, h);
		var bottom = Math.Min(this.TrimBottom * scale, h - top);

		this.placeOverlay(GuideKind.TrimLeft, ox, oy, left, h);
		this.placeOverlay(GuideKind.TrimRight, ox + w - right, oy, right, h);
		this.placeOverlay(GuideKind.TrimTop, ox + left, oy, innerWidth, top);
		this.placeOverlay(GuideKind.TrimBottom, ox + left, oy + h - bottom, innerWidth, bottom);
	}

	/// <summary>
	/// Trim 領域の矩形を指定位置・サイズで配置します。
	/// </summary>
	private void placeOverlay(GuideKind kind, double x, double y, double width, double height)
	{
		var overlay = this.overlays[kind];
		overlay.Width = width;
		overlay.Height = height;
		Canvas.SetLeft(overlay, x);
		Canvas.SetTop(overlay, y);
	}

	/// <summary>
	/// ガイドの左マウスボタンダウン時のコールバック。
	/// </summary>
	private void guideMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		var hit = (Grid)sender;
		var kind = (GuideKind)hit.Tag;
		this.selected = kind;
		this.dragging = kind;
		this.Focus();
		Keyboard.Focus(this);
		hit.CaptureMouse();
		this.updateGuides();
		e.Handled = true;
	}

	/// <summary>
	/// ガイドのマウス移動時のコールバック。
	/// </summary>
	private void guideMouseMove(object sender, MouseEventArgs e)
	{
		if (this.dragging is not { } kind || !this.tryGetDisplay(out var ox, out var oy, out var scale, out _, out _))
		{
			return;
		}

		var p = e.GetPosition(this.GuideCanvas);
		var src = this.Source!;
		var max = isVertical(kind) ? src.PixelWidth : src.PixelHeight;
		var value = (int)Math.Round(isVertical(kind) ? (p.X - ox) / scale : (p.Y - oy) / scale);
		value = Math.Clamp(value, 0, max);

		if (kind is GuideKind.TrimRight or GuideKind.TrimBottom)
		{
			value = max - value;
		}

		if (kind == GuideKind.Split)
		{
			value = (int)Math.Round(value - this.getEffectiveCenter());
		}

		this.setGuideValue(kind, value);
	}

	/// <summary>
	/// ガイドの左マウスボタンアップ時のコールバック。
	/// </summary>
	private void guideMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		this.dragging = null;
		((Grid)sender).ReleaseMouseCapture();
		e.Handled = true;
	}

	/// <summary>
	/// キー押下時の処理。矢印キーでガイドを移動できます。
	/// </summary>
	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (this.selected is not { } kind || this.Source is null)
		{
			return;
		}

		var delta = isVertical(kind)
			? e.Key switch { Key.Left => -1, Key.Right => 1, _ => 0 }
			: e.Key switch { Key.Up => -1, Key.Down => 1, _ => 0 };

		if (delta == 0)
		{
			return;
		}

		if (kind is GuideKind.TrimRight or GuideKind.TrimBottom)
		{
			delta = -delta;
		}

		this.setGuideValue(kind, this.getGuideValue(kind) + delta);
		e.Handled = true;
	}
}
