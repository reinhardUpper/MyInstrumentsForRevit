namespace MyRevitTools.DimensionQuickCommands
{
    public static class QuickCommandKind
    {
        public const string LinearDimension = "LinearDimension";

        public const string DetailItem = "DetailItem";

        public static string Normalize(string? value)
        {
            return value == DetailItem ? DetailItem : LinearDimension;
        }

        public static string GetDisplayName(string? value)
        {
            return Normalize(value) == DetailItem ? "Элемент узла" : "Размер";
        }
    }
}
