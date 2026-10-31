using R3;

namespace MangaBinder.Settings;

/// <summary>
/// スキャン対象フォルダの設定を表すリアクティブModelクラスです。
/// </summary>
public class SourceFolder : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>フォルダの表示名を取得します。</summary>
	public BindableReactiveProperty<string> DisplayName { get; }

	/// <summary>フォルダの絶対パスを取得します。</summary>
	public BindableReactiveProperty<string> FolderPath { get; }

	/// <summary>フォルダの役割を取得します。</summary>
	public BindableReactiveProperty<FolderRole> Role { get; }

	/// <summary>表示用テキストを取得します。「DisplayName（FolderPath）」の形式で、DisplayName と FolderPath の変更に応じて自動更新されます。</summary>
	public IReadOnlyBindableReactiveProperty<string> DisplayText { get; }

	/// <summary>
	/// <see cref="SourceFolder"/> の新しいインスタンスを初期化します。
	/// </summary>
	public SourceFolder()
	{
		this.DisplayName = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.FolderPath = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.Role = new BindableReactiveProperty<FolderRole>(FolderRole.Material)
			.AddTo(ref this.disposableBag);

		this.DisplayText = this.DisplayName
			.CombineLatest(this.FolderPath, (displayName, folderPath) =>
			{
				if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(folderPath))
				{
					return string.IsNullOrWhiteSpace(displayName) ? folderPath : displayName;
				}

				return $"{displayName}（{folderPath}）";
			})
			.ToReadOnlyBindableReactiveProperty(string.Empty)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>表示名・パス・役割の値をコピーした新しいインスタンスを生成します。</summary>
	/// <returns>複製した <see cref="SourceFolder"/>。</returns>
	public SourceFolder Clone()
	{
		var clone = new SourceFolder();
		clone.DisplayName.Value = this.DisplayName.Value;
		clone.FolderPath.Value = this.FolderPath.Value;
		clone.Role.Value = this.Role.Value;
		return clone;
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose() => this.disposableBag.Dispose();
}


