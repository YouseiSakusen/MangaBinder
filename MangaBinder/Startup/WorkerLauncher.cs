using System.Diagnostics;
using System.IO;
using HalationGhost.Utilities;
using HalationGhost.Utilities.ShellLinks;

namespace MangaBinder.Startup;

/// <summary>
/// Worker のスタートアップ登録と起動確認を行います。
/// </summary>
internal static class WorkerLauncher
{
	private const string ShortcutFileName = "MangaBinder.Worker.lnk";
	private const string WorkerArguments = "--hidden";
	private const string AlphaFileName = "alpha.txt";

	/// <summary>
	/// スタートアップショートカットを確認・作成・更新し、Worker が未起動であればショートカット経由で起動します。
	/// alpha.txt が存在する場合は何も行いません。
	/// </summary>
	public static void EnsureWorkerRunning()
	{
		var baseDirectory = AppContext.BaseDirectory;
		if (File.Exists(Path.Combine(baseDirectory, AlphaFileName)))
			return;

		var workerDirectory = Path.GetFullPath(Path.Combine(baseDirectory, "worker"));
		var workerPath = Path.Combine(workerDirectory, "MangaBinder.Worker.exe");
		var shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutFileName);

		ensureShortcut(shortcutPath, workerPath, workerDirectory);

		if (ProcessHelper.IsRunning(workerPath))
			return;

		Process.Start(new ProcessStartInfo
		{
			FileName = shortcutPath,
			UseShellExecute = true,
		})?.Dispose();
	}

	private static void ensureShortcut(string shortcutPath, string workerPath, string workerDirectory)
	{
		if (!File.Exists(shortcutPath))
		{
			using var created = ShellLink.Create();
			apply(created, workerPath, workerDirectory);
			created.Save(shortcutPath);
			return;
		}

		using var link = ShellLink.Load(shortcutPath);
		var upToDate =
			string.Equals(link.TargetPath, workerPath, StringComparison.OrdinalIgnoreCase)
			&& string.Equals(link.Arguments, WorkerArguments, StringComparison.Ordinal)
			&& string.Equals(link.WorkingDirectory, workerDirectory, StringComparison.OrdinalIgnoreCase);
		if (upToDate)
			return;

		apply(link, workerPath, workerDirectory);
		link.Save();
	}

	private static void apply(ShellLink link, string workerPath, string workerDirectory)
	{
		link.TargetPath = workerPath;
		link.Arguments = WorkerArguments;
		link.WorkingDirectory = workerDirectory;
	}
}
