using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// ImageSplitter の編集状態の初期化と終了を担当するマネージャーです。
/// 実画像の分割・トリミング処理は担当しません。
/// </summary>
public class ImageSplitterManager
{
	private const string StagingFolderName = ".spread";

	private const string ErrorLogFileName = "split-errors.txt";

	private readonly BindingStore bindingStore;

	private readonly SplitImageProcessor splitImageProcessor;

	private readonly ISnackbarService snackbarService;

	/// <summary>
	/// <see cref="ImageSplitterManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingStore">製本工程の正本状態ストア。</param>
	/// <param name="splitImageProcessor">1画像の分割処理。</param>
	/// <param name="snackbarService">スナックバーサービス。</param>
	public ImageSplitterManager(
		BindingStore bindingStore,
		SplitImageProcessor splitImageProcessor,
		ISnackbarService snackbarService)
	{
		this.bindingStore = bindingStore;
		this.splitImageProcessor = splitImageProcessor;
		this.snackbarService = snackbarService;
	}

	/// <summary>
	/// 編集用 Clone の設定で正本 BindingVolume の全画像を分割処理し、巻フォルダと正本 Images へ反映します。
	/// 個別画像の分割失敗は元画像を採用して継続し、エラーログを出力します。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	public async ValueTask ExecuteSplitAsync(CancellationToken cancellationToken = default)
	{
		var cloneVolume = this.bindingStore.SplitTargetVolume.Value
			?? throw new InvalidOperationException("BindingStore.SplitTargetVolume が null です。");

		var sourceVolume = this.bindingStore.BindingVolumes.Single(v => ReferenceEquals(v.Material, cloneVolume.Material));

		var volumeFolder = sourceVolume.WorkFolderPath;
		if (string.IsNullOrEmpty(volumeFolder))
		{
			throw new InvalidOperationException("BindingVolume.WorkFolderPath が設定されていません。");
		}

		var cloneSettings = cloneVolume.SplitSettings;
		var sourceSettings = sourceVolume.SplitSettings;
		sourceSettings.TrimLeft.Value = cloneSettings.TrimLeft.Value;
		sourceSettings.TrimTop.Value = cloneSettings.TrimTop.Value;
		sourceSettings.TrimRight.Value = cloneSettings.TrimRight.Value;
		sourceSettings.TrimBottom.Value = cloneSettings.TrimBottom.Value;
		sourceSettings.SplitOffset.Value = cloneSettings.SplitOffset.Value;
		sourceSettings.PageOrder.Value = cloneSettings.PageOrder.Value;
		sourceVolume.ApplyCommonSplitSettings();

		var sourceImages = sourceVolume.Images.ToArray();
		var cloneImages = cloneVolume.Images.ToArray();
		var pairs = new (BindingImage Source, bool IsTarget)[sourceImages.Length];
		for (var i = 0; i < sourceImages.Length; i++)
		{
			var cloneImage = cloneImages.Single(c => ReferenceEquals(c.MaterialImage, sourceImages[i].MaterialImage));
			pairs[i] = (sourceImages[i], cloneImage.IsSpreadSplitTarget.Value);
		}

		var stagingPath = Path.Combine(volumeFolder, StagingFolderName);
		var keepStaging = false;

		try
		{
			if (Directory.Exists(stagingPath))
			{
				Directory.Delete(stagingPath, true);
			}

			Directory.CreateDirectory(stagingPath);

			var results = new IReadOnlyList<BindingImage>[pairs.Length];
			var errors = new ConcurrentQueue<(int Index, string FileName, string Message)>();

			await Parallel.ForEachAsync(
				Enumerable.Range(0, pairs.Length),
				new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cancellationToken },
				async (index, token) =>
				{
					var (image, isTarget) = pairs[index];

					if (!isTarget)
					{
						this.copyToStaging(image, stagingPath);
						results[index] = [image];
						return;
					}

					try
					{
						results[index] = await this.splitImageProcessor.ProcessAsync(image, stagingPath, token);
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch (Exception ex)
					{
						errors.Enqueue((index, image.FileName, ex.Message));
						this.copyToStaging(image, stagingPath);
						results[index] = [image];
					}
				});

			var finalImages = results.SelectMany(r => r).ToList();
			var originalSet = new HashSet<BindingImage>(sourceImages, ReferenceEqualityComparer.Instance);

			keepStaging = true;

			foreach (var old in sourceImages)
			{
				if (old.FilePath is not null && File.Exists(old.FilePath))
				{
					File.Delete(old.FilePath);
				}
			}

			foreach (var image in finalImages)
			{
				var fileName = Path.GetFileName(image.FilePath!);
				var finalPath = Path.Combine(volumeFolder, fileName);
				File.Move(Path.Combine(stagingPath, fileName), finalPath, true);
				if (!originalSet.Contains(image))
				{
					image.FilePath = finalPath;
				}
			}

			keepStaging = false;

			sourceVolume.ReplaceImages(finalImages);
			sourceVolume.Inspect();

			if (!errors.IsEmpty)
			{
				var logPath = Path.Combine(volumeFolder, ErrorLogFileName);
				var builder = new StringBuilder();
				builder.AppendLine("見開き分割で処理できなかった画像があります。");
				builder.AppendLine("元画像をそのまま使用しました。");
				foreach (var error in errors.OrderBy(e => e.Index))
				{
					builder.AppendLine();
					builder.AppendLine(error.FileName);
					builder.AppendLine(error.Message);
				}

				File.WriteAllText(logPath, builder.ToString());

				this.snackbarService.Show(
					"見開き分割",
					"一部の画像の分割に失敗したため、元画像をそのまま使用しました。エラーログを出力しました。",
					ControlAppearance.Caution,
					new SymbolIcon { Symbol = SymbolRegular.Warning24 },
					TimeSpan.FromSeconds(10));

				Process.Start(new ProcessStartInfo
				{
					FileName = logPath,
					UseShellExecute = true,
				});
			}

			this.bindingStore.NotifyVolumeUpdated(sourceVolume);
		}
		finally
		{
			if (!keepStaging && Directory.Exists(stagingPath))
			{
				Directory.Delete(stagingPath, true);
			}
		}
	}

