using System.IO;
using HalationGhost.Wpf.Ui;
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
public class SettingsPageViewModel : IDisposable, ISavable, ILeavable
{
	private readonly AppSettings appSettings;
	private readonly IContentDialogService contentDialogService;
	private readonly IServiceScope serviceScope;
	private DisposableBag disposableBag;

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

	/// <summary>初回設定の自動保存案内 InfoBar を表示するかどうかを取得します。</summary>
	public bool ShowInitialSetupInfoBar { get; }

	/// <summary>素材フォルダが1件以上登録されているかどうかを取得します。</summary>
	public IReadOnlyBindableReactiveProperty<bool> HasMaterialFolder
		=> this.appSettings.HasMaterialFolder;

	/// <summary>製本済みフォルダが1件以上登録されているかどうかを取得します。</summary>
	public IReadOnlyBindableReactiveProperty<bool> HasBindingFolder
		=> this.appSettings.HasBindingFolder;

	/// <summary>素材フォルダと製本済みフォルダが両方登録されているかどうかを取得します。</summary>
	public IReadOnlyBindableReactiveProperty<bool> HasRequiredFolders
		=> this.appSettings.HasRequiredFolders;

	/// <summary>スキャン対象フォルダの一覧を取得します。</summary>
	public NotifyCollectionChangedSynchronizedViewList<SourceFolder> ScanFolders { get; }

	/// <summary>素材フォルダの一覧を取得します。</summary>
	public NotifyCollectionChangedSynchronizedViewList<SourceFolder> MaterialFolders { get; }

	/// <summary>製本済みフォルダ（既定を含む）の一覧を取得します。</summary>
	public NotifyCollectionChangedSynchronizedViewList<SourceFolder> BindingFolders { get; }

	/// <summary>追加・編集中のフォルダ（編集用インスタンス）を取得します。</summary>
	public SourceFolder? TargetSourceFolder => this.appSettings.TargetSourceFolder;

	/// <summary>既定の製本先トグルを操作できるかどうかを取得します。</summary>
	public bool CanChangeDefaultBinding => !this.appSettings.IsTargetDefaultBindingLocked;

	/// <summary>素材フォルダを追加するコマンドです。</summary>
	public ReactiveCommand<Unit> AddMaterialFolderCommand { get; }

	/// <summary>製本済みフォルダを追加するコマンドです。</summary>
	public ReactiveCommand<Unit> AddBindingFolderCommand { get; }

	/// <summary>スキャン対象フォルダを編集するコマンドです。</summary>
	public ReactiveCommand<SourceFolder> EditFolderCommand { get; }

	/// <summary>スキャン対象フォルダを削除するコマンドです。</summary>
	public ReactiveCommand<SourceFolder> DeleteFolderCommand { get; }

	/// <summary>編集中フォルダのパスを参照するコマンドです。</summary>
	public ReactiveCommand<Unit> BrowseTargetFolderCommand { get; }

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
		this.ShowInitialSetupInfoBar = !appSettings.AppStatus.IsInitialFolderSetupCompleted;
		this.serviceScope = serviceScopeFactory.CreateScope();

		this.ScanFolders = this.appSettings.SourceFolders
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		this.MaterialFolders = this.appSettings.MaterialFolders
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		this.BindingFolders = this.appSettings.BindingFolders
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		this.AddMaterialFolderCommand = new ReactiveCommand<Unit>(async (_, ct) => await this.showFolderDialogAsync(null, FolderRole.Material))
			.AddTo(ref this.disposableBag);

		this.AddBindingFolderCommand = new ReactiveCommand<Unit>(async (_, ct) => await this.showFolderDialogAsync(null, FolderRole.Binding))
			.AddTo(ref this.disposableBag);

		this.EditFolderCommand = new ReactiveCommand<SourceFolder>(async (folder, ct) => await this.showFolderDialogAsync(folder, folder.Role.Value))
			.AddTo(ref this.disposableBag);

		this.DeleteFolderCommand = new ReactiveCommand<SourceFolder>(async (folder, ct) => await this.deleteFolderAsync(folder))
			.AddTo(ref this.disposableBag);

		this.BrowseTargetFolderCommand = new ReactiveCommand<Unit>(_ => this.browseTargetFolder())
			.AddTo(ref this.disposableBag);

