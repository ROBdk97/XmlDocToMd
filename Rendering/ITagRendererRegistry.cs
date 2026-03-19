namespace ROBdk97.XmlDocToMd.Rendering;

/// <summary>
/// Resolves and invokes <see cref="ITagRenderStrategy"/> instances by XML tag name.
/// </summary>
/// <remarks>
/// The registry acts as the single dispatch point for the entire rendering pipeline.
/// All tag rendering goes through <see cref="Render"/> or <see cref="TryGetRenderer"/>, 
/// so swapping the registry implementation is sufficient to change the rendering
/// behaviour globally without touching any call sites.
/// <note>
/// Tag-name lookup is case-insensitive to be tolerant of mixed-case XML documentation.
/// </note>
/// </remarks>
internal interface ITagRendererRegistry
{
    /// <summary>
    /// Tries to retrieve the strategy registered for <paramref name="tagName"/>.
    /// </summary>
    /// <param name="tagName">The XML element name to look up (case-insensitive).</param>
    /// <param name="strategy">
    /// When this method returns <see langword="true"/>, contains the matching strategy;
    /// otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a strategy is registered; <see langword="false"/> otherwise.
    /// </returns>
    bool TryGetRenderer(string tagName, out ITagRenderStrategy? strategy);

    /// <summary>
    /// Renders <paramref name="element"/> using the strategy registered for
    /// <paramref name="tagName"/>.
    /// </summary>
    /// <param name="tagName">The XML element name that selects the strategy.</param>
    /// <param name="element">The XML element to render.</param>
    /// <param name="context">Conversion state threaded through the document walk.</param>
    /// <returns>The Markdown string produced by the matching strategy.</returns>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// Thrown when no strategy is registered for <paramref name="tagName"/>.
    /// </exception>
    string Render(string tagName, XElement element, ConversionContext context);
}
