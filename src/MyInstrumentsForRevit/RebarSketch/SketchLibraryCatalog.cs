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
                throw new DirectoryNotFoundException("Библиотека эскизов недоступна: " + libraryPath);
            }

            var templatesByForm = new Dictionary<string, SketchTemplate>(StringComparer.OrdinalIgnoreCase);
            var warnings = new List<string>();
            string[] formFiles = Directory
                .EnumerateFiles(libraryPath, "form.txt", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (string formFile in formFiles)
            {
                string folder = Path.GetDirectoryName(formFile) ?? libraryPath;
                string imagePath = FindFile(folder, "sketch.png", "scetch.png");
                string parametersPath = FindFile(folder, "parameters.txt");

                if (imagePath.Length == 0 || parametersPath.Length == 0)
                {
                    warnings.Add("Пропущена папка без sketch.png или parameters.txt: " + folder);
                    continue;
                }

                List<string> forms = ReadLines(formFile)
                    .Select(line => line.Trim().Trim('"'))
                    .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (forms.Count == 0)
                {
                    warnings.Add("В form.txt нет форм: " + folder);
                    continue;
                }

                List<SketchParameterPlacement> parameters = ParseParameters(parametersPath, warnings);
                var template = new SketchTemplate(folder, imagePath, forms, parameters);

                foreach (string form in forms)
                {
                    string key = NormalizeFormName(form);
                    if (key.Length == 0)
                    {
                        continue;
                    }

                    if (templatesByForm.TryGetValue(key, out SketchTemplate? existing))
                    {
                        warnings.Add(
                            "Форма \"" + form + "\" встречается несколько раз; используется "
                            + existing.FolderPath);
                        continue;
                    }

                    templatesByForm.Add(key, template);
                }
            }

            if (templatesByForm.Count == 0)
            {
                throw new InvalidDataException(
                    "В библиотеке не найдено ни одной корректной папки с form.txt, sketch.png и parameters.txt.");
            }

            return new SketchLibraryCatalog(templatesByForm, warnings);
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
    }
}
