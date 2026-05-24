using NetVips;

namespace MangaBinder.Binding.Inspection;

/// <summary>
/// NetVips を使用した製本用画像変換実装です。
/// </summary>
public sealed class VolumeImageProcessor : IVolumeImageProcessor
{
	/// <inheritdoc />
	public ValueTask<ConvertedImageResult> ConvertAsync(
		Stream sourceStream,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		using var image = Image.NewFromStream(sourceStream);

		var width = image.Width;
		var height = image.Height;

		var output = new MemoryStream();
		image.WriteToStream(output, ".jpg");
		output.Position = 0;

		return ValueTask.FromResult(new ConvertedImageResult(output, width, height));
	}
}
