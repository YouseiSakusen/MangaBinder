using System.IO;
using System.Linq;
using System.Text.Json;
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
	private readonly MangaSeriesStore mangaSeriesStore;
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
		MangaSeriesStore mangaSeriesStore,
		AppSettings appSettings,
		IServiceScopeFactory serviceScopeFactory)
	{
		this.bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
		this.mangaSeriesStore = mangaSeriesStore ?? throw new ArgumentNullException(nameof(mangaSeriesStore));
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

		// 成功した場合のみ、呼び出し元コンテキストで BindingTarget と WorkSeriesFolderPath を設定
		// BindingTarget 変更通知で clearCurrentBindingSession() が実行されるため、
		// WorkSeriesFolderPath が一度クリアされた後、改めて設定される構造
		// 同一BindingTarget参照が再設定された場合でも変更通知が発生しない場合、
		// WorkSeriesFolderPath には現在のパスが設定される

		// 1. 作品Workフォルダパスを生成
		var workSeriesFolderPath = this.appSettings.CreateWorkSeriesFolderPath(bindingSeries.Series.Title);

		// 2. 対応する Store 所有の共有 MangaSeriesViewModel を取得（同一インスタンスであることが必須）
		var sharedViewModel = this.mangaSeriesStore.FindViewModelById(bindingSeries.Series.SeriesId)
			?? throw new InvalidOperationException($"SeriesId {bindingSeries.Series.SeriesId} に対応する共有 MangaSeriesViewModel が見つかりません。");
		if (!ReferenceEquals(sharedViewModel.Series.Value, bindingSeries.Series))
		{
			throw new InvalidOperationException($"SeriesId {bindingSeries.Series.SeriesId} の共有 MangaSeriesViewModel と BindingSeries.Series のインスタンスが一致しません。");
		}

		// 3. BindingTarget を設定（この時点で clearCurrentBindingSession() が実行される）
		this.bindingStore.BindingTarget.Value = bindingSeries;
		this.bindingStore.BindingTargetViewModel.Value = sharedViewModel;

		// 4. WorkSeriesFolderPath を設定
		this.bindingStore.WorkSeriesFolderPath.Value = workSeriesFolderPath;

		return availabilityResult;
	}

	/// <summary>
	/// 指定した MaterialItem に対応する BindingVolume を BindingStore.BindingVolumes から削除し、
	/// Material.IsChecked を false に設定します。
	/// BindingVolume が存在しない場合でも IsChecked は false に設定します。
	/// </summary>
	/// <param name="material">選択解除対象の MaterialItem。</param>
	/// <param name="invalidateCompletedState">
	/// true の場合、BindingVolume を削除したときに VolumeSelectionCompleted / SeriesInspectionCompleted を false にします。
	/// </param>
	/// <exception cref="ArgumentNullException">material が null の場合。</exception>
	public async ValueTask UnselectMaterialAsync(MaterialItem material, bool invalidateCompletedState)
	{
		ArgumentNullException.ThrowIfNull(material);

		var volume = this.bindingStore.BindingVolumes
			.FirstOrDefault(v => ReferenceEquals(v.Material, material));

		if (volume is not null)
		{
			// 展開済みの場合は、論理状態の変更前に Work 巻フォルダを削除する（失敗時は例外で論理状態を維持）
			if (volume.RequiresUnselectCleanup)
			{
				await this.deleteWorkVolumeFolderAsync(volume);
			}

			this.bindingStore.BindingVolumes.Remove(volume);
			volume.Dispose();

			if (invalidateCompletedState)
			{
				this.bindingStore.VolumeSelectionCompleted.Value = false;
				this.bindingStore.SeriesInspectionCompleted.Value = false;
			}
		}

		// IsChecked を false に設定（Store内の不整合を残さない）
		material.IsChecked.Value = false;
	}

	/// <summary>
	/// 展開済み BindingVolume の Work 巻フォルダを削除します。
	/// フォルダが既に存在しない場合は削除済みとして扱います。
	/// </summary>
	/// <param name="volume">対象の BindingVolume。</param>
	/// <exception cref="InvalidOperationException">WorkFolderPath が未設定の場合。</exception>
	private async ValueTask deleteWorkVolumeFolderAsync(BindingVolume volume)
	{
		if (string.IsNullOrWhiteSpace(volume.WorkFolderPath))
		{
			throw new InvalidOperationException(
				"展開済みの BindingVolume に WorkFolderPath が設定されていないため、削除対象の Work 巻フォルダを特定できません。");
		}

		var workFolderPath = volume.WorkFolderPath;
		await Task.Run(() =>
		{
			if (Directory.Exists(workFolderPath))
			{
				Directory.Delete(workFolderPath, recursive: true);
			}
		});
	}

	/// <summary>
	/// 巻選択の検証を行います。
	/// </summary>
	/// <param name="bindingVolumesOnly">true の場合は IsBinding が true の巻のみ、false の場合は BindingVolumes 全件を対象にします。</param>
	/// <returns>検証結果。</returns>
	public VolumeSelectionValidationResult ValidateVolumeSelection(bool bindingVolumesOnly)
	{
		// ① Work フォルダ設定の確認
		if (!this.appSettings.HasValidWorkFolder)
		{
			return new VolumeSelectionValidationResult(
				error: VolumeSelectionValidationError.WorkFolderUnavailable);
		}

		var targetVolumes = this.getTargetVolumes(bindingVolumesOnly);

		// ② 製本対象0件
		if (targetVolumes.Count == 0)
		{
			return new VolumeSelectionValidationResult(
				error: VolumeSelectionValidationError.NoVolumes);
		}

		// ③ 巻番号未入力
		if (targetVolumes.Any(volume => volume.VolumeNumber.Value == null))
		{
			return new VolumeSelectionValidationResult(
				error: VolumeSelectionValidationError.VolumeNumberMissing);
		}

		// ④ 巻番号重複
		var duplicateVolumeNumbers = targetVolumes
			.GroupBy(volume => volume.VolumeNumber.Value)
			.Where(group => group.Count() > 1)
			.Select(group => group.Key!.Value)
			.OrderBy(x => x)
			.ToList();

		if (duplicateVolumeNumbers.Count > 0)
		{
			return new VolumeSelectionValidationResult(
				error: VolumeSelectionValidationError.DuplicateVolumeNumbers,
				duplicateVolumeNumbers: duplicateVolumeNumbers.AsReadOnly());
		}

		// ⑤ 抜け巻
		var sortedVolumeNumbers = targetVolumes
			.Select(volume => volume.VolumeNumber.Value!.Value)
			.OrderBy(x => x)
			.ToList();

		for (int i = 0; i < sortedVolumeNumbers.Count - 1; i++)
		{
			if (sortedVolumeNumbers[i + 1] - sortedVolumeNumbers[i] > 1)
			{
				return new VolumeSelectionValidationResult(
					warning: VolumeSelectionValidationWarning.MissingVolume);
			}
		}

		// ⑥ 問題なし
		return new VolumeSelectionValidationResult();
	}

	/// <summary>
	/// 今回製本する巻（IsBinding が true）が存在するかどうかを取得します。
	/// </summary>
	/// <returns>製本対象の巻が1件以上ある場合は true。</returns>
	public bool HasBindingVolumes()
		=> this.bindingStore.BindingVolumes.Any(volume => volume.IsBinding.Value);

	/// <summary>
	/// 製本対象から除外された巻（IsBinding が false）が存在するかどうかを取得します。
	/// </summary>
	/// <returns>除外された巻が1件以上ある場合は true。</returns>
	public bool HasExcludedVolumes()
		=> this.bindingStore.BindingVolumes.Any(volume => !volume.IsBinding.Value);

	/// <summary>
	/// 製本完了前の警告（抜け巻、ZIPファイル名の巻数表記不一致）を判定します。
	/// </summary>
	/// <returns>警告の判定結果。</returns>
	public BindingCompletionWarning GetCompletionWarning()
	{
		var validationResult = this.ValidateVolumeSelection(bindingVolumesOnly: true);
		var hasMissingVolume = validationResult.Warning == VolumeSelectionValidationWarning.MissingVolume;

		return new BindingCompletionWarning(hasMissingVolume, this.hasZipFileNameVolumeMismatch());
	}

	/// <summary>
	/// ZIPファイル名の巻数表記が、既定の巻数表記と異なるかどうかを判定します。
	/// CreateZip が false、または巻数表記として認識できない場合は false を返します。
	/// </summary>
	private bool hasZipFileNameVolumeMismatch()
	{
		if (!this.bindingStore.CreateZip.Value)
		{
			return false;
		}

		var series = this.bindingStore.BindingTarget.Value?.Series;
		var userFileName = this.bindingStore.ZipOutputFileName.Value;
		if (series is null || string.IsNullOrEmpty(userFileName))
		{
			return false;
		}

		if (!this.tryGetBindingVolumeRange(out var startVolume, out var endVolume))
		{
			return false;
		}

		var defaultFileName = this.appSettings.CreateBindingZipFileName(
			series,
			startVolume,
			endVolume,
			this.bindingStore.VolumeFolderDigits.Value);

		var separator = this.appSettings.BindingZipNameSeparator.Value;
		var defaultVolumeText = getTrailingText(defaultFileName, separator);
		var userVolumeText = getTrailingText(userFileName, separator);
		if (defaultVolumeText is null || userVolumeText is null)
		{
			return false;
		}

		if (!this.isVolumeText(userVolumeText))
		{
			return false;
		}

		return !string.Equals(defaultVolumeText, userVolumeText, StringComparison.Ordinal);
	}

	/// <summary>
	/// 拡張子を除いたファイル名から、区切り文字以降の末尾部分を取得します。
	/// </summary>
	private static string? getTrailingText(string fileName, string separator)
	{
		if (string.IsNullOrEmpty(separator))
		{
			return null;
		}

		var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
		var index = nameWithoutExtension.LastIndexOf(separator, StringComparison.Ordinal);
		if (index < 0)
		{
			return null;
		}

		return nameWithoutExtension[(index + separator.Length)..];
	}

	/// <summary>
	/// 文字列が AppSettings の設定に基づく巻数表記（全巻／途中から完結／通常）として認識できるかを判定します。
	/// </summary>
	private bool isVolumeText(string text)
	{
		var s = this.appSettings;
		bool matches(string prefix, string suffix)
			=> text.Length > prefix.Length + suffix.Length
				&& text.StartsWith(prefix, StringComparison.Ordinal)
				&& text.EndsWith(suffix, StringComparison.Ordinal);

		return matches(s.BindingZipCompleteVolumePrefix.Value, s.BindingZipCompleteVolumeSuffix.Value)
			|| matches(s.BindingZipPartialCompleteVolumePrefix.Value, s.BindingZipPartialCompleteVolumeSuffix.Value)
			|| matches(s.BindingZipNormalVolumePrefix.Value, s.BindingZipNormalVolumeSuffix.Value);
	}

	/// <summary>
	/// 対象の BindingVolume 一覧を取得します。
	/// </summary>
	private List<BindingVolume> getTargetVolumes(bool bindingVolumesOnly)
		=> this.bindingStore.BindingVolumes
			.Where(volume => !bindingVolumesOnly || volume.IsBinding.Value)
			.ToList();

	/// <summary>
	/// IsBinding が true の巻番号から最小・最大巻番号を取得します。
	/// </summary>
	private bool tryGetBindingVolumeRange(out decimal startVolume, out decimal endVolume)
	{
		var numbers = this.bindingStore.BindingVolumes
			.Where(v => v.IsBinding.Value && v.VolumeNumber.Value.HasValue)
			.Select(v => v.VolumeNumber.Value!.Value)
			.ToList();

		if (numbers.Count == 0)
		{
			startVolume = 0;
			endVolume = 0;
			return false;
		}

		startVolume = numbers.Min();
		endVolume = numbers.Max();
		return true;
	}

	/// <summary>
	/// ZIP ファイル名の初期値を、製本対象（IsBinding が true）の巻範囲から生成します。
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

		// IsBinding が true の巻の開始巻・終了巻を取得（対象が無い場合は何もしない）
		if (!this.tryGetBindingVolumeRange(out var startVolume, out var endVolume))
		{
			return;
		}

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

		// IsBinding が true の巻に含まれる全ファイルのサイズ合計を計算（UIスレッドをブロックしない）
		var targetVolumes = this.getTargetVolumes(bindingVolumesOnly: true);
		var totalSizeBytes = await Task.Run(
			() => this.calculateTotalFileSizeBytes(targetVolumes),
			cancellationToken);

		return new BindingStartableStatus(outputFilePath, outputFileExists, totalSizeBytes);
	}

	/// <summary>
	/// 現在の製本セッションをキャンセルします。
	/// 
	/// deleteWorkFolder が true の場合、BindingStore.WorkSeriesFolderPath に対応する
	/// Work 作品フォルダを削除します。
	/// フォルダ削除に失敗しても、製本セッション自体は必ず終了します。
	/// 削除失敗時の例外は呼び出し元へ伝播します。
	/// 
	/// BindingTarget が設定されていない場合は、何もせず return します。
	/// </summary>
	/// <param name="deleteWorkFolder">
	/// true の場合、Work 作品フォルダを削除します。
	/// false の場合、フォルダは削除せず、製本セッション状態のみクリアします。
	/// </param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	/// <exception cref="IOException">
	/// Work 作品フォルダの削除中に IO エラーが発生した場合。
	/// 例外が発生した場合でも製本セッションは必ず終了します。
	/// </exception>
	/// <exception cref="UnauthorizedAccessException">
	/// Work 作品フォルダの削除中にアクセス権限がない場合。
	/// 例外が発生した場合でも製本セッションは必ず終了します。
	/// </exception>
	/// <exception cref="OperationCanceledException">
	/// キャンセルトークンがキャンセルされた場合。
	/// 例外が発生した場合でも製本セッションは必ず終了します。
	/// </exception>
	public async ValueTask CancelAsync(
		bool deleteWorkFolder = false,
		CancellationToken cancellationToken = default)
	{
		// BindingTarget が設定されていない場合は何もしない
		var bindingSeries = this.bindingStore.BindingTarget.Value;
		if (bindingSeries is null)
		{
			return;
		}

		try
		{
			// deleteWorkFolder が true の場合のみ、Work 作品フォルダを削除対象とする
			if (deleteWorkFolder)
			{
				var workSeriesFolderPath = this.bindingStore.WorkSeriesFolderPath.Value;

				// 削除条件：WorkSeriesFolderPath が空ではなく、フォルダが存在する場合のみ
				if (!string.IsNullOrEmpty(workSeriesFolderPath) && Directory.Exists(workSeriesFolderPath))
				{
					// UIスレッドをブロックしないよう Task.Run で非同期実行
					await Task.Run(
						() => Directory.Delete(workSeriesFolderPath, recursive: true),
						cancellationToken).ConfigureAwait(false);
				}
			}
		}
		finally
		{
			// Workフォルダ削除の成否に関係なく、製本セッション状態を必ずクリア
			// BindingTarget を null にすることで、BindingStore が既存の仕組みにより
			// BindingVolumes、Materials、WorkSeriesFolderPath、ZipOutputFileName、
			// 各種製本セッションフラグ／設定値を一括クリアする
			this.bindingStore.BindingTarget.Value = null;
		}
	}

	/// <summary>
	/// 製本完了処理を実行します。
	/// 
	/// CreateZip が true の場合、ZIP ファイルを作成します。
	/// CreateZip が false の場合、ZIP 作成をスキップします。
	/// いずれの場合も BoundEndVolume を更新し、製本設定を保存し、DB を更新し、Store を更新します。
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
		bool deleteExcludedVolumes = false,
		CancellationToken cancellationToken = default)
	{
		// 1. BindingStore.BindingTarget.Value から現在の BindingSeries を取得する
		var bindingSeries = this.bindingStore.BindingTarget.Value;
		if (bindingSeries?.Series is null)
		{
			throw new InvalidOperationException("BindingTarget が設定されていません。");
		}

		// 2. ZIP 作成OFFで削除が選択された場合、除外巻（IsBinding == false）を削除する（失敗時は例外で中止し、設定は保存しない）
		if (deleteExcludedVolumes && !this.bindingStore.CreateZip.Value)
		{
			var excludedMaterials = this.bindingStore.BindingVolumes
				.Where(v => !v.IsBinding.Value)
				.Select(v => v.Material)
				.ToList();

			foreach (var material in excludedMaterials)
			{
				await this.UnselectMaterialAsync(material, false);
			}
		}

		// 3. IsBinding が true の巻から開始・終了巻番号を取得する
		var volumes = this.getTargetVolumes(bindingVolumesOnly: true);
		if (volumes.Count == 0)
		{
			throw new InvalidOperationException("製本対象の BindingVolume が存在しません。");
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
		DateTime boundAt;

		if (this.bindingStore.CreateZip.Value)
		{
			// CreateZip == true: ZIP を作成
			// 出力 ZIP ファイルのフルパスを決定する
			var builtOutputFilePath = this.buildOutputFilePath();
			if (string.IsNullOrEmpty(builtOutputFilePath))
			{
				throw new InvalidOperationException("出力ファイルパスを決定できません。DefaultBindingFolderPath または ZipOutputFileName が未設定です。");
			}

			outputFilePath = builtOutputFilePath;

			// BindingArchiver.CreateAsync() を呼び出して ZIP を作成する
			await bindingArchiver.CreateAsync(volumes, outputFilePath, allowOverwrite, cancellationToken);

			// 上書き時に既存 ZIP の作成日時が残らないよう、今回の作成日時へ更新してから実際の値を読み取る
			File.SetCreationTime(outputFilePath, DateTime.Now);
			boundAt = File.GetCreationTime(outputFilePath);

			// OpenFolderPath は ZIP作成先フォルダ
			openFolderPath = Path.GetDirectoryName(outputFilePath) ?? string.Empty;
			// SelectFilePath は作成したZIPファイルのフルパス
			selectFilePath = outputFilePath;
		}
		else
		{
			// CreateZip == false: ZIP を作成しない
			// OpenFolderPath は BindingStore.WorkSeriesFolderPath から取得した Work 作品フォルダ
			openFolderPath = this.bindingStore.WorkSeriesFolderPath.Value;
			boundAt = DateTime.Now;
		}

		// 7. BindingRepository.UpdateAfterBindingAsync() を呼び出す
		var newHistory = new BindingHistory
		{
			SeriesId = clonedSeries.SeriesId,
			BoundAt = boundAt,
			StartVolume = volumeNumbers.Min(),
			EndVolume = maxVolumeNumber,
			OutputFileName = this.bindingStore.CreateZip.Value ? Path.GetFileName(outputFilePath) : string.Empty,
			OutputFolderPath = openFolderPath,
		};

		// 製本設定を BindingVolumeDto の配列として JSON 化し、DB 更新と同一 Transaction で保存する
		// ObservableList はバックグラウンドで列挙せず、呼び出し元スレッドで配列化して渡す
		var bindingSettingsJson = await this.serializeBindingSettingsAsync(
			this.bindingStore.BindingVolumes.ToArray(),
			cancellationToken);

		var savedHistory = await bindingRepository.UpdateAfterBindingAsync(
			clonedSeries,
			this.bindingStore.RemoveFromBindingQueueAfterCompletion.Value,
			newHistory,
			bindingSettingsJson,
			cancellationToken);

		// 8. BindingRepository の DB 更新が正常終了した後でのみ、正本 MangaSeries を更新する
		bindingSeries.Series.BoundEndVolume = clonedSeries.BoundEndVolume;
		bindingSeries.Series.HasBindingSettings = true;

		var histories = bindingSeries.Series.BindingHistories;
		histories.Add(savedHistory);
		histories.Sort((x, y) => y.HistoryId.CompareTo(x.HistoryId));

		// StartVolume ごとに HistoryId 降順で最新3件のみ保持する（開始巻が異なる履歴は削除しない）
		var startVolumeCounts = new Dictionary<decimal, int>();
		histories.RemoveAll(history =>
		{
			startVolumeCounts.TryGetValue(history.StartVolume, out var count);
			startVolumeCounts[history.StartVolume] = count + 1;
			return count >= 3;
		});

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
	/// 製本設定の DTO 生成と JSON シリアライズをバックグラウンドで実行し、JSON 文字列を返します。
	/// </summary>
	/// <param name="volumes">対象の BindingVolume 一覧。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>製本設定の JSON 文字列。</returns>
	private Task<string> serializeBindingSettingsAsync(
		IReadOnlyList<BindingVolume> volumes,
		CancellationToken cancellationToken)
		=> Task.Run(
			() => JsonSerializer.Serialize(
				volumes.Select(volume => volume.CreateDto()).ToList()),
			cancellationToken);

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
	/// 指定された BindingVolume のWorkフォルダ配下の実ファイルを再帰的に列挙し、
	/// 合計サイズを計算します。
	/// </summary>
	/// <param name="volumes">集計対象の BindingVolume 一覧。</param>
	/// <returns>合計ファイルサイズ（バイト）。</returns>
	private long calculateTotalFileSizeBytes(IReadOnlyList<BindingVolume> volumes)
	{
		long totalSize = 0L;

		foreach (var volume in volumes)
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

}