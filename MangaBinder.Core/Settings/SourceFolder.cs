using R3;
using Wpf.Ui.Controls;

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

	/// <summary>役割に対応するアイコンシンボルを取得します。Role の変更に応じて自動更新されます。</summary>
	public IReadOnlyBindableReactiveProperty<SymbolRegular> RoleIconSymbol { get; }

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

		this.RoleIconSymbol = this.Role
			.Select(role => role switch
			{
				FolderRole.Material => SymbolRegular.FolderOpen24,
				FolderRole.Binding => SymbolRegular.Book24,
				FolderRole.DefaultBinding => SymbolRegular.Home24,
				_ => SymbolRegular.FolderOpen24,
			})
			.ToReadOnlyBindableReactiveProperty(SymbolRegular.FolderOpen24)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose() => this.disposableBag.Dispose();
}


