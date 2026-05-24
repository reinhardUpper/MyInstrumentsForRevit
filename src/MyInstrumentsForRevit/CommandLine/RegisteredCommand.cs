using System;
using Autodesk.Revit.UI;

namespace MyInstrumentsForRevit.CommandLine
{
    internal sealed class RegisteredCommand
    {
        public RegisteredCommand(string name, string displayName, string description, Action<UIApplication> execute)
        {
            Name = name;
            DisplayName = displayName;
            Description = description;
            Execute = execute;
        }

        public RegisteredCommand(string name, string description, Action<UIApplication> execute)
            : this(name, name, description, execute)
        {
        }

        public string Name { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public Action<UIApplication> Execute { get; }

        public string SearchText => Name + " " + DisplayName + " " + Description;

        public string SuggestionText => DisplayName + " (" + Name + ")";
    }
}
