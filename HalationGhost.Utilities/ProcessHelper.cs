using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace HalationGhost.Utilities;

/// <summary>
/// プロセスに関するユーティリティです。
/// </summary>
[SupportedOSPlatform("windows")]
public static class ProcessHelper
{
	private const uint ProcessQueryLimitedInformation = 0x1000;

	/// <summary>
	/// 指定した実行ファイルのフルパスと一致するプロセスが起動中かどうかを返します。
	/// </summary>
	/// <param name="executablePath">実行ファイルのフルパス。</param>
	/// <returns>起動中の場合は true。</returns>
	public static bool IsRunning(string executablePath)
		=> FindProcesses(executablePath).Count > 0;

	/// <summary>
	/// 指定した実行ファイルのフルパスと一致する起動中のプロセスを取得します。
	/// 呼び出し元は返却された <see cref="Process"/> を破棄してください。
	/// </summary>
	/// <param name="executablePath">実行ファイルのフルパス。</param>
	/// <returns>一致したプロセスの一覧。</returns>
	public static IReadOnlyList<Process> FindProcesses(string executablePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

		var fullPath = Path.GetFullPath(executablePath);
		var matched = new List<Process>();
		foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(fullPath)))
		{
			if (string.Equals(getImagePath(process.Id), fullPath, StringComparison.OrdinalIgnoreCase))
				matched.Add(process);
			else
				process.Dispose();
		}

		return matched;
	}

	private static string? getImagePath(int processId)
	{
		var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
		if (handle == IntPtr.Zero)
			return null;

		try
		{
			var buffer = new StringBuilder(32768);
			var size = buffer.Capacity;
			return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString(0, size) : null;
		}
		finally
		{
			CloseHandle(handle);
		}
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CloseHandle(IntPtr hObject);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);
}
