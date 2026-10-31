using System.IO;
using HalationGhost.Wpf.Ui;
using MangaBinder.Controls;
using MangaBinder.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using ObservableCollections;
using R3;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Settings;

/// <summary>
/// 設定ページの ViewModel です。
/// </summary>
public class SettingsPageViewModel : IDisposable, ISavable, ILeavable, IDataInitializable
{
	private readonly AppSettings appSettings;
	private readonly IContentDialogService contentDialogService;
	private readonly IServiceScopeFactory serviceScopeFactory;
	private DisposableBag disposableBag;

	/// <summary>画面遷移前の <see cref="CanLeaveAsync"/> で保存済みかどうかを示します。true の場合、直後の <see cref="SaveAsync"/> は二重保存を避けて保存を行いません。</summary>
	private bool isSavedOnLeave;

	/// <summary>作業用一時フォルダのパスを取得します。</summary>
	public BindableReactiveProperty<string> WorkFolderPath => this.appSettings.WorkFolderPath;

	/// <summary>製本後ファイル名の著者名左括弧を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipAuthorLeftBracket => this.appSettings.BindingZipAuthorLeftBracket;

	/// <summary>製本後ファイル名の著者名右括弧を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipAuthorRightBracket => this.appSettings.BindingZipAuthorRightBracket;

	/// <summary>製本後ファイル名の著者名とタイトルの区切り文字を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipNameSeparator => this.appSettings.BindingZipNameSeparator;

	/// <summary>製本後ファイル名の通常巻数の接頭辞を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipNormalVolumePrefix => this.appSettings.BindingZipNormalVolumePrefix;

	/// <summary>製本後ファイル名の通常巻数の区切り文字を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipNormalVolumeSeparator => this.appSettings.BindingZipNormalVolumeSeparator;

	/// <summary>製本後ファイル名の通常巻数の接尾辞を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipNormalVolumeSuffix => this.appSettings.BindingZipNormalVolumeSuffix;

	/// <summary>製本後ファイル名の完結を表す文字を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipCompleteVolumePrefix => this.appSettings.BindingZipCompleteVolumePrefix;

	/// <summary>製本後ファイル名の完結巻数の接尾辞を取得します。</summary>
	public BindableReactiveProperty<string> BindingZipCompleteVolumeSuffix => this.appSettings.BindingZipCompleteVolumeSuffix;

	/// <summary>完結を表す文字の位置を取得します。</summary>
	public BindableReactiveProperty<CompleteMarkerPosition> BindingZipCompleteMarkerPosition => this.appSettings.BindingZipCompleteMarkerPosition;

	/// <summary>開始巻～完結表記を使用するかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> BindingZipUsePartialCompleteMarker => this.appSettings.BindingZipUsePartialCompleteMarker;

	/// <summary>完結巻数をゼロ埋めするかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> BindingZipCompleteVolumeZeroPadding => this.appSettings.BindingZipCompleteVolumeZeroPadding;

	/// <summary>製本後アーカイブの既定拡張子を取得します。</summary>
	public BindableReactiveProperty<string> BindingDefaultArchiveExtension => this.appSettings.BindingDefaultArchiveExtension;

	/// <summary>初回設定の自動保存案内 InfoBar を表示するかどうかを取得します。初回設定がDB保存されると自動的に閉じます。</summary>
	public IReadOnlyBindableReactiveProperty<bool> ShowInitialSetupInfoBar
		=> this.appSettings.AppStatus.IsInitialFolderSetupPending;

	/// <summary>素材フォルダが1件以上登録されているかどうかを取得します。</summary>
	public IReadOnlyBindableReactiveProperty<bool> HasMaterialFolder
		=> this.appSettings.HasMaterialFolder;

	/// <summary>製本済みフォルダが1件以上登録されているかどうかを取得します。</summary>
	public IReadOnlyBindableReactiveProperty<bool> HasBindingFolder
		=> this.appSettings.HasBindingFolder;

	/// <summary>素材フォルダと製本済みフォルダが両方登録されているかどうかを取得します。</summary>
	public IReadOnlyBindableReactiveProperty<bool> HasRequiredFolders
		=> this.appSettings.HasRequiredFolders;

	/// <summary>素材フォルダの一覧を取得します。</summary>
	public NotifyCollectionChangedSynchronizedViewList<SourceFolder> MaterialFolders { get; }

	/// <summary>製本済みフォルダ（既定を含む）の一覧を取得します。</summary>
	public NotifyCollectionChangedSynchronizedViewList<SourceFolder> BindingFolders { get; }

