using MangaBinder.Bindings.Inspection;
using MangaBinder.Controls;
using MangaBinder.Helpers;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using R3;
using System.Diagnostics;
using System.IO;
using Wpf.Ui;
using Wpf.Ui.Controls;
using HalationGhost.Wpf.Ui;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程-巻選択画面の ViewModel です。
/// </summary>
public class VolumeSelectionPageViewModel : IDisposable, IDataInitializable, IBackRequestHandler
{
    /// <summary>製本工程の正本状態ストア。</summary>
    private readonly BindingStore bindingStore;

    /// <summary>スコープファクトリー。</summary>
    private readonly IServiceScopeFactory serviceScopeFactory;

    /// <summary>ナビゲーションサービス。</summary>
    private readonly INavigationService navigationService;

    /// <summary>コンテントダイアログサービス。</summary>
    private readonly IContentDialogService contentDialogService;

    /// <summary>スナックバーサービス。</summary>
    private readonly ISnackbarService snackbarService;

    /// <summary>サムネイル画像ローダー。</summary>
    private readonly ThumbnailImageLoader thumbnailImageLoader;

    /// <summary>ローディングサービス。</summary>
    private readonly LoadingService loadingService;

    private DisposableBag disposableBag;

    /// <summary>選択中の作品名を取得します。</summary>
    public IReadOnlyBindableReactiveProperty<string> SeriesTitle { get; }

    /// <summary>
    /// 作品サムネイルカード用の ViewModel を取得します。
    /// ThumbnailSource と VolumeStatus を管理し、左側パネルのサムネイルカードに使用されます。
    /// </summary>
    public MangaSeriesCardViewModel MangaSeriesCard { get; private set; }

    /// <summary>作品中間フォルダが既に存在するかどうかを取得します。</summary>
    public BindableReactiveProperty<bool> HasExistingWorkFolder => this.bindingStore.HasExistingWorkFolder;

    /// <summary>Root.Children 直下のフォルダ数を取得します。</summary>
    public IReadOnlyBindableReactiveProperty<int> MaterialFolderCount => this.bindingStore.MaterialFolderCount;

    /// <summary>Root.Children 直下の圧縮ファイル数を取得します。</summary>
    public IReadOnlyBindableReactiveProperty<int> MaterialArchiveCount => this.bindingStore.MaterialArchiveCount;

    /// <summary>Root.Children 直下の圧縮ファイルの合計物理ファイルサイズ（バイト）を取得します。</summary>
    public IReadOnlyBindableReactiveProperty<long> MaterialArchiveTotalBytes => this.bindingStore.MaterialArchiveTotalBytes;

    /// <summary>Root.Children 直下の EPUB 数を取得します。</summary>
    public IReadOnlyBindableReactiveProperty<int> MaterialEpubCount => this.bindingStore.MaterialEpubCount;

    /// <summary>選択済みの巻の合計画像ファイルサイズ（バイト）を取得します。</summary>
    public IReadOnlyBindableReactiveProperty<long> SelectedVolumeTotalImageBytes => this.bindingStore.SelectedVolumeTotalImageBytes;

    /// <summary>次工程へ進めるかどうかを取得します。</summary>
    public IReadOnlyBindableReactiveProperty<bool> CanGoNext => this.bindingStore.CanGoNext;

    /// <summary>素材展開方法を取得します。</summary>
    public BindableReactiveProperty<ImageExpansionMethod> ImageExpansionMethod => this.bindingStore.ImageExpansionMethod;

    /// <summary>素材展開方法のオプション一覧を取得します。</summary>
    public IReadOnlyList<ImageExpansionOption> ImageExpansionOptions => this.bindingStore.ImageExpansionOptions;

    /// <summary>巻フォルダ名の桁数を取得します（1, 2, 3 など）。</summary>
    public BindableReactiveProperty<int> VolumeFolderDigits => this.bindingStore.VolumeFolderDigits;

    /// <summary>巻フォルダ名の桁数選択肢一覧を取得します。</summary>
    public IReadOnlyList<VolumeFolderDigitOption> VolumeFolderDigitOptions => this.bindingStore.VolumeFolderDigitOptions;

    /// <summary>次工程へ進むコマンドを取得します。</summary>
    public ReactiveCommand GoNextCommand { get; }

