using MangaBinder.Jobs.Contexts;
using MangaBinder.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MangaBinder.Jobs.Extensions;

/// <summary>
/// Worker 用の DI 登録拡張メソッドを提供します。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// <see cref="WorkerContext"/> を Singleton で登録します。
    /// </summary>
    /// <param name="services">DI サービスコレクション。</param>
    /// <returns>メソッドチェーン用に <paramref name="services"/> を返します。</returns>
    public static IServiceCollection AddMangaWorkerContext(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var builder = new WorkerContextBuilder(config);
            var context = builder.BuildAsync().GetAwaiter().GetResult();
            SupportedExtensionHelper.Initialize(context.SupportedExtensions);
            return context;
        });

        services.AddSingleton<IMangaBinderConfig>(sp => sp.GetRequiredService<WorkerContext>());

        return services;
    }
}
