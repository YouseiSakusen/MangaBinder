using System.Diagnostics;
using System.Runtime.InteropServices;
using HalationGhost.Utilities;

namespace MangaBinder.Startup;

/// <summary>
/// UI の多重起動防止を行います。
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
	private const string MutexName = @"Local\MangaBinder.UI.SingleInstance";
	private const int SwRestore = 9;

	private Mutex? mutex;

	/// <summary>
	/// 名前付き Mutex を作成し、初回起動かどうかを判定します。
	/// 所有権は取得せず、Mutex の存在（ハンドルの生存）だけで判定するため、非同期処理でスレッドが変わっても問題ありません。
	/// </summary>
	/// <returns>初回起動の場合は true。多重起動の場合は false。</returns>
	public bool TryAcquire()
	{
		this.mutex = new Mutex(false, MutexName, out var createdNew);
		return createdNew;
	}

	/// <summary>
	/// 既に起動している MangaBinder のウィンドウを復元して前面に表示します。
	/// </summary>
	public void ActivateExistingWindow()
	{
		var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("実行ファイルのパスを取得できません。");
		var currentId = Environment.ProcessId;

		foreach (var process in ProcessHelper.FindProcesses(processPath))
		{
			using (process)
			{
				if (process.Id == currentId)
					continue;

				var handle = process.MainWindowHandle;
				if (handle == IntPtr.Zero)
					continue;

				if (IsIconic(handle))
					ShowWindow(handle, SwRestore);

				SetForegroundWindow(handle);
				return;
			}
		}
	}

	/// <summary>
	/// Mutex を解放します。
	/// </summary>
	public void Dispose()
	{
		this.mutex?.Dispose();
		this.mutex = null;
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsIconic(IntPtr hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetForegroundWindow(IntPtr hWnd);
}
