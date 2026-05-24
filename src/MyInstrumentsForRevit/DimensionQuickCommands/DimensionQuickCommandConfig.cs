using System;

namespace MyRevitTools.DimensionQuickCommands
{
    public class DimensionQuickCommandConfig
    {
        public Guid Id { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string DimensionTypeName { get; set; } = string.Empty;

        public string DimensionTypeUniqueId { get; set; } = string.Empty;

        public int DimensionTypeElementId { get; set; }

        public int SlotNumber { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