    /// <summary>前画面へ戻るコマンドを取得します。</summary>
    public ReactiveCommand GoBackCommand { get; }

    /// <summary>指定された MaterialItemViewModel の選択状態を反転するコマンドを取得します。</summary>
    public ReactiveCommand<MaterialItemViewModel> ToggleMaterialSelectionCommand { get; }

    /// <summary>Reactive側の右ListView で現在選択中の項目を取得または設定します。</summary>
    public BindableReactiveProperty<BindingVolumeViewModel?> SelectedBindingVolume { get; }

    /// <summary>現在選択中の SelectedBindingVolume を選択巻一覧から除外するコマンドを取得します。</summary>
    public ReactiveCommand RemoveSelectedBindingVolumeCommand { get; }

    /// <summary>指定された MaterialItemViewModel を削除するコマンドを取得します。</summary>
    public ReactiveCommand<MaterialItemViewModel> DeleteMaterialCommand { get; }

    /// <summary>Reactive側の新しい素材 TreeView の DragHandler を取得します。</summary>
    public MaterialItemDragHandler MaterialItemDragHandler { get; } = new MaterialItemDragHandler();

    /// <summary>Reactive側の新しい選択巻一覧の DropHandler を取得します。</summary>
    public BindingVolumeDropHandler BindingVolumeDropHandler { get; }

    /// <summary>Reactive側の新しい選択巻一覧除外領域の DropHandler を取得します。</summary>
    public BindingVolumeRemoveDropHandler BindingVolumeRemoveDropHandler { get; }

    /// <summary>Reactive側の新しい素材ツリーの ViewModel 一覧を取得します。</summary>
    public NotifyCollectionChangedSynchronizedViewList<MaterialItemViewModel> MaterialItems { get; }

    /// <summary>Reactive側の新しい選択巻一覧の ViewModel 一覧を取得します。</summary>
    public NotifyCollectionChangedSynchronizedViewList<BindingVolumeViewModel> BindingVolumes { get; }

    /// <summary>内部保持する新しい素材ツリーの synchronized view。</summary>
    private ISynchronizedView<MaterialItem, MaterialItemViewModel>? materialItemsView;

    /// <summary>内部保持する新しい選択巻一覧の synchronized view。</summary>
    private ISynchronizedView<BindingVolume, BindingVolumeViewModel>? bindingVolumesView;