		this.BrowseWorkFolderCommand = new ReactiveCommand<Unit>(_ => this.browseWorkFolder())
			.AddTo(ref this.disposableBag);
	}

	/// <summary>追加・編集共通ダイアログを表示し、確定時のみ Manager で正本へ反映します。</summary>
	/// <param name="source">編集対象の正本。追加時は null。</param>
	/// <param name="role">追加時の初期ロール。</param>
	private async ValueTask showFolderDialogAsync(SourceFolder? source, FolderRole role)
	{
		var manager = this.serviceScope.ServiceProvider.GetRequiredService<SettingsManager>();
		if (source is null)
			manager.BeginAddFolder(role);
		else
			manager.BeginEditFolder(source);

		var dialog = new ContentDialog
		{
			Title = source is null ? "フォルダの追加" : "フォルダの編集",
			Content = new FolderEditDialogContent { DataContext = this },
			PrimaryButtonText = "確定",
			CloseButtonText = "キャンセル",
			DefaultButton = ContentDialogButton.Primary,
		};

		var result = await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);
		if (result == ContentDialogResult.Primary)
			manager.CommitTargetFolder();
		else
			manager.CancelTargetFolder();
	}

	/// <summary>削除可否を確認し、削除できない理由の表示または削除確認後の削除を行います。</summary>
	/// <param name="folder">削除対象のフォルダ。</param>
	private async ValueTask deleteFolderAsync(SourceFolder folder)
	{
		var manager = this.serviceScope.ServiceProvider.GetRequiredService<SettingsManager>();
		var reason = await manager.GetDeleteBlockReasonAsync(folder);

		if (reason != FolderDeleteBlockReason.None)
		{
			var message = reason switch
			{
				FolderDeleteBlockReason.LastMaterialFolder => "最後の素材フォルダは削除できません。",
				FolderDeleteBlockReason.LastBindingFolder => "最後の製本済みフォルダは削除できません。",
				FolderDeleteBlockReason.DefaultBindingFolder => "既定の製本先のフォルダは削除できません。\n\n別の製本済みフォルダを既定の製本先に変更してから削除してください。",
				_ => "指定したフォルダをスキャンする処理が実行中又は、実行待ちのため削除できません。\n\n時間を空けて、再度実行してください。",
			};
			await ContentDialogHelper.ShowErrorAsync(this.contentDialogService, message, "指定したフォルダを削除できません");
			return;
		}

		var confirmed = await ContentDialogHelper.ShowConfirmAsync(
			this.contentDialogService,
			"フォルダの登録を削除",
			$"「{folder.DisplayName.Value}」の登録を削除します。よろしいですか？",
			"削除");
		if (confirmed)
			manager.DeleteFolder(folder);
	}

	/// <summary>フォルダ選択ダイアログを表示して編集中フォルダのパスを設定します。</summary>
	private void browseTargetFolder()
	{
		var dialog = new OpenFolderDialog { Title = "フォルダを選択してください" };
		if (dialog.ShowDialog() == true)
			this.appSettings.TargetSourceFolder!.FolderPath.Value = dialog.FolderName;
	}

	/// <summary>フォルダ選択ダイアログを表示して作業用フォルダパスを設定します。</summary>
	private void browseWorkFolder()
	{
		var dialog = new OpenFolderDialog { Title = "作業用フォルダを選択してください" };
		if (dialog.ShowDialog() == true)
			this.WorkFolderPath.Value = dialog.FolderName;
	}

	/// <summary>
	/// 設定をデータベースへ非同期で保存します。
	/// AppSettings テーブルを保存したのち、SourceFolders の初期化と Job 投入を行います。
	/// </summary>
	/// <returns>保存結果を表す <see cref="ISaveResult"/>。</returns>
	public async ValueTask<ISaveResult> SaveAsync()
	{
		// 設定画面改修中のため保存を一時停止
		return SaveResult.Success("保存停止中");

		//var appSettingsService = this.serviceScope.ServiceProvider.GetRequiredService<AppSettingsService>();
		//await appSettingsService.SaveAppSettingsAsync();
		//await appSettingsService.InitializeSourceFoldersAsync();
		//return SaveResult.Success("設定を保存しました");
	}

	/// <summary>
	/// 必須フォルダの設定状況を確認し、画面から離れてよいかを返します。
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

			this.createDefaultFolders();
		}

		if (string.IsNullOrWhiteSpace(this.appSettings.WorkFolderPath.Value))
			this.appSettings.WorkFolderPath.Value = AppSettings.DefaultWorkPath;

		this.appSettings.AppStatus.IsInitialFolderSetupCompleted = true;
		return true;
	}

	/// <summary>不足している必須フォルダと未設定の展開先フォルダを既定の場所へ作成し、設定へ登録します。</summary>
	private void createDefaultFolders()
	{
		if (!this.appSettings.HasMaterialFolder.Value)
			this.addDefaultFolder("素材", AppSettings.DefaultMaterialPath, FolderRole.Material);

		if (!this.appSettings.HasBindingFolder.Value)
			this.addDefaultFolder("製本済み", AppSettings.DefaultBindingPath, FolderRole.DefaultBinding);

		if (string.IsNullOrWhiteSpace(this.appSettings.WorkFolderPath.Value))
		{
			Directory.CreateDirectory(AppSettings.DefaultWorkPath);
			this.appSettings.WorkFolderPath.Value = AppSettings.DefaultWorkPath;
		}
	}

	/// <summary>フォルダを作成し、指定の役割で設定へ登録します。</summary>
	private void addDefaultFolder(string displayName, string path, FolderRole role)
	{
		Directory.CreateDirectory(path);

		var folder = new SourceFolder();
		folder.DisplayName.Value = displayName;
		folder.FolderPath.Value = path;
		folder.Role.Value = role;
		this.appSettings.SourceFolders.Add(folder);
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
		this.serviceScope.Dispose();
	}
}


