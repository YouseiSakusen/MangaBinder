using System.IO;
using System.Text.RegularExpressions;
using MangaBinder.Settings;

namespace MangaBinder.Binding.Prepress;

/// <summary>
/// 巻フォルダ内ファイル名の主番号部分をゼロ埋めして桁数を揃えるクラスです。
/// </summary>
/// <remarks>
/// 完全再採番は行いません。元ファイル名の意味を残しつつ、主番号の桁数のみを統一します。
/// </remarks>
public sealed class VolumeFileNameNormalizer
{
	// "prefix + 主番号 + (枝番: _nn または -nn)? + 拡張子" を捉えるパターン
	// 主番号: 末尾から見て最初の数値グループ（枝番がある場合はその前の数値）
	// 枝番: _ または - + 数値 + 拡張子直前
	private static readonly Regex fileNamePattern = new(
		@"^(?<prefix>.*?)(?<main>\d+)(?<suffix>(?:[_\-]\d+)*)(?<ext>\.[^.]+)$",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	/// <summary>
	/// 巻フォルダ直下のファイル名を解析し、リネーム結果の一覧を返します。
	/// エラーが1件でもある場合は実リネームを実行しません。
	/// </summary>
	/// <param name="folderPath">対象巻フォルダのパス。</param>
	/// <returns>各ファイルの <see cref="NormalizationResult"/> 一覧。</returns>
	public IReadOnlyList<NormalizationResult> Normalize(string folderPath)
	{
		var files = Directory.GetFiles(folderPath)
			.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		// リネーム対象（画像かつ変換不要）のみ解析
		var candidates = files
			.Where(f => this.isRenameTarget(Path.GetExtension(f)))
			.ToArray();

		if (candidates.Length == 0)
			return [];

		// 主番号を解析
		var analyses = candidates
			.Select(f => this.analyzeFileName(f))
			.ToArray();

		var errors = analyses.Where(a => a.HasError).ToList();

		// 解析成功分から最大桁数を決定
		var successAnalyses = analyses.Where(a => !a.HasError).ToArray();
		if (successAnalyses.Length == 0)
			return analyses.Select(a => new NormalizationResult(a.FilePath, a.FilePath, true, a.ErrorMessage)).ToArray();

		var maxDigits = successAnalyses.Max(a => a.MainNumber!.Length);

		// 補正後ファイル名を生成
		var results = analyses.Select(a =>
		{
			if (a.HasError)
				return new NormalizationResult(a.FilePath, a.FilePath, true, a.ErrorMessage);

			var paddedMain = a.MainNumber!.PadLeft(maxDigits, '0');
			var newName = $"{a.Prefix}{paddedMain}{a.Suffix}{a.Extension}";
			var newPath = Path.Combine(Path.GetDirectoryName(a.FilePath)!, newName);
			return new NormalizationResult(a.FilePath, newPath, false, null);
		}).ToList();

		// 衝突チェック: 補正後パスと既存ファイルパス・他の補正後パスとの重複
		var existingPaths = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
		var newPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		var checkedResults = results.Select(r =>
		{
			if (r.HasError)
				return r;
			if (!r.RequiresRename)
				return r;

			// 補正後パスが既存ファイル（リネーム元以外）と衝突するか
			if (existingPaths.Contains(r.NewPath) &&
				!string.Equals(r.OriginalPath, r.NewPath, StringComparison.OrdinalIgnoreCase))
				return r with { HasError = true, ErrorMessage = $"補正後ファイル名が既存ファイルと衝突します: {Path.GetFileName(r.NewPath)}" };

			// 他の補正後パスと衝突するか
			if (!newPaths.Add(r.NewPath))
				return r with { HasError = true, ErrorMessage = $"補正後ファイル名が重複します: {Path.GetFileName(r.NewPath)}" };

			return r;
		}).ToList();

		// エラーが1件でもあれば実リネームせずに結果だけ返す
		if (checkedResults.Any(r => r.HasError))
			return checkedResults;

		// 実リネーム（一時ファイル名経由で衝突回避）
		this.executeRename(checkedResults);

		return checkedResults;
	}

	/// <summary>
	/// 対象ファイルがリネーム対象かどうかを判定します。
	/// </summary>
	private bool isRenameTarget(string extension)
		=> SupportedExtensionHelper.IsImage(extension) && !SupportedExtensionHelper.RequiresConversion(extension);

	/// <summary>
	/// ファイルパスから主番号・プレフィックス・サフィックス・拡張子を解析します。
	/// </summary>
	private FileAnalysis analyzeFileName(string filePath)
	{
		var name = Path.GetFileNameWithoutExtension(filePath);
		var ext = Path.GetExtension(filePath);
		var fullName = Path.GetFileName(filePath);
		var match = fileNamePattern.Match(fullName);

		if (!match.Success)
			return new FileAnalysis(filePath, null, null, null, ext, true, $"ファイル名を解析できませんでした: {fullName}");

		return new FileAnalysis(
			filePath,
			match.Groups["prefix"].Value,
			match.Groups["main"].Value,
			match.Groups["suffix"].Value,
			match.Groups["ext"].Value,
			false,
			null);
	}

	/// <summary>
	/// 一時ファイル名を使ってリネームを実行します。
	/// </summary>
	private void executeRename(IReadOnlyList<NormalizationResult> results)
	{
		var renameTargets = results.Where(r => r.RequiresRename).ToArray();
		if (renameTargets.Length == 0)
			return;

		// 1パス目: 元 → 一時ファイル名
		var tempMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var result in renameTargets)
		{
			var tempPath = Path.Combine(
				Path.GetDirectoryName(result.OriginalPath)!,
				$"__temp__{Guid.NewGuid():N}{Path.GetExtension(result.OriginalPath)}");
			File.Move(result.OriginalPath, tempPath);
			tempMap[result.OriginalPath] = tempPath;
		}

		// 2パス目: 一時ファイル名 → 補正後ファイル名
		foreach (var result in renameTargets)
		{
			var tempPath = tempMap[result.OriginalPath];
			File.Move(tempPath, result.NewPath);
		}
	}

	/// <summary>ファイル名解析の内部結果を保持するレコード。</summary>
	private sealed record FileAnalysis(
		string FilePath,
		string? Prefix,
		string? MainNumber,
		string? Suffix,
		string Extension,
		bool HasError,
		string? ErrorMessage);
}
