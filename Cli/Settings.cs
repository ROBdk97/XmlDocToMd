namespace ROBdk97.XmlDocToMd.Cli;

/// <summary>
/// Persistent configuration loaded from a JSON file (<tt>settings.json</tt> by default).
/// </summary>
/// <remarks>
/// The settings file is created automatically with empty defaults when it does not yet
/// exist. The path can be overridden with the <c>-f</c> / <c>--settings</c> CLI option.
/// </remarks>
public class Settings
{
    /// <summary>
    /// File names (without path) that should be skipped during directory-wide conversion.
    /// </summary>
    /// <tip>
    /// Add system-generated XML files such as <tt>System.Runtime.xml</tt> here to keep
    /// the output focused on your own assemblies.
    /// </tip>
    public List<string> FilesToIgnore { get; set; } = [];

    /// <summary>
    /// Namespace fragments whose members are excluded from conversion.
    /// Any member whose fully-qualified name contains one of these strings is silently
    /// dropped before rendering begins.
    /// </summary>
    /// <tip>
    /// Use this to hide internal or generated namespaces such as
    /// <tt>CompilerServices</tt> or <tt>Internal</tt>.
    /// </tip>
    public List<string> NameSpacesToRemove { get; set; } = [];
}
