namespace ROBdk97.XmlDocToMd.Rendering;

/// <summary>
/// Defines the contract for a single XML-tag-to-Markdown renderer.
/// </summary>
/// <remarks>
/// Each implementation handles exactly one XML tag name (identified by
/// <see cref="TagName"/>). Strategies are collected at startup by
/// <see cref="TagRenderers.CreateStrategies"/> and registered in an
/// <see cref="ITagRendererRegistry"/> for O(1) dispatch.
/// <tip>
/// Implement this interface to add support for a custom or non-standard XML doc tag
/// without modifying any existing code — just register the new strategy via
/// <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/>.
/// </tip>
/// </remarks>
internal interface ITagRenderStrategy
{
    /// <summary>
    /// The XML element name this strategy handles (e.g. <tt>"summary"</tt>,
    /// <tt>"param"</tt>, <tt>"note"</tt>).
    /// </summary>
    /// <note>
    /// The name is matched case-insensitively by <see cref="ITagRendererRegistry"/>,
    /// so <tt>"Summary"</tt> and <tt>"summary"</tt> resolve to the same strategy.
    /// </note>
    string TagName { get; }

    /// <summary>
    /// Converts <paramref name="element"/> to a Markdown string.
    /// </summary>
    /// <param name="element">The XML element to render. Must not be <see langword="null"/>.</param>
    /// <param name="context">
    /// Shared conversion state (assembly name, registry, warning logger). Passed through
    /// unchanged so child nodes can be rendered recursively.
    /// </param>
    /// <returns>The Markdown fragment for this element.</returns>
    string Render(XElement element, ConversionContext context);
}
