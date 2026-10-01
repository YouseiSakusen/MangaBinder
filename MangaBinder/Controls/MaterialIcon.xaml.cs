using System.Windows;
using System.Windows.Controls;
using MangaBinder.Bindings;
using Wpf.Ui.Controls;

namespace MangaBinder.Controls;

/// <summary>
/// 素材種別を表すアイコンを表示するUserControlです。
/// <see cref="ItemType"/> により、Folder / Archive / EPUB / Root のアイコンと色が切り替わります。
/// </summary>
public partial class MaterialIcon : UserControl
{
	/// <summary>
	/// 素材種別を指定するDependencyPropertyです。
	/// </summary>
	public static readonly DependencyProperty ItemTypeProperty =
		DependencyProperty.Register(
			nameof(ItemType),
			typeof(MaterialItemType),
			typeof(MaterialIcon),
			new PropertyMetadata(MaterialItemType.Folder, OnItemTypeChanged));

	/// <summary>
	/// アイコンの Filled 属性を指定するDependencyPropertyです。
	/// </summary>
	public static readonly DependencyProperty FilledProperty =
		DependencyProperty.Register(
			nameof(Filled),
			typeof(bool),
			typeof(MaterialIcon),
			new PropertyMetadata(true, OnFilledChanged));

	/// <summary>
	/// 素材種別を取得または設定します。
	/// </summary>
	public MaterialItemType ItemType
	{
		get => (MaterialItemType)GetValue(ItemTypeProperty);
		set => SetValue(ItemTypeProperty, value);
	}

	/// <summary>
	/// アイコンの Filled 属性を取得または設定します。
	/// </summary>
	public bool Filled
	{
		get => (bool)GetValue(FilledProperty);
		set => SetValue(FilledProperty, value);
	}

	public MaterialIcon()
	{
		InitializeComponent();
	}

	/// <summary>
	/// ItemType が変更された際の処理です。
	/// アイコンと色をItemTypeに応じて切り替えます。
	/// Filled は ItemType では設定せず、Filled DependencyProperty から取得します。
	/// </summary>
	private static void OnItemTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not MaterialIcon control || e.NewValue is not MaterialItemType itemType)
			return;

		var icon = control.IconElement;

		switch (itemType)
		{
			case MaterialItemType.Root:
				icon.Symbol = SymbolRegular.Folder24;
				icon.SetResourceReference(ForegroundProperty, "MaterialRootIconBrush");
				break;

			case MaterialItemType.Folder:
				icon.Symbol = SymbolRegular.FolderOpen24;
				icon.SetResourceReference(ForegroundProperty, "MaterialFolderIconBrush");
				break;

			case MaterialItemType.Archive:
				icon.Symbol = SymbolRegular.Archive24;
				icon.SetResourceReference(ForegroundProperty, "MaterialArchiveIconBrush");
				break;

			case MaterialItemType.Epub:
				icon.Symbol = SymbolRegular.Book24;
				icon.SetResourceReference(ForegroundProperty, "MaterialEpubIconBrush");
				break;

			default:
				throw new ArgumentOutOfRangeException(
					nameof(itemType),
					itemType,
					$"MaterialIcon does not support MaterialItemType '{itemType}'. Supported types are: Root, Folder, Archive, Epub.");
		}
	}

	/// <summary>
	/// Filled が変更された際の処理です。
	/// SymbolIcon の Filled 属性を更新します。
	/// </summary>
	private static void OnFilledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not MaterialIcon control || e.NewValue is not bool filled)
			return;

		control.IconElement.Filled = filled;
	}
}

