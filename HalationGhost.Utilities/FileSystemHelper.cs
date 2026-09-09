using Microsoft.VisualBasic.FileIO;
using System.Runtime.Versioning;

namespace HalationGhost.Utilities;

/// <summary>
/// ファイルシステム操作に関するユーティリティです。
/// </summary>
[SupportedOSPlatform("windows")]
public static class FileSystemHelper
{
	/// <summary>
	/// 指定されたファイルまたはディレクトリをごみ箱へ移動します。
	/// </summary>
	/// <param name="path">ごみ箱へ移動するファイルまたはディレクトリのパス。</param>
	/// <exception cref="ArgumentException">path が null、空文字、または空白のみの場合。</exception>
	/// <exception cref="FileNotFoundException">指定されたファイルまたはディレクトリが存在しない場合。</exception>
	public static void SendToRecycleBin(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		if (Directory.Exists(path))
		{
			FileSystem.DeleteDirectory(
				path,
				UIOption.OnlyErrorDialogs,
				RecycleOption.SendToRecycleBin);
			return;
		}

		if (File.Exists(path))
		{
			FileSystem.DeleteFile(
				path,
				UIOption.OnlyErrorDialogs,
				RecycleOption.SendToRecycleBin);
			return;
		}

		throw new FileNotFoundException($"指定されたパスが見つかりません: {path}");
	}
}
