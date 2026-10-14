using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MyInstrumentsForRevit.RebarSketch
{
    internal sealed class SketchLibraryCatalog
    {
        private readonly Dictionary<string, SketchTemplate> _templatesByForm;

        private SketchLibraryCatalog(
            Dictionary<string, SketchTemplate> templatesByForm,
            IReadOnlyList<string> warnings)
        {
            _templatesByForm = templatesByForm;
            Warnings = warnings;
        }

        public IReadOnlyList<string> Warnings { get; }

        public static SketchLibraryCatalog Load(string libraryPath)
        {
            if (string.IsNullOrWhiteSpace(libraryPath) || !Directory.Exists(libraryPath))
            {
                throw new DirectoryNotFoundException(
                    "Библиотека эскизов недоступна.\nПуть: " + libraryPath
                    + "\n\nПроверьте путь, подключение к сети/VPN и права на чтение папки.");
            }

            var templatesByForm = new Dictionary<string, SketchTemplate>(StringComparer.OrdinalIgnoreCase);
            var warnings = new List<string>();
            string[] templateFolders;
            try
            {
                templateFolders = Directory
                    .EnumerateFiles(libraryPath, "*.txt", SearchOption.AllDirectories)
                    .Where(path => Path.GetFileName(path).Equals("families.txt", StringComparison.OrdinalIgnoreCase)
                        || Path.GetFileName(path).Equals("form.txt", StringComparison.OrdinalIgnoreCase))
                    .Select(path => Path.GetDirectoryName(path) ?? libraryPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new UnauthorizedAccessException(
                    "Нет прав на чтение структуры библиотеки эскизов.\nПуть: " + libraryPath
                    + "\n\n" + exception.Message,
                    exception);
            }
            catch (IOException exception)
            {
                throw new IOException(
                    "Не удалось прочитать структуру библиотеки эскизов.\nПуть: " + libraryPath
                    + "\n\nВозможная причина: сетевой ресурс недоступен или одна из вложенных папок повреждена.\n"
                    + exception.Message,
                    exception);
            }

            foreach (string folder in templateFolders)
            {
                try
                {
                    // Each folder is a separate template, even when it also contains child templates.
                    string formFile = FindFile(folder, "families.txt", "form.txt");
                    string formFileName = Path.GetFileName(formFile);
                    string imagePath = FindFile(folder, "scetch.png", "sketch.png");
                    string parametersPath = FindFile(folder, "parameters.txt");

                    if (imagePath.Length == 0)
                    {
                        warnings.Add("Нет scetch.png или sketch.png рядом с " + formFileName + ": " + folder);
                        continue;
                    }

                    List<string> forms = ReadLines(formFile)
                        .Select(line => line.Trim().Trim('"'))
                        .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (forms.Count == 0)
                    {
                        warnings.Add(formFileName + " пустой или содержит только комментарии: " + folder);
                        continue;
                    }

                    List<SketchParameterPlacement> parameters = parametersPath.Length > 0
                        ? ParseParameters(parametersPath, warnings)
                        : new List<SketchParameterPlacement>();
                    if (parametersPath.Length == 0)
                    {
                        warnings.Add("Нет parameters.txt — эскиз будет без подписей параметров: " + folder);
                    }
                    var template = new SketchTemplate(folder, imagePath, forms, parameters);

                    foreach (string form in forms)
                    {
                        string key = NormalizeFormName(form);
                        if (key.Length == 0)
                        {
                            warnings.Add("Пустое имя семейства или формы в " + formFileName + ": " + folder);
                            continue;
                        }

                        if (templatesByForm.TryGetValue(key, out SketchTemplate? existing))
                        {
                            warnings.Add(
                                "Семейство или форма \"" + form + "\" встречается несколько раз; используется "
                                + existing.FolderPath);
                            continue;
                        }

                        templatesByForm.Add(key, template);
                    }
                }
                catch (UnauthorizedAccessException exception)
                {
                    warnings.Add("Нет прав на чтение папки: " + folder + " (" + exception.Message + ")");
                }
                catch (IOException exception)
                {
                    warnings.Add("Не удалось прочитать файлы в папке: " + folder + " (" + exception.Message + ")");
                }
            }

            if (templatesByForm.Count == 0)
            {
                throw new InvalidDataException(BuildNoTemplatesMessage(libraryPath, templateFolders, warnings));
            }

            return new SketchLibraryCatalog(templatesByForm, warnings);
        }

        private static string BuildNoTemplatesMessage(
            string libraryPath,
            IReadOnlyCollection<string> templateFolders,
            IReadOnlyCollection<string> warnings)
        {
            var message = new StringBuilder();
            message.AppendLine("Не удалось загрузить ни одного шаблона эскиза.");
            message.AppendLine("Путь: " + libraryPath);
            message.AppendLine("Папок с families.txt или form.txt: " + templateFolders.Count);

            try
            {
                LibraryFileCounts counts = CountLibraryFiles(libraryPath);
                message.AppendLine("Найдено families.txt: " + counts.FamiliesCount);
                message.AppendLine("Найдено form.txt: " + counts.FormCount);
                message.AppendLine("Найдено sketch.png: " + counts.SketchCount);
                message.AppendLine("Найдено scetch.png: " + counts.ScetchCount);
                message.AppendLine("Найдено parameters.txt: " + counts.ParametersCount);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException)
            {
                message.AppendLine("Не удалось посчитать файлы в библиотеке: " + exception.Message);
            }

            message.AppendLine();
            message.AppendLine("Подходящая папка должна содержать families.txt (или form.txt), scetch.png (или sketch.png) и parameters.txt.");
            if (warnings.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("Причины пропуска папок:");
                foreach (string warning in warnings.Take(12))
                {
                    message.AppendLine("• " + warning);
                }

                if (warnings.Count > 12)
                {
                    message.AppendLine("• …ещё: " + (warnings.Count - 12));
                }
            }

            return message.ToString();
        }

        private static LibraryFileCounts CountLibraryFiles(string libraryPath)
        {
            int sketchCount = 0;
            int scetchCount = 0;
            int parametersCount = 0;
            int familiesCount = 0;
            int formCount = 0;
            foreach (string path in Directory.EnumerateFiles(libraryPath, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(path);
                if (name.Equals("sketch.png", StringComparison.OrdinalIgnoreCase)) sketchCount++;
                else if (name.Equals("scetch.png", StringComparison.OrdinalIgnoreCase)) scetchCount++;
                else if (name.Equals("parameters.txt", StringComparison.OrdinalIgnoreCase)) parametersCount++;
                else if (name.Equals("families.txt", StringComparison.OrdinalIgnoreCase)) familiesCount++;
                else if (name.Equals("form.txt", StringComparison.OrdinalIgnoreCase)) formCount++;
            }

            return new LibraryFileCounts(sketchCount, scetchCount, parametersCount, familiesCount, formCount);
        }

        public bool TryFind(IEnumerable<string> formCandidates, out SketchTemplate? template, out string formName)
        {
            foreach (string candidate in formCandidates.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                if (_templatesByForm.TryGetValue(NormalizeFormName(candidate), out template))
                {
                    formName = candidate;
                    return true;
                }
            }

            template = null;
            formName = formCandidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "<не определена>";
            return false;
        }

        private static List<SketchParameterPlacement> ParseParameters(
            string parametersPath,
            ICollection<string> warnings)
        {
            var result = new List<SketchParameterPlacement>();
            string[] lines = ReadLines(parametersPath);

            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] fields = line.Split(',');
                if (fields.Length < 4
                    || !TryParseFloat(fields[1], out float x)
                    || !TryParseFloat(fields[2], out float y)
                    || !TryParseFloat(fields[3], out float angle))
                {
                    warnings.Add(
                        "Некорректная строка " + (index + 1) + " в " + parametersPath);
                    continue;
                }

                string parameterName = fields[0].Trim().Trim('"');
                if (parameterName.Length == 0)
                {
                    warnings.Add("Пустое имя параметра в строке " + (index + 1) + ": " + parametersPath);
                    continue;
                }

                bool isNarrow = fields.Length > 4 && fields[4].Trim() == "1";
                result.Add(new SketchParameterPlacement(parameterName, x, y, angle, isNarrow));
            }

            return result;
        }

        private static bool TryParseFloat(string value, out float result)
        {
            return float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result)
                || float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out result);
        }

        private static string FindFile(string folder, params string[] names)
        {
            foreach (string name in names)
            {
                string path = Path.Combine(folder, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        private static string[] ReadLines(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string text;

            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                text = Encoding.GetEncoding(1251).GetString(bytes);
            }

            return text
                .TrimStart('\uFEFF')
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        }

        private static string NormalizeFormName(string value)
        {
            string normalized = value.Trim().Trim('"');
            if (normalized.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - 4);
            }

            normalized = string.Join(" ", normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return normalized.Replace('\u0451', '\u0435').Replace('\u0401', '\u0415').ToUpperInvariant();
        }

        private readonly struct LibraryFileCounts
        {
            public LibraryFileCounts(int sketchCount, int scetchCount, int parametersCount, int familiesCount, int formCount)
            {
                SketchCount = sketchCount;
                ScetchCount = scetchCount;
                ParametersCount = parametersCount;
                FamiliesCount = familiesCount;
                FormCount = formCount;
            }

            public int SketchCount { get; }
            public int ScetchCount { get; }
            public int ParametersCount { get; }

            public int FamiliesCount { get; }

            public int FormCount { get; }
        }
    }
}
