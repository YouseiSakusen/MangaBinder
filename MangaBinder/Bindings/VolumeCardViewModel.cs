using System.IO;
using System.Windows.Media;
using MangaBinder.Bindings.Inspection;
using R3;
using Microsoft.Extensions.DependencyInjection;

namespace MangaBinder.Bindings;

/// <summary>
/// SeriesInspection の巻カード表示用 ViewModel です。
/// BindingStore が保持する BindingVolume をラップし、
/// Select() で導出した Reactive プロパティにより UI へ表示内容を提供します。
/// </summary>
public class VolumeCardViewModel : IDisposable
{
	private DisposableBag disposableBag;
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>
	/// ViewModel が保持する BindingVolume インスタンスを取得します。
	/// BindingStore 内の同一インスタンスを参照し、ForceNotify() で再通知を受け取ります。
	/// </summary>
	public BindableReactiveProperty<BindingVolume> Volume { get; }

	/// <summary>
	/// BindingVolume.WorkFolderPath から取得した巻フォルダ名を取得します。
	/// WorkFolderPath が null / 空白の場合は string.Empty です。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<string> VolumeFolderName { get; }

	/// <summary>
	/// 画像数情報を表示用の文字列フォーマットで取得します。
	/// 横長画像ありの場合は「横長画像数 / 総画像数：3 / 198」の形式で返します。
	/// エラーまたは横長画像がない場合は「総画像数：198」の形式で返します。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<string> ImageCountText { get; }

	/// <summary>
	/// 素材フォルダの直下にサブフォルダが存在するかどうかを取得します。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<bool> HasSubFolder { get; }

	/// <summary>
	/// 代表画像のサムネイル ImageSource を取得または設定します。
	/// 初期値は null です。LoadThumbnailAsync() で読み込まれます。
	/// </summary>
	public BindableReactiveProperty<ImageSource?> ThumbnailSource { get; }

	/// <summary>
	/// 検査結果にエラーがあるかどうかを取得します。
	/// EPUB展開エラー または 画像処理エラーが発生している場合 true です。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<bool> HasError { get; }

	/// <summary>
	/// 検査エラーの表示用文字列を取得します。
	/// Errorがない場合は string.Empty です。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<string> ErrorText { get; }

	/// <summary>
	/// 検査結果に警告があるかどうかを取得します。
	/// エラーがなく、ファイル名競合またはEPUB警告が存在する場合 true です。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<bool> HasWarning { get; }

	/// <summary>
	/// 検査警告の表示用文字列を取得します。
	/// 警告がない場合は string.Empty です。
	/// 複数警告は " / " で連結します。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<string> WarningText { get; }

	/// <summary>
	/// 横長画像数を表示するかどうかを取得します。
	/// EpubExtractionError == None かつ LandscapeImageCount > 0 の場合 true です。
	/// XAML側での表示条件判定用に提供されます。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<bool> ShowsLandscapeImageCount { get; }

