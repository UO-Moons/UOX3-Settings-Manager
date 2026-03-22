using System.Collections.ObjectModel;

namespace UOX3SettingsManager.Models
{
    public class IniSection
    {
        public string Name { get; set; }
        public ObservableCollection<IniEntry> Entries { get; set; }

        public IniSection()
        {
            Name = string.Empty;
            Entries = new ObservableCollection<IniEntry>();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
