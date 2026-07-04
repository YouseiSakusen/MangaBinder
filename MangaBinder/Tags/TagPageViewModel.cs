using System.Collections.ObjectModel;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using R3;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Tags;

/// <summary>
/// タグ画面の ViewModel です。
/// </summary>
public class TagPageViewModel : IDataInitializable, IDisposable
{
	private readonly IServiceScopeFactory serviceScopeFactory;
	private readonly ISnackbarService snackbarService;
	private DisposableBag disposableBag;

	/// <summary>タグ定義一覧を取得します。</summary>
	public ObservableCollection<MangaTag> Tags { get; }

	/// <summary>登録タグ数を取得します。</summary>
	public int TagCount => this.Tags.Count;

	/// <summary>タグが0件かどうかを取得します。</summary>
	public bool IsEmpty => this.Tags.Count == 0;

	/// <summary>タグが1件以上あるかどうかを取得します。</summary>
	public bool HasTags => this.Tags.Count > 0;

	/// <summary>新規追加タグ名の入力値を取得します。</summary>
	public BindableReactiveProperty<string> NewTagName { get; }

	/// <summary>タグ追加成功時にインクリメントされるフォーカス要求カウンタです。</summary>
	public BindableReactiveProperty<int> TagNameFocusRequest { get; }

	/// <summary>タグを追加するコマンドです。</summary>
	public ReactiveCommand<Unit> AddTagCommand { get; }

	/// <summary>タグを削除するコマンドです。</summary>
	public ReactiveCommand<MangaTag> DeleteTagCommand { get; }

	/// <summary>現在インライン編集中のタグを取得します。null の場合は通常表示です。</summary>
	public BindableReactiveProperty<MangaTag?> EditingTag { get; }

	/// <summary>編集中のタグ名の入力値を取得します。</summary>
	public BindableReactiveProperty<string> EditingTagName { get; }

	/// <summary>編集開始時にインクリメントされるフォーカス＆全選択要求カウンタです。</summary>
	public BindableReactiveProperty<int> RenameTextBoxFocusRequest { get; }

	/// <summary>タグ名変更を開始するコマンドです。</summary>
	public ReactiveCommand<MangaTag> StartRenameTagCommand { get; }

	/// <summary>タグ名変更を確定するコマンドです。</summary>
	public ReactiveCommand<Unit> CommitRenameTagCommand { get; }

	/// <summary>タグ名変更をキャンセルするコマンドです。</summary>
	public ReactiveCommand<Unit> CancelRenameTagCommand { get; }

	/// <summary>
	/// <see cref="TagPageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
	/// <param name="serviceScopeFactory">サービススコープファクトリ。</param>
	/// <param name="snackbarService">スナックバーサービス。</param>
	public TagPageViewModel(MangaSeriesStore mangaSeriesStore, IServiceScopeFactory serviceScopeFactory, ISnackbarService snackbarService)
	{
		this.serviceScopeFactory = serviceScopeFactory;
		this.snackbarService = snackbarService;

		this.Tags = new ObservableCollection<MangaTag>(mangaSeriesStore.GetTags());

		this.NewTagName = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.TagNameFocusRequest = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);

		var canAdd = this.NewTagName
			.Select(n => !string.IsNullOrWhiteSpace(n));

		this.AddTagCommand = new ReactiveCommand<Unit>(canAdd, initialCanExecute: false)
			.AddTo(ref this.disposableBag);

		this.AddTagCommand.Subscribe(_ => this.addTag())
			.AddTo(ref this.disposableBag);

		this.DeleteTagCommand = new ReactiveCommand<MangaTag>()
			.AddTo(ref this.disposableBag);

		this.DeleteTagCommand.Subscribe(tag => this.deleteTag(tag))
			.AddTo(ref this.disposableBag);

		this.EditingTag = new BindableReactiveProperty<MangaTag?>(null)
			.AddTo(ref this.disposableBag);

