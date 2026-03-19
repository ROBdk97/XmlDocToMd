namespace ROBdk97.XmlDocToMd.Logging;
    /// <summary>
    /// Writes warning messages to an arbitrary <see cref="TextWriter"/>, prefixed with
    /// <tt>WARN: </tt>.
    /// </summary>
    /// <example>
    /// Typical usage — write warnings to <see langword="stderr"/>:
    /// <code lang="csharp">
    /// IWarningLogger logger = new TextWriterWarningLogger(Console.Error);
    /// </code>
    /// </example>
internal class TextWriterWarningLogger : IWarningLogger
{
    private readonly TextWriter _textWriter;

        /// <summary>
        /// Initialises a new instance that writes to <paramref name="textWriter"/>.
        /// </summary>
        /// <param name="textWriter">The destination writer. Must not be <see langword="null"/>.</param>
    internal TextWriterWarningLogger(TextWriter textWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        _textWriter = textWriter;
    }

        /// <inheritdoc/>
    public void LogWarning(string warning)
    {
        _textWriter.WriteLine("WARN: " + warning);
    }
}
