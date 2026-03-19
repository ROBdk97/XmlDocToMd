namespace ROBdk97.XmlDocToMd.Logging;
    /// <summary>
    /// Defines a sink for non-fatal diagnostic messages emitted during XML-to-Markdown
    /// conversion, such as unknown tag warnings.
    /// </summary>
    /// <note>
    /// Implementations must be thread-safe if conversion is ever parallelised, because
    /// a single <see cref="IWarningLogger"/> instance is shared across the entire document
    /// walk for a given <see cref="ConversionContext"/>.
    /// </note>
internal interface IWarningLogger
{
    /// <summary>
    /// Records a single warning message.
    /// </summary>
    /// <param name="warning">Human-readable description of the problem.</param>
    void LogWarning(string warning);
}
