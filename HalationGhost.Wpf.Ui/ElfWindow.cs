using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Controls;

namespace HalationGhost.Wpf.Ui;

/// <summary>
/// ウィンドウ位置・サイズ保存機能を備えた共通基底ウィンドウクラス。
/// FluentWindow を拡張し、ウィンドウの位置・サイズ・最大化状態を自動的に保存・復元します。
/// </summary>
public class ElfWindow : FluentWindow
{
	private readonly WindowPlacementRepository repository = new();
	private string windowPlacementFilePath = string.Empty;

	/// <summary>
	/// ウィンドウ位置・サイズの保存と復元を有効にするかどうかを取得または設定します。
	/// </summary>
	[Category("Elf Window")]
	[Description("ウィンドウ位置・サイズの保存と復元を有効にします。")]
	[DefaultValue(true)]
	public bool IsWindowPlacementEnabled { get; init; } = true;

	/// <summary>
	/// ウィンドウ位置・サイズ保存ファイルの保存先を設定します。
	/// </summary>
	/// <remarks>
	/// <para>
	/// このメソッドをコンストラクタで呼び出してください。
	/// </para>
	/// <para>
	/// filePath が空文字の場合は保存・復元機能は無効になります。
	/// </para>
	/// </remarks>
	/// <param name="filePath">保存先ファイルパス。空文字の場合は機能を無効にします。</param>
	protected void ConfigureWindowPlacement(string filePath)
	{
		this.windowPlacementFilePath = filePath;
	}

	/// <summary>
	/// ウィンドウの初期化が完了したときに呼ばれます。
	/// ここでウィンドウ位置・サイズの復元を行います。
	/// </summary>
	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);

		if (!this.IsWindowPlacementEnabled || string.IsNullOrEmpty(this.windowPlacementFilePath))
		{
			return;
		}

		this.RestoreWindowPlacement();
	}

	/// <summary>
	/// ウィンドウが閉じられるときに呼ばれます。
	/// ここでウィンドウ位置・サイズの保存を行います。
	/// </summary>
	protected override void OnClosing(CancelEventArgs e)
	{
		base.OnClosing(e);

		if (!this.IsWindowPlacementEnabled || string.IsNullOrEmpty(this.windowPlacementFilePath))
		{
			return;
		}

		this.SaveWindowPlacement();
	}

	/// <summary>
	/// ウィンドウ位置・サイズ情報を保存ファイルから復元します。
	/// 復元ファイルが存在しない場合や破損している場合は何もしません。
	/// </summary>
	private void RestoreWindowPlacement()
	{
		try
		{
			var placement = this.repository.Load(this.windowPlacementFilePath);
			if (placement == null)
			{
				// ファイルが存在しない = 初回起動 = 正常系
				return;
			}

			// ウィンドウの位置・サイズを復元
			this.Left = placement.Left;
			this.Top = placement.Top;
			this.Width = placement.Width;
			this.Height = placement.Height;

			// ウィンドウの状態を復元
			// Maximized の場合は最後に状態を設定する
			if (placement.WindowState == WindowState.Maximized)
			{
				this.WindowState = WindowState.Maximized;
			}
			else if (placement.WindowState == WindowState.Normal)
			{
				this.WindowState = WindowState.Normal;
			}
			// Minimized は起動時の状態として復元しない
		}
		catch
		{
			// 復元失敗（ファイル破損など）= 初回起動扱い = 何もしない
		}
	}

	/// <summary>
	/// ウィンドウの現在の位置・サイズ情報を保存ファイルに保存します。
	/// 保存に失敗した場合は何もしません。
	/// </summary>
	private void SaveWindowPlacement()
	{
		try
		{
			var placement = new WindowPlacement
			{
				Left = this.Left,
				Top = this.Top,
				Width = this.Width,
				Height = this.Height,
				WindowState = this.WindowState
			};

			// Maximized 状態の場合は RestoreBounds を保存する
			if (this.WindowState == WindowState.Maximized && this.RestoreBounds != default)
			{
				placement.Left = this.RestoreBounds.Left;
				placement.Top = this.RestoreBounds.Top;
				placement.Width = this.RestoreBounds.Width;
				placement.Height = this.RestoreBounds.Height;
				placement.WindowState = WindowState.Maximized;
			}
			// Minimized 状態の場合は RestoreBounds を Normal として保存する
			else if (this.WindowState == WindowState.Minimized && this.RestoreBounds != default)
			{
				placement.Left = this.RestoreBounds.Left;
				placement.Top = this.RestoreBounds.Top;
				placement.Width = this.RestoreBounds.Width;
				placement.Height = this.RestoreBounds.Height;
				placement.WindowState = WindowState.Normal;
			}

			this.repository.Save(placement, this.windowPlacementFilePath);
		}
		catch
		{
			// 保存失敗 = 次回起動時は復元されない = 許容範囲
		}
	}
}
