﻿using System;

namespace TimelapseTemplateEditor
{
    /// <summary>
    /// Set of helper methods for working with the Csv Strings.
    /// </summary>
    public class CsvHelperMethods
    {
        public static string ConvertLineBreaksToBars(string originalList)
        {
            string[] newLineDelimiters = { Environment.NewLine };
            char[] barDelimiter = { '|' };
            string[] rawItemList = originalList.Split(newLineDelimiters, StringSplitOptions.RemoveEmptyEntries);
            string trimmedItem = String.Empty;
            string newList = String.Empty;

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
            string trimmedItem = String.Empty;
            string newList = String.Empty;

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

        public String[] csvToArray(string valString)
        {
            String[] valArray = Array.ConvertAll(valString.Split('|'), p => p.Trim());
            return valArray;
        }
        public string arrayToCSV(String[] valArray)
        {
            string newComboString = String.Empty;
            foreach (string s in valArray) //turns the array back into a string to re-add it.
            {
                newComboString += s + "| ";
            }
            if (!String.IsNullOrEmpty(newComboString)) //removes trailing "| " from string
            {
                newComboString = newComboString.Substring(0, newComboString.Length - 2);
            }
            return newComboString;
        }

        public string deleteItemFromCSV(string comboBoxString, string selectedItemString)
        {
            String[] valArray = csvToArray(comboBoxString);
            string newComboString = String.Empty;
            foreach (string s in valArray) //turns the array back into a string to re-add it.
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
        public string editItemInCSV(string comboBoxString, string selectedItemString, string editedValue)
        {
            String[] valArray = csvToArray(comboBoxString);
            string newComboString = String.Empty;
            foreach (string s in valArray) //turns the array back into a string to re-add it.
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
