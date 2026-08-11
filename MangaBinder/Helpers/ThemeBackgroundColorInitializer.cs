using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using MangaBinder.Settings;

namespace MangaBinder.Helpers;

/// <summary>
/// 初回起動時にWpf.Uiテーマから自動生成したサムネイル背景色をDBへ保存する処理を担当します。
/// </summary>
public class ThemeBackgroundColorInitializer
{
	/// <summary>ロガー。</summary>
	private readonly ILogger<ThemeBackgroundColorInitializer> logger;

	/// <summary>アプリケーション設定。</summary>
	private readonly AppSettings appSettings;

	/// <summary>アプリケーション設定サービス。</summary>
	private readonly AppSettingsService appSettingsService;

	/// <summary>
	/// <see cref="ThemeBackgroundColorInitializer"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="logger">ロガー。</param>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="appSettingsService">アプリケーション設定サービス。</param>
	public ThemeBackgroundColorInitializer(
		ILogger<ThemeBackgroundColorInitializer> logger,
		AppSettings appSettings,
		AppSettingsService appSettingsService)
	{
		this.logger = logger;
		this.appSettings = appSettings;
		this.appSettingsService = appSettingsService;
	}

	/// <summary>
	/// ThumbnailBackgroundColorが未設定の場合、
	/// 現在適用されているWpf.Uiテーマから自動生成してDBへ保存します。
	/// </summary>
	public async ValueTask InitializeAsync()
	{
		// ThumbnailBackgroundColorが既に設定済みかチェック
		if (!this.isThumbnailBackgroundColorEmpty())
		{
			this.logger.LogInformation("ThumbnailBackgroundColorは既に設定されています。初期化をスキップします。");
			return;
		}

		this.logger.LogInformation("ThumbnailBackgroundColorが未設定です。テーマから自動生成を開始します。");

		try
		{
			// テーマから背景色を算出
			var backgroundColor = this.calculateBackgroundColorFromTheme();
			if (backgroundColor == null)
			{
				this.logger.LogError("テーマから背景色を算出できませんでした。");
				return;
			}

			// AppSettingsに反映
			this.appSettings.ThumbnailBackgroundColor.Value = backgroundColor;

			// DBへ保存
			await this.appSettingsService.SaveAppSettingsAsync();

			this.logger.LogInformation("ThumbnailBackgroundColorを自動生成してDBへ保存しました。背景色={BackgroundColor}", backgroundColor);
		}
		catch (Exception ex)
		{
			this.logger.LogError(ex, "ThumbnailBackgroundColorの初期化に失敗しました。");
		}
	}

	/// <summary>
	/// ThumbnailBackgroundColorが空かどうかを判定します。
	/// </summary>
	/// <returns>空の場合は true、値が設定されている場合は false。</returns>
	private bool isThumbnailBackgroundColorEmpty()
	{
		var value = this.appSettings.ThumbnailBackgroundColor.Value;
		return string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value);
	}

	/// <summary>
	/// Wpf.Uiテーマから SolidBackgroundFillColorBaseBrush を取得し、
	/// 不透明な #RRGGBB 形式の背景色を生成します。
	/// </summary>
	/// <returns>生成された背景色。失敗時は null。</returns>
	private string? calculateBackgroundColorFromTheme()
	{
		// テーマリソースから SolidBackgroundFillColorBaseBrush を取得
		var brush = this.getBrushFromResources("SolidBackgroundFillColorBaseBrush");
		if (brush == null)
		{
			this.logger.LogError("SolidBackgroundFillColorBaseBrushが取得できません。");
			return null;
		}

		// Brush の Color から #RRGGBB を生成
		try
		{
			var color = brush.Color;
			var r = color.R;
			var g = color.G;
			var b = color.B;

			return $"#{r:X2}{g:X2}{b:X2}";
		}
		catch (Exception ex)
		{
			this.logger.LogError(ex, "背景色の #RRGGBB 形式への変換に失敗しました。");
			return null;
		}
	}

	/// <summary>
	/// Application.Current.ResourcesからBrushを取得します。
	/// </summary>
	/// <param name="resourceKey">リソースキー。</param>
	/// <returns>見つかったBrush、見つからない場合は null。</returns>
	private SolidColorBrush? getBrushFromResources(string resourceKey)
	{
		try
		{
			var resource = Application.Current.Resources[resourceKey];
			if (resource is SolidColorBrush brush)
			{
				return brush;
			}

			this.logger.LogError("リソース{ResourceKey}が SolidColorBrush ではありません。型={Type}",
				resourceKey, resource?.GetType().Name ?? "null");
			return null;
		}
		catch (Exception ex)
		{
			this.logger.LogError(ex, "リソース{ResourceKey}の取得に失敗しました。", resourceKey);
			return null;
		}
	}
}