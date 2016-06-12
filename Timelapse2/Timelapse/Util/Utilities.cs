using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace Timelapse.Util
{
    /// <summary>
    /// Utilities collect a variety of miscellaneous utility functions
    /// </summary>
    public class Utilities
    {
        private static readonly char[] BarDelimiter = { '|' };
        private static readonly string[] NewLineDelimiters = { Environment.NewLine };

        // get a location for the template database from the user
        public static bool TryGetFileFromUser(string title, string defaultFilePath, string filter, out string selectedFilePath)
        {
            // Get the template file, which should be located where the images reside
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = title;
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.Multiselect = false;
            if (String.IsNullOrWhiteSpace(defaultFilePath))
            {
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            else
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(defaultFilePath);
                openFileDialog.FileName = Path.GetFileName(defaultFilePath);
            }
            openFileDialog.AutoUpgradeEnabled = true;

            // Set filter for file extension and default file extension 
            openFileDialog.DefaultExt = Constants.File.TemplateDatabaseFileExtension;
            openFileDialog.Filter = filter;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                selectedFilePath = openFileDialog.FileName;
                return true;
            }

            selectedFilePath = null;
            return false;
        }

        public static string ConvertBarsToLineBreaks(string barSeparatedChoices)
        {
            string[] choices = barSeparatedChoices.Split(Utilities.BarDelimiter);

            string newlineSeparatedChoices = String.Empty;
            foreach (string choice in choices)
            {
                string trimmedItem = choice.Trim();
                if (String.IsNullOrEmpty(trimmedItem))
                {
                    continue; // ignore blank items
                }
                if (!String.IsNullOrEmpty(newlineSeparatedChoices))
                {
                    newlineSeparatedChoices += Environment.NewLine; // Add a newline if there is already a string in there
                }
                newlineSeparatedChoices += trimmedItem;
            }
            newlineSeparatedChoices = newlineSeparatedChoices.TrimEnd('\r', '\n'); // remove the last "newline" if items exists
            return newlineSeparatedChoices;
        }

        public static List<string> ConvertBarsToList(string barSeparatedChoices)
        {
            return new List<string>(barSeparatedChoices.Split(Utilities.BarDelimiter));
        }

        public static string ConvertLineBreaksToBars(string newlineSeparatedChoices)
        {
            string[] choices = newlineSeparatedChoices.Split(Utilities.NewLineDelimiters, StringSplitOptions.RemoveEmptyEntries);

            string barSeparatedChoices = String.Empty;
            foreach (string choice in choices)
            {
                string trimmedItem = choice.Trim();
                if (String.IsNullOrEmpty(trimmedItem))
                {
                    continue; // ignore blank items
                }
                if (!String.IsNullOrEmpty(barSeparatedChoices))
                {
                    barSeparatedChoices += "|"; // Add a '|' if there is already a string in there
                }
                barSeparatedChoices += trimmedItem;
            }
            barSeparatedChoices = barSeparatedChoices.TrimEnd(BarDelimiter); // remove the last "|" if items exists
            return barSeparatedChoices;
        }

        // Calculate the point as a ratio of its position on the image, so we can locate it regardless of the actual image size
        public static Point ConvertPointToRatio(Point p, double width, double height)
        {
            Point ratioPt = new Point((double)p.X / (double)width, (double)p.Y / (double)height);
            return ratioPt;
        }

        // The inverse of the above operation
        public static Point ConvertRatioToPoint(Point p, double width, double height)
        {
            Point imagePt = new Point(p.X * width, p.Y * height);
            return imagePt;
        }

        /// <summary>
        /// Format the passed value for use as string value in a SQL statement or query.
        /// </summary>
        public static string QuoteForSql(string value)
        {
            // promote null values to empty strings
            if (value == null)
            {
                return "''";
            }

            // for an input of "foo's bar" the output is "'foo''s bar'"
            return "'" + value.Replace("'", "''") + "'";
        }
    }
}
