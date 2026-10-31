using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace HalationGhost.Utilities.ShellLinks;

/// <summary>
/// Windows のショートカット（.lnk）を作成・読み込み・更新します。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ShellLink : IDisposable
{
	private const int MaxPath = 32768;
	private const uint StgmRead = 0;

	private object? comObject;
	private string? filePath;

	private ShellLink(object comObject, string? filePath)
	{
		this.comObject = comObject;
		this.filePath = filePath;
	}

	/// <summary>
	/// 新規ショートカットを作成します。保存は <see cref="Save(string)"/> で行います。
	/// </summary>
	/// <returns>新規の <see cref="ShellLink"/>。</returns>
	public static ShellLink Create()
		=> new(new ShellLinkCoClass(), null);

	/// <summary>
	/// 既存のショートカットを読み込みます。
	/// </summary>
	/// <param name="path">.lnk ファイルのパス。</param>
	/// <returns>読み込んだ <see cref="ShellLink"/>。</returns>
	public static ShellLink Load(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		var link = new ShellLink(new ShellLinkCoClass(), path);
		try
		{
			((IPersistFile)link.comObject!).Load(path, StgmRead);
			return link;
		}
		catch
		{
			link.Dispose();
			throw;
		}
	}

	/// <summary>
	/// リンク先実行ファイルのパスを取得・設定します。
	/// </summary>
	public string TargetPath
	{
		get
		{
			var buffer = new StringBuilder(MaxPath);
			this.shellLink.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);
			return buffer.ToString();
		}
		set => this.shellLink.SetPath(value);
	}

	/// <summary>
	/// 起動引数を取得・設定します。
	/// </summary>
	public string Arguments
	{
		get
		{
			var buffer = new StringBuilder(MaxPath);
			this.shellLink.GetArguments(buffer, buffer.Capacity);
			return buffer.ToString();
		}
		set => this.shellLink.SetArguments(value);
	}

	/// <summary>
	/// 作業ディレクトリを取得・設定します。
	/// </summary>
	public string WorkingDirectory
	{
		get
		{
			var buffer = new StringBuilder(MaxPath);
			this.shellLink.GetWorkingDirectory(buffer, buffer.Capacity);
			return buffer.ToString();
		}
		set => this.shellLink.SetWorkingDirectory(value);
	}

	/// <summary>
	/// 読み込み元のファイルへ上書き保存します。
	/// </summary>
	/// <exception cref="InvalidOperationException">保存先が未確定（<see cref="Create"/> 直後）の場合。</exception>
	public void Save()
		=> this.Save(this.filePath ?? throw new InvalidOperationException("保存先が指定されていません。"));

	/// <summary>
	/// 指定したパスへショートカットを保存します。
	/// </summary>
	/// <param name="path">保存先の .lnk ファイルのパス。</param>
	public void Save(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		((IPersistFile)this.shellLink).Save(path, true);
		this.filePath = path;
	}

	/// <summary>
	/// COM オブジェクトを解放します。
	/// </summary>
	public void Dispose()
	{
		if (this.comObject is null)
			return;

		Marshal.FinalReleaseComObject(this.comObject);
		this.comObject = null;
	}

	private IShellLinkW shellLink
		=> (IShellLinkW)(this.comObject ?? throw new ObjectDisposedException(nameof(ShellLink)));
}
