using System;

namespace MyRevitTools.DimensionQuickCommands
{
    public class DimensionQuickCommandConfig
    {
        public Guid Id { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string HotkeyText { get; set; } = string.Empty;

        public string CommandKind { get; set; } = QuickCommandKind.LinearDimension;

        public string CommandKindDisplayName => QuickCommandKind.GetDisplayName(CommandKind);

        public string DimensionTypeName { get; set; } = string.Empty;

        public string DimensionTypeUniqueId { get; set; } = string.Empty;

        public int DimensionTypeElementId { get; set; }

        public int SlotNumber { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
