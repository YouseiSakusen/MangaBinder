using System;
using System.IO;
using MangaBinder.Bindings;
using Microsoft.Extensions.Configuration;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using HalationGhost.Wpf.Ui;

namespace MangaBinder;

/// <summary>
/// メインウィンドウのコードビハインドです。
/// </summary>
public partial class MainWindow : ElfWindow
{
	/// <summary>
	/// <see cref="MainWindow"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="viewModel">DI から注入される ViewModel。</param>
	/// <param name="navigationViewPageProvider">ページプロバイダーサービス。</param>
	/// <param name="snackbarService">スナックバーサービス。</param>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	/// <param name="configuration">設定。</param>
	public MainWindow(
		MainWindowViewModel viewModel,
		INavigationViewPageProvider navigationViewPageProvider,
		ISnackbarService snackbarService,
		IContentDialogService contentDialogService,
		IConfiguration configuration)
	{
		System.Diagnostics.Debug.WriteLine(">>> MainWindow コンストラクタ開始");

		// ウィンドウ位置・サイズ保存機能の設定
		var fileName = configuration["WindowPlacement:FileName"] ?? "window-placement.json";
		var directory = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"HalationGhost",
			"MangaBinder");
		Directory.CreateDirectory(directory);
		var filePath = Path.Combine(directory, fileName);
		this.ConfigureWindowPlacement(filePath);

		this.DataContext = viewModel;
		System.Diagnostics.Debug.WriteLine(">>> MainWindow InitializeComponent 直前");
		this.InitializeComponent();
		System.Diagnostics.Debug.WriteLine("<<< MainWindow InitializeComponent 直後");
		System.Diagnostics.Debug.WriteLine(">>> MainWindow SetPageService 直前");
		this.SetPageService(navigationViewPageProvider);
		System.Diagnostics.Debug.WriteLine("<<< MainWindow SetPageService 直後");
		snackbarService.SetSnackbarPresenter(this.SnackbarPresenter);
		contentDialogService.SetDialogHost(this.RootContentDialog);

		System.Diagnostics.Debug.WriteLine("<<< MainWindow コンストラクタ終了");
	}
}
