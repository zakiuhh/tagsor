namespace Tagsor
{
    public class KeyDisplayItem
    {
        public string Label { get; set; } = "";
        public string? IconGeometry { get; set; }
        public bool HasIcon => !string.IsNullOrEmpty(IconGeometry);
        public bool ShowSeparator { get; set; }
    }
}
