using MangaBinder.Settings;

namespace MangaBinder.Jobs;

/// <summary>
/// ジョブキューのレコードを表すクラスです（Worker内部用）。
/// </summary>
public sealed class JobQueue
{
    public long Id { get; set; }
    public JobType Type { get; set; }
    public JobStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool SkipThumbnailSizeLimit { get; set; }
}
