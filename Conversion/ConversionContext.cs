using ROBdk97.XmlDocToMd.Cli;
using ROBdk97.XmlDocToMd.Logging;
using ROBdk97.XmlDocToMd.Rendering;

namespace ROBdk97.XmlDocToMd.Conversion;

/// <summary>
/// Carries all mutable state that is threaded through the recursive XML-to-Markdown
/// conversion. One instance is created per top-level conversion call and is mutated
/// in-place as the document tree is walked.
/// </summary>
internal class ConversionContext
{
    /// <summary>
    /// Receives diagnostic messages for tags that are unrecognised but not fatal.
    /// </summary>
    /// <note>
    /// This property must be set before conversion begins when
    /// <see cref="UnexpectedTagAction"/> is <see cref="UnexpectedTagActionEnum.Warn"/>;
    /// otherwise a <see langword="null"/>-reference exception will be thrown the first
    /// time an unknown tag is encountered.
    /// </note>
    internal IWarningLogger? WarningLogger { get; set; }

    /// <summary>
    /// Short name of the assembly currently being converted (e.g. <tt>MyLib</tt>).
    /// Used to strip redundant namespace prefixes from member names.
    /// Set automatically by <see cref="MutateAssemblyName"/>.
    /// </summary>
    internal string AssemblyName
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>
    /// Registry of <see cref="ITagRenderStrategy"/> instances used to render
    /// each XML element. Defaults to the built-in strategy set so that contexts created
    /// outside of the DI container continue to work without explicit configuration.
    /// </summary>
    /// <tip>
    /// Inject a custom <see cref="ITagRendererRegistry"/> to override or extend the
    /// default rendering behaviour for individual tags without touching any other code.
    /// </tip>
    internal ITagRendererRegistry? Registry { get; set; } = TagRendererRegistry.Default;

    /// <summary>
    /// Specifies how tags that have no registered strategy should be treated.
    /// Defaults to <see cref="UnexpectedTagActionEnum.Error"/>.
    /// </summary>
    internal UnexpectedTagActionEnum UnexpectedTagAction { get; set; } = UnexpectedTagActionEnum.Error;

    /// <summary>
    /// Indicates whether the output should be formatted for GitHub Flavored Markdown.
    /// </summary>
    internal bool IsGitHub
    {
        get;
        set => field = value;
    }

    /// <summary>
    /// The current XML file being processed.
    /// </summary>
    internal string CurrentXmlFile
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>
    /// Absolute repository root used to resolve fallback source-file links.
    /// Empty when repository-relative linking is unavailable.
    /// </summary>
    internal string RepositoryRootPath
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>
    /// Absolute path of the Markdown file currently being written.
    /// Used to compute relative repository links for GitHub output.
    /// </summary>
    internal string OutputMarkdownFile
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>
    /// Number of leading namespace parts to trim from DevExpress and other vendor namespaces.
    /// Set to 0 to disable namespace trimming. Default is 0.
    /// </summary>
    internal int NamespaceTrimDepth
    {
        get;
        set => field = value;
    }

    /// <summary>
    /// Updates <see cref="AssemblyName"/> to <paramref name="assemblyName"/> and returns
    /// <see langword="this"/> so the call can be chained inline.
    /// </summary>
    /// <param name="assemblyName">The new assembly name to store.</param>
    /// <returns>The same <see cref="ConversionContext"/> instance with the updated name.</returns>
    internal ConversionContext MutateAssemblyName(string assemblyName)
    {
        AssemblyName = assemblyName;
        return this;
    }
}
