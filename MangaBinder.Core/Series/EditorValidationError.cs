namespace MangaBinder.Series;

/// <summary>
/// 作品編集画面の保存前検証で検出したエラーの種類を表します。
/// 表示するメッセージは画面側が決定します。
/// </summary>
public enum EditorValidationError
{
	/// <summary>エラーなし。保存処理へ進めます。</summary>
	None,

	/// <summary>タイトルが未入力です。</summary>
	TitleRequired,

	/// <summary>同一タイトルの正式作品が登録済みのため、作者の入力が必要です。</summary>
	AuthorRequired,

	/// <summary>同一作品（正規化タイトルと作者が一致）が既に登録されています。</summary>
	DuplicateSeries,

	/// <summary>既存正式作品のタイトルが、元のタイトルと同一作品と判定できない内容に変更されています。</summary>
	TitleMismatch,

	/// <summary>素材ファイルが登録されていません。</summary>
	MaterialRequired,

	/// <summary>登録先の素材フォルダが選択されていません。</summary>
	DestinationRequired,
}
