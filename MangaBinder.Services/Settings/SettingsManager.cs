using MangaBinder.Jobs;
using Microsoft.Extensions.Logging;
using System.IO;

namespace MangaBinder.Settings;

/// <summary>
/// アプリケーション設定の読み込み・保存・フォルダ操作・Job 登録の協調を担うマネージャークラスです。
/// </summary>
public class SettingsManager
{
	/// <summary>アプリケーション設定インスタンス。</summary>
	private readonly AppSettings appSettings;

	/// <summary>ジョブリポジトリ。</summary>
	private readonly JobRepository jobRepository;

	/// <summary>設定リポジトリ。</summary>
	private readonly SettingsRepository settingsRepository;

	/// <summary>共用設定リポジトリ。</summary>
	private readonly SharedSettingsRepository sharedSettingsRepository;

	/// <summary>ロガー。</summary>
	private readonly ILogger<SettingsManager> logger;

	/// <summary>
	/// <see cref="SettingsManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定インスタンス。</param>
	/// <param name="jobRepository">ジョブリポジトリ。</param>
	/// <param name="settingsRepository">設定リポジトリ。</param>
	/// <param name="sharedSettingsRepository">共用設定リポジトリ。</param>
	/// <param name="logger">ロガー。</param>
	public SettingsManager(
		AppSettings appSettings,
		JobRepository jobRepository,
		SettingsRepository settingsRepository,
		SharedSettingsRepository sharedSettingsRepository,
		ILogger<SettingsManager> logger)
	{
		this.appSettings = appSettings;
		this.jobRepository = jobRepository;
		this.settingsRepository = settingsRepository;
		this.sharedSettingsRepository = sharedSettingsRepository;
		this.logger = logger;
	}

	/// <summary>
	/// データベースからアプリケーション設定および対象フォルダ一覧を非同期で読み込みます。
	/// </summary>
	public async ValueTask LoadAsync()
	{
		this.logger.LogInformation("設定データの読み込みを開始します。");

		await this.settingsRepository.LoadAsync();
		await this.loadSourceFoldersAsync();
		await this.loadSupportedExtensionsAsync();

		this.logger.LogInformation("設定データの読み込みが完了しました。");
	}

	/// <summary>
	/// データベースへ AppSettings テーブルの設定値のみを非同期で保存します。
	/// SourceFolders の更新と Job 投入は行いません。
	/// UI 設定・軽量状態保存や終了時保存に使用します。
	/// </summary>
	public async ValueTask SaveAppSettingsAsync()
	{
		this.logger.LogInformation("AppSettings の保存を開始します。");

		await this.settingsRepository.SaveAppSettingsAsync();

		this.logger.LogInformation("AppSettings の保存が完了しました。");
	}

	/// <summary>
	/// 設定画面の新しいセッションを開始し、比較用の SourceFolders の Clone を作成します。
	/// </summary>
	public void BeginSettingsSession()
	{
		this.appSettings.BeginSettingsSession();
	}

	/// <summary>
	/// 設定画面の内容を保存します。
	/// AppSettings の設定値・SourceFolders・必要な Job・初回設定完了の AppStatus を同一 Transaction で保存し、
	/// Commit 成功後に AppStatus の正本を更新します。
	/// </summary>
	public async ValueTask SaveSettingsAsync()
	{
		this.logger.LogInformation("設定画面の保存を開始します。");

		if (string.IsNullOrWhiteSpace(this.appSettings.WorkFolderPath.Value))
			this.appSettings.WorkFolderPath.Value = AppSettings.DefaultWorkPath;

		var jobTypes = this.appSettings.GetRequiredScanJobTypes();
		var completeInitialSetup = !this.appSettings.AppStatus.IsInitialFolderSetupCompleted.Value
			&& this.appSettings.HasRequiredFolders.Value;

		await this.settingsRepository.SaveSettingsAsync(jobTypes, completeInitialSetup);

		if (completeInitialSetup)
			this.appSettings.AppStatus.IsInitialFolderSetupCompleted.Value = true;

		this.logger.LogInformation("設定画面の保存が完了しました。");
	}

	/// <summary>
	/// 不足している必須フォルダと未設定の展開先フォルダを既定の場所へ作成し、設定へ登録します。
	/// </summary>
	public void CreateDefaultFolders()
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

	/// <summary>
	/// フォルダを作成し、指定の役割で設定へ登録します。
	/// </summary>
	private void addDefaultFolder(string displayName, string path, FolderRole role)
	{
		Directory.CreateDirectory(path);

		var folder = new SourceFolder();
		folder.DisplayName.Value = displayName;
		folder.FolderPath.Value = path;
		folder.Role.Value = role;
		this.appSettings.SourceFolders.Add(folder);
	}

	/// <summary>
	/// SharedSettingsRepository 経由で SourceFolders を読み込み、AppSettings に追加します。
	/// </summary>
	private async ValueTask loadSourceFoldersAsync()
	{
		var records = await this.sharedSettingsRepository.GetSourceFoldersAsync();

		foreach (var record in records)
		{
			var folder = new SourceFolder();
			folder.FolderPath.Value = record.FolderPath;
			folder.DisplayName.Value = record.DisplayName;
			folder.Role.Value = record.Role;
			this.appSettings.SourceFolders.Add(folder);
		}
	}

