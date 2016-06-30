using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using Rectangle = System.Drawing.Rectangle;

namespace Timelapse.Util
{
    /// <summary>
    /// Utilities collect a variety of miscellaneous utility functions
    /// </summary>
    public class Utilities
    {
        private static readonly char[] BarDelimiter = { '|' };
        private static readonly string[] NewLineDelimiters = { Environment.NewLine };

        public static bool IsSingleTemplateFileDrag(DragEventArgs dragEvent, out string templateDatabasePath)
        {
            if (dragEvent.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFiles = (string[])dragEvent.Data.GetData(DataFormats.FileDrop);
                if (droppedFiles != null && droppedFiles.Length == 1)
                {
                    templateDatabasePath = droppedFiles[0];
                    if (Path.GetExtension(templateDatabasePath) == Constants.File.TemplateDatabaseFileExtension)
                    {
                        return true;
                    }
                }
            }

            templateDatabasePath = null;
            return false;
        }

        public static void OnHelpDocumentPreviewDrag(DragEventArgs dragEvent)
        {
            string templateDatabaseFilePath;
            if (Utilities.IsSingleTemplateFileDrag(dragEvent, out templateDatabaseFilePath))
            {
                dragEvent.Effects = DragDropEffects.All;
            }
            else
            {
                dragEvent.Effects = DragDropEffects.None;
            }
            dragEvent.Handled = true;
        }

        public static void SetDefaultDialogPosition(Window window)
        {
            Debug.Assert(window.Owner != null, "Set dialog's owner property before calling ShowDialog().");
            window.Left = window.Owner.Left + (window.Owner.Width - window.ActualWidth) / 2; // Center it horizontally
            window.Top = window.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
        }

        public static bool TryFitWindowInWorkingArea(Window window)
        {
            if (Double.IsNaN(window.Left))
            {
                window.Left = 0;
            }
            if (Double.IsNaN(window.Top))
            {
                window.Top = 0;
            }

            Rectangle windowPosition = new Rectangle((int)window.Left, (int)window.Top, (int)window.Width, (int)window.Height);
            Rectangle workingArea = Screen.GetWorkingArea(windowPosition);
            bool windowFitsInWorkingArea = true;

            // move window up if it extends below the working area
            if (windowPosition.Bottom > workingArea.Bottom)
            {
                int pixelsToMoveUp = windowPosition.Bottom - workingArea.Bottom;
                if (pixelsToMoveUp > windowPosition.Top)
                {
                    // window is too tall and has to shorten to fit screen
                    window.Top = 0;
                    window.Height = workingArea.Bottom;
                    windowFitsInWorkingArea = false;
                }
                else if (pixelsToMoveUp > 0)
                {
                    // move window up
                    window.Top -= pixelsToMoveUp;
                }
            }

            // move window left if it extends right of the working area
            if (windowPosition.Right > workingArea.Right)
            {
                int pixelsToMoveLeft = windowPosition.Right - workingArea.Right;
                if (pixelsToMoveLeft > windowPosition.Top)
                {
                    // window is too wide and has to narrow to fit screen
                    window.Left = 0;
                    window.Width = workingArea.Width;
                    windowFitsInWorkingArea = false;
                }
                else if (pixelsToMoveLeft > 0)
                {
                    // move window left
                    window.Left -= pixelsToMoveLeft;
                }
            }

            return windowFitsInWorkingArea;
        }

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
