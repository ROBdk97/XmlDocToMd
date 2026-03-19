using Microsoft.Extensions.DependencyInjection;
using ROBdk97.XmlDocToMd.Logging;
using ROBdk97.XmlDocToMd.Rendering;

namespace ROBdk97.XmlDocToMd.Infrastructure;

/// <summary>
/// Extension methods that register the XmlDocToMd services into a
/// <see cref="IServiceCollection"/>.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all built-in tag-render strategies, the <see cref="ITagRendererRegistry"/>, and the default <see cref="IWarningLogger"/> (writing to <tt>stderr</tt>).
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for fluent chaining.</returns>
    /// <note>
    /// Each <see cref="ITagRenderStrategy"/> returned by <see cref="TagRenderers.CreateStrategies"/>
    /// is registered as a singleton. The <see cref="TagRendererRegistry"/> receives the full
    /// enumerable via constructor injection and builds its lookup dictionary at startup.
    /// </note>
    /// <tip>
    /// To add or replace a strategy, register your own <see cref="ITagRenderStrategy"/>
    /// singleton <i>after</i> calling this method — the last registration wins during
    /// dictionary construction inside <see cref="TagRendererRegistry"/>.
    /// </tip>
    internal static IServiceCollection AddXmlDocToMd(this IServiceCollection services)
    {
        foreach (var strategy in TagRenderers.CreateStrategies())
            services.AddSingleton<ITagRenderStrategy>(strategy);

        services.AddSingleton<ITagRendererRegistry, TagRendererRegistry>();
        services.AddSingleton<IWarningLogger>(_ => new TextWriterWarningLogger(System.Console.Error));

        return services;
    }
}