    /// <summary>
    /// <see cref="VolumeSelectionPageViewModel"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="bindingStore">製本工程の正本状態ストア。</param>
    /// <param name="serviceScopeFactory">スコープファクトリー。</param>
    /// <param name="navigationService">ナビゲーションサービス。</param>
    /// <param name="contentDialogService">コンテントダイアログサービス。</param>
    /// <param name="snackbarService">スナックバーサービス。</param>
    /// <param name="thumbnailImageLoader">サムネイル画像ローダー。</param>
    /// <param name="loadingService">ローディングサービス。</param>
    public VolumeSelectionPageViewModel(
        BindingStore bindingStore,
        IServiceScopeFactory serviceScopeFactory,
        INavigationService navigationService,
        IContentDialogService contentDialogService,
        ISnackbarService snackbarService,
        ThumbnailImageLoader thumbnailImageLoader,
        LoadingService loadingService)
    {
        this.bindingStore = bindingStore;
        this.serviceScopeFactory = serviceScopeFactory;
        this.navigationService = navigationService;
        this.contentDialogService = contentDialogService;
        this.snackbarService = snackbarService;
        this.thumbnailImageLoader = thumbnailImageLoader;
        this.loadingService = loadingService;

        // SeriesTitle: BindingTarget.Value?.Series.Title から Reactive に導出
        this.SeriesTitle = this.bindingStore.BindingTarget
            .Select(bindingTarget => bindingTarget?.Series?.Title ?? string.Empty)
            .ToReadOnlyBindableReactiveProperty(string.Empty)
            .AddTo(ref this.disposableBag);

        this.GoNextCommand = new ReactiveCommand()
            .AddTo(ref this.disposableBag);
        this.GoNextCommand.Subscribe(_ => this.executeGoNextAsync())
            .AddTo(ref this.disposableBag);

        this.GoBackCommand = new ReactiveCommand()
            .AddTo(ref this.disposableBag);
        this.GoBackCommand.Subscribe(_ => this.executeGoBack())
            .AddTo(ref this.disposableBag);

        this.ToggleMaterialSelectionCommand = new ReactiveCommand<MaterialItemViewModel>()
            .AddTo(ref this.disposableBag);
        this.ToggleMaterialSelectionCommand.Subscribe(item => this.toggleMaterialSelection(item))
            .AddTo(ref this.disposableBag);

        // Reactive側の新除外操作コマンドを初期化
        this.SelectedBindingVolume = new BindableReactiveProperty<BindingVolumeViewModel?>(null)
            .AddTo(ref this.disposableBag);

        this.RemoveSelectedBindingVolumeCommand = new ReactiveCommand()
            .AddTo(ref this.disposableBag);
        this.RemoveSelectedBindingVolumeCommand.Subscribe(_ =>
        {
            var selectedVolume = this.SelectedBindingVolume.Value;
            if (selectedVolume is not null)
            {
                this.removeBindingVolume(selectedVolume);
            }
        })
        .AddTo(ref this.disposableBag);

        this.DeleteMaterialCommand = new ReactiveCommand<MaterialItemViewModel>()
            .AddTo(ref this.disposableBag);
        this.DeleteMaterialCommand.Subscribe(item => this.executeDeleteMaterialAsync(item))
            .AddTo(ref this.disposableBag);

        // Reactive側の新素材ツリーを初期化
        // BindingStore.Materials から MaterialItemViewModel へ変換
        var materialItemsView = this.bindingStore.Materials
            .CreateView(material => new MaterialItemViewModel(material));

        this.materialItemsView = materialItemsView;

        this.MaterialItems = materialItemsView
            .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
            .AddTo(ref this.disposableBag);

        // ViewChanged イベントで削除・置換・ソート時に ViewModel を Dispose
        materialItemsView.ViewChanged += this.onMaterialItemsViewChanged;

        // Reactive側の新選択巻一覧を初期化
        // BindingStore.BindingVolumes から BindingVolumeViewModel へ変換
        var bindingVolumesView = this.bindingStore.BindingVolumes
            .CreateView(bindingVolume => new BindingVolumeViewModel(bindingVolume));

        this.bindingVolumesView = bindingVolumesView;

        this.BindingVolumes = bindingVolumesView
            .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
            .AddTo(ref this.disposableBag);

        // MangaSeriesCard: 作品サムネイルカード用ViewModel
        this.MangaSeriesCard = new MangaSeriesCardViewModel()
            .AddTo(ref this.disposableBag);

        // BindingTarget 変更時に MangaSeriesCard へ Series と ThumbnailSource を接続
        this.bindingStore.BindingTarget.Subscribe(bindingTarget =>
        {
            var series = bindingTarget?.Series;
            if (series is not null)
            {
                // ThumbnailImageLoader で最終表示用 ImageSource を取得
                var imageSource = this.thumbnailImageLoader.Load(series);

                // MangaSeriesCard へ設定
                this.MangaSeriesCard.ThumbnailSource.Value = imageSource;

                // MangaSeriesCard の Series へ接続
                if (!ReferenceEquals(this.MangaSeriesCard.Series.Value, series))
                {
                    this.MangaSeriesCard.Series.Value = series;
                }
                else
                {
                    // 同一インスタンスの場合は ForceNotify() で再通知させる
                    this.MangaSeriesCard.Series.ForceNotify();
                }
            }
            else
            {
                // series が null の場合はクリア
                this.MangaSeriesCard.ThumbnailSource.Value = null;
                this.MangaSeriesCard.Series.Value = null;
            }
        }).AddTo(ref this.disposableBag);

        // Reactive側の新しいD&D Handlerを初期化
        this.BindingVolumeDropHandler = new BindingVolumeDropHandler(
            item => this.selectMaterialFromDrop(item),
            (item, newIndex) => this.moveBindingVolumeFromDrop(item, newIndex));

        this.BindingVolumeRemoveDropHandler = new BindingVolumeRemoveDropHandler(
            item => this.removeBindingVolume(item));
    }

    /// <inheritdoc/>
    public async ValueTask InitializeDataAsync()
    {
        // UI 状態をリセット
        this.SelectedBindingVolume.Value = null;

        // VolumeSelectionManager で新 Reactive 側の素材初期化を実行
        await this.initializeMaterialsAsync();
    }

