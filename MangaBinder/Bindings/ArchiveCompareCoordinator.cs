using System.Windows;
using System.Windows.Controls;
using MangaBinder.Controls;
using R3;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Bindings;

/// <summary>
/// 圧縮ファイル比較機能のDialog表示をオーケストレーションするコーディネーター。
/// 圧縮ファイル比較用Dialogの表示処理を担当します。
/// </summary>
public class ArchiveCompareCoordinator
{
	/// <summary>コンテントダイアログサービス。</summary>
	private readonly IContentDialogService contentDialogService;

	/// <summary>製本工程の正本状態ストア。</summary>
	private readonly BindingStore bindingStore;

	/// <summary>
	/// <see cref="ArchiveCompareCoordinator"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	/// <param name="bindingStore">製本工程の正本状態ストア。</param>
	public ArchiveCompareCoordinator(IContentDialogService contentDialogService, BindingStore bindingStore)
	{
		this.contentDialogService = contentDialogService;
		this.bindingStore = bindingStore;
	}

	/// <summary>
	/// 圧縮ファイル比較Dialog を表示します。
	/// </summary>
	/// <returns>非同期操作を表す ValueTask。</returns>
	public async ValueTask StartAsync()
	{
		// BindingStore.Materials から Root.Children 直下のアーカイブのみを抽出
		var archiveCandidates = this.bindingStore.Materials
			.SelectMany(root => root.Children)
			.Where(item => item.ItemType == MaterialItemType.Archive)
			.ToList();

		// ArchiveCompareDialogContentViewModel を生成
		var viewModel = new ArchiveCompareDialogContentViewModel(archiveCandidates);

		// MainWindow から Content サイズを算出
		var mainWindow = Application.Current?.MainWindow;
		double contentWidth;
		double contentHeight;

		if (mainWindow is not null)
		{
			contentWidth = mainWindow.ActualWidth * 4 / 5;
			contentHeight = mainWindow.ActualHeight * 0.53;
		}
		else
		{
			// fallback 値
			contentWidth = 1200;
			contentHeight = double.NaN;
		}

		// ArchiveCompareDialogContent を生成
		// 初期表示時は Height を明示的に設定しない（Auto相当）
		var archiveCompareContent = new ArchiveCompareDialogContent
		{
			Width = contentWidth,
			MinWidth = 720,
			// ViewModel を DataContext に設定
			DataContext = viewModel,
		};

		// CompareCommand を購読し、Dialog表示期間だけ比較処理を実行
		// Content 生成後に購読することで、比較実行時に archiveCompareContent を参照可能にする
		using var compareSubscription = viewModel.CompareCommand.Subscribe(_ =>
		{
			var left = viewModel.LeftArchive.Value!;
			var right = viewModel.RightArchive.Value!;
			var leftItems = GetComparisonItems(left);
			var rightItems = GetComparisonItems(right);

			// 入力中の除外文字列を Applied... に反映
			viewModel.AppliedLeftExcludedText.Value = viewModel.LeftExcludedText.Value;
			viewModel.AppliedRightExcludedText.Value = viewModel.RightExcludedText.Value;

			// 除外文字列を適用して比較を実行
			var comparisonChildren = compare(
				leftItems,
				rightItems,
				viewModel.AppliedLeftExcludedText.Value,
				viewModel.AppliedRightExcludedText.Value);

			// 比較結果をArchiveルートNodeで包む
			var root = new ArchiveCompareNode(left, right, comparisonChildren);
			viewModel.CompareResult.Value = new[] { root };

			// 比較結果表示領域を表示
			viewModel.IsResultVisible.Value = true;

			// 比較結果が表示されるタイミングで Content の高さを固定値に設定
			archiveCompareContent.Height = contentHeight;
		});

		// ContentDialog を生成
		var dialog = new ContentDialog
		{
			Title = "圧縮ファイル比較",
			Content = archiveCompareContent,
			CloseButtonText = "閉じる",
			DialogMaxWidth = double.PositiveInfinity,
		};

		// ContentDialog の外側 ScrollViewer の垂直スクロールバーを無効化
		// これにより、上部UIは固定されたまま、比較結果TreeViewだけがスクロール可能になる
		ScrollViewer.SetVerticalScrollBarVisibility(dialog, ScrollBarVisibility.Disabled);

		// Dialog を表示
		await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);

