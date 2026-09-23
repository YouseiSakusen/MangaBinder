using System.IO;
using System.Linq;
using HalationGhost.Utilities;
using MangaBinder.Bindings;
using MangaBinder.Helpers;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程全体に関係する操作を担当する Manager です。
/// </summary>
public class BindingManager
{
	private readonly BindingStore bindingStore;
	private readonly AppSettings appSettings;
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>
	/// <see cref="BindingManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingStore">製本工程正本状態ストア。</param>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="serviceScopeFactory">サービススコープファクトリー。</param>
	public BindingManager(
		BindingStore bindingStore,
		AppSettings appSettings,
		IServiceScopeFactory serviceScopeFactory)
	{
		this.bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
		this.appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
		this.serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
	}

	/// <summary>
	/// 指定された作品を製本対象として設定します。
	/// 素材フォルダの利用可否を非同期確認した上で、成功した場合のみ BindingStore.BindingTarget を更新します。
	/// </summary>
	/// <param name="bindingSeries">製本対象の作品状態。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>素材フォルダの利用可否確認結果。</returns>
	public async ValueTask<MaterialSourceAvailabilityResult> SetBindingTargetAsync(
		BindingSeries bindingSeries,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(bindingSeries);

		// ファイルシステム確認を非同期実行（UIスレッドをブロックしない）
		var availabilityResult = await Task.Run(
			() => MaterialSourceAvailabilityHelper.CheckAvailability(bindingSeries.Series),
			cancellationToken);

		// 失敗した場合は BindingTarget を変更せず、結果を返す
		if (!availabilityResult.IsSuccess)
		{
			return availabilityResult;
		}

		// 成功した場合のみ、呼び出し元コンテキストで BindingTarget を設定
		// セッション状態の初期化は BindingStore の BindingTarget 変更通知で自動実行される
		this.bindingStore.BindingTarget.Value = bindingSeries;

		return availabilityResult;
	}

	/// <summary>
	/// ZIP ファイル名の初期値を生成して BindingStore に設定します。
	/// 既に ZipOutputFileName が空ではない場合は上書きしません。
	/// </summary>
	public void InitializeZipOutputFileName()
	{
		// 既に ZipOutputFileName が設定されている場合は何もしない
		if (!string.IsNullOrEmpty(this.bindingStore.ZipOutputFileName.Value))
		{
			return;
		}

		// BindingStore.BindingTarget から現在の BindingSeries / MangaSeries を取得
		var bindingSeries = this.bindingStore.BindingTarget.Value;
		if (bindingSeries?.Series is null)
		{
			// 製本対象が設定されていない場合は何もしない
			return;
		}

		// BindingStore.BindingVolumes から今回選択されている巻範囲の開始巻・終了巻を取得
		var volumes = this.bindingStore.BindingVolumes;
		if (volumes.Count == 0)
		{
			// 選択巻が無い場合は何もしない
			return;
		}

		// VolumeNumber.Value が null でない値のみを対象にして Min/Max を計算
		var volumeNumbers = volumes
			.Where(v => v.VolumeNumber.Value.HasValue)
			.Select(v => v.VolumeNumber.Value!.Value)
			.ToList();

		if (volumeNumbers.Count == 0)
		{
			// 有効な巻番号が無い場合は何もしない
			return;
		}

		var startVolume = volumeNumbers.Min();
		var endVolume = volumeNumbers.Max();

		// ZIPファイル名を生成して BindingStore.ZipOutputFileName.Value に設定
		var zipFileName = this.appSettings.CreateBindingZipFileName(
			bindingSeries.Series,
			startVolume,
			endVolume,
			this.bindingStore.VolumeFolderDigits.Value);

		this.bindingStore.ZipOutputFileName.Value = zipFileName;
	}