	/// <summary>
	/// <see cref="VolumeCardViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="volume">表示対象の BindingVolume インスタンス。</param>
	/// <param name="serviceScopeFactory">Scoped サービス取得用。</param>
	/// <exception cref="ArgumentNullException"><paramref name="volume"/> が null の場合。</exception>
	public VolumeCardViewModel(
		BindingVolume volume,
		IServiceScopeFactory serviceScopeFactory)
	{
		if (volume == null)
			throw new ArgumentNullException(nameof(volume));

		this.serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));

		// Volume を BindableReactiveProperty でラップ
		this.Volume = new BindableReactiveProperty<BindingVolume>(volume)
			.AddTo(ref this.disposableBag);

		// VolumeFolderName：WorkFolderPath からフォルダ名を抽出
		this.VolumeFolderName = this.Volume
			.Select(v => string.IsNullOrWhiteSpace(v.WorkFolderPath)
				? string.Empty
				: Path.GetFileName(v.WorkFolderPath) ?? string.Empty)
			.ToReadOnlyBindableReactiveProperty(string.Empty)
			.AddTo(ref this.disposableBag);

		// ImageCountText：画像数とエラー状態を考慮した表示文字列
		this.ImageCountText = this.Volume
			.Select(v => this.CreateImageCountText(v))
			.ToReadOnlyBindableReactiveProperty(string.Empty)
			.AddTo(ref this.disposableBag);

		// HasSubFolder：サブフォルダの有無
		this.HasSubFolder = this.Volume
			.Select(v => v.HasSubFolder)
			.ToReadOnlyBindableReactiveProperty(false)
			.AddTo(ref this.disposableBag);

		// ThumbnailSource：サムネイル画像
		this.ThumbnailSource = new BindableReactiveProperty<ImageSource?>(null)
			.AddTo(ref this.disposableBag);

		// HasError：EPUB Error または 画像処理Error がある場合 true
		this.HasError = this.Volume
			.Select(v =>
				v.EpubExtractionError != EpubExtractionError.None
				|| v.HasImageProcessingError)
			.ToReadOnlyBindableReactiveProperty(false)
			.AddTo(ref this.disposableBag);

		// ErrorText：エラー表示文字列
		this.ErrorText = this.Volume
			.Select(v => this.CreateErrorText(v))
			.ToReadOnlyBindableReactiveProperty(string.Empty)
			.AddTo(ref this.disposableBag);

		// HasWarning：エラーがなく、ファイル名競合またはEPUB警告がある場合 true
		this.HasWarning = this.Volume
			.Select(v =>
			{
				var hasError = v.EpubExtractionError != EpubExtractionError.None
					|| v.HasImageProcessingError;

				return !hasError
					&& (v.HasImageFileNameConflict
						|| v.EpubExtractionWarnings != EpubExtractionWarning.None);
			})
			.ToReadOnlyBindableReactiveProperty(false)
			.AddTo(ref this.disposableBag);

		// WarningText：警告表示文字列
		this.WarningText = this.Volume
			.Select(v => this.CreateWarningText(v))
			.ToReadOnlyBindableReactiveProperty(string.Empty)
			.AddTo(ref this.disposableBag);

		// ShowsLandscapeImageCount：横長画像数表示フラグ
		// EpubExtractionError == None かつ LandscapeImageCount > 0 の場合 true
		this.ShowsLandscapeImageCount = this.Volume
			.Select(v =>
				v.EpubExtractionError == EpubExtractionError.None
				&& v.LandscapeImageCount > 0)
			.ToReadOnlyBindableReactiveProperty(false)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// 画像数表示用の文字列を生成します。
	/// 
	/// 表示仕様:
	/// - EpubExtractionError != None の場合: 総画像数：xxx のみ表示
	/// - EpubExtractionError == None かつ LandscapeImageCount == 0 の場合: 総画像数：xxx
	/// - EpubExtractionError == None かつ LandscapeImageCount > 0 の場合: 横長画像数 / 総画像数：x / y
	/// 
	/// 注意: HasImageProcessingError は横長画像数を隠す条件に使用しない
	/// </summary>
	private string CreateImageCountText(BindingVolume volume)
	{
		// EPUB Extraction Error がある場合は総画像数のみ表示
		if (volume.EpubExtractionError != EpubExtractionError.None)
		{
			return $"総画像数：{volume.ImageFileCount}";
		}

		// EPUB Error がない場合
		if (volume.LandscapeImageCount == 0)
		{
			// 横長画像がない場合は総画像数のみ
			return $"総画像数：{volume.ImageFileCount}";
		}
		else
		{
			// 横長画像がある場合は「横長画像数 / 総画像数」形式
			return $"横長画像数 / 総画像数：{volume.LandscapeImageCount} / {volume.ImageFileCount}";
		}
	}

	/// <summary>
	/// エラー表示用の文字列を生成します。
	/// </summary>
	private string CreateErrorText(BindingVolume volume)
	{
		// 1. EPUB Error がある場合
		if (volume.EpubExtractionError != EpubExtractionError.None)
		{
			return EpubExtractionMessageFormatter.FormatError(
				volume.EpubExtractionError,
				volume.EpubExtractionErrorMessage);
		}

		// 2. EPUB Error がなく、HasImageProcessingError == true の場合
		if (volume.HasImageProcessingError)
		{
			return "一部の画像を処理できませんでした。";
		}

		// 3. エラーがない場合
		return string.Empty;
	}

	/// <summary>
	/// 警告表示用の文字列を生成します。
	/// エラーがある場合は string.Empty を返します。
	/// </summary>
	private string CreateWarningText(BindingVolume volume)
	{
		// エラー判定
		var hasError = volume.EpubExtractionError != EpubExtractionError.None
			|| volume.HasImageProcessingError;

		// エラーがある場合は警告を表示しない
		if (hasError)
		{
			return string.Empty;
		}

		var messages = new List<string>();

		// ① ファイル名競合
		if (volume.HasImageFileNameConflict)
		{
			messages.Add("同一ファイル名になる画像があります");
		}

		// ② EPUB Warning
		var epubWarnings = EpubExtractionMessageFormatter.FormatWarnings(
			volume.EpubExtractionWarnings);
		messages.AddRange(epubWarnings);

		// 複数警告は " / " で連結
		return string.Join(" / ", messages);
	}

	/// <summary>
	/// 代表画像のサムネイルを非同期に読み込みます。
	/// 読み込み完了後、ThumbnailSource へ結果を代入します。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>読み込み処理を表すタスク。</returns>
	public async ValueTask LoadThumbnailAsync(CancellationToken cancellationToken = default)
	{
		// Scoped サービスを取得するためにスコープを作成
		using var scope = this.serviceScopeFactory.CreateScope();
		var loader = scope.ServiceProvider.GetRequiredService<VolumeCardThumbnailLoader>();

		// サムネイル読み込みを実行
		// 例外は呼び出し元へ伝播させる
		var imageSource = await loader.LoadAsync(
			this.Volume.Value,
			cancellationToken);

		// 読み込み結果を代入（UIスレッドへの戻りまで待機、ConfigureAwait(false)は使用しない）
		this.ThumbnailSource.Value = imageSource;
	}

	/// <summary>
	/// このビューモデルが保持するリソースを解放します。
	/// </summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
