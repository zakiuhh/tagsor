namespace Tagsor
{
    public class KeyDisplayItem
    {
        public string KeyId { get; set; } = "";
        public string Label { get; set; } = "";
        public string? IconGeometry { get; set; }
        public int RepeatCount { get; set; } = 1;
        public bool HasIcon => !string.IsNullOrEmpty(IconGeometry);
        public bool ShowSeparator { get; set; }
    }
}
