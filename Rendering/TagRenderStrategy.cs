namespace ROBdk97.XmlDocToMd.Rendering;

/// <summary>
/// General-purpose <see cref="ITagRenderStrategy"/> implementation that combines a
/// <c>string.Format</c>-style template with a pluggable value extractor.
/// </summary>
/// <remarks>
/// Most built-in tag renderers are instances of this class, created by
/// <see cref="TagRenderers.CreateStrategies"/>. The rendering algorithm is:
/// <list type="number">
/// <item><description>Call <i>valueExtractor</i> to obtain an ordered list of string tokens.</description></item>
/// <item><description>If the list is empty, return the format string verbatim (used for self-closing tags that produce a fixed output).</description></item>
/// <item><description>Otherwise call <see cref="string.Format(string, object[])"/> with the tokens as positional arguments.</description></item>
/// </list>
/// <tip>
/// For tags that require non-trivial branching logic (e.g. <c>&lt;list&gt;</c>) the
/// constructor still accepts a fully custom lambda, so a subclass is never needed.
/// </tip>
/// </remarks>
internal sealed class TagRenderStrategy : ITagRenderStrategy
{
    private readonly string _formatString;
    private readonly Func<XElement, ConversionContext, IEnumerable<string>> _valueExtractor;

    /// <summary>
    /// Initialises a new strategy for <paramref name="tagName"/>.
    /// </summary>
    /// <param name="tagName">
    /// The XML element name this strategy handles (e.g. <tt>"summary"</tt>).
    /// </param>
    /// <param name="formatString">
    /// A <see cref="string.Format(string, object[])"/>-compatible template.
    /// Use <c>{0}</c>, <c>{1}</c>, … as placeholders for the values returned by
    /// <paramref name="valueExtractor"/>.
    /// </param>
    /// <param name="valueExtractor">
    /// A delegate that extracts the ordered token list from the XML element and the
    /// current <see cref="ConversionContext"/>. Return an empty enumerable when the
    /// format string should be emitted unchanged.
    /// </param>
    internal TagRenderStrategy(
        string tagName,
        string formatString,
        Func<XElement, ConversionContext, IEnumerable<string>> valueExtractor)
    {
        TagName = tagName;
        _formatString = formatString;
        _valueExtractor = valueExtractor;
    }

    /// <inheritdoc/>
    public string TagName { get; }

    /// <summary>
    /// Applies <i>valueExtractor</i> to <paramref name="element"/> and formats the result
    /// against the template supplied at construction time.
    /// </summary>
    /// <param name="element">The XML element to render.</param>
    /// <param name="context">Conversion state passed through to the value extractor.</param>
    /// <returns>The formatted Markdown fragment.</returns>
    /// <note>
    /// When the extractor returns no values the format string is returned as-is, which
    /// supports fixed-output tags such as self-closing <c>&lt;br/&gt;</c>.
    /// </note>
    public string Render(XElement element, ConversionContext context)
    {
        var vals = _valueExtractor(element, context).ToArray();
        if (vals.Length == 0)
            return _formatString;
        return string.Format(_formatString, args: vals);
    }
}
