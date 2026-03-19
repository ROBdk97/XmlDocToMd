using ROBdk97.XmlDocToMd.Logging;

namespace ROBdk97.XmlDocToMd.Cli;

/// <summary>
/// Controls how the converter reacts when it encounters an XML documentation tag that
/// has no registered <see cref="Rendering.ITagRenderStrategy"/>.
/// </summary>
/// <remarks>
/// Choose the policy that best fits the maturity of your XML documentation:
/// <tip>
/// Start with <see cref="Warn"/> while authoring documentation, then switch to
/// <see cref="Error"/> once all tags are accounted for.
/// </tip>
/// </remarks>
public enum UnexpectedTagActionEnum
{
    /// <summary>
    /// Throws an <see cref="System.Collections.Generic.KeyNotFoundException"/> when an
    /// unknown tag is encountered, halting conversion immediately.
    /// </summary>
    /// <warning>
    /// Using <see cref="Error"/> in a post-build event will cause the build to fail when
    /// any unsupported tag is present in the XML documentation.
    /// </warning>
    Error,

    /// <summary>
    /// Emits a <tt>WARN: </tt> diagnostic to <tt>stderr</tt> via the configured
    /// <see cref="IWarningLogger"/> and continues conversion, producing partial output.
    /// </summary>
    Warn,

    /// <summary>
    /// Silently skips tags with no registered renderer.
    /// No diagnostic is emitted and no output is produced for the skipped element.
    /// </summary>
    Accept
}
