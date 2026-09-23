using System.Collections.Generic;

namespace MyInstrumentsForRevit.RebarSketch
{
    internal sealed class SketchTemplate
    {
        public SketchTemplate(
            string folderPath,
            string imagePath,
            IReadOnlyList<string> formNames,
            IReadOnlyList<SketchParameterPlacement> parameters)
        {
            FolderPath = folderPath;
            ImagePath = imagePath;
            FormNames = formNames;
            Parameters = parameters;
        }

        public string FolderPath { get; }

        public string ImagePath { get; }

        public IReadOnlyList<string> FormNames { get; }

        public IReadOnlyList<SketchParameterPlacement> Parameters { get; }
    }

    internal sealed class SketchParameterPlacement
    {
        public SketchParameterPlacement(string parameterName, float x, float y, float angle, bool isNarrow)
        {
            ParameterName = parameterName;
            X = x;
            Y = y;
            Angle = angle;
            IsNarrow = isNarrow;
        }

        public string ParameterName { get; }

        public float X { get; }

        public float Y { get; }

        public float Angle { get; }

        public bool IsNarrow { get; }
    }
}
