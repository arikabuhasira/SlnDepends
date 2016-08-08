// ReSharper disable InconsistentNaming
using System;
using System.Collections.Extensions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using rx = System.Text.RegularExpressions.Regex;
using Edges = System.Collections.Generic.List<string>;
using Graph = System.Collections.Generic.Dictionary<string, System.Collections.Generic.IEnumerable<string>>;

namespace SlnDepends
{
    public class VsDependsException : Exception
    {
        public VsDependsException(string msg) : base(msg) { }
        public static implicit operator VsDependsException(string msg) => new VsDependsException(msg);
    }

    static class Program
    {
        static void printf(string msg) => Console.WriteLine(msg);
        static void die   (string msg, Exception e = null) 
        {
            printf("CRITICAL:" + msg);
            if (e != null) printf($"EXCEPTION:\n{e}");   // print exception
            Environment.Exit(1);
        }

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => die("unhandle exception", (eventArgs.ExceptionObject as Exception));

            if (args.Length != 1) die("Missing sln file argument.");
            if (rx.IsMatch(args[0], @"(?i)^(-h|--help|\\\?)$")) Usage();   // print help with -h or --help or /?

            var sln = args[0];
            var sln_dir = Path.GetDirectoryName(sln);

            var sln_content = ReadFile(sln).AsSingleLine();
            var cs_projects = ParseSlnProjects(sln_content);
            if (cs_projects.Length == 0) die("zero projects found in sln");

            printf($"sln contains {cs_projects.Length}");

            printf("== build graph:");
            Graph sln_graph = new Graph(StringComparer.InvariantCultureIgnoreCase);
            foreach (var csprojFilename in cs_projects)
            {
                printf($"> process {csprojFilename}");

                var csprojAbsFilename = Path.Combine(sln_dir, csprojFilename);   // resolve project full path
                var csproj = ReadFile(csprojAbsFilename).AsSingleLine();         // read project
                var assembly = ParseProjectAssemblyName(csproj);                 // get project assembly
                var references = ParseProjectRefs(csproj);                       // get project references
                var edges = string.Join(";", references);                        // dbg: concat references

                printf($"assembly={assembly}");
                printf($"edges ({references.Length})={edges}");

                sln_graph.Add(assembly /* Node */, references /* Edges */);
            }
            printf("== end of graph");

            printf("=== finding cycles ====");
            var cycles = sln_graph.FindCycles();
            printf($"cycles={cycles.Count()}");
        }

        static void Usage()
        {
            printf("usage: slncyclic [FILE]");
            printf("Find cyclic dependecies within VS solution file");
            Environment.Exit(1); 
        }

        static string ReadFile(string filename)
        {
            if (!File.Exists(filename)) die($"file not exists. fail to read {filename}.");
            using (var reader = File.OpenText(filename))
                return reader.ReadToEnd();
        }

        static string[] ParseSlnProjects(string content)
        {
            var matches = rx.Matches(content, @"(?mi)([a-zA-Z\.\\\-0-9_]+csproj)"); // FIXME: remove multiline option ?m
            var csprojList = new List<string>();
            for (var i=0; i<matches.Count;i++) csprojList.Add(matches[i].Value);
            return csprojList.ToArray();
        }

        static string[] ParseProjectRefs(string content)
        {
            var matches = rx.Matches(content, @"(?mis)(<hintpath>(?<dll>.*?)<\/hintpath>|projectref.+?<name>(?<dll>.+?)<\/name>)");

            var csprojDlls = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            for (var i = 0; i < matches.Count; i++)
            {
                var dllNameFull = matches[i].Groups["dll"].Value;                       // with extension and path
                var dllName = rx.Replace(dllNameFull, @"^(.+)\\(.+?)\.dll$", "$2");     // bare name (without extension + path)
                if (!csprojDlls.Contains(dllName)) csprojDlls.Add(dllName);
            }

            return csprojDlls.ToArray();
        }

        static string ParseProjectAssemblyName(string content)
        {
            var m = rx.Match(content, @"(?i)<assemblyname>(?<output>.+?)<\/assemblyname>");
            if (!m.Success) throw (VsDependsException) "fail to parse assembly name";
            return m.Groups["output"].Value;
        }

        private static string AsSingleLine(this string text) => text.Replace("\r\n", "");
    }
}
