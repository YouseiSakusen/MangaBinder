namespace MangaBinder.Series;

/// <summary>
/// 作品編集の開始準備の結果を表します。
/// </summary>
public enum EditPrepareResult
{
	/// <summary>準備が完了しました。編集画面へ遷移できます。</summary>
	Ready,

	/// <summary>実在する素材フォルダが複数あるため、編集を開始できません。編集状態は初期化されていません。</summary>
	MultipleMaterialFolders,
}
