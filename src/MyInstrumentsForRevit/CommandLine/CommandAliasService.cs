using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MyInstrumentsForRevit.CommandLine
{
    internal static class CommandAliasService
    {
        private const string FileName = "command-aliases.txt";
        private static readonly Dictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string AliasFilePath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MyInstrumentsForRevit");
                return Path.Combine(folder, FileName);
            }
        }

        public static IReadOnlyDictionary<string, string> CurrentAliases => Aliases;

        public static void EnsureLoaded()
        {
            EnsureFileExists();
            Reload();
        }

        public static void Reload()
        {
            Aliases.Clear();
            EnsureFileExists();

            foreach (string rawLine in File.ReadAllLines(AliasFilePath))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
                {
                    continue;
                }

                string alias = line.Substring(0, separatorIndex).Trim();
                string command = line.Substring(separatorIndex + 1).Trim();
                if (!string.IsNullOrWhiteSpace(alias) && !string.IsNullOrWhiteSpace(command))
                {
                    Aliases[alias] = command;
                }
            }
        }

        public static string Resolve(string commandName)
        {
            string current = commandName;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (Aliases.TryGetValue(current, out string target))
            {
                if (!visited.Add(current))
                {
                    break;
                }

                current = target;
            }

            return current;
        }

        public static void SaveAlias(string alias, string command)
        {
            EnsureFileExists();
            Reload();

            Aliases[alias] = command;
            var lines = new List<string>
            {
                "# MyInstrumentsForRevit command aliases",
                "# Format: alias = command"
            };

            lines.AddRange(Aliases
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => pair.Key + " = " + pair.Value));

            File.WriteAllLines(AliasFilePath, lines);
        }

        public static IEnumerable<RegisteredCommand> BuildAliasCommands(IReadOnlyDictionary<string, RegisteredCommand> commands)
        {
            return Aliases
                .Where(alias => commands.ContainsKey(Resolve(alias.Value)))
                .Select(alias =>
                {
                    string resolved = Resolve(alias.Value);
                    RegisteredCommand target = commands[resolved];
                    return new RegisteredCommand(alias.Key, target.DisplayName, "Alias for " + resolved + ": " + target.Description, target.Execute);
                });
        }

        private static void EnsureFileExists()
        {
            string path = AliasFilePath;
            string folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            if (File.Exists(path))
            {
                return;
            }

            File.WriteAllLines(path, new[]
            {
                "# MyInstrumentsForRevit command aliases",
                "# Format: alias = command",
                "# Examples:",
                "3d = view.3d",
                "rebar = rebar.toggle",
                "rf = filters.refresh"
            });
        }
    }
}