		// Dialog が閉じられたら ViewModel を破棄
		viewModel.Dispose();
	}

	/// <summary>
	/// 同じ親階層の左右Childrenを Name の完全一致で対応付け、比較Treeを生成します。
	/// 同名ノードが左右に存在する場合のみ Children 同士を再帰比較します。
	/// </summary>
	private static IEnumerable<MaterialItem> GetComparisonItems(MaterialItem archive)
	{
		var children = archive.Children;

		// Archive直下に1個だけFolder が存在する場合、そのFolderをラッパーとして扱い、
		// そのFolder.Children を比較対象として返す
		if (children.Count == 1 && children[0].ItemType == MaterialItemType.Folder)
		{
			return children[0].Children;
		}

		// それ以外の場合は、Archive自身のChildren をそのまま返す
		return children;
	}

	/// <summary>
	/// 同じ親階層の左右Childrenを Name の完全一致で対応付け、比較Treeを生成します。
	/// 同名ノードが左右に存在する場合のみ Children 同士を再帰比較します。
	/// </summary>
	private static IReadOnlyList<ArchiveCompareNode> compare(
		IEnumerable<MaterialItem> leftItems,
		IEnumerable<MaterialItem> rightItems)
	{
		// 同一階層の同名重複は想定外データのため、Add / ToDictionary の ArgumentException で即座に検知する
		var leftByName = leftItems.ToDictionary(x => x.Name, StringComparer.Ordinal);
		var rightByName = rightItems.ToDictionary(x => x.Name, StringComparer.Ordinal);

		var pairs = new Dictionary<string, (MaterialItem? Left, MaterialItem? Right)>(StringComparer.Ordinal);

		foreach (var left in leftByName.Values)
		{
			pairs.Add(left.Name, (left, null));
		}

		foreach (var right in rightByName.Values)
		{
			if (pairs.TryGetValue(right.Name, out var pair))
			{
				pairs[right.Name] = (pair.Left, right);
			}
			else
			{
				pairs.Add(right.Name, (null, right));
			}
		}

		return pairs
			.OrderBy(x => x.Key)
			.Select(x => new ArchiveCompareNode(
				x.Value.Left,
				x.Value.Right,
				compare(
					x.Value.Left?.Children ?? Enumerable.Empty<MaterialItem>(),
					x.Value.Right?.Children ?? Enumerable.Empty<MaterialItem>())))
			.ToList();
	}

	/// <summary>
	/// 左右の除外文字列を考慮して比較Treeを生成します。
	/// 同じ親階層の左右Childrenを、除外文字列を適用した Name で対応付けます。
	/// </summary>
	private static IReadOnlyList<ArchiveCompareNode> compare(
		IEnumerable<MaterialItem> leftItems,
		IEnumerable<MaterialItem> rightItems,
		string leftExcludedText,
		string rightExcludedText)
	{
		// 同一階層の同名重複は想定外データのため、Add / ToDictionary の ArgumentException で即座に検知する
		var leftByKey = leftItems.ToDictionary(
			x => GenerateComparisonKey(x.Name, leftExcludedText),
			StringComparer.Ordinal);
		var rightByKey = rightItems.ToDictionary(
			x => GenerateComparisonKey(x.Name, rightExcludedText),
			StringComparer.Ordinal);

		var pairs = new Dictionary<string, (MaterialItem? Left, MaterialItem? Right)>(StringComparer.Ordinal);

		foreach (var (key, left) in leftByKey)
		{
			pairs.Add(key, (left, null));
		}

		foreach (var (key, right) in rightByKey)
		{
			if (pairs.TryGetValue(key, out var pair))
			{
				pairs[key] = (pair.Left, right);
			}
			else
			{
				pairs.Add(key, (null, right));
			}
		}

		return pairs
			.OrderBy(x => x.Key)
			.Select(x => new ArchiveCompareNode(
				x.Value.Left,
				x.Value.Right,
				compare(
					x.Value.Left?.Children ?? Enumerable.Empty<MaterialItem>(),
					x.Value.Right?.Children ?? Enumerable.Empty<MaterialItem>(),
					leftExcludedText,
					rightExcludedText)))
			.ToList();
	}

	/// <summary>
	/// MaterialItem の Name から除外文字列を除外した比較キーを生成します。
	/// </summary>
	/// <param name="name">元の MaterialItem.Name。</param>
	/// <param name="excludedText">除外する文字列。null または空文字列の場合は name をそのまま返す。</param>
	/// <returns>
	/// 除外文字列が null / 空文字列の場合は name をそのまま返す。
	/// そうでない場合は、name 内の除外文字列と完全一致する部分をすべて除外したテキストを返す。
	/// </returns>
	private static string GenerateComparisonKey(string name, string excludedText)
	{
		if (string.IsNullOrEmpty(excludedText))
		{
			return name;
		}

		// StringComparison.Ordinal 相当で、除外文字列をすべて削除
		return name.Replace(excludedText, string.Empty, StringComparison.Ordinal);
	}
}
