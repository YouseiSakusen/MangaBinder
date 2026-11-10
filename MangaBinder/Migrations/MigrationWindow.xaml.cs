// このウィンドウに限りコードビハインドを許可する。通常のWPF画面ではコードビハインドを使用しない。
using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Controls;

namespace MangaBinder.Migrations;

/// <summary>
/// マイグレーション実行中にユーザーへ起動準備中であることを通知するウィンドウです。
/// </summary>
public partial class MigrationWindow : FluentWindow
{
	private readonly MigrationService migrationService;

	// 処理完了後にだけウィンドウを閉じられるようにするためのフラグ
	private bool canClose;

	/// <summary>マイグレーション中に発生した例外を取得します。成功時は null です。</summary>
	public Exception? Error { get; private set; }

	/// <summary>
	/// <see cref="MigrationWindow"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="migrationService">マイグレーションサービス。</param>
	public MigrationWindow(MigrationService migrationService)
	{
		this.migrationService = migrationService;
		this.InitializeComponent();
		this.Loaded += this.onLoaded;
	}

	/// <summary>
	/// 表示後にマイグレーションを実行し、完了メッセージを表示してウィンドウを閉じます。
	/// 失敗時は例外を保持して閉じ、呼び出し元で処理します。
	/// </summary>
	private async void onLoaded(object sender, RoutedEventArgs e)
	{
		try
		{
			await this.migrationService.ExecuteAsync();
			this.MessageTextBlock.Text = "データベースのバージョンアップが完了しました！MangaBinderを起動します！";
			await Task.Delay(1000);
		}
		catch (Exception ex)
		{
			this.Error = ex;
		}

		this.canClose = true;
		this.Close();
	}

	/// <summary>
	/// マイグレーション中にウィンドウが閉じられないようにします。
	/// </summary>
	protected override void OnClosing(CancelEventArgs e)
	{
		if (!this.canClose)
			e.Cancel = true;

		base.OnClosing(e);
	}
}
