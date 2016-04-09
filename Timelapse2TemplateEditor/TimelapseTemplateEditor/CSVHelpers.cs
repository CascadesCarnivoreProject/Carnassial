using System;

namespace TimelapseTemplateEditor
{
    /// <summary>
    /// Set of helper methods for working with the Csv Strings.
    /// </summary>
    public class CsvHelperMethods
    {

        public static string  ConvertLineBreaksToBars (string originalList)
        {
            string[] newLineDelimiters = { Environment.NewLine };
            char[] barDelimiter = { '|' };
            string[] rawItemList = originalList.Split(newLineDelimiters, StringSplitOptions.RemoveEmptyEntries);
            string trimmedItem = "";
            string newList = "";

            foreach (string rawItem in rawItemList)
            {
                trimmedItem = rawItem.Trim();
                if (string.IsNullOrEmpty(trimmedItem)) continue; // ignore blank items
                if (!string.IsNullOrEmpty(newList)) newList += "|"; // Add a '|' if there is already a string in there
                newList += trimmedItem;
            }
            newList = newList.TrimEnd(barDelimiter); //remove the last "|" if items exists
            return newList;
        }

        public static string ConvertBarsToLineBreaks(string originalList)
        {
            string[] newLineDelimiters = { Environment.NewLine };
            char[] barDelimiter = { '|' };
            string[] rawItemList = originalList.Split(barDelimiter);
            string trimmedItem = "";
            string newList = "";

            foreach (string rawItem in rawItemList)
            {
                trimmedItem = rawItem.Trim();
                if (string.IsNullOrEmpty(trimmedItem)) continue; // ignore blank items
                if (!string.IsNullOrEmpty(newList)) newList += Environment.NewLine; // Add a newline if there is already a string in there
                newList += trimmedItem;
            }
            newList = newList.TrimEnd('\r', '\n');  //remove the last "newline" if items exists
            return newList;
        }


            // All the stuff below here is old and can likely be deleted

        public String[] csvToArray(String valString)
        {
            String[] valArray = Array.ConvertAll(valString.Split('|'), p => p.Trim());
            return valArray;
        }
        public String arrayToCSV(String[] valArray)
        {
            String newComboString = "";
            foreach (String s in valArray) //turns the array back into a string to re-add it.
            {
                newComboString += s + "| ";
            }
            if (!String.IsNullOrEmpty(newComboString)) //removes trailing "| " from string
            {
                newComboString = newComboString.Substring(0, newComboString.Length - 2);
            }
            return newComboString;
        }

        public String deleteItemFromCSV(String comboBoxString, String selectedItemString)
        {
            String[] valArray = csvToArray(comboBoxString);
            String newComboString = "";
            foreach (String s in valArray) //turns the array back into a string to re-add it.
            {
                if (!s.Equals(selectedItemString)) //if its not the deleted value, add to new return string
                {
                    newComboString += s + "| ";
                }
            }
            if (!String.IsNullOrEmpty(newComboString)) //removes trailing ", " from string
            {
                newComboString = newComboString.Substring(0, newComboString.Length - 2);
            }
            return newComboString;
        }

        //very similar to delete, only replacing one item
        public String editItemInCSV(String comboBoxString, String selectedItemString, String editedValue)
        {
            String[] valArray = csvToArray(comboBoxString);
            String newComboString = "";
            foreach (String s in valArray) //turns the array back into a string to re-add it.
            {
                if (!s.Equals(selectedItemString)) //if its not the edited value, add to new return string
                {
                    newComboString += s + "| ";
                }
                else
                {
                    newComboString += editedValue + "| ";
                }
            }
            if (!String.IsNullOrEmpty(newComboString)) //removes trailing ", " from string
            {
                newComboString = newComboString.Substring(0, newComboString.Length - 2);
            }
            return newComboString;
        }
    }
}
