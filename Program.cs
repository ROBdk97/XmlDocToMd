using CommandLine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ROBdk97.XmlDocToMd
{
    class Program
    {
        static DateTime startTime;
        internal static Settings settings;

        public static void Main(string[] args)
        {
            if(args.Length == 0)
            {
                args = ["--help"];
            }
            startTime = DateTime.Now;
            Parser.Default
                .ParseArguments<Options>(args)
                .WithParsed(
                    o =>
                    {
                        settings = SettingsExt.LoadSettings(o.SettingsFile);
                        if (o.SearchDirectory != null && o.OutputFile != null)
                        { // If the user specified a search directory, directory (default (OutputRB)) and output file (Directory),
                            // then search the directory for all .xml files in the directory and subdirectories and convert them to .md files in the output directory.

                            // if not ending with a backslash, add one.
                            if(!o.SearchDirectory.EndsWith("\\"))
                                o.SearchDirectory += "\\";
                            if(!o.OutputFile.EndsWith("\\"))
                                o.OutputFile += "\\";
                            // if the search directory does not exist, print an error and exit.
                            if(!Directory.Exists(o.SearchDirectory))
                            {
                                Console.WriteLine($"Search directory \"{o.SearchDirectory}\" does not exist.");
                                Debug.WriteLine($"Search directory \"{o.SearchDirectory}\" does not exist.");
                                return;
                            }
                            // if the output directory does not exist, create it.
                            if(!Directory.Exists(o.OutputFile))
                                Directory.CreateDirectory(o.OutputFile);
                            Console.WriteLine($"Starting XML to Markdown conversion to \"{o.OutputFile}\".");
                            Debug.WriteLine($"Starting XML to Markdown conversion to \"{o.OutputFile}\".");
                            Console.WriteLine($"Searching \"{o.SearchDirectory}\" for \"{o.Directory}\" directories.");
                            Debug.WriteLine($"Searching \"{o.SearchDirectory}\" for \"{o.Directory}\" directories.");
                            string[] releaseFolders = Directory.GetDirectories(
                                o.SearchDirectory,
                                o.Directory,
                                SearchOption.AllDirectories);
                            List<string> files = new List<string>();
                            foreach(string rf in releaseFolders)
                            {
                                string[] filesInFolder = Directory.GetFiles(rf, "*.xml", SearchOption.AllDirectories);
                                foreach(string file in filesInFolder)
                                {
                                    if(settings.FilesToIgnore.Contains(Path.GetFileName(file)))
                                        continue;
                                    string outputFile = $"{o.OutputFile}{Path.GetFileName(file).Replace(".xml", ".md")}";
                                    if(o.Readme)
                                        outputFile = $"{o.OutputFile}README.md";
                                    Console.WriteLine($"Converting {file} to {outputFile}");
                                    Debug.WriteLine($"Converting {file} to {outputFile}");
                                    RunXmlToMarkdown(file, outputFile, o);
                                    files.Add(Path.GetFileName(file));
                                }
                            }
                            Console.WriteLine("Conversion to Markdown done.");
                            Debug.WriteLine("Conversion to Markdown done.");
                            return;
                        } else if(o.InputFile != null && o.OutputFile != null)
                        {
                            RunXmlToMarkdown(o.InputFile, o.OutputFile, o);
                        }
                    });
            Console.WriteLine($"Total time: {DateTime.Now - startTime}");
            Debug.WriteLine($"Total time: {DateTime.Now - startTime}");
        }

        /// <summary>
        /// Main function to convert XML to Markdown.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="options"></param>
        private static void RunXmlToMarkdown(string input, string output, Options options)
        {
            XmlToMarkdown.CurrentXmlFile = input;
            var inReader = options.ConsoleIn ? Console.In : new StreamReader(input);
            using(var outWriter = options.ConsoleIn ? Console.Out : new StreamWriter(output))
            {
                var xml = inReader.ReadToEnd();
                var doc = XDocument.Parse(xml);

                XmlToMarkdown.IsGitHub = options.Git;
                // move the AssemblyDoc node to the assembly node.
                MoveAssemblyDoc(doc);
                // Remove unwanted NameSpaces
                RemoveNameSpaces(doc);
                // Add a returns tag to all methods that dont have one.
                AddReturnsToMethods(doc);
                // convert the XML to Markdown.
                var context = new ConversionContext()
                {
                    UnexpectedTagAction = options.UnexpectedTagAction,
                    WarningLogger = new TextWriterWarningLogger(Console.Error),
                };
                var md = doc.Root.ToMarkDown(context);
                // add a footer to the markdown.
                md += $"\n\n---\n\nGenerated by [XmlDocToMd](https://github.com/ROBdk97/XmlDocToMd) by [ROBdk97](https://github.com/ROBdk97)";
                outWriter.Write(md);
                outWriter.Close();
            }
            try
            {
                if(!string.IsNullOrWhiteSpace(options.SecondaryOutputDirectory))
                    File.Copy(output, $"{options.SecondaryOutputDirectory}\\docs\\{Path.GetFileName(output)}", true);
            } catch(Exception ex)
            {
                Console.WriteLine($"Error copying file to secondary output directory: {ex.Message}");
            }
        }

        private static void MoveAssemblyDoc(XDocument doc)
        {
            // try to find all members with AssemblyDoc in their name and add the content to the assembly node.
            var members = doc.Root
                .Element("members")
                .Elements("member")
                .Where(member => member.Attribute("name").Value.Contains("AssemblyDoc"))
                .ToList();  // ToList is necessary because we're modifying the collection

            foreach(var member in members)
            {
                // get the assembly node.
                var assembly = doc.Root.Element("assembly");
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
            var members = doc.Root
                .Element("members")
                .Elements("member")
                .Where(member => member.Attribute("name").Value.StartsWith("M:"))
                .ToList();  // ToList is necessary because we're modifying the collection
            foreach(var member in members)
            {
                // if the member does not have a returns tag, add one.
                if(member.Element("returns") == null)
                {
                    member.Add(new XElement("returns", string.Empty));
                }
            }
        }

        /// <summary>
        /// Remove all Nodes containing EXAMPLE XML Doc
        /// </summary>
        private static void RemoveNameSpaces(XDocument doc)
        {
            if (doc.Root == null) return;

            var membersElement = doc.Root.Element("members");
            if (membersElement == null) return;

            // Pre-compute the set of all namespaces to remove for faster checks
            var namespacesToRemove = new HashSet<string>(settings.NameSpacesToRemove);

            // Find all member elements to remove in a single iteration
            var nodesToRemove = membersElement
                .Elements("member")
                .Where(member => member.Attribute("name") != null &&
                                 namespacesToRemove.Any(ns => member.Attribute("name").Value.Contains(ns)))
                .ToList();

            // Remove all identified nodes.
            foreach (var node in nodesToRemove)
            {
                node.Remove();
            }
        }
    }
}