	/// <summary>
	/// SharedSettingsRepository 経由でサポート拡張子一覧を読み込み、AppSettings に反映します。
	/// </summary>
	private async ValueTask loadSupportedExtensionsAsync()
	{
		var extensions = await this.sharedSettingsRepository.GetSupportedFileExtensionsAsync();
		this.appSettings.ReloadSupportedExtensions(extensions);
	}

	/// <summary>
	/// 追加用の <see cref="SourceFolder"/> を生成し、<see cref="AppSettings.TargetSourceFolder"/> へ設定します。
	/// </summary>
	/// <param name="role">追加するフォルダの役割。</param>
	/// <returns>追加用の <see cref="SourceFolder"/>。</returns>
	public SourceFolder BeginAdd(FolderRole role)
	{
		var folder = new SourceFolder();
		folder.Role.Value = role;
		this.appSettings.TargetSourceFolder = folder;
		return folder;
	}

	/// <summary>
	/// 編集元の複製を生成し、<see cref="AppSettings.TargetSourceFolder"/> へ設定します。編集元は変更しません。
	/// </summary>
	/// <param name="original">編集元の <see cref="SourceFolder"/>。</param>
	/// <returns>編集用の <see cref="SourceFolder"/>。</returns>
	public SourceFolder BeginEdit(SourceFolder original)
	{
		var folder = original.Clone();
		this.appSettings.TargetSourceFolder = folder;
		return folder;
	}

	/// <summary>
	/// 追加を確定し、編集中の <see cref="SourceFolder"/> を <see cref="AppSettings.SourceFolders"/> へ登録します。
	/// </summary>
	public void CommitAdd()
	{
		var target = this.appSettings.TargetSourceFolder!;
		this.switchDefaultBinding(target, null);
		this.appSettings.SourceFolders.Add(target);
		this.appSettings.TargetSourceFolder = null;
	}

	/// <summary>
	/// 編集を確定し、編集元を編集用の <see cref="SourceFolder"/> へ置き換えます。
	/// </summary>
	/// <param name="original">編集元の <see cref="SourceFolder"/>。</param>
	public void CommitEdit(SourceFolder original)
	{
		var target = this.appSettings.TargetSourceFolder!;
		this.switchDefaultBinding(target, original);

		var index = this.appSettings.SourceFolders.IndexOf(original);
		this.appSettings.SourceFolders[index] = target;
		original.Dispose();
		this.appSettings.TargetSourceFolder = null;
	}

	/// <summary>
	/// 追加・編集をキャンセルし、編集用の <see cref="SourceFolder"/> を破棄します。
	/// </summary>
	public void CancelEdit()
	{
		this.appSettings.TargetSourceFolder?.Dispose();
		this.appSettings.TargetSourceFolder = null;
	}

	/// <summary>
	/// 編集用フォルダが既定の製本先の場合、他の既定の製本先を通常の製本済みフォルダへ変更します。
	/// </summary>
	private void switchDefaultBinding(SourceFolder target, SourceFolder? original)
	{
		if (target.Role.Value != FolderRole.DefaultBinding)
			return;

		foreach (var folder in this.appSettings.SourceFolders)
		{
			if (folder != original && folder.Role.Value == FolderRole.DefaultBinding)
				folder.Role.Value = FolderRole.Binding;
		}
	}

	/// <summary>
	/// 指定フォルダの削除可否を判定します。
	/// </summary>
	/// <param name="folder">削除対象のフォルダ。</param>
	/// <returns>削除不可の理由。削除可能な場合は <see cref="FolderDeleteBlockReason.None"/>。</returns>
	public async ValueTask<FolderDeleteBlockReason> GetDeleteBlockReasonAsync(SourceFolder folder)
	{
		var isMaterial = folder.Role.Value == FolderRole.Material;
		var sameKindCount = this.appSettings.SourceFolders
			.Count(f => (f.Role.Value == FolderRole.Material) == isMaterial);
		if (sameKindCount <= 1)
			return isMaterial ? FolderDeleteBlockReason.LastMaterialFolder : FolderDeleteBlockReason.LastBindingFolder;

		if (folder.Role.Value == FolderRole.DefaultBinding)
			return FolderDeleteBlockReason.DefaultBinding;

		var jobType = isMaterial ? JobType.MaterialScan : JobType.BindingScan;
		if (await this.jobRepository.ExistsPendingOrRunningAsync(jobType))
			return FolderDeleteBlockReason.ScanJobActive;

		return FolderDeleteBlockReason.None;
	}

	/// <summary>
	/// 指定フォルダの登録を <see cref="AppSettings.SourceFolders"/> から削除します。実フォルダは削除しません。
	/// </summary>
	/// <param name="folder">削除対象のフォルダ。</param>
	public void DeleteFolder(SourceFolder folder)
	{
		this.appSettings.SourceFolders.Remove(folder);
		folder.Dispose();
	}
}
