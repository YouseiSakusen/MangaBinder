using MangaBinder.Bindings;
using MangaBinder.Settings;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本前確認工程全体を統括するマネージャーです。
/// 将来の処理では BindingStore から製本状態を直接取得し、
/// 下位の Builder / Inspector 等を制御して製本前確認処理を実行します。
/// </summary>
public class SeriesInspectionManager
{
	private readonly BindingStore bindingStore;
	private readonly AppSettings appSettings;
	private readonly WorkVolumeBuilder workVolumeBuilder;

	/// <summary>
	/// <see cref="SeriesInspectionManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingStore">製本工程の正本状態ストア。</param>
	/// <param name="appSettings">アプリケーション設定。Workフォルダパス生成に使用します。</param>
	/// <param name="workVolumeBuilder">Work巻フォルダ実体化ビルダー。</param>
	/// <exception cref="ArgumentNullException"><paramref name="bindingStore"/>、<paramref name="appSettings"/>、または <paramref name="workVolumeBuilder"/> が null の場合。</exception>
	public SeriesInspectionManager(
		BindingStore bindingStore,
		AppSettings appSettings,
		WorkVolumeBuilder workVolumeBuilder)
	{
		this.bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
		this.appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
		this.workVolumeBuilder = workVolumeBuilder ?? throw new ArgumentNullException(nameof(workVolumeBuilder));
	}

	/// <summary>
	/// 製本前確認処理を実行します。
	/// BindingStore から製本状態を取得し、
	/// 製本前確認工程全体の処理を制御します。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	/// <exception cref="OperationCanceledException">キャンセルトークンがキャンセルされた場合。</exception>
	/// <exception cref="InvalidOperationException">BindingTarget が設定されていない、または BindingVolume の VolumeNumber が null の場合。</exception>
	public async ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
	{
		// ① CancellationToken のキャンセル要求を確認する
		cancellationToken.ThrowIfCancellationRequested();

		// ② BindingStore.BindingTarget から対象作品を取得する
		var bindingTarget = this.bindingStore.BindingTarget.Value;
		if (bindingTarget is null)
		{
			throw new InvalidOperationException(
				"製本前確認処理には BindingStore.BindingTarget が設定されている必要があります。");
		}

		// ③ AppSettings.CreateWorkSeriesFolderPath() を使用して、対象作品の作品Workフォルダパスを取得する
		var seriesFolderPath = this.appSettings.CreateWorkSeriesFolderPath(bindingTarget.Series.Title);

		// ④ BindingStore.RecreateWorkFolder.Value が true の場合、作品Workフォルダが存在すれば削除する
		// ファイルシステムの重い処理でUIスレッドをブロックしないようにする
		if (this.bindingStore.RecreateWorkFolder.Value && Directory.Exists(seriesFolderPath))
		{
			await Task.Run(
				() => Directory.Delete(seriesFolderPath, recursive: true),
				cancellationToken).ConfigureAwait(false);
		}

		// ⑤ BindingStore.BindingVolumes を順番に処理して WorkFolderPath を設定し、
		// 新規実体化が必要なボリュームのみをフィルタリングする
		var volumeFolderDigits = this.bindingStore.VolumeFolderDigits.Value;
		var volumesRequiringNewBuild = new List<BindingVolume>();

		foreach (var bindingVolume in this.bindingStore.BindingVolumes)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// VolumeNumber が null の場合は不正
			var volumeNumber = bindingVolume.VolumeNumber.Value;
			if (volumeNumber is null)
			{
				throw new InvalidOperationException(
					$"BindingVolume (Material: {bindingVolume.Material.Name}) の VolumeNumber が設定されていません。" +
					"巻選択工程を通過した状態として不正です。");
			}

			// 巻フォルダ名を生成
			var volumeFolderName = this.appSettings.CreateWorkVolumeFolderName(
				volumeNumber.Value,
				volumeFolderDigits);

			// 巻WorkフォルダのフルパスをBindingVolumeに設定
			bindingVolume.WorkFolderPath = Path.Combine(seriesFolderPath, volumeFolderName);

			// ⑥ 各 BindingVolume.WorkFolderPath について Directory.Exists() で存在を確認
			var workFolderExists = Directory.Exists(bindingVolume.WorkFolderPath);

			// 巻フォルダが存在しない場合は新規実体化対象に追加
			if (!workFolderExists)
			{
				volumesRequiringNewBuild.Add(bindingVolume);
			}
		}

		// ⑦ 新規実体化が必要なボリュームを Material.SourcePath でGroupBy する
		// 文字列比較には StringComparer.OrdinalIgnoreCase を使用
		var groupsBySourcePath = volumesRequiringNewBuild
			.GroupBy(v => v.Material.SourcePath, StringComparer.OrdinalIgnoreCase)
			.ToList();

		// ⑧ 各グループについてWorkVolumeBuilder.BuildAsync() を呼び出す
		foreach (var group in groupsBySourcePath)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// グループ内のボリュームリストを IReadOnlyList に変換して渡す
			var volumeList = group.ToList().AsReadOnly();
			await this.workVolumeBuilder.BuildAsync(volumeList, cancellationToken)
				.ConfigureAwait(false);
		}
	}
}
