using MangaBinder.Settings;
using ObservableCollections;
using R3;
using Reactive.Bindings.R3;

namespace MangaBinder.Controls;

/// <summary>
/// 素材フォルダ選択ダイアログの ViewModel です。
/// </summary>
public class MaterialsConfirmDialogContentViewModel : IDisposable
{
	private DisposableBag disposableBag = new();

	/// <summary>
	/// ダイアログの説明メッセージを取得します。
	/// </summary>
	public BindableReactiveProperty<string> MessageText { get; }

	/// <summary>
	/// 素材フォルダ候補一覧を取得します。
	/// </summary>
	public ObservableList<MaterialFolderCandidateItem> MaterialFolderCandidates { get; }

	/// <summary>
	/// 現在選択中の素材フォルダ候補を取得または設定します。
	/// </summary>
	public BindableReactiveProperty<MaterialFolderCandidateItem?> SelectedMaterialFolder { get; }

	/// <summary>
	/// フォルダを開くコマンドを取得します。
	/// </summary>
	public ReactiveCommand<Unit> OpenFolderCommand { get; }

	/// <summary>
	/// <see cref="MaterialsConfirmDialogContentViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="materialFolderOpener">素材フォルダを開くサービス。</param>
	public MaterialsConfirmDialogContentViewModel(MaterialFolderOpener materialFolderOpener)
	{
		ArgumentNullException.ThrowIfNull(materialFolderOpener);

		this.MessageText = new BindableReactiveProperty<string>(
			"作品の素材フォルダは1つです。\n" +
			"\n" +
			"使用しないフォルダは別の場所へ移動するか、\n" +
			"1つのフォルダへ統合してください。\n" +
			"\n" +
			"選択したフォルダを作品の素材フォルダとして使用します。")
			.AddTo(ref this.disposableBag);

		this.MaterialFolderCandidates = new ObservableList<MaterialFolderCandidateItem>();

		this.SelectedMaterialFolder = new BindableReactiveProperty<MaterialFolderCandidateItem?>(null)
			.AddTo(ref this.disposableBag);

		// フォルダを開くコマンド - SelectedMaterialFolder が存在する場合のみ実行可能
		this.OpenFolderCommand = this.SelectedMaterialFolder
			.Select(item => item != null)
			.ToReactiveCommand()
			.AddTo(ref this.disposableBag);

		this.OpenFolderCommand.Subscribe(async _ =>
		{
			var selectedCandidate = this.SelectedMaterialFolder.Value;
			if (selectedCandidate != null)
			{
				await materialFolderOpener.OpenAsync(selectedCandidate.MangaSource);
			}
		})
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// 素材フォルダ候補を設定します。
	/// </summary>
	/// <param name="materialFolderCandidates">素材フォルダ候補一覧（SourceFolder と MangaSource の組み合わせ）。</param>
	public void SetMaterialFolderCandidates(IList<(SourceFolder sourceFolder, MangaSource mangaSource)> materialFolderCandidates)
	{
		ArgumentNullException.ThrowIfNull(materialFolderCandidates);

		this.MaterialFolderCandidates.Clear();

		foreach (var (sourceFolder, mangaSource) in materialFolderCandidates)
		{
			var item = new MaterialFolderCandidateItem(sourceFolder, mangaSource);
			this.MaterialFolderCandidates.Add(item);
		}

		// 初期選択は先頭項目
		if (this.MaterialFolderCandidates.Count > 0)
		{
			this.SelectedMaterialFolder.Value = this.MaterialFolderCandidates[0];
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}

/// <summary>
/// 素材フォルダ選択ダイアログの候補アイテムを表します。
/// </summary>
public class MaterialFolderCandidateItem
{
	/// <summary>登録先の素材フォルダを取得します。</summary>
	public SourceFolder SourceFolder { get; }

	/// <summary>実際に素材が属する MangaSource を取得します。</summary>
	public MangaSource MangaSource { get; }

	/// <summary>
	/// ComboBox で表示するテキストを取得します。
	/// 形式: {SourceFolder.DisplayName}（{MangaSource.Path}）
	/// </summary>
	public string DisplayText { get; }

	/// <summary>
	/// <see cref="MaterialFolderCandidateItem"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="sourceFolder">登録先の素材フォルダ。</param>
	/// <param name="mangaSource">実際に素材が属する MangaSource。</param>
	public MaterialFolderCandidateItem(SourceFolder sourceFolder, MangaSource mangaSource)
	{
		ArgumentNullException.ThrowIfNull(sourceFolder);
		ArgumentNullException.ThrowIfNull(mangaSource);

		this.SourceFolder = sourceFolder;
		this.MangaSource = mangaSource;
		this.DisplayText = $"{sourceFolder.DisplayName.Value}（{mangaSource.Path}）";
	}
}
