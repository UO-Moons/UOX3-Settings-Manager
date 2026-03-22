namespace UOX3SettingsManager.Models
{
    public class LauncherSettings
    {
        public string UoxExecutablePath { get; set; }
        public string IniFilePath { get; set; }
        public string DocsHtmlPath { get; set; }
        public string ThemeName { get; set; }
        public bool BackupBeforeSave { get; set; }

        public LauncherSettings()
        {
            UoxExecutablePath = string.Empty;
            IniFilePath = string.Empty;
            DocsHtmlPath = string.Empty;
            ThemeName = "Light";
            BackupBeforeSave = true;
        }
    }
}