    /// <summary>
    /// 素材フォルダを非同期で読み込みます。
    /// </summary>
    /// <inheritdoc/>
    public void Dispose()
    {
        // MaterialItems の全子 ViewModel を破棄
        foreach (var viewModel in this.MaterialItems)
        {
            viewModel?.Dispose();
        }

        // materialItemsView への参照をクリア
        this.materialItemsView = null;

		// BindingVolumesView の参照をクリア
		// BindingVolumeViewModel は BindingVolume を所有しないため、
		// ここでは参照をクリアするだけで十分
		this.bindingVolumesView = null;

		this.disposableBag.Dispose();
	}

	/// <summary>
	/// 選択済み巻を収集してバリデーション後に次画面へ遷移します。
	/// </summary>
	private async void executeGoNextAsync()
	{
		// VolumeSelectionManager を解決して検証を実行
		VolumeSelectionValidationResult validationResult;

		using (var scope = this.serviceScopeFactory.CreateScope())
		{
			var volumeSelectionManager = scope.ServiceProvider.GetRequiredService<VolumeSelectionManager>();
			validationResult = volumeSelectionManager.ValidateVolumeSelection();
		}

		// Error 処理
		switch (validationResult.Error)
		{
			case VolumeSelectionValidationError.None:
				// 次の Warning 判定へ進む
				break;

			case VolumeSelectionValidationError.WorkFolderUnavailable:
				await ContentDialogHelper.ShowErrorAsync(
					this.contentDialogService,
					"ワークフォルダが設定されていないか、存在しません。\n設定画面で確認してください。");
				return;

			case VolumeSelectionValidationError.NoVolumes:
				await ContentDialogHelper.ShowErrorAsync(
					this.contentDialogService,
					"製本対象が選択されていません。");
				return;

			case VolumeSelectionValidationError.VolumeNumberMissing:
				await ContentDialogHelper.ShowErrorAsync(
					this.contentDialogService,
					"巻番号が未入力の項目があります。");
				return;

			case VolumeSelectionValidationError.DuplicateVolumeNumbers:
				{
					var duplicateText = string.Join(", ", 
						validationResult.DuplicateVolumeNumbers.Select(n => $"{n:0.#}巻"));
					var snackbarMessage = $"巻番号が重複しています：{duplicateText}";

					this.snackbarService.Show(
						"巻番号が重複しています",
						snackbarMessage,
						ControlAppearance.Danger,
						new SymbolIcon { Symbol = SymbolRegular.Warning24 },
						TimeSpan.MaxValue);
					return;
				}

			default:
				throw new InvalidOperationException(
					$"想定外の VolumeSelectionValidationError 値が返されました: {validationResult.Error}");
		}

		// Warning 処理（Error == None の場合のみ）
		switch (validationResult.Warning)
		{
			case VolumeSelectionValidationWarning.None:
				// 警告なし。そのまま次工程処理へ進む
				break;

			case VolumeSelectionValidationWarning.MissingVolume:
				{
					var confirmed = await ContentDialogHelper.ShowConfirmAsync(
						this.contentDialogService,
						"確認",
						"抜け巻があります。\nこのまま続行しますか？",
						"続行");
					if (!confirmed)
						return;
					break;
				}

			default:
				throw new InvalidOperationException(
					$"想定外の VolumeSelectionValidationWarning 値が返されました: {validationResult.Warning}");
		}

		// ① 遷移
		this.navigationService.NavigateWithHierarchy(typeof(SeriesInspectionPage));
	}

    /// <summary>
    /// 指定された MaterialItemViewModel の選択状態を反転します。
    /// VolumeSelectionManager をスコープ内で解決して選択操作を実行します。
    /// </summary>
    private void toggleMaterialSelection(MaterialItemViewModel item)
    {
        using var scope = this.serviceScopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<VolumeSelectionManager>();
        manager.ToggleMaterialSelection(item.Material, item.IsSelectionOverrideEnabled.Value);
    }

    /// <summary>
    /// 指定された BindingVolumeViewModel を選択巻一覧から除外します。
    /// VolumeSelectionManager をスコープ内で解決して除外操作を実行します。
    /// </summary>
    private void removeBindingVolume(BindingVolumeViewModel item)
    {
        using var scope = this.serviceScopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<VolumeSelectionManager>();
        manager.UnselectMaterial(item.Material);
    }

