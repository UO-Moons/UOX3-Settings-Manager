namespace UOX3SettingsManager.Models
{
    public class IniDocumentationEntry
    {
        public string SectionName { get; set; }
        public string KeyName { get; set; }
        public string ExampleValue { get; set; }
        public string Description { get; set; }
        public string HintText { get; set; }

        public IniDocumentationEntry()
        {
            SectionName = string.Empty;
            KeyName = string.Empty;
            ExampleValue = string.Empty;
            Description = string.Empty;
            HintText = string.Empty;
        }
    }
}