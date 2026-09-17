using System.IO;
using System.Text.RegularExpressions;
using MangaBinder.Bindings;
using MangaBinder.Helpers;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 製本前確認新ルート向けの、ファイル名正規化（主番号のゼロ埋め）Simulation と実リネーム処理を行うクラスです。
/// </summary>
/// <remarks>
/// 1巻内の BindingImage について、正規化後のファイル名を Simulation し、
/// その結果を BindingImage 自身へ保持します。
/// NormalizeAsync を呼び出すことで、Simulation 結果に基づいて実ファイルのリネームを実行します。
/// </remarks>
public sealed class VolumeFileNameNormalizer
{
	// "prefix + 主番号 + (枝番: _nn または -nn)? + 拡張子" を捉えるパターン
	private static readonly Regex fileNamePattern = new(
		@"^(?<prefix>.*?)(?<main>\d+)(?<suffix>(?:[_\-]\d+)*)(?<ext>\.[^.]+)$",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	// UTF-8 / Shift_JIS 取り違えで典型的に現れる不自然な日本語文字パターン
	// 「縺」「繧」「繝」「譁」等のスクランブル文字を検出
	// 複数出現または複数種類が含まれる場合に文字化けの可能性が高い
	private static readonly string[] mojibakeCharacters = new[]
	{
		"\u7E3A", // 縺
		"\u7E67", // 繧
		"\u7E5D", // 繝
		"\u8B41", // 譁
	};

	/// <summary>
	/// 1巻内の BindingImage について、ファイル名正規化の Simulation を行います。
	/// Simulation 結果は各 BindingImage の FileNameNormalizationStatus 等へ設定されます。
	/// </summary>
	/// <param name="volume">対象巻。</param>
	public void Simulate(BindingVolume volume)
	{
		// Simulation 対象の BindingImage を決定
		// 画像処理が正常終了して Work 側に実体化された画像のみが対象
		var targetImages = volume.Images
			.Where(img => img.ProcessStatus == BindingImageProcessStatus.Succeeded && img.FilePath is not null)
			.ToList();

		if (targetImages.Count == 0)
			return;

		// 各 BindingImage の正規化状態を初期化
		foreach (var image in targetImages)
		{
			image.SimulatedNormalizedFileName = null;
			image.FileNameNormalizationStatus = FileNameNormalizationStatus.NotProcessed;
			image.FileNameNormalizationErrorMessage = null;
			image.HasSuspectedMojibake = false;
		}

		// 全 BindingImage に対して、ファイル名解析前に文字化け疑い判定を実行
		foreach (var image in targetImages)
		{
			image.HasSuspectedMojibake = this.HasSuspectedMojibake(image.FileName);
		}

		// ファイル名を解析して正規化ファイル名を生成
		var analyses = new List<FileAnalysis>();
		foreach (var image in targetImages)
		{
			var analysis = this.AnalyzeFileName(image.FileName);
			analyses.Add(new FileAnalysis(image, analysis.Prefix, analysis.MainNumber, analysis.Suffix, analysis.Extension, analysis.ErrorMessage));
		}

		// 解析失敗の場合は各 BindingImage へ反映
		var successAnalyses = analyses.Where(a => a.ErrorMessage == null).ToList();
		var failedAnalyses = analyses.Where(a => a.ErrorMessage != null).ToList();

		foreach (var failed in failedAnalyses)
		{
			failed.Image.FileNameNormalizationStatus = FileNameNormalizationStatus.ParseFailed;
			failed.Image.FileNameNormalizationErrorMessage = failed.ErrorMessage;
		}

		if (successAnalyses.Count == 0)
			return;

		// 成功分から最大桁数を決定
		var maxDigits = successAnalyses.Max(a => a.MainNumber!.Length);

		// 正規化後のファイル名を生成
		foreach (var analysis in successAnalyses)
		{
			var paddedMain = analysis.MainNumber!.PadLeft(maxDigits, '0');
			var normalizedFileName = $"{analysis.Prefix}{paddedMain}{analysis.Suffix}{analysis.Extension}";
			analysis.Image.SimulatedNormalizedFileName = normalizedFileName;

			// 現在のファイル名と比較
			if (string.Equals(analysis.Image.FileName, normalizedFileName, StringComparison.OrdinalIgnoreCase))
			{
				analysis.Image.FileNameNormalizationStatus = FileNameNormalizationStatus.NotRequired;
			}
			else
			{
				analysis.Image.FileNameNormalizationStatus = FileNameNormalizationStatus.Ready;
			}
		}

		// Conflict 判定
		this.CheckConflicts(volume, successAnalyses);
	}

	/// <summary>
	/// 1巻内の BindingImage について、Simulation 実行後、CanRenameFile == true の画像を実リネームします。
	/// </summary>
	/// <param name="volume">対象巻。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	public async ValueTask NormalizeAsync(BindingVolume volume, CancellationToken cancellationToken = default)
	{
		// UIスレッド外で「Simulation + リネーム処理全体」を実行
		await Task.Run(
			() => this.ExecuteNormalizationSequence(volume, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Simulation と実リネーム処理を逐次実行します。
	/// UIスレッド外で実行されることを前提としています。
	/// </summary>
	private void ExecuteNormalizationSequence(BindingVolume volume, CancellationToken cancellationToken)
	{
		// Simulate を実行（ファイルシステムI/Oを含む）
		this.Simulate(volume);

		// リネーム対象：FileNameNormalizationStatus == Ready の画像
		var renameTargets = volume.Images
			.Where(img => img.CanRenameFile)
			.ToList();

		if (renameTargets.Count == 0)
			return;

		// 同一巻内で逐次リネーム処理を実行
		foreach (var image in renameTargets)
		{
			// 各ファイルリネーム前にキャンセル確認
			cancellationToken.ThrowIfCancellationRequested();

			this.RenameImageFile(image);
		}
	}

	/// <summary>
	/// 1つの BindingImage に対してファイルリネームを実行します。
	/// </summary>
	private void RenameImageFile(BindingImage image)
	{
		try
		{
			var currentPath = image.FilePath;
			var normalizedFileName = image.SimulatedNormalizedFileName;

			if (currentPath is null || normalizedFileName is null)
			{
				// Simulate が正常に実行されていない場合は内部不整合
				throw new InvalidOperationException(
					$"BindingImage の状態が不正です。FilePath={currentPath}, SimulatedNormalizedFileName={normalizedFileName}");
			}

			var currentFileName = Path.GetFileName(currentPath);
			var workFolderPath = Path.GetDirectoryName(currentPath);

			if (string.IsNullOrEmpty(workFolderPath))
			{
				throw new InvalidOperationException($"Work フォルダパスを特定できません: {currentPath}");
			}

			// 現在のファイル名と正規化後ファイル名が同じ場合はスキップ
			if (string.Equals(currentFileName, normalizedFileName, StringComparison.OrdinalIgnoreCase))
			{
				image.FileNameNormalizationStatus = FileNameNormalizationStatus.NotRequired;
				return;
			}

			var newPath = Path.Combine(workFolderPath, normalizedFileName);

			// ファイルをリネーム
			File.Move(currentPath, newPath);

			// リネーム成功時：BindingImage を更新
			image.FileName = normalizedFileName;
			image.FilePath = newPath;
			image.FileNameNormalizationStatus = FileNameNormalizationStatus.Renamed;
			image.FileNameNormalizationErrorMessage = null;
		}
		catch (OperationCanceledException)
		{
			// キャンセルは上位へ伝播
			throw;
		}
		catch (IOException ex)
		{
			// ファイルI/O失敗
			image.FileNameNormalizationStatus = FileNameNormalizationStatus.RenameFailed;
			image.FileNameNormalizationErrorMessage = $"ファイルリネーム失敗: {image.FileName} -> {image.SimulatedNormalizedFileName} ({ex.Message})";
		}
		catch (UnauthorizedAccessException ex)
		{
			// アクセス権限不足
			image.FileNameNormalizationStatus = FileNameNormalizationStatus.RenameFailed;
			image.FileNameNormalizationErrorMessage = $"ファイルリネーム失敗（アクセス権限不足）: {image.FileName} -> {image.SimulatedNormalizedFileName} ({ex.Message})";
		}
	}

	/// <summary>
	/// ファイル名から prefix、主番号、枝番、拡張子を解析します。
	/// </summary>
	private ParseResult AnalyzeFileName(string fileName)
	{
		var match = fileNamePattern.Match(fileName);

		if (!match.Success)
		{
			return new ParseResult(
				Prefix: null,
				MainNumber: null,
				Suffix: null,
				Extension: null,
				ErrorMessage: $"ファイル名を解析できませんでした: {fileName}");
		}

		return new ParseResult(
			Prefix: match.Groups["prefix"].Value,
			MainNumber: match.Groups["main"].Value,
			Suffix: match.Groups["suffix"].Value,
			Extension: match.Groups["ext"].Value,
			ErrorMessage: null);
	}

	/// <summary>
	/// ファイル名が文字化けしている可能性を軽量なヒューリスティックで判定します。
	/// </summary>
	private bool HasSuspectedMojibake(string fileName)
	{
		// Unicode Replacement Character「�」を含む場合は即座に真
		if (fileName.Contains('\uFFFD'))
			return true;

		// UTF-8 / Shift_JIS 取り違えで典型的に現れるスクランブル文字について、
		// 複数出現または複数種類が含まれる場合に文字化けの可能性が高い
		var mojibakeCharacterCount = 0;
		var mojibakeCharacterKinds = new HashSet<string>();

		foreach (var mojibakeChar in mojibakeCharacters)
		{
			if (fileName.Contains(mojibakeChar))
			{
				mojibakeCharacterKinds.Add(mojibakeChar);
				// ファイル名内の出現回数をカウント
				for (int i = 0; i <= fileName.Length - mojibakeChar.Length; i++)
				{
					if (fileName.IndexOf(mojibakeChar, i, StringComparison.Ordinal) >= 0)
					{
						mojibakeCharacterCount++;
						i = fileName.IndexOf(mojibakeChar, i, StringComparison.Ordinal) + mojibakeChar.Length - 1;
					}
				}
			}
		}

		// 複数出現（同一文字が2個以上）または複数種類（異なる文字が2種類以上）の場合に真
		if (mojibakeCharacterCount > 1 || mojibakeCharacterKinds.Count > 1)
			return true;

		return false;
	}

	/// <summary>
	/// 正規化後ファイル名の Conflict をチェックします。
	/// </summary>
	private void CheckConflicts(BindingVolume volume, List<FileAnalysis> successAnalyses)
	{
		// Work フォルダ内の既存ファイル一覧（正規化対象外）
		var workFolderPath = volume.WorkFolderPath;
		var existingFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		if (Directory.Exists(workFolderPath))
		{
			var allFiles = Directory.GetFiles(workFolderPath);
			foreach (var file in allFiles)
			{
				existingFileNames.Add(Path.GetFileName(file));
			}
		}

		// 正規化後ファイル名同士の重複チェック用
		var normalizedFileNames = new Dictionary<string, List<BindingImage>>(StringComparer.OrdinalIgnoreCase);

		foreach (var analysis in successAnalyses)
		{
			if (analysis.Image.SimulatedNormalizedFileName == null)
				continue;

			var normalizedName = analysis.Image.SimulatedNormalizedFileName;

			if (!normalizedFileNames.ContainsKey(normalizedName))
			{
				normalizedFileNames[normalizedName] = new List<BindingImage>();
			}
			normalizedFileNames[normalizedName].Add(analysis.Image);
		}

		// Conflict を検出して設定
		foreach (var kvp in normalizedFileNames)
		{
			var normalizedName = kvp.Key;
			var images = kvp.Value;

			// BindingImage 同士の重複
			if (images.Count > 1)
			{
				// 競合に参加するすべての BindingImage を Conflict にする
				foreach (var image in images)
				{
					image.FileNameNormalizationStatus = FileNameNormalizationStatus.Conflict;
					image.FileNameNormalizationErrorMessage = $"正規化後ファイル名が他の画像と競合しています: {normalizedName}";
				}
			}
			else if (images.Count == 1)
			{
				// Work フォルダ内の既存ファイルとの重複
				if (existingFileNames.Contains(normalizedName) &&
					!string.Equals(images[0].FileName, normalizedName, StringComparison.OrdinalIgnoreCase))
				{
					var image = images[0];
					image.FileNameNormalizationStatus = FileNameNormalizationStatus.Conflict;
					image.FileNameNormalizationErrorMessage = $"正規化後ファイル名が Work フォルダ内の既存ファイルと競合しています: {normalizedName}";
				}
			}
		}
	}

	/// <summary>ファイル名解析の中間結果を保持するレコード。</summary>
	private sealed record ParseResult(
		string? Prefix,
		string? MainNumber,
		string? Suffix,
		string? Extension,
		string? ErrorMessage);

	/// <summary>Simulation 過程で各 BindingImage を追跡するレコード。</summary>
	private sealed record FileAnalysis(
		BindingImage Image,
		string? Prefix,
		string? MainNumber,
		string? Suffix,
		string? Extension,
		string? ErrorMessage);
}