	/// <summary>
	/// 正本 BindingImage の実ファイルを、ファイル名を変えずステージングへコピーします。
	/// </summary>
	private void copyToStaging(BindingImage image, string stagingPath)
	{
		var sourcePath = image.FilePath
			?? throw new InvalidOperationException($"BindingImage.FilePath が設定されていません: {image.FileName}");
		File.Copy(sourcePath, Path.Combine(stagingPath, Path.GetFileName(sourcePath)));
	}

	/// <summary>
	/// 現在の SplitTargetVolume から編集用 Clone を作成して SplitVolumes へ追加し、
	/// SplitTargetVolume を Clone へ切り替えます。
	/// </summary>
	/// <exception cref="InvalidOperationException">SplitTargetVolume が null の場合。</exception>
	public void Initialize()
	{
		var sourceVolume = this.bindingStore.SplitTargetVolume.Value
			?? throw new InvalidOperationException("BindingStore.SplitTargetVolume が null です。");

		var cloneVolume = sourceVolume.CloneForImageSplitter();
		this.bindingStore.SplitVolumes.Add(cloneVolume);
		this.bindingStore.SplitTargetVolume.Value = cloneVolume;

		var (maxWidth, maxHeight) = cloneVolume.GetMaxImageSize();
		this.bindingStore.SplitTrimHorizontalMaximum.Value = maxWidth;
		this.bindingStore.SplitTrimVerticalMaximum.Value = maxHeight;
		this.bindingStore.SplitOffsetMinimum.Value = -(maxWidth / 2);
		this.bindingStore.SplitOffsetMaximum.Value = maxWidth / 2;
	}

	/// <summary>
	/// ImageSplitter の編集状態を終了します。
	/// SplitTargetVolume を null にし、SplitVolumes の破棄は BindingStore の購読に委ねます。
	/// </summary>
	public void Finish()
	{
		this.bindingStore.SplitTargetVolume.Value = null;
	}
}
