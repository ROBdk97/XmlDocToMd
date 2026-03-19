using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using ROBdk97.XmlDocToMd.Cli;
using ROBdk97.XmlDocToMd.Conversion;
using ROBdk97.XmlDocToMd.Infrastructure;
using ROBdk97.XmlDocToMd.Logging;
using ROBdk97.XmlDocToMd.Rendering;
using System.Diagnostics;

namespace ROBdk97.XmlDocToMd;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            args = ["--help"];
        }

        IServiceProvider serviceProvider = new ServiceCollection()
            .AddXmlDocToMd()
            .BuildServiceProvider();

        DateTime startTime = DateTime.Now;
        Parser.Default
            .ParseArguments<Options>(args)
            .WithParsed(
                options =>
                {
                    Settings settings = SettingsExt.LoadSettings(options.SettingsFile);
                    if (options.SearchDirectory != null && options.OutputFile != null)
                    {
                        if (!options.SearchDirectory.EndsWith('\\'))
                            options.SearchDirectory += "\\";
                        if (!options.OutputFile.EndsWith('\\'))
                            options.OutputFile += "\\";
                        if (!Directory.Exists(options.SearchDirectory))
                        {
                            Console.WriteLine($"Search directory \"{options.SearchDirectory}\" does not exist.");
                            Debug.WriteLine($"Search directory \"{options.SearchDirectory}\" does not exist.");
                            return;
                        }
                        if (!Directory.Exists(options.OutputFile))
                            Directory.CreateDirectory(options.OutputFile);
                        Console.WriteLine($"Starting XML to Markdown conversion to \"{options.OutputFile}\".");
                        Debug.WriteLine($"Starting XML to Markdown conversion to \"{options.OutputFile}\".");
                        Console.WriteLine($"Searching \"{options.SearchDirectory}\" for \"{options.Directory}\" directories.");
                        Debug.WriteLine($"Searching \"{options.SearchDirectory}\" for \"{options.Directory}\" directories.");
                        string[] releaseFolders = Directory.GetDirectories(
                            options.SearchDirectory,
                            options.Directory,
                            SearchOption.AllDirectories);

                        var conversionTargets = releaseFolders
                            .SelectMany(rf => Directory.GetFiles(rf, "*.xml", SearchOption.AllDirectories))
                            .Where(file => !settings.FilesToIgnore.Contains(Path.GetFileName(file)))
                            .Where(file => !IsInDirectory(file, "obj"))
                            .Select(file => new
                            {
                                InputFile = file,
                                OutputFile = options.Readme
                                    ? Path.Combine(options.OutputFile, "README.md")
                                    : Path.Combine(options.OutputFile, Path.GetFileNameWithoutExtension(file) + ".md")
                            })
                            .GroupBy(item => item.OutputFile, StringComparer.OrdinalIgnoreCase)
                            .Select(group => group
                                .OrderByDescending(item => IsInDirectory(item.InputFile, "bin"))
                                .ThenByDescending(item => File.GetLastWriteTimeUtc(item.InputFile))
                                .ThenBy(item => item.InputFile, StringComparer.OrdinalIgnoreCase)
                                .First())
                            .OrderBy(item => item.OutputFile, StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        foreach (var target in conversionTargets)
                        {
                            Console.WriteLine($"Converting {target.InputFile} to {target.OutputFile}");
                            Debug.WriteLine($"Converting {target.InputFile} to {target.OutputFile}");
                            RunXmlToMarkdown(target.InputFile, target.OutputFile, options, serviceProvider, settings);
                        }
                        Console.WriteLine("Conversion to Markdown done.");
                        Debug.WriteLine("Conversion to Markdown done.");
                        return;
                    }
                    else if (options.InputFile != null && options.OutputFile != null)
                    {
                        RunXmlToMarkdown(options.InputFile, options.OutputFile, options, serviceProvider, settings);
                    }
                });
        Console.WriteLine($"Total time: {DateTime.Now - startTime}");
        Debug.WriteLine($"Total time: {DateTime.Now - startTime}");
    }

    private static bool IsInDirectory(string path, string directoryName)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directoryName))
        {
            return false;
        }

        var parts = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return parts.Any(part => part.Equals(directoryName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Main function to convert XML to Markdown.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="output"></param>
    /// <param name="options"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="settings"></param>
    private static void RunXmlToMarkdown(
        string input,
        string output,
        Options options,
        IServiceProvider serviceProvider,
        Settings settings)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(settings);

        ReflectionHelper.SetCurrentXmlFile(input);

        var inReader = options.ConsoleIn ? Console.In : new StreamReader(input);
        using var outWriter = options.ConsoleIn ? Console.Out : new StreamWriter(output);

        var xml = inReader.ReadToEnd();
        var doc = XDocument.Parse(xml);

        // move the AssemblyDoc node to the assembly node.
        MoveAssemblyDoc(doc);
        // Remove unwanted NameSpaces
        RemoveNameSpaces(doc, settings);
        // Add a returns tag to all methods that dont have one.
        AddReturnsToMethods(doc);

        // convert the XML to Markdown.
        IWarningLogger warningLogger = serviceProvider.GetRequiredService<IWarningLogger>();
        ITagRendererRegistry registry = serviceProvider.GetRequiredService<ITagRendererRegistry>();

        var context = new ConversionContext()
        {
            UnexpectedTagAction = options.UnexpectedTagAction,
            WarningLogger = warningLogger,
            Registry = registry,
            CurrentXmlFile = input,
            IsGitHub = options.Git,
        };

        var md = doc.Root?.ToMarkDown(context) ?? string.Empty;
        // add a footer to the markdown.
        md += "\n\n---\n\nGenerated by [XmlDocToMd](https://github.com/ROBdk97/XmlDocToMd) by [ROBdk97](https://github.com/ROBdk97)";
        outWriter.Write(md);
        outWriter.Close();

        // Copy to secondary output if specified
        if (!string.IsNullOrWhiteSpace(options.SecondaryOutputDirectory))
        {
            try
            {
                File.Copy(output, $"{options.SecondaryOutputDirectory}\\docs\\{Path.GetFileName(output)}", true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error copying file to secondary output directory: {ex.Message}");
            }
        }
    }

    private static void MoveAssemblyDoc(XDocument doc)
    {
        // try to find all members with AssemblyDoc in their name and add the content to the assembly node.
        var members = doc.Root?
            .Element("members")?
            .Elements("member")
            .Where(member => member.Attribute("name")?.Value.Contains("AssemblyDoc") == true)
            .ToList();
        if (members is null) return;
        foreach (var member in members)
        {
            // get the assembly node.
            var assembly = doc.Root?.Element("assembly");
            if (assembly is null) continue;
            // add all the content of the AssemblyDoc Node to the assembly node.
            assembly.Add(member.Nodes());
            // remove the AssemblyDoc Node
            member.Remove();
        }
    }

    /// <summary>
    /// Add a Returns Tag to all Methods that dont have a return value.
    /// </summary>
    /// <param name="doc"></param>
    private static void AddReturnsToMethods(XDocument doc)
    {
        // try to find all members with a name starting with M: and add a returns tag if there is none.
        var members = doc.Root?
            .Element("members")?
            .Elements("member")
            .Where(member => member.Attribute("name")?.Value.StartsWith("M:") == true)
            .ToList();
        if (members is null) return;
        foreach (var member in members)
        {
            // if the member does not have a returns tag, add one.
            if (member.Element("returns") is null)
            {
                member.Add(new XElement("returns", string.Empty));
            }
        }
    }

    /// <summary>
    /// Remove all Nodes containing EXAMPLE XML Doc
    /// </summary>
    private static void RemoveNameSpaces(XDocument doc, Settings settings)
    {
        if (doc.Root is null) return;

        var membersElement = doc.Root.Element("members");
        if (membersElement is null) return;

        // Pre-compute the set of all namespaces to remove for faster checks
        var namespacesToRemove = new HashSet<string>(settings.NameSpacesToRemove);

        // Find all member elements to remove in a single iteration
        var nodesToRemove = membersElement
            .Elements("member")
            .Where(member =>
            {
                var name = member.Attribute("name")?.Value;
                return name != null && namespacesToRemove.Any(ns => name.Contains(ns));
            })
            .ToList();

        // Remove all identified nodes.
        foreach (var node in nodesToRemove)
        {
            node.Remove();
        }
    }
}
