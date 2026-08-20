using R3;
using System.Reflection;
using Wpf.Ui.Controls;

namespace MangaBinder.Series;

/// <summary>
/// 新規作品登録タイトル入力ダイアログコンテンツの ViewModel です。
/// </summary>
public class NewSeriesTitleDialogContentViewModel : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>タイトル検索を担う Manager。</summary>
	private readonly MangaSeriesManager mangaSeriesManager;

	/// <summary>入力中のタイトルを取得します。</summary>
	public BindableReactiveProperty<string> Title { get; }

	/// <summary>説明文表示用のアプリケーション名を取得します。</summary>
	public string ApplicationName { get; }

	/// <summary>InfoBar のエラーメッセージを取得します。</summary>
	public BindableReactiveProperty<string> ErrorMessage { get; }

	/// <summary>InfoBar を表示するかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> IsErrorMessageVisible { get; }

	/// <summary>ダイアログ確定後の新規 MangaSeries を取得します。</summary>
	public MangaSeries? ConfirmedSeries { get; private set; }

	/// <summary>
	/// <see cref="NewSeriesTitleDialogContentViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="mangaSeriesManager">タイトル検索を担う Manager。</param>
	public NewSeriesTitleDialogContentViewModel(MangaSeriesManager mangaSeriesManager)
	{
		this.disposableBag = default;
		this.mangaSeriesManager = mangaSeriesManager;

		// Assembly から製品名を取得
		var assemblyName = Assembly.GetEntryAssembly()?.GetName();
		this.ApplicationName = assemblyName?.Name ?? "MangaBinder";

		this.Title = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.ErrorMessage = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.IsErrorMessageVisible = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// ContentDialog の Closing イベントを処理します。
	/// </summary>
	/// <param name="sender">イベント送信元（ContentDialog）。</param>
	/// <param name="e">Closing イベント引数。</param>
	public void HandleDialogClosing(ContentDialog sender, ContentDialogClosingEventArgs e)
	{
		// CloseButton（キャンセル）によるクローズの場合は何もしない
		if (e.Result != ContentDialogResult.Primary)
		{
			return;
		}

		// Primary（作品編集開始）によるクローズの場合のみ、タイトル判定と検索を実行

		// 判定前に前回の InfoBar をクリア
		this.IsErrorMessageVisible.Value = false;
		this.ErrorMessage.Value = string.Empty;

		var titleValue = this.Title.Value ?? string.Empty;

		// タイトル未入力判定
		if (string.IsNullOrWhiteSpace(titleValue))
		{
			// タイトルが未入力の場合
			this.ErrorMessage.Value = "タイトルを入力してください。";
			this.IsErrorMessageVisible.Value = true;

			// Closing をキャンセルしてダイアログを表示したままにする
			e.Cancel = true;
			return;
		}

		// タイトルが入力されている場合、同一タイトルを検索
		var sameSeriesList = this.mangaSeriesManager.FindSameTitle(titleValue);

		// 検索結果によって分岐
		switch (sameSeriesList.Count)
		{
			case 0:
				// 検索結果0件：新規作品として進める
				this.ConfirmedSeries = new MangaSeries
				{
					Title = titleValue
				};

				// ダイアログを正常に閉じる（Closing をキャンセルしない）
				break;

			case 1:
				// 検索結果1件：今回は未実装
				// Closing をキャンセルしてダイアログを表示したままにする
				// 次段階で ExistingSeriesDialogContent へ切り替える予定
				e.Cancel = true;
				break;

			default:
				// 検索結果2件以上：エラー表示してダイアログを閉じない
				this.ErrorMessage.Value = "同じタイトルの作品が複数見つかりました。";
				this.IsErrorMessageVisible.Value = true;

				// Closing をキャンセルしてダイアログを表示したままにする
				e.Cancel = true;
				break;
		}
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
