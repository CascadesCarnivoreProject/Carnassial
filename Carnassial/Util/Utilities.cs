using MetadataExtractor;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MessageBox = Carnassial.Dialog.MessageBox;
using MetadataDirectory = MetadataExtractor.Directory;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using Rectangle = System.Drawing.Rectangle;

namespace Carnassial.Util
{
    /// <summary>
    /// A variety of miscellaneous utility functions.
    /// </summary>
    public class Utilities
    {
        private static string GetDotNetVersion()
        {
            // adapted from https://msdn.microsoft.com/en-us/library/hh925568.aspx.
            int release = 0;
            using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
            {
                if (ndpKey != null)
                {
                    object releaseAsObject = ndpKey.GetValue("Release");
                    if (releaseAsObject != null)
                    {
                        release = (int)releaseAsObject;
                    }
                }
            }

            if (release >= 394802)
            {
                return "4.6.2 or later";
            }
            if (release >= 394254)
            {
                return "4.6.1";
            }
            if (release >= 393295)
            {
                return "4.6";
            }
            if (release >= 379893)
            {
                return "4.5.2";
            }
            if (release >= 378675)
            {
                return "4.5.1";
            }
            if (release >= 378389)
            {
                return "4.5";
            }

            return "4.5 or later not detected";
        }

        public static ParallelOptions GetParallelOptions(int maximumDegreeOfParallelism)
        {
            ParallelOptions parallelOptions = new ParallelOptions();
            parallelOptions.MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, maximumDegreeOfParallelism);
            return parallelOptions;
        }

        public static Dictionary<string, string> LoadMetadata(string filePath)
        {
            Dictionary<string, string> metadata = new Dictionary<string, string>();
            foreach (MetadataDirectory metadataDirectory in ImageMetadataReader.ReadMetadata(filePath))
            {
                foreach (Tag metadataTag in metadataDirectory.Tags)
                {
                    metadata.Add(metadataDirectory.Name + "." + metadataTag.Name, metadataTag.Description);
                }
            }
            return metadata;
        }

        public static bool IsDigits(string value)
        {
            foreach (char character in value)
            {
                if (!Char.IsDigit(character))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool IsSingleTemplateFileDrag(DragEventArgs dragEvent, out string templateDatabasePath)
        {
            if (dragEvent.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFiles = (string[])dragEvent.Data.GetData(DataFormats.FileDrop);
                if (droppedFiles != null && droppedFiles.Length == 1)
                {
                    templateDatabasePath = droppedFiles[0];
                    if (Path.GetExtension(templateDatabasePath) == Constant.File.TemplateDatabaseFileExtension)
                    {
                        return true;
                    }
                }
            }

            templateDatabasePath = null;
            return false;
        }

        public static void OnInstructionsPreviewDrag(DragEventArgs dragEvent)
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
            Debug.Assert(window.Owner != null, "Window's owner property is null.  Is a set of it prior to calling ShowDialog() missing?");
            window.Left = window.Owner.Left + (window.Owner.Width - window.ActualWidth) / 2; // Center it horizontally
            window.Top = window.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
        }

        public static void ShowExceptionReportingDialog(string title, UnhandledExceptionEventArgs e, Window owner)
        {
            MessageBox exitNotification = new MessageBox("Uh-oh.  We're so sorry.", owner);
            exitNotification.Message.StatusImage = MessageBoxImage.Error;
            exitNotification.Message.Title = title;
            exitNotification.Message.Problem = String.Format("This is a bug.  The What section below has been copied.  Please paste it into a new issue at {0} or an email to {1}.  Please also include a clear and specific description of what you were doing at the time.",
                                                             CarnassialConfigurationSettings.GetIssuesBrowserAddress(),
                                                             CarnassialConfigurationSettings.GetDevTeamEmailLink().ToEmailAddress());
            exitNotification.Message.What = String.Format("{0}, {1}, .NET {2}{3}", typeof(CarnassialWindow).Assembly.GetName(), Environment.OSVersion, Utilities.GetDotNetVersion(), Environment.NewLine);
            if (e.ExceptionObject != null)
            {
                exitNotification.Message.What += e.ExceptionObject.ToString();
            }
            exitNotification.Message.Reason = "It's not you, it's us.  If you let us know we'll get it fixed.  If you don't tell us probably we don't know there's a problem and things won't get any better.";
            exitNotification.Message.Result = String.Format("The data file is likely OK.  If it's not you can restore from the {0} folder.", Constant.File.BackupFolder);
            exitNotification.Message.Hint = "\u2022 If you do the same thing this'll probably happen again.  If so, that's helpful to know as well." + Environment.NewLine;
            exitNotification.Message.Hint += "\u2022 If the automatic copy of the What content didn't take click on the What details, hit ctrl+a to select all of it, and ctrl+c to copy.";

            Clipboard.SetText(exitNotification.Message.What);
            exitNotification.ShowDialog();
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
                if (pixelsToMoveLeft > windowPosition.Left)
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
            openFileDialog.AutoUpgradeEnabled = true;
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.Multiselect = false;
            openFileDialog.Title = title;
            if (String.IsNullOrWhiteSpace(defaultFilePath))
            {
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            else
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(defaultFilePath);
                openFileDialog.FileName = Path.GetFileName(defaultFilePath);
            }

            // Set filter for file extension and default file extension 
            openFileDialog.DefaultExt = Constant.File.TemplateDatabaseFileExtension;
            openFileDialog.Filter = filter;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                selectedFilePath = openFileDialog.FileName;
                return true;
            }

            selectedFilePath = null;
            return false;
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