		this.EditingTagName = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.RenameTextBoxFocusRequest = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);

		this.StartRenameTagCommand = new ReactiveCommand<MangaTag>()
			.AddTo(ref this.disposableBag);

		this.StartRenameTagCommand.Subscribe(tag => this.startRenameTag(tag))
			.AddTo(ref this.disposableBag);

		this.CommitRenameTagCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);

		this.CommitRenameTagCommand.Subscribe(_ => this.commitRenameTag())
			.AddTo(ref this.disposableBag);

		this.CancelRenameTagCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);

		this.CancelRenameTagCommand.Subscribe(_ => this.cancelRenameTag())
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// タグ名変更を開始します。
	/// </summary>
	private void startRenameTag(MangaTag tag)
	{
		this.EditingTag.Value = tag;
		this.EditingTagName.Value = tag.Name;
		this.RenameTextBoxFocusRequest.Value++;
	}

	/// <summary>
	/// タグ名変更を確定します。
	/// </summary>
	private void commitRenameTag()
	{
		var target = this.EditingTag.Value;
		if (target is null)
			return;

		using var scope = this.serviceScopeFactory.CreateScope();
		var editor = scope.ServiceProvider.GetRequiredService<TagEditor>();

		var result = editor.Rename(target, this.EditingTagName.Value);

		if (!result.IsSuccess)
		{
			var message = result.FailureReason switch
			{
				TagRenameFailureReason.EmptyName => "タグ名を入力してください。",
				TagRenameFailureReason.DuplicateName => "同じ名前のタグが既に登録されています。",
				TagRenameFailureReason.NotFound => "変更対象のタグが見つかりません。",
				_ => "タグ名の変更に失敗しました。",
			};

			this.snackbarService.Show(
				"タグ名変更",
				message,
				ControlAppearance.Caution,
				new SymbolIcon { Symbol = SymbolRegular.Warning24 },
				TimeSpan.MaxValue);

			return;
		}

		// ObservableCollection を更新して画面に反映
		// TagRepository.Rename が返した同一インスタンスを使うことで
		// MangaSeriesStore.GetTags() と ViewModel.Tags が常に同じオブジェクトを参照する
		var index = this.Tags.IndexOf(target);
		if (index >= 0)
			this.Tags[index] = result.RenamedTag!;

		this.EditingTag.Value = null;
		this.EditingTagName.Value = string.Empty;
	}

	/// <summary>
	/// タグ名変更をキャンセルします。
	/// </summary>
	private void cancelRenameTag()
	{
		this.EditingTag.Value = null;
		this.EditingTagName.Value = string.Empty;
	}

	/// <summary>
	/// タグを削除します。
	/// </summary>
	/// <param name="tag">削除するタグ。</param>
	private void deleteTag(MangaTag tag)
	{
		using var scope = this.serviceScopeFactory.CreateScope();
		var editor = scope.ServiceProvider.GetRequiredService<TagEditor>();

		editor.Delete(tag.TagId);
		this.Tags.Remove(tag);
	}

	/// <summary>
	/// タグを追加します。
	/// </summary>
	private void addTag()
	{
		using var scope = this.serviceScopeFactory.CreateScope();
		var editor = scope.ServiceProvider.GetRequiredService<TagEditor>();

		var result = editor.Add(this.NewTagName.Value);

		if (!result.IsSuccess)
		{
			var message = result.FailureReason switch
			{
				TagAddFailureReason.EmptyName => "タグ名を入力してください。",
				TagAddFailureReason.Duplicate => "同じ名前のタグが既に登録されています。",
				_ => "タグの追加に失敗しました。",
			};

			this.snackbarService.Show(
				"タグ追加",
				message,
				ControlAppearance.Caution,
				new SymbolIcon { Symbol = SymbolRegular.Warning24 },
				TimeSpan.MaxValue);

			return;
		}

		this.Tags.Add(result.AddedTag!);
		this.NewTagName.Value = string.Empty;
		this.TagNameFocusRequest.Value++;
	}

	/// <summary>
	/// ナビゲーション完了後に呼ばれる初期データ読み込み処理。
	/// 新規タグ入力欄へのフォーカスを要求します。
	/// </summary>
	public ValueTask InitializeDataAsync()
	{
		this.TagNameFocusRequest.Value++;
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// リソースを解放します。
	/// </summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
