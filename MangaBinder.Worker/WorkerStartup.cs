using System.Runtime.InteropServices;

namespace MangaBinder.Jobs;

/// <summary>
/// Worker の起動制御（多重起動防止・コンソール表示制御）を行います。
/// </summary>
internal static class WorkerStartup
{
	private const string MutexName = @"Local\MangaBinder.Worker.SingleInstance";
	private const string HiddenArgument = "--hidden";
	private const int SwHide = 0;

	/// <summary>
	/// 名前付き Mutex を作成します。既に他の Worker が起動中の場合は null を返します。
	/// 所有権は取得せず、Mutex の存在（ハンドルの生存）だけで多重起動を判定するため、
	/// 非同期処理でスレッドが変わっても問題ありません。呼び出し元はプロセス終了まで保持してください。
	/// </summary>
	/// <returns>初回起動の場合は Mutex。多重起動の場合は null。</returns>
	public static Mutex? TryAcquireInstance()
	{
		var mutex = new Mutex(false, MutexName, out var createdNew);
		if (createdNew)
			return mutex;

		mutex.Dispose();
		return null;
	}

	/// <summary>
	/// 起動引数から <c>--hidden</c> を取り除き、指定されていた場合はコンソールを非表示にします。
	/// </summary>
	/// <param name="args">起動引数。</param>
	/// <returns>Generic Host へ渡す引数。</returns>
	public static string[] ApplyConsoleMode(string[] args)
	{
		var hidden = args.Any(a => string.Equals(a, HiddenArgument, StringComparison.OrdinalIgnoreCase));
		if (!hidden)
			return args;

		var consoleWindow = GetConsoleWindow();
		if (consoleWindow != IntPtr.Zero)
			ShowWindow(consoleWindow, SwHide);

		return args.Where(a => !string.Equals(a, HiddenArgument, StringComparison.OrdinalIgnoreCase)).ToArray();
	}

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetConsoleWindow();

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