    /// <summary>
    /// D&D により TreeView から選択巻一覧へドロップされた MaterialItemViewModel を処理します。
    /// VolumeSelectionManager.SelectMaterial() を呼び出して選択します。
    /// </summary>
    /// <param name="item">ドロップされた MaterialItemViewModel。</param>
    private void selectMaterialFromDrop(MaterialItemViewModel item)
    {
        using var scope = this.serviceScopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<VolumeSelectionManager>();
        manager.SelectMaterial(item.Material, item.IsSelectionOverrideEnabled.Value);
    }

    /// <summary>
    /// D&D により選択巻一覧内で並び替えされた BindingVolumeViewModel を処理します。
    /// VolumeSelectionManager.MoveBindingVolume() を呼び出して移動します。
    /// </summary>
    /// <param name="item">移動対象の BindingVolumeViewModel。</param>
    /// <param name="newIndex">移動先のインデックス。</param>
    private void moveBindingVolumeFromDrop(BindingVolumeViewModel item, int newIndex)
    {
        using var scope = this.serviceScopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<VolumeSelectionManager>();
        manager.MoveBindingVolume(item.BindingVolume, newIndex);
    }

    /// <summary>
    /// NestedArchive 警告メッセージを構築します。
    /// ファイル名がある場合は名前を含め、ない場合は汎用メッセージを返します。
    /// </summary>
    private string buildNestedArchiveWarningMessage(IReadOnlyList<string> fileNames)
    {
        if (fileNames.Count == 0)
        {
            // ファイル名が取得できない場合は汎用メッセージ
            return "圧縮ファイル内に圧縮ファイルが含まれているファイルが存在します。対象のファイルを手作業で解凍してください。";
        }

        // ファイル名を含めたメッセージを構築
        var fileNameList = string.Join("\n", fileNames);
        return $"以下の圧縮ファイル内に、圧縮ファイルが含まれています。\n\n{fileNameList}\n\n上記のファイルを手作業で展開してください。";
    }

    /// <summary>
    /// MaterialItems の ViewChanged イベント ハンドラ。
    /// ISynchronizedView での削除・置換・ソート時に MaterialItemViewModel を適切に Dispose する。
    /// </summary>
    private void onMaterialItemsViewChanged(in SynchronizedViewChangedEventArgs<MaterialItem, MaterialItemViewModel> e)
    {
        switch (e.Action)
        {
            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                // 削除された MaterialItemViewModel を Dispose
                if (e.IsSingleItem)
                {
                    e.OldItem.View?.Dispose();
                }
                else
                {
                    foreach (var viewModel in e.OldViews)
                    {
                        viewModel?.Dispose();
                    }
                }
                break;

            case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                // 置換前の MaterialItemViewModel を Dispose
                if (e.IsSingleItem)
                {
                    e.OldItem.View?.Dispose();
                }
                else
                {
                    foreach (var viewModel in e.OldViews)
                    {
                        viewModel?.Dispose();
                    }
                }
                break;

            case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                // Reset は Sort / Reverse / Clear の場合が考えられる
                // IsClear で判定し、Clear の場合だけ旧 MaterialItemViewModel を Dispose
                if (e.SortOperation.IsClear)
                {
                    foreach (var viewModel in e.OldViews)
                    {
                        viewModel?.Dispose();
                    }
                }
                // Sort / Reverse の場合は現在有効な MaterialItemViewModel を Dispose しない
                break;
        }
    }

