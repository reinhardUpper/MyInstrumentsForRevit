using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using Autodesk.Revit.UI;

namespace MyRevitTools.DimensionQuickCommands
{
    public static class DimensionQuickCommandStorage
    {
        private static readonly object SyncRoot = new object();
        private static List<DimensionQuickCommandConfig>? cachedConfigs;
        private static DateTime cachedWriteTimeUtc;

        public static string ConfigDirectory
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "MyRevitTools", "DimensionQuickCommands");
            }
        }

        public static string ConfigPath => Path.Combine(ConfigDirectory, "dimension_quick_commands.json");

        public static List<DimensionQuickCommandConfig> Load(bool showErrors = true)
        {
            lock (SyncRoot)
            {
                try
                {
                    if (!File.Exists(ConfigPath))
                    {
                        cachedConfigs = new List<DimensionQuickCommandConfig>();
                        cachedWriteTimeUtc = DateTime.MinValue;
                        return new List<DimensionQuickCommandConfig>();
                    }

                    DateTime writeTime = File.GetLastWriteTimeUtc(ConfigPath);
                    if (cachedConfigs != null && writeTime == cachedWriteTimeUtc)
                    {
                        return Clone(cachedConfigs);
                    }

                    using (var stream = File.OpenRead(ConfigPath))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(List<DimensionQuickCommandConfig>));
                        var loaded = serializer.ReadObject(stream) as List<DimensionQuickCommandConfig>
                            ?? new List<DimensionQuickCommandConfig>();

                        cachedConfigs = Normalize(loaded);
                        cachedWriteTimeUtc = writeTime;
                        return Clone(cachedConfigs);
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Runtime.Serialization.SerializationException || ex is ArgumentException)
                {
                    cachedConfigs = new List<DimensionQuickCommandConfig>();
                    cachedWriteTimeUtc = DateTime.MinValue;
                    if (showErrors)
                    {
                        TaskDialog.Show("Менеджер размерных команд", "Не удалось прочитать настройки быстрых размеров.\n\n" + ex.Message);
                    }

                    return new List<DimensionQuickCommandConfig>();
                }
            }
        }

        public static bool Save(IReadOnlyList<DimensionQuickCommandConfig> configs, bool showErrors = true)
        {
            lock (SyncRoot)
            {
                try
                {
                    Directory.CreateDirectory(ConfigDirectory);

                    var normalized = Normalize(configs);
                    using (var stream = File.Create(ConfigPath))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(List<DimensionQuickCommandConfig>));
                        serializer.WriteObject(stream, normalized);
                    }

                    cachedConfigs = Clone(normalized);
                    cachedWriteTimeUtc = File.GetLastWriteTimeUtc(ConfigPath);
                    return true;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
                {
                    if (showErrors)
                    {
                        TaskDialog.Show("Менеджер размерных команд", "Не удалось сохранить настройки быстрых размеров.\n\n" + ex.Message);
                    }

                    return false;
                }
            }
        }

        public static void InvalidateCache()
        {
            lock (SyncRoot)
            {
                cachedConfigs = null;
                cachedWriteTimeUtc = DateTime.MinValue;
            }
        }

        private static List<DimensionQuickCommandConfig> Normalize(IEnumerable<DimensionQuickCommandConfig> configs)
        {
            return configs
                .Where(config => config != null)
                .Where(config => config.SlotNumber >= 1 && config.SlotNumber <= 4)
                .Select(config =>
                {
                    config.CommandKind = QuickCommandKind.Normalize(config.CommandKind);
                    return config;
                })
                .OrderBy(config => config.SlotNumber)
                .ThenBy(config => config.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static List<DimensionQuickCommandConfig> Clone(IReadOnlyList<DimensionQuickCommandConfig> configs)
        {
            string json;
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(List<DimensionQuickCommandConfig>));
                serializer.WriteObject(stream, configs.ToList());
                json = Encoding.UTF8.GetString(stream.ToArray());
            }

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(List<DimensionQuickCommandConfig>));
                return serializer.ReadObject(stream) as List<DimensionQuickCommandConfig>
                    ?? new List<DimensionQuickCommandConfig>();
            }
        }
    }
}