	/// <summary>フォルダを追加するコマンドです。パラメーターは追加するフォルダの <see cref="FolderRole"/> です。</summary>
	public ReactiveCommand<FolderRole> AddFolderCommand { get; }

	/// <summary>フォルダを編集するコマンドです。</summary>
	public ReactiveCommand<SourceFolder> EditFolderCommand { get; }

	/// <summary>フォルダの登録を削除するコマンドです。</summary>
	public ReactiveCommand<SourceFolder> DeleteFolderCommand { get; }

	/// <summary>作業用フォルダのパスを参照するコマンドです。</summary>
	public ReactiveCommand<Unit> BrowseWorkFolderCommand { get; }

	/// <summary>
	/// <see cref="SettingsPageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="serviceScopeFactory">スコープファクトリー。</param>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	public SettingsPageViewModel(
		AppSettings appSettings,
		IServiceScopeFactory serviceScopeFactory,
		IContentDialogService contentDialogService)
	{
		this.appSettings = appSettings;
		this.contentDialogService = contentDialogService;
		this.serviceScopeFactory = serviceScopeFactory;

		this.MaterialFolders = this.appSettings.MaterialFolders
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		this.BindingFolders = this.appSettings.BindingFolders
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		this.AddFolderCommand = new ReactiveCommand<FolderRole>()
			.AddTo(ref this.disposableBag);
		this.AddFolderCommand
			.SubscribeAwait(async (role, cancellationToken) => await this.addFolderAsync(role), AwaitOperation.Drop)
			.AddTo(ref this.disposableBag);

		this.EditFolderCommand = new ReactiveCommand<SourceFolder>()
			.AddTo(ref this.disposableBag);
		this.EditFolderCommand
			.SubscribeAwait(async (folder, cancellationToken) => await this.editFolderAsync(folder), AwaitOperation.Drop)
			.AddTo(ref this.disposableBag);

		this.DeleteFolderCommand = new ReactiveCommand<SourceFolder>()
			.AddTo(ref this.disposableBag);
		this.DeleteFolderCommand
			.SubscribeAwait(async (folder, cancellationToken) => await this.deleteFolderAsync(folder), AwaitOperation.Drop)
			.AddTo(ref this.disposableBag);

		this.BrowseWorkFolderCommand = new ReactiveCommand<Unit>(_ => this.browseWorkFolder())
			.AddTo(ref this.disposableBag);
	}

	/// <summary>フォルダ追加 Dialog を表示します。</summary>
	/// <param name="role">追加するフォルダの役割。</param>
	private async ValueTask addFolderAsync(FolderRole role)
	{
		using var scope = this.serviceScopeFactory.CreateScope();
		var manager = scope.ServiceProvider.GetRequiredService<SettingsManager>();
		using var dialogViewModel = new FolderEditDialogContentViewModel(manager, role);
		await this.showFolderEditDialogAsync(dialogViewModel, "フォルダの追加");
	}

	/// <summary>フォルダ編集 Dialog を表示します。</summary>
	/// <param name="folder">編集元のフォルダ。</param>
	private async ValueTask editFolderAsync(SourceFolder folder)
	{
		using var scope = this.serviceScopeFactory.CreateScope();
		var manager = scope.ServiceProvider.GetRequiredService<SettingsManager>();
		using var dialogViewModel = new FolderEditDialogContentViewModel(manager, folder);
		await this.showFolderEditDialogAsync(dialogViewModel, "フォルダの編集");
	}

	/// <summary>フォルダ追加・編集 Dialog を表示し、結果を Dialog 用 ViewModel へ渡します。</summary>
	private async ValueTask showFolderEditDialogAsync(FolderEditDialogContentViewModel dialogViewModel, string title)
	{
		var dialog = new ContentDialog
		{
			Title = title,
			Content = new FolderEditDialogContent { DataContext = dialogViewModel },
			PrimaryButtonText = "確定",
			CloseButtonText = "キャンセル",
			DefaultButton = ContentDialogButton.Primary,
		};

		var result = await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);
		dialogViewModel.Complete(result == ContentDialogResult.Primary);
	}

	/// <summary>削除可否を確認し、確認後にフォルダの登録を削除します。</summary>
	/// <param name="folder">削除対象のフォルダ。</param>
	private async ValueTask deleteFolderAsync(SourceFolder folder)
	{
		using var scope = this.serviceScopeFactory.CreateScope();
		var manager = scope.ServiceProvider.GetRequiredService<SettingsManager>();

		var reason = await manager.GetDeleteBlockReasonAsync(folder);
		if (reason != FolderDeleteBlockReason.None)
		{
			var (title, message) = reason switch
			{
				FolderDeleteBlockReason.LastMaterialFolder => ("指定したフォルダを削除できません", "素材フォルダは最低1件必要です。最後の1件は削除できません。"),
				FolderDeleteBlockReason.LastBindingFolder => ("指定したフォルダを削除できません", "製本済みフォルダは最低1件必要です。最後の1件は削除できません。"),
				FolderDeleteBlockReason.DefaultBinding => ("指定したフォルダを削除できません", "既定の製本先は削除できません。別のフォルダを既定の製本先に設定してから削除してください。"),
				FolderDeleteBlockReason.ScanJobActive => ("指定したフォルダを削除できません", "指定したフォルダをスキャンする処理が実行中又は、実行待ちのため削除できません。\n\n時間を空けて、再度実行してください。"),
				_ => throw new InvalidOperationException(),
			};
			await ContentDialogHelper.ShowErrorAsync(this.contentDialogService, message, title);
			return;
		}

		var confirmed = await ContentDialogHelper.ShowConfirmAsync(
			this.contentDialogService,
			"フォルダの登録を削除",
			$"「{folder.DisplayName.Value}」の登録を削除します。\n\nフォルダ内のファイルは削除されません。",
			"削除");
		if (!confirmed)
			return;

		manager.DeleteFolder(folder);
	}

	/// <summary>フォルダ選択ダイアログを表示して作業用フォルダパスを設定します。</summary>
	private void browseWorkFolder()
	{
		var dialog = new OpenFolderDialog { Title = "作業用フォルダを選択してください" };
		if (dialog.ShowDialog() == true)
			this.WorkFolderPath.Value = dialog.FolderName;
	}

	/// <summary>
	/// 設定画面の表示ごとに新しいセッションを開始し、比較用の SourceFolders の Clone を作成します。
	/// </summary>
	public ValueTask InitializeDataAsync()
	{
		this.isSavedOnLeave = false;

		using var scope = this.serviceScopeFactory.CreateScope();
		var manager = scope.ServiceProvider.GetRequiredService<SettingsManager>();
		manager.BeginSettingsSession();
		return default;
	}

	/// <summary>
	/// 設定をデータベースへ非同期で保存します。
	/// 画面遷移時は <see cref="CanLeaveAsync"/> で保存済みのため、二重保存を行いません。
	/// アプリ終了時など、遷移を伴わない場合に保存を実行します。
	/// </summary>
	/// <returns>保存結果を表す <see cref="ISaveResult"/>。</returns>
	public async ValueTask<ISaveResult> SaveAsync()
	{
		if (this.isSavedOnLeave)
		{
			this.isSavedOnLeave = false;
			return SaveResult.Success();
		}

		await this.saveSettingsAsync();
		return SaveResult.Success();
	}

	/// <summary>Manager 経由で設定画面の内容を保存します。</summary>
	private async ValueTask saveSettingsAsync()
	{
		using var scope = this.serviceScopeFactory.CreateScope();
		var manager = scope.ServiceProvider.GetRequiredService<SettingsManager>();
		await manager.SaveSettingsAsync();
	}

	/// <summary>
	/// 必須フォルダの設定状況を確認し、遷移確定前に設定を保存して、画面から離れてよいかを返します。
	/// 未設定の場合はダイアログを表示し、「デフォルトを使用」選択時に不足フォルダを作成・登録します。
	/// </summary>
	/// <returns>離れてよい場合は true、設定画面に留まる場合は false。</returns>
	public async ValueTask<bool> CanLeaveAsync()
	{
		if (!this.appSettings.HasRequiredFolders.Value)
		{
			var dialog = new ContentDialog
			{
				Title = "必須フォルダ未設定",
				Content = "素材フォルダまたは製本済みフォルダが設定されていません。\n\n"
					+ "設定するまで別の画面に移動できません。\n\n"
					+ "アプリのインストールフォルダに、デフォルトのフォルダを作成しますか？",
				PrimaryButtonText = "デフォルトを使用",
				CloseButtonText = "自分で設定する",
				DefaultButton = ContentDialogButton.Primary,
			};

			var result = await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);
			if (result != ContentDialogResult.Primary)
				return false;

			using var scope = this.serviceScopeFactory.CreateScope();
			var manager = scope.ServiceProvider.GetRequiredService<SettingsManager>();
			manager.CreateDefaultFolders();
		}

		await this.saveSettingsAsync();
		this.isSavedOnLeave = true;
		return true;
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}


