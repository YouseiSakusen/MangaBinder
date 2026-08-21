using R3;
using System.Reflection;

namespace MangaBinder.Series;

/// <summary>
/// 新規作品登録タイトル入力ダイアログコンテンツの ViewModel です。
/// タイトル入力画面の UI 状態を保持します。
/// </summary>
public class NewSeriesTitleDialogContentViewModel : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>入力中のタイトルを取得します。</summary>
	public BindableReactiveProperty<string> Title { get; }

	/// <summary>説明文表示用のアプリケーション名を取得します。</summary>
	public string ApplicationName { get; }

	/// <summary>InfoBar のエラーメッセージを取得します。</summary>
	public BindableReactiveProperty<string> ErrorMessage { get; }

	/// <summary>InfoBar を表示するかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> IsErrorMessageVisible { get; }

	/// <summary>タイトル入力フィールドへのフォーカス＆全選択をリクエストするカウンタを取得します。</summary>
	public BindableReactiveProperty<int> TitleInputFocusRequest { get; }

	/// <summary>
	/// <see cref="NewSeriesTitleDialogContentViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	public NewSeriesTitleDialogContentViewModel()
	{
		this.disposableBag = default;

		// Assembly から製品名を取得
		var assemblyName = Assembly.GetEntryAssembly()?.GetName();
		this.ApplicationName = assemblyName?.Name ?? "MangaBinder";

		this.Title = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.ErrorMessage = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.IsErrorMessageVisible = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.TitleInputFocusRequest = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
