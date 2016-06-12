using System;

namespace TimelapseTemplateEditor.Util
{
    /// <summary>
    /// Set of helper methods for working with delimited strings.
    /// </summary>
    public class CsvHelper
    {
        private static readonly char[] BarDelimiter = { '|' };
        private static readonly string[] NewLineDelimiters = { Environment.NewLine };

        public static string ConvertLineBreaksToBars(string originalList)
        {
            string[] rawItemList = originalList.Split(CsvHelper.NewLineDelimiters, StringSplitOptions.RemoveEmptyEntries);
            string newList = String.Empty;

            foreach (string rawItem in rawItemList)
            {
                string trimmedItem = rawItem.Trim();
                if (String.IsNullOrEmpty(trimmedItem))
                {
                    continue; // ignore blank items
                }
                if (!String.IsNullOrEmpty(newList))
                {
                    newList += "|"; // Add a '|' if there is already a string in there
                }
                newList += trimmedItem;
            }
            newList = newList.TrimEnd(BarDelimiter); // remove the last "|" if items exists
            return newList;
        }

        public static string ConvertBarsToLineBreaks(string originalList)
        {
            string[] rawItemList = originalList.Split(CsvHelper.BarDelimiter);
            string newList = String.Empty;

            foreach (string rawItem in rawItemList)
            {
                string trimmedItem = rawItem.Trim();
                if (String.IsNullOrEmpty(trimmedItem))
                {
                    continue; // ignore blank items
                }
                if (!String.IsNullOrEmpty(newList))
                {
                    newList += Environment.NewLine; // Add a newline if there is already a string in there
                }
                newList += trimmedItem;
            }
            newList = newList.TrimEnd('\r', '\n'); // remove the last "newline" if items exists
            return newList;
        }
    }
}