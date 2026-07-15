namespace MangaBinder.Bindings
{
	/// <summary>
	/// 素材アイテムの種別を表す列挙型です。
	/// UI/Worker 共通で使用されます。
	/// </summary>
	public enum MaterialItemType
	{
		/// <summary>作品フォルダルート。</summary>
		Root = 0,

		/// <summary>実フォルダ、またはアーカイブ内部フォルダ。</summary>
		Folder = 1,

		/// <summary>zip / rar / cbz などのアーカイブファイル。</summary>
		Archive = 2,

		/// <summary>epub ファイル。</summary>
		Epub = 3,
	}

	/// <summary>
	/// アーカイブからの抽出結果の状態を表す列挙型です。
	/// </summary>
	public enum ExtractionStatus
	{
		/// <summary>抽出成功。</summary>
		Success,

		/// <summary>アーカイブ内に画像が一つも存在しない。</summary>
		NoImageFound,

		/// <summary>画像はないが、別の圧縮ファイルが含まれている状態。</summary>
		NestedArchiveFound,

		/// <summary>読み取り不可、または壊れたアーカイブ。</summary>
		UnsupportedFormat,
	}

	/// <summary>
	/// ファイルの種別を表す列挙型です。
	/// </summary>
	public enum FileType
	{
		/// <summary>画像ファイルです。</summary>
		Image = 1,

		/// <summary>アーカイブファイルです。</summary>
		Archive = 2,

		/// <summary>電子書籍ファイルです。</summary>
		Epub = 3,
	}

	/// <summary>
	/// サムネイル処理の状態を表す列挙型です。
	/// </summary>
	public enum ThumbnailStatus
	{
		/// <summary>未処理です。</summary>
		None = 0,

		/// <summary>処理が完了しました。</summary>
		Completed = 1,

		/// <summary>500MB リミットを超過したためスキップされました。</summary>
		LimitExceeded = 2,

		/// <summary>エラーが発生しました。</summary>
		Failed = 3,

		/// <summary>圧縮ファイル内に対応アーカイブファイルのみが含まれており、画像が取得できませんでした。</summary>
		ArchiveInArchive = 4,
	}

	/// <summary>
	/// 製本開始キューの進行状態を表す列挙型です。
	/// </summary>
	public enum BindingStartStatus
	{
		/// <summary>未設定。</summary>
		None = 0,

		/// <summary>設定中。</summary>
		Configuring = 1,

		/// <summary>実行待ち。</summary>
		Ready = 2,

		/// <summary>処理中。</summary>
		Processing = 3,

		/// <summary>エラー。</summary>
		Error = 4,
	}

	/// <summary>
	/// 素材フォルダ解析の結果ステータスを表す列挙型です。
	/// </summary>
	public enum MaterialFolderStatus
	{
		/// <summary>成功。</summary>
		Success = 0,

		/// <summary>Material ロールの所在情報が存在しない。</summary>
		NoMaterialSource = 1,

		/// <summary>所在パスが見つからない。</summary>
		MaterialSourceNotFound = 2,

		/// <summary>ドライブの準備ができていない。</summary>
		DriveNotReady = 3,
	}
}

namespace MangaBinder.Jobs
{
	/// <summary>
	/// ジョブの実行状態を表す列挙型です。
	/// </summary>
	public enum JobStatus
	{
		/// <summary>待機中（実行を待っている状態）</summary>
		Pending = 0,

		/// <summary>実行中</summary>
		Running = 1,

		/// <summary>完了</summary>
		Success = 2,

		/// <summary>エラー終了</summary>
		Error = 3,
	}

	/// <summary>
	/// ジョブの種別を表す列挙型です。
	/// </summary>
	public enum JobType
	{
		/// <summary>素材フォルダのスキャン（Materialロールのフォルダが対象）</summary>
		MaterialScan = 0,

		/// <summary>製本フォルダのスキャン（Binding / DefaultBindingロールのフォルダが対象）</summary>
		BindingScan = 1,

		/// <summary>大容量ファイルからのサムネイル作成</summary>
		LargeThumbnailCreate = 2,

		/// <summary>Google Books API を使用した書誌情報インポート</summary>
		GoogleBooksImport = 3,

		/// <summary>アーカイブ内部構造スキャン（素材フォルダ内のアーカイブをスキャン）</summary>
		MaterialArchiveScan = 4,
	}

	/// <summary>
	/// ジョブスケジュールの実行サイクルを表す列挙型です。
	/// </summary>
	public enum JobScheduleType
	{
		/// <summary>毎日実行。</summary>
		Daily = 0,

		/// <summary>毎週実行。</summary>
		Weekly = 1,

		/// <summary>一定間隔で実行。</summary>
		Interval = 2,

		/// <summary>Worker 起動時に 1 回だけ実行。</summary>
		Startup = 3,
	}
}

namespace MangaBinder
{
	/// <summary>
	/// Google Books インポートの状態を表す列挙型です。
	/// </summary>
	public enum GoogleBooksImportStatus
	{
		/// <summary>未インポートです。</summary>
		NotImported = 0,

		/// <summary>インポートが成功しました。</summary>
		Success = 1,

		/// <summary>書籍が見つかりませんでした。</summary>
		NotFound = 2,

		/// <summary>エラーが発生しました。</summary>
		Failed = 9,
	}

	/// <summary>
	/// あらすじの取得元種別を表す列挙型です。
	/// </summary>
	public enum DescriptionSource
	{
		/// <summary>取得元なし（未設定）。</summary>
		None = 0,

		/// <summary>Google Books API から取得。</summary>
		GoogleBooks = 1,

		/// <summary>手入力（ユーザーが作品編集画面で入力）。</summary>
		Manual = 2,
	}
}

namespace MangaBinder
{
	/// <summary>
	/// 既存作品の編集中タイトルと同一性判定結果を表す列挙型です。
	/// </summary>
	public enum ExistingSeriesTitleMatchResult
	{
		/// <summary>編集中作品自身と同一タイトルです。</summary>
		SameAsEditingSeriesSelf,

		/// <summary>同一タイトルの作品が見つかりません。</summary>
		NoMatchFound,

		/// <summary>別の SeriesId を持つ作品と同一タイトルです。</summary>
		DifferentSeriesMatched,
	}

	/// <summary>
	/// 既存作品保存時の素材フォルダ名変更判定結果を表す列挙型です。
	/// </summary>
	public enum MaterialFolderRenameCheckResult
	{
		/// <summary>現在フォルダ名と期待フォルダ名が一致しており、Rename は不要です。</summary>
		Ok,

		/// <summary>素材フォルダ名の変更が必要です。</summary>
		RenameNeeded,

		/// <summary>登録されている素材フォルダが物理的に存在しません。</summary>
		CurrentFolderNotFound,

		/// <summary>Rename先フォルダ名と同名のフォルダが既に存在します。</summary>
		RenameTargetAlreadyExists,
	}
}

namespace MangaBinder.Settings
{
	/// <summary>
	/// フォルダの役割を表す列挙型です。
	/// </summary>
	public enum FolderRole
	{
		/// <summary>素材フォルダです。スキャン元の画像ファイルを格納します。</summary>
		Material,

		/// <summary>発刊フォルダです。出力された電子書籍ファイルを格納します。</summary>
		Binding,

		/// <summary>既定の発刊先フォルダです。発刊先が未指定の場合に使用されます。</summary>
		DefaultBinding,
	}
}