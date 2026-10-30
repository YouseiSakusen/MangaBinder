using MangaBinder.Settings;
using Microsoft.Win32;
using R3;

namespace MangaBinder.Controls;

/// <summary>
/// <see cref="FolderEditDialogContent"/> の ViewModel です。
/// 確定まで正本を変更しないための Dialog 専用の一時状態を保持します。
/// </summary>
public class FolderEditDialogContentViewModel : IDisposable
{
	/// <summary>設定管理マネージャー。</summary>
	private readonly SettingsManager settingsManager;

	/// <summary>編集元のフォルダ。追加の場合は null。</summary>
	private readonly SourceFolder? originalFolder;

	/// <summary>リソース管理用の DisposableBag。</summary>
	private DisposableBag disposableBag;

	/// <summary>Dialog で編集中のフォルダを取得します。</summary>
	public SourceFolder TargetSourceFolder { get; }

	/// <summary>製本済みフォルダ（既定の製本先設定を表示する対象）かどうかを取得します。</summary>
	public bool IsBindingFolder { get; }

	/// <summary>既定の製本先 ToggleSwitch を操作可能かどうかを取得します。</summary>
	public bool CanToggleDefaultBinding { get; }

	/// <summary>フォルダ参照コマンドを取得します。</summary>
	public ReactiveCommand<Unit> BrowseFolderCommand { get; }

	/// <summary>
	/// 追加用の <see cref="FolderEditDialogContentViewModel"/> を初期化します。
	/// </summary>
	/// <param name="settingsManager">設定管理マネージャー。</param>
	/// <param name="role">追加するフォルダの役割。</param>
	public FolderEditDialogContentViewModel(SettingsManager settingsManager, FolderRole role)
		: this(settingsManager)
	{
		this.TargetSourceFolder = settingsManager.BeginAdd(role);
		this.IsBindingFolder = role != FolderRole.Material;
		this.CanToggleDefaultBinding = this.IsBindingFolder;
	}

	/// <summary>
	/// 編集用の <see cref="FolderEditDialogContentViewModel"/> を初期化します。
	/// </summary>
	/// <param name="settingsManager">設定管理マネージャー。</param>
	/// <param name="originalFolder">編集元のフォルダ。</param>
	public FolderEditDialogContentViewModel(SettingsManager settingsManager, SourceFolder originalFolder)
		: this(settingsManager)
	{
		this.originalFolder = originalFolder;
		this.TargetSourceFolder = settingsManager.BeginEdit(originalFolder);
		this.IsBindingFolder = originalFolder.Role.Value != FolderRole.Material;
		this.CanToggleDefaultBinding = originalFolder.Role.Value == FolderRole.Binding;
	}

	private FolderEditDialogContentViewModel(SettingsManager settingsManager)
	{
		this.settingsManager = settingsManager;
		this.TargetSourceFolder = null!;

		this.BrowseFolderCommand = new ReactiveCommand<Unit>(_ => this.browseFolder())
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// Dialog の結果を反映します。確定の場合は追加・編集を確定し、それ以外は破棄します。
	/// </summary>
	/// <param name="isConfirmed">確定された場合は true。</param>
	public void Complete(bool isConfirmed)
	{
		if (!isConfirmed)
		{
			this.settingsManager.CancelEdit();
			return;
		}

		if (this.originalFolder is null)
			this.settingsManager.CommitAdd();
		else
			this.settingsManager.CommitEdit(this.originalFolder);
	}

	/// <summary>フォルダ選択ダイアログを表示し、編集中のフォルダパスへ反映します。</summary>
	private void browseFolder()
	{
		var dialog = new OpenFolderDialog { Title = "フォルダを選択してください" };
		if (dialog.ShowDialog() == true)
			this.TargetSourceFolder.FolderPath.Value = dialog.FolderName;
	}

	/// <summary>
	/// リソースを破棄します。確定されていない編集用フォルダは破棄されます。
	/// </summary>
	public void Dispose()
	{
		this.settingsManager.CancelEdit();
		this.disposableBag.Dispose();
	}
}
