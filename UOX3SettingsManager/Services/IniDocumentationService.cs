using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UOX3SettingsManager.Models;

namespace UOX3SettingsManager.Services
{
    public class IniDocumentationService
    {
        public Dictionary<string, IniDocumentationEntry> LoadDocumentation(string htmlFilePath)
        {
            Dictionary<string, IniDocumentationEntry> documentationLookup = new Dictionary<string, IniDocumentationEntry>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(htmlFilePath))
            {
                return documentationLookup;
            }

            if (!File.Exists(htmlFilePath))
            {
                return documentationLookup;
            }

            string htmlText = File.ReadAllText(htmlFilePath);

            int iniSectionStart = htmlText.IndexOf("id=\"uoxINISettings\"", StringComparison.OrdinalIgnoreCase);
            if (iniSectionStart < 0)
            {
                return documentationLookup;
            }

            int iniSectionEnd = htmlText.IndexOf("</div>", iniSectionStart, StringComparison.OrdinalIgnoreCase);
            if (iniSectionEnd < 0)
            {
                iniSectionEnd = htmlText.Length;
            }

            string iniHtml = htmlText.Substring(iniSectionStart);

            Regex sectionRegex = new Regex(
                "<label\\s+for=\"spoiler_ini_[^\"]+\">\\s*\\[(?<section>[^\\]]+)\\]",
                RegexOptions.IgnoreCase);

            Regex settingRegex = new Regex(
                "<div\\s+class=\"settingsDiv\">\\s*<p><span\\s+class=\"hl\">(?<key>[^<]+)</span><strong>=(?<example>[^<]*)</strong><br>\\s*(?<description>.*?)</p>\\s*</div>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            MatchCollection sectionMatches = sectionRegex.Matches(iniHtml);
            if (sectionMatches.Count == 0)
            {
                return documentationLookup;
            }

            for (int sectionIndex = 0; sectionIndex < sectionMatches.Count; sectionIndex++)
            {
                Match currentSectionMatch = sectionMatches[sectionIndex];
                string sectionName = DecodeHtml(CleanText(currentSectionMatch.Groups["section"].Value));

                int sectionContentStart = currentSectionMatch.Index + currentSectionMatch.Length;
                int sectionContentEnd = iniHtml.Length;

                if (sectionIndex + 1 < sectionMatches.Count)
                {
                    sectionContentEnd = sectionMatches[sectionIndex + 1].Index;
                }

                string sectionContent = iniHtml.Substring(sectionContentStart, sectionContentEnd - sectionContentStart);
                MatchCollection settingMatches = settingRegex.Matches(sectionContent);

                for (int settingIndex = 0; settingIndex < settingMatches.Count; settingIndex++)
                {
                    Match settingMatch = settingMatches[settingIndex];

                    string keyName = DecodeHtml(CleanText(settingMatch.Groups["key"].Value));
                    string exampleValue = DecodeHtml(CleanText(settingMatch.Groups["example"].Value));
                    string description = DecodeHtml(CleanText(RemoveHtml(settingMatch.Groups["description"].Value)));

                    IniDocumentationEntry documentationEntry = new IniDocumentationEntry();
                    documentationEntry.SectionName = sectionName;
                    documentationEntry.KeyName = keyName;
                    documentationEntry.ExampleValue = exampleValue;
                    documentationEntry.Description = description;
                    documentationEntry.HintText = BuildHintText(documentationEntry);

                    string lookupKey = BuildLookupKey(sectionName, keyName);
                    if (!documentationLookup.ContainsKey(lookupKey))
                    {
                        documentationLookup.Add(lookupKey, documentationEntry);
                    }
                }
            }

            return documentationLookup;
        }

        public string BuildLookupKey(string sectionName, string keyName)
        {
            string normalizedSection = (sectionName ?? string.Empty).Trim().ToUpperInvariant();
            string normalizedKey = (keyName ?? string.Empty).Trim().ToUpperInvariant();
            return normalizedSection + "." + normalizedKey;
        }

        private string BuildHintText(IniDocumentationEntry documentationEntry)
        {
            string hintText = "[" + documentationEntry.SectionName + "]\r\n" + documentationEntry.KeyName;

            if (!string.IsNullOrWhiteSpace(documentationEntry.ExampleValue))
            {
                hintText += "=" + documentationEntry.ExampleValue;
            }

            if (!string.IsNullOrWhiteSpace(documentationEntry.Description))
            {
                hintText += "\r\n\r\n" + documentationEntry.Description;
            }

            return hintText;
        }

        private string RemoveHtml(string htmlText)
        {
            if (string.IsNullOrWhiteSpace(htmlText))
            {
                return string.Empty;
            }

            string cleanedText = Regex.Replace(htmlText, "<br\\s*/?>", " ", RegexOptions.IgnoreCase);
            cleanedText = Regex.Replace(cleanedText, "<[^>]+>", " ");
            return cleanedText;
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string cleanedText = text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            cleanedText = Regex.Replace(cleanedText, "\\s+", " ");
            return cleanedText.Trim();
        }

        private string DecodeHtml(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string decodedText = text;
            decodedText = decodedText.Replace("&amp;", "&");
            decodedText = decodedText.Replace("&lt;", "<");
            decodedText = decodedText.Replace("&gt;", ">");
            decodedText = decodedText.Replace("&quot;", "\"");
            decodedText = decodedText.Replace("&#39;", "'");
            decodedText = decodedText.Replace("&nbsp;", " ");
            return decodedText;
        }
    }
}