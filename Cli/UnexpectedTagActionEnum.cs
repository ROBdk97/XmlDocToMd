using ROBdk97.XmlDocToMd.Logging;

namespace ROBdk97.XmlDocToMd.Cli;

/// <summary>
/// Controls how the converter reacts to recoverable conversion problems, such as XML
/// documentation tags without a registered <see cref="Rendering.ITagRenderStrategy"/>
/// or other non-fatal rendering issues encountered while walking the XML tree.
/// </summary>
/// <remarks>
/// Choose the policy that best fits the maturity of your XML documentation:
/// <tip>
/// Start with <see cref="Warn"/> while authoring documentation, then switch to
/// <see cref="Error"/> once all conversion issues are accounted for.
/// </tip>
/// </remarks>
public enum UnexpectedTagActionEnum
{
    /// <summary>
    /// Throws when a recoverable conversion problem is encountered, halting conversion
    /// immediately.
    /// </summary>
    Error,

    /// <summary>
    /// Emits a <tt>WARN: </tt> diagnostic to <tt>stderr</tt> via the configured
    /// <see cref="IWarningLogger"/> and continues conversion, producing partial output.
    /// </summary>
    Warn,

    /// <summary>
    /// Silently ignores recoverable conversion problems.
    /// No diagnostic is emitted and no output is produced for the skipped element.
    /// </summary>
    Accept
}
