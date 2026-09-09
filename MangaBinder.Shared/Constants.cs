namespace MangaBinder;

/// <summary>
/// アプリケーション全体で使用する共通定数を定義します。
/// </summary>
public static class Constants
{
	/// <summary>
	/// ビュー名に関連する定数群です。
	/// </summary>
	public static class ViewNames
	{
		/// <summary>
		/// エディタページを表す文字列です。
		/// </summary>
		public const string EditorPage = "EditorPage";
	}

	/// <summary>
	/// ユーザーへ表示するメッセージ定数群です。
	/// </summary>
	public static class Messages
	{
		/// <summary>
		/// 素材ファイルまたはフォルダが他のアプリで使用されている場合のメッセージです。
		/// </summary>
		public const string MaterialInUse =
			"素材ファイルまたはフォルダが他のアプリで使用されている可能性があります。\n" +
			"他のアプリで開いている場合は終了して、再度実行してください。";
	}
}
