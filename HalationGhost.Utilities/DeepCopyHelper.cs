using System.Text.Json;

namespace HalationGhost.Utilities;

/// <summary>
/// JSON ベースのディープコピーを行うヘルパーユーティリティです。
/// </summary>
public static class DeepCopyHelper
{
	/// <summary>
	/// 指定したオブジェクトを JSON シリアライズ・デシリアライズしてディープコピーします。
	/// </summary>
	/// <typeparam name="T">コピー対象のオブジェクト型。</typeparam>
	/// <param name="value">コピー対象のオブジェクト。</param>
	/// <returns>ディープコピーされたオブジェクト。</returns>
	/// <exception cref="ArgumentNullException">value が null の場合にスローされます。</exception>
	/// <exception cref="InvalidOperationException">デシリアライズ結果が null の場合にスローされます。</exception>
	public static T Copy<T>(T value)
	{
		ArgumentNullException.ThrowIfNull(value);

		// JSON シリアライズ
		var json = JsonSerializer.Serialize(value);

		// JSON デシリアライズ
		var result = JsonSerializer.Deserialize<T>(json);

		// デシリアライズ結果の検証
		if (result == null)
			throw new InvalidOperationException($"デシリアライズに失敗しました。型 '{typeof(T).FullName}' から null が返されました。");

		return result;
	}
}

