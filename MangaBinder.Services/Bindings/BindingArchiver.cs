using System.IO;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本対象のWorkフォルダを1つのZIPファイルへまとめるクラスです。
/// SharpCompress を使用した非同期ZIP作成を行います。
/// </summary>
public class BindingArchiver
{
	/// <summary>
	/// <see cref="BindingArchiver"/> の新しいインスタンスを初期化します。
	/// </summary>
	public BindingArchiver()
	{
	}

	/// <summary>
	/// 指定された BindingVolume のWorkフォルダ配下全ファイルを、
	/// 1つのZIPファイルへ非同期で格納します。
	/// </summary>
	/// <param name="volumes">製本対象の巻一覧。</param>
	/// <param name="outputFilePath">出力ZIPファイルのフルパス。</param>
	/// <param name="allowOverwrite">
	/// 同名の既存ZIPファイルを上書きする場合は true、新規作成のみの場合は false。
	/// true の場合、既存ファイルの内容は完全に破棄され、今回の BindingVolumes だけから新しいZIPが再生成されます。
	/// false の場合、既存ファイルが存在すれば例外が発生します。
	/// </param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	/// <exception cref="ArgumentException">
	/// 以下の場合に例外が発生します：
	/// - volume の WorkFolderPath が null または空
	/// - WorkFolderPath が存在しない
	/// </exception>
	/// <exception cref="IOException">
	/// ファイル操作に失敗した場合、例外が発生します：
	/// - allowOverwrite == false で outputFilePath が既に存在する (FileMode.CreateNew)
	/// - ファイル列挙、読み込み、ZIP書き込みでのI/Oエラー
	/// </exception>
	public async ValueTask CreateAsync(
		IReadOnlyList<BindingVolume> volumes,
		string outputFilePath,
		bool allowOverwrite = false,
		CancellationToken cancellationToken = default)
	{
		// 入力値の検証
		ArgumentNullException.ThrowIfNull(volumes);
		ArgumentNullException.ThrowIfNullOrWhiteSpace(outputFilePath);

		// 各BindingVolumeのWorkFolderPathを検証し、有効なパスのリストを作成
		var workFolderPaths = new List<string>(volumes.Count);
		foreach (var volume in volumes)
		{
			var workFolderPath = volume.WorkFolderPath;

			if (string.IsNullOrEmpty(workFolderPath))
			{
				throw new ArgumentException(
					$"BindingVolume の WorkFolderPath が null または空です。",
					nameof(volumes));
			}

			var workFolderInfo = new DirectoryInfo(workFolderPath);
			if (!workFolderInfo.Exists)
			{
				throw new ArgumentException(
					$"Workフォルダが存在しません: {workFolderPath}",
					nameof(volumes));
			}

			workFolderPaths.Add(workFolderPath);
		}

		try
		{
			// ZIP作成（allowOverwrite に応じて FileMode を選択）
			// allowOverwrite == false: FileMode.CreateNew（新規作成のみ、既存ファイルで例外）
			// allowOverwrite == true: FileMode.Create（既存ファイルを上書き）
			var fileMode = allowOverwrite ? FileMode.Create : FileMode.CreateNew;
			using (var outputStream = new FileStream(outputFilePath, fileMode, FileAccess.Write))
			{
				var writer = await WriterFactory.OpenAsyncWriter(
					outputStream,
					ArchiveType.Zip,
					new WriterOptions(CompressionType.None),
					cancellationToken);

				await using (writer)
				{
					// 検証済みの WorkFolderPath を使用して各巻のファイルをZIPへ追加
					foreach (var workFolderPath in workFolderPaths)
					{
						await this.addVolumeFilesToZipAsync(writer, workFolderPath, cancellationToken);
					}
				}
			}
		}
		catch (Exception zipCreationException)
		{
			// ZIP作成途中で例外が発生した場合、途中まで作成されたファイルを削除
			Exception? deleteException = this.tryDeleteOutputFile(outputFilePath);

			// 削除に失敗した場合は両例外を AggregateException で伝播
			if (deleteException is not null)
			{
				throw new AggregateException(
					"ZIP作成に失敗し、後始末でもエラーが発生しました。",
					zipCreationException,
					deleteException);
			}

			// 削除に成功した場合は元の例外だけを伝播（スタックトレース維持）
			throw;
		}
	}

	/// <summary>
	/// 指定されたパスにファイルが存在する場合、削除を試みます。
	/// </summary>
	/// <param name="filePath">削除対象のファイルパス。</param>
	/// <returns>削除に成功した場合は null、失敗した場合は発生した例外を返します。</returns>
	private Exception? tryDeleteOutputFile(string filePath)
	{
		try
		{
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
			}

			return null;
		}
		catch (Exception ex)
		{
			// 削除失敗の例外を呼び出し元へ返す
			return ex;
		}
	}

	/// <summary>
	/// 指定されたWorkフォルダ配下の全ファイルをZIPへ追加します。
	/// </summary>
	/// <param name="writer">ZIPWriter。</param>
	/// <param name="workFolderPath">Workフォルダのパス。null または空は許可されません。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	private async ValueTask addVolumeFilesToZipAsync(
		IAsyncWriter writer,
		string workFolderPath,
		CancellationToken cancellationToken)
	{
		// workFolderPath は既に CreateAsync 内で null/空チェックされているため、
		// ここでも安全性を確保するために再度検証してから使用する
		if (string.IsNullOrEmpty(workFolderPath))
		{
			throw new ArgumentException(
				"workFolderPath が null または空です。",
				nameof(workFolderPath));
		}

		var workFolderInfo = new DirectoryInfo(workFolderPath);
		var workFolderName = workFolderInfo.Name;

		// WorkFolderPath配下の全ファイルを再帰的に列挙
		var files = workFolderInfo.EnumerateFiles("*", SearchOption.AllDirectories).ToList();

		foreach (var fileInfo in files)
		{
			// WorkFolderPath からの相対パスを取得
			var relativePath = Path.GetRelativePath(workFolderPath, fileInfo.FullName);

			// ZIP用の区切り文字を "/" に正規化
			var entryName = (workFolderName + "/" + relativePath).Replace("\\", "/");

			// ファイルをストリームで読み込んでZIPへ追加
			using (var fileStream = new FileStream(
				fileInfo.FullName,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read))
			{
				// WriteAsync(entryName, stream, modificationTime)
				// CancellationToken は WriteAsync メソッドでサポートされていない可能性があるため、ここでは渡さない
				await writer.WriteAsync(entryName, fileStream, fileInfo.LastWriteTime);
			}
		}
	}
}