    /// <summary>
    /// VolumeSelectionManager を使用して素材を初期化します。
    /// LoadingService でロードを表示しながら、method-local scope で Manager を解決します。
    /// </summary>
    private async ValueTask initializeMaterialsAsync()
    {
        using (this.loadingService.Begin("素材を解析しています..."))
        {
            // UI描画機会を確保
            await Task.Yield();

            // method-local DI scope で VolumeSelectionManager を解決
            using var scope = this.serviceScopeFactory.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<VolumeSelectionManager>();

            // Manager.InitializeAsync() を呼び出して素材初期化
            var result = await manager.InitializeAsync(CancellationToken.None);

            // 初期化結果をチェックしてエラー処理・警告を実行
            if (result.Status == MaterialFolderStatus.DriveNotReady)
            {
                var driveLetter = string.IsNullOrEmpty(result.TargetPath)
                    ? string.Empty
                    : Path.GetPathRoot(result.TargetPath) ?? string.Empty;
                var driveMessage = string.IsNullOrEmpty(driveLetter)
                    ? "ドライブの接続を確認してください。"
                    : $"ドライブ({driveLetter})の接続を確認してください。";
                this.snackbarService.Show(
                    "ドライブが接続されていません",
                    driveMessage,
                    ControlAppearance.Caution,
                    new SymbolIcon { Symbol = SymbolRegular.Warning24 },
                    TimeSpan.MaxValue);
                return;
            }

            if (result.Status == MaterialFolderStatus.NoMaterialSource ||
                result.Status == MaterialFolderStatus.MaterialSourceNotFound)
            {
                this.snackbarService.Show(
                    "素材フォルダが見つかりません",
                    "素材フォルダが存在しません。",
                    ControlAppearance.Danger,
                    new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
                    TimeSpan.MaxValue);
                return;
            }

            // NestedArchive 警告
            if (result.HasNestedArchive)
            {
                // Snackbar の本文を組み立て
                var snackbarBody = this.buildNestedArchiveWarningMessage(result.NestedArchiveFileNames);

                // Snackbar で警告を表示
                this.snackbarService.Show(
                    "製本できない素材が含まれています",
                    snackbarBody,
                    ControlAppearance.Danger,
                    new SymbolIcon { Symbol = SymbolRegular.Warning24 },
                    TimeSpan.MaxValue);
                return;
            }

            // Success の場合は BindingStore.Materials が既に更新されており、
            // MaterialItems（Projection）が自動的に更新される
            // BindingStore の派生状態が自動的に更新される
        }
    }

    /// <summary>
    /// 指定された MaterialItemViewModel を削除するための非同期処理を実行します。
    /// ContentDialog で削除方法を確認し、VolumeSelectionManager.DeleteMaterial を呼び出します。
    /// </summary>
    /// <param name="item">削除対象の MaterialItemViewModel。</param>
    private async void executeDeleteMaterialAsync(MaterialItemViewModel item)
    {
        // 対象確認
        if (item is null || !item.CanDeleteMaterial)
        {
            return;
        }

        // ContentDialog を生成
        var dialog = new ContentDialog
        {
            Title = "素材を削除",
            Content = $"「{item.Material.Name}」を削除します。\n\n" +
                      "製本対象の巻に含まれている場合は、製本対象からも削除されます。",
            PrimaryButtonText = "完全に削除する",
            SecondaryButtonText = "ごみ箱に入れる",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary
        };

        // ContentDialog を表示
        var result = await this.contentDialogService.ShowAsync(
            dialog,
            CancellationToken.None);

        // ユーザーが選択した削除方法を決定
        bool sendToRecycleBin;
        switch (result)
        {
            case ContentDialogResult.Primary:
                // 完全削除
                sendToRecycleBin = false;
                break;

            case ContentDialogResult.Secondary:
                // ごみ箱へ移動
                sendToRecycleBin = true;
                break;

            case ContentDialogResult.None:
            default:
                // キャンセル
                return;
        }

        // Method-local scope で VolumeSelectionManager を取得
        using var scope = this.serviceScopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<VolumeSelectionManager>();

        // DeleteMaterial() を実行
        var succeeded = manager.DeleteMaterial(item.Material, sendToRecycleBin);

        // 結果に応じて処理
        if (!succeeded)
        {
            // 削除失敗時は Snackbar を表示
            this.snackbarService.Show(
                "素材を削除できませんでした",
                Constants.Messages.MaterialInUse,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
                TimeSpan.MaxValue);
            return;
        }

        // 削除成功時は成功通知なし、BindingStore の派生状態が自動的に更新される
    }

    /// <summary>
    /// ナビゲーション戻る処理を実行します。
    /// </summary>
    private void executeGoBack()
    {
        this.navigationService.GoBack();
    }

    /// <inheritdoc/>
    public async ValueTask OnBackRequestedAsync()
    {
        await ValueTask.CompletedTask;
        this.executeGoBack();
    }
}