	/// <summary>
	/// 製本完了処理を開始する前の事前確認情報を取得します。
	/// CreateZip が false の場合、ZIP関連の確認は実施されません。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>事前確認情報。</returns>
	public async ValueTask<BindingStartableStatus> GetStartableStatusAsync(CancellationToken cancellationToken = default)
	{
		// CreateZip が false の場合、ZIP関連の事前確認は不要
		if (!this.bindingStore.CreateZip.Value)
		{
			// ZIP関連の確認なし
			return new BindingStartableStatus(
				outputFilePath: string.Empty,
				outputFileExists: false,
				totalSizeBytes: 0);
		}

		// CreateZip が true の場合、通常の事前確認を実施
		// 出力先フォルダと出力ファイル名からフルパスを組み立てる
		var outputFilePath = this.buildOutputFilePath();

		// 出力ファイルのフルパスを取得できた場合、同名ファイルが既に存在するか確認
		var outputFileExists = outputFilePath is not null && File.Exists(outputFilePath);

		// BindingVolumes に含まれる全ファイルのサイズ合計を計算（UIスレッドをブロックしない）
		var totalSizeBytes = await Task.Run(
			() => this.calculateTotalFileSizeBytes(),
			cancellationToken);

		return new BindingStartableStatus(outputFilePath, outputFileExists, totalSizeBytes);
	}

	/// <summary>
	/// 製本完了処理を実行します。
	/// 
	/// CreateZip が true の場合、ZIP ファイルを作成します。
	/// CreateZip が false の場合、ZIP 作成をスキップします。
	/// いずれの場合も BoundEndVolume を更新し、DB を更新し、Store を更新します。
	/// 途中で例外が発生した場合は、その例外を呼び出し元へ伝播させます。
	/// </summary>
	/// <param name="allowOverwrite">
	/// 同名の既存ZIPファイルを上書きする場合は true、新規作成のみの場合は false。
	/// CreateZip が false の場合は無視されます。
	/// true の場合、既存ファイルは完全に破棄され、今回の BindingVolumes だけから新しいZIPが再生成されます。
	/// </param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>
	/// 製本完了結果。
	/// 今回製本した BindingSeries、出力情報、および Explorer 操作用のパス情報を含みます。
	/// CreateZip が true の場合、OutputFilePath と SelectFilePath は ZIPファイルのパス、
	/// OpenFolderPath は ZIP作成先フォルダです。
	/// CreateZip が false の場合、OutputFilePath と SelectFilePath は string.Empty、
	/// OpenFolderPath は Work 作品フォルダです。
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// 製本対象が未設定、または出力ファイルパスが不正な場合にスローされます。
	/// </exception>
	public async ValueTask<BindingCompletionResult> CompleteAsync(
		bool allowOverwrite = false,
		CancellationToken cancellationToken = default)
	{
		// 1. BindingStore.BindingTarget.Value から現在の BindingSeries を取得する
		var bindingSeries = this.bindingStore.BindingTarget.Value;
		if (bindingSeries?.Series is null)
		{
			throw new InvalidOperationException("BindingTarget が設定されていません。");
		}

		// 2. BindingStore.BindingVolumes から今回製本する最大巻番号を取得する
		var volumes = this.bindingStore.BindingVolumes;
		if (volumes.Count == 0)
		{
			throw new InvalidOperationException("BindingVolumes が空です。");
		}

		var volumeNumbers = volumes
			.Where(v => v.VolumeNumber.Value.HasValue)
			.Select(v => v.VolumeNumber.Value!.Value)
			.ToList();

		if (volumeNumbers.Count == 0)
		{
			throw new InvalidOperationException("有効な巻番号が存在しません。");
		}

		var maxVolumeNumber = volumeNumbers.Max();

		// 3. 正本 MangaSeries を直接変更せず、DeepCopyHelper.Copy() を使用してクローンを作成する
		var clonedSeries = DeepCopyHelper.Copy(bindingSeries.Series);

		// 4. クローンした MangaSeries の BoundEndVolume を更新する
		// 今回の最大巻番号が現在の BoundEndVolume より大きい場合のみ更新
		if (maxVolumeNumber > clonedSeries.BoundEndVolume)
		{
			clonedSeries.BoundEndVolume = (int)maxVolumeNumber;
		}

		// 5. IServiceScopeFactory.CreateScope() で Scope を作成し、Scope 内から以下を解決する
		using var scope = this.serviceScopeFactory.CreateScope();
		var serviceProvider = scope.ServiceProvider;

		var bindingArchiver = serviceProvider.GetRequiredService<BindingArchiver>();
		var bindingRepository = serviceProvider.GetRequiredService<BindingRepository>();
		var bindingQueueDispatcher = serviceProvider.GetRequiredService<BindingQueueDispatcher>();
		var mangaSeriesStore = serviceProvider.GetRequiredService<MangaSeriesStore>();

		// 6. CreateZip フラグに応じて処理を分岐
		string outputFilePath = string.Empty;
		string openFolderPath;
		string selectFilePath = string.Empty;

		if (this.bindingStore.CreateZip.Value)
		{
			// CreateZip == true: ZIP を作成
			// 出力 ZIP ファイルのフルパスを決定する
			outputFilePath = this.buildOutputFilePath();
			if (string.IsNullOrEmpty(outputFilePath))
			{
				throw new InvalidOperationException("出力ファイルパスを決定できません。DefaultBindingFolderPath または ZipOutputFileName が未設定です。");
			}

			// BindingArchiver.CreateAsync() を呼び出して ZIP を作成する
			await bindingArchiver.CreateAsync(volumes, outputFilePath, allowOverwrite, cancellationToken);

			// OpenFolderPath は ZIP作成先フォルダ
			openFolderPath = Path.GetDirectoryName(outputFilePath) ?? string.Empty;
			// SelectFilePath は作成したZIPファイルのフルパス
			selectFilePath = outputFilePath;
		}
		else
		{
			// CreateZip == false: ZIP を作成しない
			// OpenFolderPath は Work 作品フォルダ
			openFolderPath = this.appSettings.CreateWorkSeriesFolderPath(bindingSeries.Series.Title);
		}

		// 7. BindingRepository.UpdateAfterBindingAsync() を呼び出す
		await bindingRepository.UpdateAfterBindingAsync(
			clonedSeries,
			this.bindingStore.RemoveFromBindingQueueAfterCompletion.Value,
			cancellationToken);

		// 8. BindingRepository の DB 更新が正常終了した後でのみ、正本 MangaSeries を更新する
		bindingSeries.Series.BoundEndVolume = clonedSeries.BoundEndVolume;

		// 9. MangaSeriesStore.NotifySeriesChanged(seriesId) を呼び出す
		mangaSeriesStore.NotifySeriesChanged(bindingSeries.Series.SeriesId);

		// 10. BindingStore.RemoveFromBindingQueueAfterCompletion.Value == true の場合のみ、
		//     BindingQueueDispatcher.Remove(seriesId) を呼び出す
		if (this.bindingStore.RemoveFromBindingQueueAfterCompletion.Value)
		{
			bindingQueueDispatcher.Remove(bindingSeries.Series.SeriesId);
		}

		// 11. 全処理正常終了後、BindingCompletionResult を生成して返す
		return new BindingCompletionResult(bindingSeries, outputFilePath, openFolderPath, selectFilePath);
	}

