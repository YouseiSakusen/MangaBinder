using MangaBinder.Bindings;
using MangaBinder.Helpers;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程全体に関係する操作を担当する Manager です。
/// </summary>
public class BindingManager
{
	private readonly BindingStore bindingStore;

	/// <summary>
	/// <see cref="BindingManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingStore">製本工程正本状態ストア。</param>
	public BindingManager(BindingStore bindingStore)
	{
		this.bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
	}

	/// <summary>
	/// 指定された作品を製本対象として設定します。
	/// 素材フォルダの利用可否を非同期確認した上で、成功した場合のみ BindingStore.BindingTarget を更新します。
	/// </summary>
	/// <param name="bindingSeries">製本対象の作品状態。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>素材フォルダの利用可否確認結果。</returns>
	public async ValueTask<MaterialSourceAvailabilityResult> SetBindingTargetAsync(
		BindingSeries bindingSeries,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(bindingSeries);

		// ファイルシステム確認を非同期実行（UIスレッドをブロックしない）
		var availabilityResult = await Task.Run(
			() => MaterialSourceAvailabilityHelper.CheckAvailability(bindingSeries.Series),
			cancellationToken);

		// 失敗した場合は BindingTarget を変更せず、結果を返す
		if (!availabilityResult.IsSuccess)
		{
			return availabilityResult;
		}

		// 成功した場合のみ、呼び出し元コンテキストで BindingTarget を設定
		this.bindingStore.BindingTarget.Value = bindingSeries;

		return availabilityResult;
	}
}
