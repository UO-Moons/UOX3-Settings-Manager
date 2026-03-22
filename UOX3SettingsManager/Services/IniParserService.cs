using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UOX3SettingsManager.Models;

namespace UOX3SettingsManager.Services
{
    public class IniParserService
    {
        public ObservableCollection<IniSection> LoadIniFile(string filePath, out List<string> originalLines)
        {
            originalLines = new List<string>();

            if (!File.Exists(filePath))
            {
                return new ObservableCollection<IniSection>();
            }

            originalLines.AddRange(File.ReadAllLines(filePath));

            ObservableCollection<IniSection> sections = new ObservableCollection<IniSection>();
            IniSection currentSection = null;

            for (int lineIndex = 0; lineIndex < originalLines.Count; lineIndex++)
            {
                string rawLine = originalLines[lineIndex];
                string trimmedLine = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    continue;
                }

                if (trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                {
                    continue;
                }

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    string sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                    currentSection = FindSection(sections, sectionName);

                    if (currentSection == null)
                    {
                        currentSection = new IniSection();
                        currentSection.Name = sectionName;
                        sections.Add(currentSection);
                    }

                    continue;
                }

                int equalsIndex = rawLine.IndexOf('=');
                if (equalsIndex <= 0)
                {
                    continue;
                }

                if (currentSection == null)
                {
                    currentSection = FindSection(sections, "Global");

                    if (currentSection == null)
                    {
                        currentSection = new IniSection();
                        currentSection.Name = "Global";
                        sections.Add(currentSection);
                    }
                }

                string keyName = rawLine.Substring(0, equalsIndex).Trim();
                string valueText = rawLine.Substring(equalsIndex + 1);

                IniEntry iniEntry = new IniEntry();
                iniEntry.SectionName = currentSection.Name;
                iniEntry.KeyName = keyName;
                iniEntry.OriginalValue = valueText;
                iniEntry.ValueText = valueText;
                iniEntry.OriginalLine = rawLine;
                iniEntry.LineIndex = lineIndex;
                iniEntry.IsModified = false;

                currentSection.Entries.Add(iniEntry);
            }

            return sections;
        }

        public void SaveIniFile(string filePath, List<string> originalLines, ObservableCollection<IniSection> sections, bool createBackup)
        {
            if (filePath == null)
            {
                throw new InvalidOperationException("INI file path is invalid.");
            }

            if (originalLines == null)
            {
                throw new InvalidOperationException("Original INI data is not loaded.");
            }

            if (sections == null)
            {
                throw new InvalidOperationException("INI sections are not loaded.");
            }

            List<string> updatedLines = new List<string>(originalLines);

            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                IniSection iniSection = sections[sectionIndex];

                for (int entryIndex = 0; entryIndex < iniSection.Entries.Count; entryIndex++)
                {
                    IniEntry iniEntry = iniSection.Entries[entryIndex];

                    if (iniEntry.LineIndex >= 0 && iniEntry.LineIndex < updatedLines.Count)
                    {
                        updatedLines[iniEntry.LineIndex] = iniEntry.KeyName + "=" + iniEntry.ValueText;
                    }
                }
            }

            if (createBackup && File.Exists(filePath))
            {
                string backupFilePath = filePath + ".bak";
                File.Copy(filePath, backupFilePath, true);
            }

            File.WriteAllLines(filePath, updatedLines);

            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                IniSection iniSection = sections[sectionIndex];

                for (int entryIndex = 0; entryIndex < iniSection.Entries.Count; entryIndex++)
                {
                    IniEntry iniEntry = iniSection.Entries[entryIndex];
                    iniEntry.OriginalValue = iniEntry.ValueText;
                    iniEntry.IsModified = false;
                }
            }
        }

        private IniSection FindSection(ObservableCollection<IniSection> sections, string sectionName)
        {
            for (int index = 0; index < sections.Count; index++)
            {
                if (string.Equals(sections[index].Name, sectionName, StringComparison.OrdinalIgnoreCase))
                {
                    return sections[index];
                }
            }

            return null;
        }
    }
}
