namespace MangaBinder;

/// <summary>
/// DB のバージョンがアプリの要求するバージョンと一致しないことを表す例外です。
/// </summary>
public class DatabaseVersionMismatchException : Exception
{
	/// <summary>DB に保存されているバージョンを取得します。</summary>
	public string StoredVersion { get; }

	/// <summary>アプリが要求するバージョンを取得します。</summary>
	public string RequiredVersion { get; }

	/// <summary>
	/// <see cref="DatabaseVersionMismatchException"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="storedVersion">DB に保存されているバージョン。</param>
	/// <param name="requiredVersion">アプリが要求するバージョン。</param>
	public DatabaseVersionMismatchException(string storedVersion, string requiredVersion)
		: base($"DBバージョンが一致しません。DB={storedVersion}, 要求={requiredVersion}")
	{
		this.StoredVersion = storedVersion;
		this.RequiredVersion = requiredVersion;
	}
}
