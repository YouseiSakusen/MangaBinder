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

		/// <summary>
		/// メモ編集ダイアログを表す文字列です。
		/// </summary>
		public const string MemoEditDialog = "MemoEditDialog";
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

		/// <summary>
		/// 抜け巻がある場合の警告メッセージです。
		/// </summary>
		public const string MissingVolume = "抜け巻があります。";

		/// <summary>
		/// 続行確認メッセージです。
		/// </summary>
		public const string ContinueConfirmation = "このまま続行しますか？";

		/// <summary>
		/// ZIPファイル名の巻数表記が製本対象の巻範囲と一致しない場合の警告メッセージです。
		/// </summary>
		public const string ZipFileNameVolumeMismatch = "ZIPファイル名の巻数表記が、製本対象の巻範囲と一致していません。";

		/// <summary>
		/// 製本対象が選択されていない場合のメッセージです。
		/// </summary>
		public const string NoBindingVolumes = "製本対象が選択されていません。";
	}
}
