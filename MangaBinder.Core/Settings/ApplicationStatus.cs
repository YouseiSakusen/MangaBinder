namespace MangaBinder.Settings;

/// <summary>
/// アプリケーション全体の状態を保持するクラスです。
/// </summary>
public class ApplicationStatus
{
	/// <summary>初回の素材フォルダ設定が完了しているかどうかを取得または設定します。</summary>
	public bool IsInitialFolderSetupCompleted { get; set; }
}
