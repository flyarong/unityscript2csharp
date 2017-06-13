using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CommandLine;

namespace UnityScript2CSharp
{
    class Program
    {
        static int Main(string[] args)
        {
            var options  = Parser.Default.ParseArguments<CommandLineArguments>(args);
            if (options.Errors.Any())
            {
                return -2;
            }

            var editorSubFolder = String.Format("{0}Editor{0}", Path.DirectorySeparatorChar);
            var pluginSubFolder = String.Format("{0}Plugins{0}", Path.DirectorySeparatorChar);

            Console.WriteLine($"Editor: {editorSubFolder}\r\nPlugin: {pluginSubFolder}");

            var allFiles = Directory.GetFiles(Path.Combine(options.Value.ProjectPath, "Assets"), "*.js", SearchOption.AllDirectories);
            var filter = new Regex(string.Format(@"{0}{1}{0}", Path.DirectorySeparatorChar, editorSubFolder), RegexOptions.Compiled);

            var runtimeScripts = allFiles.Where(scriptPath => !filter.IsMatch(scriptPath)).Select(scriptPath => new SourceFile { FileName = scriptPath, Contents = File.ReadAllText(scriptPath)});
            var editorScripts = allFiles.Where(scriptPath => scriptPath.Contains(editorSubFolder)).Select(scriptPath => new SourceFile { FileName = scriptPath, Contents = File.ReadAllText(scriptPath) });
            var pluginScripts = allFiles.Where(scriptPath => scriptPath.Contains(pluginSubFolder)).Select(scriptPath => new SourceFile { FileName = scriptPath, Contents = File.ReadAllText(scriptPath) });

            if (options.Value.Dump)
            {
                DumpScripts("Runtime", runtimeScripts);
                DumpScripts("Editor", editorScripts);
                DumpScripts("Plugin", pluginScripts);
            }

            var converter = new UnityScript2CSharpConverter(options.Value.IgnoreErrors);

            var references = new List<string>(options.Value.References);
            references.Add(typeof(object).Assembly.Location);

            foreach (var reference in options.Value.References)
            {
                if (!File.Exists(reference))
                {
                    Console.WriteLine($"Cannot find referenced assembly: {reference}");
                    return -1;
                }
            }

            ConvertScripts("runtime", runtimeScripts, converter, references, options.Value.Defines);
            ConvertScripts("editor", editorScripts, converter, references, options.Value.Defines);
            ConvertScripts("plugins", pluginScripts, converter, references, options.Value.Defines);

            return 0;
        }

        private static void ConvertScripts(string scriptType, IEnumerable<SourceFile> runtimeScripts, UnityScript2CSharpConverter converter, List<string> references, IEnumerable<string> defines)
        {
            Console.WriteLine("Converting '{0}' ", scriptType);
            converter.Convert(
                runtimeScripts,
                defines,
                references,
                HandleConvertedScript);
        }

        private static void HandleConvertedScript(string scriptPath, string content)
        {
            var csPath = Path.ChangeExtension(scriptPath, ".cs");
            File.WriteAllText(csPath, content);

            var jsMetaFile = scriptPath + ".meta";
            var csMetaFile = jsMetaFile.Replace(".js.", ".cs.");
            File.Copy(jsMetaFile, csMetaFile, true);

            // Remove js + meta files
        }

        private static void DumpScripts(string desc, IEnumerable<SourceFile> scripts)
        {
            Console.WriteLine($"\r\n{desc} Scripts");
            foreach (var rts in scripts)
            {
                Console.WriteLine(rts.FileName);
            }
        }
    }
}
