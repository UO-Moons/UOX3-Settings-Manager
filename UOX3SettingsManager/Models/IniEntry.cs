using System.ComponentModel;

namespace UOX3SettingsManager.Models
{
    public class IniEntry : INotifyPropertyChanged
    {
        private string valueText;
        private bool isModified;

        public string SectionName { get; set; }
        public string KeyName { get; set; }
        public string OriginalValue { get; set; }
        public string OriginalLine { get; set; }
        public int LineIndex { get; set; }
        public string HintText { get; set; }

        public string ValueText
        {
            get
            {
                return valueText;
            }
            set
            {
                SetValueInternal(value);
            }
        }

        public bool IsModified
        {
            get
            {
                return isModified;
            }
            set
            {
                if (isModified != value)
                {
                    isModified = value;
                    OnPropertyChanged("IsModified");
                    OnPropertyChanged("StatusText");
                }
            }
        }

        public string StatusText
        {
            get
            {
                if (IsModified)
                {
                    return "Modified";
                }

                return string.Empty;
            }
        }

        public void SetValueSilently(string newValue)
        {
            SetValueInternal(newValue);
        }

        private void SetValueInternal(string newValue)
        {
            if (valueText != newValue)
            {
                valueText = newValue;
                IsModified = (valueText != OriginalValue);
                OnPropertyChanged("ValueText");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}