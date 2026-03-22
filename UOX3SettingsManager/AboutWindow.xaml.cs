using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace UOX3SettingsManager
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo();
            processStartInfo.FileName = e.Uri.AbsoluteUri;
            processStartInfo.UseShellExecute = true;

            Process.Start(processStartInfo);
            e.Handled = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}