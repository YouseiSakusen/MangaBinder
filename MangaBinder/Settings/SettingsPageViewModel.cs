using HalationGhost.Wpf.Ui.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using ObservableCollections;
using R3;

namespace MangaBinder.Settings;

/// <summary>
/// 設定ページの ViewModel です。
/// </summary>
public class SettingsPageViewModel : IDisposable, ISavable
{
	private readonly AppSettings appSettings;
	private readonly IServiceScope serviceScope;
	private DisposableBag disposableBag;

	/// <summary>アプリケーション設定を取得します。</summary>
	public AppSettings AppSettings => this.appSettings;

	/// <summary>作業用一時フォルダのパスを取得します。</summary>
	public BindableReactiveProperty<string> WorkFolderPath => this.appSettings.WorkFolderPath;

	/// <summary>スキャン対象フォルダの一覧を取得します。</summary>
	public NotifyCollectionChangedSynchronizedViewList<SourceFolder> ScanFolders { get; }

	/// <summary>新規追加フォルダの表示名を取得します。</summary>
	public BindableReactiveProperty<string> NewFolderDisplayName { get; }

	/// <summary>新規追加フォルダの役割インデックスを取得します。</summary>
	public BindableReactiveProperty<int> NewFolderRoleIndex { get; }

	/// <summary>スキャン対象フォルダを追加するコマンドです。</summary>
	public ReactiveCommand<Unit> AddFolderCommand { get; }

	/// <summary>スキャン対象フォルダを削除するコマンドです。</summary>
	public ReactiveCommand<SourceFolder> DeleteFolderCommand { get; }

	/// <summary>作業用フォルダのパスを参照するコマンドです。</summary>
	public ReactiveCommand<Unit> BrowseWorkFolderCommand { get; }

	/// <summary>
	/// <see cref="SettingsPageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="serviceScopeFactory">スコープファクトリー。</param>
	public SettingsPageViewModel(AppSettings appSettings, IServiceScopeFactory serviceScopeFactory)
	{
		this.appSettings = appSettings;
		this.serviceScope = serviceScopeFactory.CreateScope();

		this.ScanFolders = this.appSettings.SourceFolders
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		this.NewFolderDisplayName = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.NewFolderRoleIndex = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);

		// 表示名が空白でなく、かつ役割インデックスが有効な場合のみ追加コマンドを実行可能にする
		var canAdd = Observable.CombineLatest(
			this.NewFolderDisplayName,
			this.NewFolderRoleIndex,
			(name, roleIdx) => !string.IsNullOrWhiteSpace(name) && Enum.IsDefined((FolderRole)roleIdx));

		this.AddFolderCommand = new ReactiveCommand<Unit>(canAdd, initialCanExecute: false)
			.AddTo(ref this.disposableBag);
		this.AddFolderCommand
			.Subscribe(_ => this.addFolder())
			.AddTo(ref this.disposableBag);

		this.DeleteFolderCommand = new ReactiveCommand<SourceFolder>(config => this.deleteFolder(config))
			.AddTo(ref this.disposableBag);

		this.BrowseWorkFolderCommand = new ReactiveCommand<Unit>(_ => this.browseWorkFolder())
			.AddTo(ref this.disposableBag);
	}

	/// <summary>フォルダ選択ダイアログを表示し、選択されたパスで新規フォルダ設定を一覧へ追加します。</summary>
	private void addFolder()
	{
		var dialog = new OpenFolderDialog { Title = "追加するフォルダを選択してください" };
		if (dialog.ShowDialog() != true)
			return;

		var config = new SourceFolder();
		config.DisplayName.Value = this.NewFolderDisplayName.Value;
		config.FolderPath.Value = dialog.FolderName;
		config.Role.Value = (FolderRole)this.NewFolderRoleIndex.Value;
		this.appSettings.SourceFolders.Add(config);

		this.NewFolderDisplayName.Value = string.Empty;
		this.NewFolderRoleIndex.Value = 0;
	}

	/// <summary>指定されたフォルダ設定を一覧から削除します。</summary>
	/// <param name="config">削除するフォルダ設定。</param>
	private void deleteFolder(SourceFolder config)
	{
		if (this.appSettings.SourceFolders.Remove(config))
			config.Dispose();
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
		var appSettingsService = this.serviceScope.ServiceProvider.GetRequiredService<AppSettingsService>();
		await appSettingsService.SaveAppSettingsAsync();
		await appSettingsService.InitializeSourceFoldersAsync();
		return SaveResult.Success("設定を保存しました");
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
		this.serviceScope.Dispose();
	}
}
