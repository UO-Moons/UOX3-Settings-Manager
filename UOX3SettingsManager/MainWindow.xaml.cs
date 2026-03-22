using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UOX3SettingsManager.Models;
using UOX3SettingsManager.Services;

namespace UOX3SettingsManager
{
    public partial class MainWindow : Window
    {
        private readonly IniParserService iniParserService;
        private readonly IniDocumentationService iniDocumentationService;

        private ObservableCollection<IniSection> loadedSections;
        private List<string> originalLines;
        private IniSection selectedSection;

        private readonly Stack<IniChange> undoStack;
        private readonly Stack<IniChange> redoStack;
        private bool suppressHistoryRecording;
        private string pendingOldValue;

        private Dictionary<string, IniDocumentationEntry> documentationLookup;

        private readonly LauncherSettingsService launcherSettingsService;
        private LauncherSettings launcherSettings;
        private string currentThemeName;

        public ICommand UndoCommand { get; private set; }
        public ICommand RedoCommand { get; private set; }

        public MainWindow()
        {
            InitializeComponent();

            iniParserService = new IniParserService();
            iniDocumentationService = new IniDocumentationService();
            launcherSettingsService = new LauncherSettingsService();

            loadedSections = new ObservableCollection<IniSection>();
            originalLines = new List<string>();
            selectedSection = null;

            undoStack = new Stack<IniChange>();
            redoStack = new Stack<IniChange>();
            suppressHistoryRecording = false;
            pendingOldValue = null;

            documentationLookup = new Dictionary<string, IniDocumentationEntry>(StringComparer.OrdinalIgnoreCase);

            UndoCommand = new RelayCommand(ExecuteUndoLast, CanExecuteUndoLast);
            RedoCommand = new RelayCommand(ExecuteRedoLast, CanExecuteRedoLast);

            DataContext = this;

            launcherSettings = new LauncherSettings();
            LoadLauncherSettingsIntoUi();

            StatusTextBlock.Text = "Settings file: " + launcherSettingsService.GetSettingsFilePath();
        }

        private void BrowseExecutableButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
            openFileDialog.Title = "Select UOX3 Executable";

            if (openFileDialog.ShowDialog() == true)
            {
                UoxExecutablePathTextBox.Text = openFileDialog.FileName;

                string possibleIniPath = Path.Combine(Path.GetDirectoryName(openFileDialog.FileName), "uox.ini");
                if (File.Exists(possibleIniPath) && string.IsNullOrWhiteSpace(IniFilePathTextBox.Text))
                {
                    IniFilePathTextBox.Text = possibleIniPath;
                }
                SaveLauncherSettingsFromUi();
            }
        }

        private void BrowseIniButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "INI Files (*.ini)|*.ini|All Files (*.*)|*.*";
            openFileDialog.Title = "Select UOX3 INI File";

