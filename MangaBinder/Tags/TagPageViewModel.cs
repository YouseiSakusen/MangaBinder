using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using R3;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Tags;

/// <summary>
/// タグ画面の ViewModel です。
/// <see cref="TagStore"/> が保持するタグ一覧と Reactive 状態を View へ公開し、操作は <see cref="TagEditor"/> へ依頼する薄い接続層です。
/// </summary>
public class TagPageViewModel : IDataInitializable, IDisposable
{
	private readonly TagStore tagStore;
	private readonly IServiceScopeFactory serviceScopeFactory;
	private readonly ISnackbarService snackbarService;
	private DisposableBag disposableBag;

	/// <summary>タグ一覧を取得します。TagStore の正本コレクションから直接生成した View です。</summary>
	public NotifyCollectionChangedSynchronizedViewList<TagNotifier> Tags { get; }

	/// <summary>登録タグ数を取得します。TagStore の状態をそのまま公開します。</summary>
	public BindableReactiveProperty<int> TagCount => this.tagStore.TagCount;

	/// <summary>タグが0件かどうかを取得します。TagStore の状態をそのまま公開します。</summary>
	public BindableReactiveProperty<bool> IsEmpty => this.tagStore.IsEmpty;

	/// <summary>タグが1件以上あるかどうかを取得します。TagStore の状態をそのまま公開します。</summary>
	public BindableReactiveProperty<bool> HasTags => this.tagStore.HasTags;

	/// <summary>新規追加タグ名の入力値を取得します。TagStore の状態をそのまま公開します。</summary>
	public BindableReactiveProperty<string> NewTagName => this.tagStore.NewTagName;

	/// <summary>タグ追加成功時にインクリメントされるフォーカス要求カウンタです。</summary>
	public BindableReactiveProperty<int> TagNameFocusRequest { get; }

	/// <summary>タグを追加するコマンドです。</summary>
	public ReactiveCommand<Unit> AddTagCommand { get; }

	/// <summary>タグを削除するコマンドです。</summary>
	public ReactiveCommand<TagNotifier> DeleteTagCommand { get; }

	/// <summary>現在インライン編集中のタグを取得します。null の場合は通常表示です。TagStore の状態をそのまま公開します。</summary>
	public BindableReactiveProperty<TagNotifier?> EditingTag => this.tagStore.EditingTag;

	/// <summary>編集中のタグ名の入力値を取得します。TagStore の状態をそのまま公開します。</summary>
	public BindableReactiveProperty<string> EditingTagName => this.tagStore.EditingTagName;

	/// <summary>編集開始時にインクリメントされるフォーカス＆全選択要求カウンタです。</summary>
	public BindableReactiveProperty<int> RenameTextBoxFocusRequest { get; }

	/// <summary>タグ名変更を開始するコマンドです。</summary>
	public ReactiveCommand<TagNotifier> StartRenameTagCommand { get; }

	/// <summary>タグ名変更を確定するコマンドです。</summary>
	public ReactiveCommand<Unit> CommitRenameTagCommand { get; }

	/// <summary>タグ名変更をキャンセルするコマンドです。</summary>
	public ReactiveCommand<Unit> CancelRenameTagCommand { get; }

	/// <summary>
	/// <see cref="TagPageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="tagStore">タグマスタの正本と Reactive 状態を保持するストア。</param>
	/// <param name="serviceScopeFactory">サービススコープファクトリ。</param>
	/// <param name="snackbarService">スナックバーサービス。</param>
	public TagPageViewModel(TagStore tagStore, IServiceScopeFactory serviceScopeFactory, ISnackbarService snackbarService)
	{
		this.tagStore = tagStore;
		this.serviceScopeFactory = serviceScopeFactory;
		this.snackbarService = snackbarService;

		// Store のタグ一覧を View として公開（コピーしない）
		this.Tags = this.tagStore.Tags
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		this.TagNameFocusRequest = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);

		var canAdd = this.tagStore.NewTagName
			.Select(n => !string.IsNullOrWhiteSpace(n));

		this.AddTagCommand = new ReactiveCommand<Unit>(canAdd, initialCanExecute: false)
			.AddTo(ref this.disposableBag);

		this.AddTagCommand.Subscribe(_ => this.addTag())
			.AddTo(ref this.disposableBag);

		this.DeleteTagCommand = new ReactiveCommand<TagNotifier>()
			.AddTo(ref this.disposableBag);

		this.DeleteTagCommand.Subscribe(tag => this.deleteTag(tag))
			.AddTo(ref this.disposableBag);

		this.RenameTextBoxFocusRequest = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);

		this.StartRenameTagCommand = new ReactiveCommand<TagNotifier>()
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
	private void startRenameTag(TagNotifier tag)
	{
		this.tagStore.BeginEditing(tag);
		this.RenameTextBoxFocusRequest.Value++;
	}

	/// <summary>
	/// タグ名変更を確定します。
	/// TagStore が更新され、TagNotifier の通知によって画面が自動反映されます。
	/// </summary>
	private void commitRenameTag()
	{
		var target = this.tagStore.EditingTag.Value;
		if (target is null)
			return;

		using var scope = this.serviceScopeFactory.CreateScope();
		var editor = scope.ServiceProvider.GetRequiredService<TagEditor>();

		var result = editor.Rename(target.Tag.Value, this.tagStore.EditingTagName.Value);

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

		// Store が TagEditor.Rename で更新される。画面は TagNotifier の通知で自動反映。
		this.tagStore.EndEditing();
	}

	/// <summary>
	/// タグ名変更をキャンセルします。
	/// </summary>
	private void cancelRenameTag()
	{
		this.tagStore.EndEditing();
	}

	/// <summary>
	/// タグを削除します。
	/// Store が更新されるのを待って画面が自動反映されます。
	/// </summary>
	/// <param name="tag">削除するタグ。</param>
	private void deleteTag(TagNotifier tag)
	{
		using var scope = this.serviceScopeFactory.CreateScope();
		var editor = scope.ServiceProvider.GetRequiredService<TagEditor>();

		editor.Delete(tag.Tag.Value.TagId);
		// Store が TagEditor.Delete で更新される。画面はView経由で自動反映。
	}

	/// <summary>
	/// タグを追加します。
	/// Store が更新されるのを待って画面が自動反映されます。
	/// </summary>
	private void addTag()
	{
		using var scope = this.serviceScopeFactory.CreateScope();
		var editor = scope.ServiceProvider.GetRequiredService<TagEditor>();

		var result = editor.Add(this.tagStore.NewTagName.Value);

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

		// Store が TagEditor.Add で更新される。画面はView経由で自動反映。
		this.tagStore.NewTagName.Value = string.Empty;
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
