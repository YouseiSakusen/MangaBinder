using R3;

namespace MangaBinder;

/// <summary>
/// Home 画面の表示状態を保持するモデルクラスです。
/// </summary>
public sealed class HomeStateInformation
{
	/// <summary>登録作品数を取得します。</summary>
	public BindableReactiveProperty<int> SeriesCount { get; } = new();

	/// <summary>素材フォルダが1件以上登録されているかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> HasMaterialSourceFolder { get; } = new();

	/// <summary>素材フォルダスキャン完了済みジョブが存在するかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> HasCompletedMaterialFolderScanJob { get; } = new();

	/// <summary>Home 画面の Empty State 種別を取得します。</summary>
	public BindableReactiveProperty<HomeEmptyStateKind> EmptyStateKind { get; } = new();
}