            if (openFileDialog.ShowDialog() == true)
            {
                IniFilePathTextBox.Text = openFileDialog.FileName;
                SaveLauncherSettingsFromUi();
            }
        }

        private void BrowseDocsButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "HTML Files (*.html;*.htm)|*.html;*.htm|All Files (*.*)|*.*";
            openFileDialog.Title = "Select UOX3 Docs HTML";

            if (openFileDialog.ShowDialog() == true)
            {
                DocsHtmlPathTextBox.Text = openFileDialog.FileName;
                LoadDocumentationFile();
                ApplyDocumentationHints();
                RefreshCurrentSectionView();
                SaveLauncherSettingsFromUi();
            }
        }

        private void LoadIniButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string iniFilePath = IniFilePathTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(iniFilePath))
                {
                    throw new InvalidOperationException("Please select a UOX3 INI file.");
                }

                if (!File.Exists(iniFilePath))
                {
                    throw new FileNotFoundException("The selected INI file was not found.", iniFilePath);
                }

                loadedSections = iniParserService.LoadIniFile(iniFilePath, out originalLines);
                LoadDocumentationFile();
                ApplyDocumentationHints();

                SectionsListBox.ItemsSource = loadedSections;
                EntriesDataGrid.ItemsSource = null;
                selectedSection = null;
                SelectedSectionTextBlock.Text = "None";

                undoStack.Clear();
                redoStack.Clear();
                pendingOldValue = null;

                if (loadedSections.Count > 0)
                {
                    SectionsListBox.SelectedIndex = 0;
                }

                StatusTextBlock.Text = "INI loaded. Sections: " + loadedSections.Count + ". Docs: " + documentationLookup.Count;
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Load INI Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Failed to load INI file.";
            }
        }

        private void SaveIniButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveIniFileInternal();
                StatusTextBlock.Text = "INI saved successfully.";
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Save INI Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Failed to save INI file.";
            }
        }

        private void SaveAndLaunchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveIniFileInternal();

                string executablePath = UoxExecutablePathTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    throw new InvalidOperationException("Please select a UOX3 executable file.");
                }

                if (!File.Exists(executablePath))
                {
                    throw new FileNotFoundException("The selected UOX3 executable was not found.", executablePath);
                }

                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = executablePath;
                processStartInfo.WorkingDirectory = Path.GetDirectoryName(executablePath);
                processStartInfo.UseShellExecute = true;

                Process.Start(processStartInfo);

                StatusTextBlock.Text = "INI saved and UOX3 launched successfully.";
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Failed to save and launch UOX3.";
            }
        }

        private void SaveIniFileInternal()
        {
            ValidateBeforeSave();

            string iniFilePath = IniFilePathTextBox.Text.Trim();
            bool createBackup = (BackupBeforeSaveCheckBox.IsChecked == true);

            iniParserService.SaveIniFile(iniFilePath, originalLines, loadedSections, createBackup);

            undoStack.Clear();
            redoStack.Clear();
            pendingOldValue = null;

            RefreshCurrentSectionView();
            CommandManager.InvalidateRequerySuggested();
        }

        private void SectionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                selectedSection = SectionsListBox.SelectedItem as IniSection;

                if (selectedSection == null)
                {
                    EntriesDataGrid.ItemsSource = null;
                    SelectedSectionTextBlock.Text = "None";
                    return;
                }

                SelectedSectionTextBlock.Text = selectedSection.Name;
                ApplySearchFilter();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Selection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void EntriesDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            IniEntry iniEntry = e.Row.Item as IniEntry;
            if (iniEntry != null)
            {
                pendingOldValue = iniEntry.ValueText;
            }
            else
            {
                pendingOldValue = null;
            }
        }

        private void EntriesDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (suppressHistoryRecording)
            {
                return;
            }

            if (e.EditAction != DataGridEditAction.Commit)
            {
                pendingOldValue = null;
                return;
            }

            IniEntry iniEntry = e.Row.Item as IniEntry;
            TextBox editingTextBox = e.EditingElement as TextBox;

            if (iniEntry == null || editingTextBox == null)
            {
                pendingOldValue = null;
                return;
            }

            string oldValue = pendingOldValue ?? iniEntry.ValueText;
            string newValue = editingTextBox.Text;

            if (oldValue != newValue)
            {
                IniChange iniChange = new IniChange();
                iniChange.Entry = iniEntry;
                iniChange.OldValue = oldValue;
                iniChange.NewValue = newValue;

                undoStack.Push(iniChange);
                redoStack.Clear();

                StatusTextBlock.Text = "Changed " + iniEntry.KeyName;
                CommandManager.InvalidateRequerySuggested();
            }

            pendingOldValue = null;
        }

        private void UndoSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IniEntry selectedEntry = EntriesDataGrid.SelectedItem as IniEntry;
                if (selectedEntry == null)
                {
                    StatusTextBlock.Text = "No entry selected to undo.";
                    return;
                }

                suppressHistoryRecording = true;
                selectedEntry.SetValueSilently(selectedEntry.OriginalValue);
                suppressHistoryRecording = false;

                StatusTextBlock.Text = "Reverted selected entry: " + selectedEntry.KeyName;
                RefreshCurrentSectionView();
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception exception)
            {
                suppressHistoryRecording = false;
                MessageBox.Show(exception.Message, "Undo Selected Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Failed to undo selected entry.";
            }
        }

        private void UndoAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (loadedSections == null || loadedSections.Count == 0)
                {
                    StatusTextBlock.Text = "No loaded INI data to undo.";
                    return;
                }

                suppressHistoryRecording = true;

                for (int sectionIndex = 0; sectionIndex < loadedSections.Count; sectionIndex++)
                {
                    IniSection iniSection = loadedSections[sectionIndex];

                    for (int entryIndex = 0; entryIndex < iniSection.Entries.Count; entryIndex++)
                    {
                        IniEntry iniEntry = iniSection.Entries[entryIndex];
                        iniEntry.SetValueSilently(iniEntry.OriginalValue);
                    }
                }

                suppressHistoryRecording = false;

                undoStack.Clear();
                redoStack.Clear();
                pendingOldValue = null;

                StatusTextBlock.Text = "All unsaved changes have been reverted.";
                RefreshCurrentSectionView();
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception exception)
            {
                suppressHistoryRecording = false;
                MessageBox.Show(exception.Message, "Undo All Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Failed to undo changes.";
            }
        }

        private void UndoLastButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteUndoLast(null);
        }

        private void ExecuteUndoLast(object parameter)
        {
            if (undoStack.Count == 0)
            {
                return;
            }

            IniChange iniChange = undoStack.Pop();

            IniChange redoChange = new IniChange();
            redoChange.Entry = iniChange.Entry;
            redoChange.OldValue = iniChange.OldValue;
            redoChange.NewValue = iniChange.NewValue;

            suppressHistoryRecording = true;
            iniChange.Entry.SetValueSilently(iniChange.OldValue);
            suppressHistoryRecording = false;

            redoStack.Push(redoChange);

            RefreshCurrentSectionView();
            StatusTextBlock.Text = "Undo: " + iniChange.Entry.KeyName;
            CommandManager.InvalidateRequerySuggested();
        }

        private bool CanExecuteUndoLast(object parameter)
        {
            return undoStack.Count > 0;
        }

        private void ExecuteRedoLast(object parameter)
        {
            if (redoStack.Count == 0)
            {
                return;
            }

            IniChange iniChange = redoStack.Pop();

            IniChange undoChange = new IniChange();
            undoChange.Entry = iniChange.Entry;
            undoChange.OldValue = iniChange.OldValue;
            undoChange.NewValue = iniChange.NewValue;

            suppressHistoryRecording = true;
            iniChange.Entry.SetValueSilently(iniChange.NewValue);
            suppressHistoryRecording = false;

            undoStack.Push(undoChange);

            RefreshCurrentSectionView();
            StatusTextBlock.Text = "Redo: " + iniChange.Entry.KeyName;
            CommandManager.InvalidateRequerySuggested();
        }

        private bool CanExecuteRedoLast(object parameter)
        {
            return redoStack.Count > 0;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                SaveLauncherSettingsFromUi();
            }
            catch
            {
            }

            if (!HasModifiedEntries())
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                "You have unsaved changes. Do you want to save before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    SaveIniFileInternal();
                }
                catch
                {
                    e.Cancel = true;
                    return;
                }

                if (HasModifiedEntries())
                {
                    e.Cancel = true;
                }
            }
        }

        private void LoadDocumentationFile()
        {
            string docsHtmlPath = DocsHtmlPathTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(docsHtmlPath) || !File.Exists(docsHtmlPath))
            {
                documentationLookup = new Dictionary<string, IniDocumentationEntry>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            documentationLookup = iniDocumentationService.LoadDocumentation(docsHtmlPath);
        }

        private void ApplyDocumentationHints()
        {
            if (loadedSections == null)
            {
                return;
            }

            for (int sectionIndex = 0; sectionIndex < loadedSections.Count; sectionIndex++)
            {
                IniSection iniSection = loadedSections[sectionIndex];

                for (int entryIndex = 0; entryIndex < iniSection.Entries.Count; entryIndex++)
                {
                    IniEntry iniEntry = iniSection.Entries[entryIndex];
                    string lookupKey = iniDocumentationService.BuildLookupKey(iniEntry.SectionName, iniEntry.KeyName);

                    IniDocumentationEntry documentationEntry;
                    if (documentationLookup.TryGetValue(lookupKey, out documentationEntry))
                    {
                        iniEntry.HintText = documentationEntry.HintText;
                    }
                    else
                    {
                        iniEntry.HintText = string.Empty;
                    }
                }
            }
        }

        private void ApplySearchFilter()
        {
            if (selectedSection == null)
            {
                EntriesDataGrid.ItemsSource = null;
                return;
            }

            string searchText = SearchTextBox.Text;
            ObservableCollection<IniEntry> filteredEntries = new ObservableCollection<IniEntry>();

            for (int index = 0; index < selectedSection.Entries.Count; index++)
            {
                IniEntry iniEntry = selectedSection.Entries[index];

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    filteredEntries.Add(iniEntry);
                }
                else
                {
                    if (ContainsText(iniEntry.KeyName, searchText) || ContainsText(iniEntry.ValueText, searchText) || ContainsText(iniEntry.HintText, searchText))
                    {
                        filteredEntries.Add(iniEntry);
                    }
                }
            }

            EntriesDataGrid.ItemsSource = filteredEntries;
            StatusTextBlock.Text = "Showing " + filteredEntries.Count + " entries in section [" + selectedSection.Name + "].";
        }

        private bool ContainsText(string sourceText, string searchText)
        {
            if (sourceText == null)
            {
                return false;
            }

            return sourceText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ValidateBeforeSave()
        {
            string iniFilePath = IniFilePathTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(iniFilePath))
            {
                throw new InvalidOperationException("Please select a UOX3 INI file.");
            }

            if (loadedSections == null || loadedSections.Count == 0)
            {
                throw new InvalidOperationException("No INI data is loaded.");
            }

            string executablePath = UoxExecutablePathTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(executablePath) && !File.Exists(executablePath))
            {
                throw new InvalidOperationException("The selected UOX3 executable file does not exist.");
            }
        }

        private void RefreshCurrentSectionView()
        {
            if (selectedSection == null)
            {
                EntriesDataGrid.ItemsSource = null;
                return;
            }

            ApplySearchFilter();
        }

        private bool HasModifiedEntries()
        {
            if (loadedSections == null)
            {
                return false;
            }

            for (int sectionIndex = 0; sectionIndex < loadedSections.Count; sectionIndex++)
            {
                IniSection iniSection = loadedSections[sectionIndex];

                for (int entryIndex = 0; entryIndex < iniSection.Entries.Count; entryIndex++)
                {
                    if (iniSection.Entries[entryIndex].IsModified)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void ApplyLightTheme()
        {
            Application.Current.Resources["WindowBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245));
            Application.Current.Resources["ControlBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
            Application.Current.Resources["ControlForegroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(17, 17, 17));
            Application.Current.Resources["BorderBrushColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(208, 208, 208));
            Application.Current.Resources["ModifiedRowBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(246, 237, 177));
            Application.Current.Resources["StatusForegroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30));
            Application.Current.Resources["GroupBoxForegroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(17, 17, 17));
            Application.Current.Resources["DataGridHeaderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 232, 232));
        }

        private void ApplyDarkTheme()
        {
            Application.Current.Resources["WindowBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30));
            Application.Current.Resources["ControlBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48));
            Application.Current.Resources["ControlForegroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
            Application.Current.Resources["BorderBrushColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80));
            Application.Current.Resources["ModifiedRowBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(92, 84, 30));
            Application.Current.Resources["StatusForegroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
            Application.Current.Resources["GroupBoxForegroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
            Application.Current.Resources["DataGridHeaderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 64));
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            ComboBoxItem selectedItem = ThemeComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem == null)
            {
                return;
            }

            string selectedTheme = selectedItem.Content as string;
            if (string.Equals(selectedTheme, "Dark", StringComparison.OrdinalIgnoreCase))
            {
                currentThemeName = "Dark";
                ApplyDarkTheme();
                StatusTextBlock.Text = "Dark theme applied.";
            }
            else
            {
                currentThemeName = "Light";
                ApplyLightTheme();
                StatusTextBlock.Text = "Light theme applied.";
            }

            SaveLauncherSettingsFromUi();
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.Owner = this;
            aboutWindow.ShowDialog();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            HelpWindow helpWindow = new HelpWindow();
            helpWindow.Owner = this;
            helpWindow.ShowDialog();
        }

        private void LoadLauncherSettingsIntoUi()
        {
            launcherSettings = launcherSettingsService.LoadSettings();

            if (launcherSettings == null)
            {
                launcherSettings = new LauncherSettings();
            }

            UoxExecutablePathTextBox.Text = launcherSettings.UoxExecutablePath ?? string.Empty;
            IniFilePathTextBox.Text = launcherSettings.IniFilePath ?? string.Empty;
            DocsHtmlPathTextBox.Text = launcherSettings.DocsHtmlPath ?? string.Empty;
            BackupBeforeSaveCheckBox.IsChecked = launcherSettings.BackupBeforeSave;

            currentThemeName = string.IsNullOrWhiteSpace(launcherSettings.ThemeName) ? "Light" : launcherSettings.ThemeName;

            if (string.Equals(currentThemeName, "Dark", StringComparison.OrdinalIgnoreCase))
            {
                ThemeComboBox.SelectedIndex = 1;
                ApplyDarkTheme();
            }
            else
            {
                currentThemeName = "Light";
                ThemeComboBox.SelectedIndex = 0;
                ApplyLightTheme();
            }
        }

        private void SaveLauncherSettingsFromUi()
        {
            if (!IsLoaded)
            {
                return;
            }

            if (launcherSettingsService == null)
            {
                return;
            }

            if (launcherSettings == null)
            {
                launcherSettings = new LauncherSettings();
            }

            launcherSettings.UoxExecutablePath = UoxExecutablePathTextBox != null ? UoxExecutablePathTextBox.Text.Trim() : string.Empty;
            launcherSettings.IniFilePath = IniFilePathTextBox != null ? IniFilePathTextBox.Text.Trim() : string.Empty;
            launcherSettings.DocsHtmlPath = DocsHtmlPathTextBox != null ? DocsHtmlPathTextBox.Text.Trim() : string.Empty;
            launcherSettings.BackupBeforeSave = (BackupBeforeSaveCheckBox != null && BackupBeforeSaveCheckBox.IsChecked == true);
            launcherSettings.ThemeName = string.IsNullOrWhiteSpace(currentThemeName) ? "Light" : currentThemeName;

            launcherSettingsService.SaveSettings(launcherSettings);
        }

        private void BackupBeforeSaveCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            SaveLauncherSettingsFromUi();
        }
    }
}