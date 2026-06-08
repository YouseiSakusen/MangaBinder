using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MangaBinder.Bindings;
using MangaBinder.Tags;

namespace MangaBinder;

/// <summary>
/// 漫画作品のドメインモデルです。
/// </summary>
public class MangaSeries : INotifyPropertyChanged
{
    /// <summary>DBの主キーです。</summary>
    public long SeriesId { get; set; }

    /// <summary>内部用ノーマライズタイトルです。名寄せ・比較用に使用します。</summary>
    public string NormalizedTitleInternal { get; init; } = string.Empty;

    /// <summary>作品の代表タイトルです。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>著者名です。製本済みから抽出される正の情報です。</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>作品のあらすじです。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>作品自体が完結しているかを示すフラグです。</summary>
    public bool SeriesCompleted { get; init; }

    /// <summary>素材フォルダ名から判定される全巻所持フラグです。</summary>
    public bool IsOwnedCompleted { get; init; }

    /// <summary>素材フォルダが見つからなくなった作品として扱うかどうかを示します。</summary>
    public bool IsSourceMissing { get; set; }

    /// <summary>開始巻です。</summary>
    public int StartVolume { get; init; }

    /// <summary>作品の総巻数（完結巻）です。</summary>
    public int EndVolume { get; init; }

    /// <summary>製本済みファイルから抽出された製本済み最終巻です。</summary>
    public int BoundEndVolume { get; init; }

    /// <summary>素材フォルダ内のファイル名・フォルダ名から推定した手持ちの最大巻数です。</summary>
    public int OwnedMaxVolume { get; set; }

    /// <summary>外部用（API検索用）ノーマライズタイトルです。将来の外部API連携用です。</summary>
    public string NormalizedTitleExternal { get; init; } = string.Empty;

    /// <summary>略称タイトルです。</summary>
    public string ShortTitle { get; init; } = string.Empty;

    /// <summary>サムネイル画像のファイル名です。</summary>
    public string ThumbnailFileName { get; set; } = string.Empty;

    /// <summary>サムネイル処理の状態です。</summary>
    public ThumbnailStatus ThumbnailStatus { get; set; } = ThumbnailStatus.None;

    // /// <summary>サムネイル生成処理時間（ミリ秒）です。</summary>
    // public long ThumbnailProcessingTimeMs { get; set; }

    /// <summary>出版社名です。</summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>Google Books インポートの状態です。</summary>
    public GoogleBooksImportStatus GoogleBooksImportStatus { get; set; } = GoogleBooksImportStatus.NotImported;

    /// <summary>Google Books インポート日時です。</summary>
    public string GoogleBooksImportedAt { get; set; } = string.Empty;

    /// <summary>Google Books インポートメッセージです。</summary>
    public string GoogleBooksImportMessage { get; set; } = string.Empty;

    /// <summary>あらすじの取得元種別です。</summary>
    public DescriptionSource DescriptionSource { get; set; } = DescriptionSource.None;

    /// <summary>あらすじ取得元の実タイトルです（GoogleBooks で採用した書籍タイトル等）。</summary>
    public string DescriptionSourceTitle { get; set; } = string.Empty;

    /// <summary>サムネイル画像のファイル名（拡張子なし）を取得します。</summary>
    public string ThumbnailFileNameBase => $"{this.SeriesId:D6}_{this.ShortTitle}";

    /// <summary>この作品に付与されたタグ一覧を取得します。</summary>
    public ObservableCollection<MangaTag> Tags { get; } = new();

    /// <summary>タグ表示用テキストを取得します。</summary>
    public string TagDisplayText
    {
        get
        {
            if (this.Tags.Count == 0)
                return "⊕ タグを付ける";
            if (this.Tags.Count == 1)
                return $"🏷 {this.Tags[0].Name}";
            return $"🏷 {this.Tags[0].Name} +{this.Tags.Count - 1}";
        }
    }

    /// <summary>
    /// <see cref="MangaSeries"/> の新しいインスタンスを初期化します。
    /// </summary>
    public MangaSeries()
    {
        this.Tags.CollectionChanged += (_, _) => this.OnPropertyChanged(nameof(TagDisplayText));
    }

    /// <summary>製本対象として選択されているかどうかを示します。</summary>
    public bool IsSelected
    {
        get => this.isSelected;
        set
        {
            if (this.isSelected == value)
                return;
            this.isSelected = value;
            this.OnPropertyChanged();
        }
    }

    private bool isSelected;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>作品の所在情報リストです。</summary>
    public List<MangaSource> Sources { get; } = new();

    /// <summary>素材フォルダ（<see cref="FolderRole.Material"/>）の所在情報一覧を取得します。</summary>
    public IReadOnlyList<MangaSource> MaterialSources
        => this.Sources.Where(s => s.Role == Settings.FolderRole.Material).ToList();

    /// <summary>素材フォルダが1件以上存在するかを示します。</summary>
    public bool HasMaterialSources => this.Sources.Any(s => s.Role == Settings.FolderRole.Material);

    /// <summary>素材フォルダが1件のみの場合、その <see cref="MangaSource"/> を取得します。2件以上の場合は <c>null</c> を返します。</summary>
    public MangaSource? SingleMaterialSource
    {
        get
        {
            var materials = this.Sources.Where(s => s.Role == Settings.FolderRole.Material).ToList();
            return materials.Count == 1 ? materials[0] : null;
        }
    }

    /// <summary>素材フォルダが複数件存在するかを示します。</summary>
    public bool HasMultipleMaterialSources => this.Sources.Count(s => s.Role == Settings.FolderRole.Material) > 1;

    /// <summary>全巻数テキストです。完結していない場合は "-" を返します。</summary>
    public string TotalVolumeText
    {
        get
        {
            return this.SeriesCompleted
                ? $"全{this.EndVolume}巻"
                : "-";
        }
    }

    /// <summary>所持推定巻数テキストです。</summary>
    public string OwnedEstimatedVolumeText
    {
        get
        {
            return this.OwnedMaxVolume > 0
                ? $"所持推定：{this.OwnedMaxVolume}"
                : "所持推定：-";
        }
    }

    /// <summary>製本済み最終巻テキストです。</summary>
    public string BoundEndVolumeText
    {
        get
        {
            return this.BoundEndVolume > 0
                ? $"製本済み：{this.BoundEndVolume}"
                : "製本済み：-";
        }
    }

    /// <summary>あらすじ出典名の表示用テキストです。</summary>
    public string DescriptionSourceName
    {
        get
        {
            return this.DescriptionSource switch
            {
                DescriptionSource.GoogleBooks => "Google",
                _ => string.Empty,
            };
        }
    }

    /// <summary>あらすじ出典情報を表示するかどうかを示します。</summary>
    public bool HasDescriptionSource
        => this.DescriptionSource != DescriptionSource.None
           && !string.IsNullOrEmpty(this.DescriptionSourceTitle);
}


