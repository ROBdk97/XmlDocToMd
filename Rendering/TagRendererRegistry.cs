namespace ROBdk97.XmlDocToMd.Rendering;

/// <summary>
/// Default dictionary-backed implementation of <see cref="ITagRendererRegistry"/>.
/// Strategies are indexed at construction time for O(1) dispatch per tag.
/// </summary>
/// <remarks>
/// <note>
/// The static <see cref="Default"/> singleton is initialised lazily from
/// <see cref="TagRenderers.CreateStrategies"/>. It is intended for use outside the
/// DI container (e.g. in <see cref="ConversionContext"/> defaults or unit tests).
/// When running inside the DI container the singleton registered via
/// <see cref="Infrastructure.ServiceCollectionExtensions.AddXmlDocToMd"/> should be used instead.
/// </note>
/// </remarks>
internal sealed class TagRendererRegistry : ITagRendererRegistry
{
    private static readonly Lazy<ITagRendererRegistry> _default =
        new(() => new TagRendererRegistry(TagRenderers.CreateStrategies()));

    /// <summary>
    /// A lazily-initialised default registry built from the full built-in strategy set.
    /// </summary>
    /// <tip>
    /// Prefer injecting <see cref="ITagRendererRegistry"/> from the DI container in
    /// production code. Use <see cref="Default"/> only in contexts where DI is unavailable.
    /// </tip>
    internal static ITagRendererRegistry Default => _default.Value;

    private readonly Dictionary<string, ITagRenderStrategy> _strategies;

    /// <summary>
    /// Initialises the registry by indexing <paramref name="strategies"/> by their
    /// <see cref="ITagRenderStrategy.TagName"/> (case-insensitive).
    /// </summary>
    /// <param name="strategies">
    /// The full set of strategies to register. Duplicate tag names are not allowed.
    /// </param>
    public TagRendererRegistry(IEnumerable<ITagRenderStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.TagName, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public bool TryGetRenderer(string tagName, out ITagRenderStrategy? strategy)
        => _strategies.TryGetValue(tagName, out strategy);

    /// <inheritdoc/>
    public string Render(string tagName, XElement element, ConversionContext context)
    {
        if (!_strategies.TryGetValue(tagName, out var strategy))
            throw new KeyNotFoundException($"No renderer registered for tag \"{tagName}\".");

        return strategy!.Render(element, context);
    }
}