	/// <summary>
	/// 出力先フォルダと出力ファイル名からフルパスを組み立てます。
	/// 組み立てできない場合は null を返します。
	/// </summary>
	/// <returns>
	/// 出力 ZIP ファイルのフルパス。
	/// DefaultBindingFolderPath または ZipOutputFileName が未設定の場合は null。
	/// </returns>
	private string? buildOutputFilePath()
	{
		var outputFolderPath = this.appSettings.DefaultBindingFolderPath;
		var outputFileName = this.bindingStore.ZipOutputFileName.Value;

		if (string.IsNullOrEmpty(outputFolderPath) || string.IsNullOrEmpty(outputFileName))
		{
			return null;
		}

		return Path.Combine(outputFolderPath, outputFileName);
	}

	/// <summary>
	/// BindingVolumes に含まれる全 BindingVolume のWorkフォルダ配下の実ファイルを再帰的に列挙し、
	/// 合計サイズを計算します。
	/// </summary>
	/// <returns>合計ファイルサイズ（バイト）。</returns>
	private long calculateTotalFileSizeBytes()
	{
		long totalSize = 0L;

		foreach (var volume in this.bindingStore.BindingVolumes)
		{
			// WorkFolderPath が null の場合はスキップ
			if (string.IsNullOrEmpty(volume.WorkFolderPath))
			{
				continue;
			}

			var workFolderInfo = new DirectoryInfo(volume.WorkFolderPath);
			if (!workFolderInfo.Exists)
			{
				continue;
			}

			// WorkFolderPath 配下の全ファイルを再帰的に列挙してサイズを集計
			// ファイル列挙またはサイズ取得で例外が発生した場合は呼び出し元へ伝播させる
			foreach (var fileInfo in workFolderInfo.EnumerateFiles("*", SearchOption.AllDirectories))
			{
				totalSize += fileInfo.Length;
			}
		}

		return totalSize;
	}

	/// <summary>
	/// 巻選択工程から次へ進むことができるかどうかを検証します。
	/// </summary>
	/// <returns>検証結果。</returns>
	}
