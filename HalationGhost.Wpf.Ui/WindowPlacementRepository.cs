using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HalationGhost.Wpf.Ui;

/// <summary>
/// ウィンドウ位置・サイズ情報を JSON ファイルで永続化するリポジトリ。
/// </summary>
internal sealed class WindowPlacementRepository
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
	};

	/// <summary>
	/// JSON ファイルからウィンドウ位置・サイズ情報を読み込む。
	/// ファイルが存在しない場合は null を返す。デシリアライズ失敗時は例外を投げる。
	/// </summary>
	/// <param name="filePath">読み込み先ファイルパス</param>
	/// <returns>ウィンドウ位置・サイズ情報、またはファイル非存在時は null</returns>
	public WindowPlacement? Load(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return null;
		}

		var json = File.ReadAllText(filePath);
		var placement = JsonSerializer.Deserialize<WindowPlacement>(json, JsonOptions);
		return placement;
	}

	/// <summary>
	/// ウィンドウ位置・サイズ情報を JSON ファイルに保存。
	/// 保存先ディレクトリが存在しない場合は作成する。
	/// </summary>
	/// <param name="placement">保存するウィンドウ位置・サイズ情報</param>
	/// <param name="filePath">保存先ファイルパス</param>
	public void Save(WindowPlacement placement, string filePath)
	{
		var directory = Path.GetDirectoryName(filePath);
		if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
		{
			Directory.CreateDirectory(directory);
		}

		var json = JsonSerializer.Serialize(placement, JsonOptions);
		File.WriteAllText(filePath, json);
	}
}
