using MetadataExtractor;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
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
        // Tuple.Create().GetHashCode() without the instantiation overhead, see Tuple.cs at https://github.com/dotnet/coreclr/
        public static int CombineHashCodes(object obj1, object obj2)
        {
            int hash = obj1.GetHashCode();
            return (hash << 5) + hash ^ obj2.GetHashCode();
        }

        public static int CombineHashCodes(params object[] objects)
        {
            int hash = objects[0].GetHashCode();
            for (int index = 1; index < objects.Length; ++index)
            {
                hash = (hash << 5) + hash ^ objects[index].GetHashCode();
            }
            return hash;
        }

        public static void ConfigureNavigatorSliderTick(Slider slider)
        {
            if (slider.Maximum <= 50)
            {
                slider.IsSnapToTickEnabled = true;
                slider.TickFrequency = 1.0;
            }
            else
            {
                slider.IsSnapToTickEnabled = false;
                slider.TickFrequency = 0.02 * slider.Maximum;
            }
        }

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

        public static int GetIncrement(bool forward, ModifierKeys modifiers)
        {
            int increment = forward ? 1 : -1;
            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                increment *= 5;
            }
            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                increment *= 10;
            }
            return increment;
        }

        public static ParallelOptions GetParallelOptions(int maximumDegreeOfParallelism)
        {
            ParallelOptions parallelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, maximumDegreeOfParallelism)
            };
            return parallelOptions;
        }

        public static TimeSpan Limit(TimeSpan value, TimeSpan minimum, TimeSpan maximum)
        {
            if (value > maximum)
            {
                return maximum;
            }
            if (value < minimum)
            {
                value = minimum;
            }
            return value;
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
                    if (Path.GetExtension(templateDatabasePath) == Constant.File.TemplateFileExtension)
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
            if (Utilities.IsSingleTemplateFileDrag(dragEvent, out string templateDatabaseFilePath))
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
            window.Left = window.Owner.Left + (window.Owner.Width - window.ActualWidth) / 2;
            window.Top = window.Owner.Top + 20;
        }

        public static void ShowExceptionReportingDialog(string title, UnhandledExceptionEventArgs e, Window owner)
        {
            MessageBox exitNotification = new MessageBox("Uh-oh.  We're so sorry.", owner);
            exitNotification.Message.StatusImage = MessageBoxImage.Error;
            exitNotification.Message.Title = title;
            exitNotification.Message.Problem = String.Format("This is a bug and we'd appreciate your help in fixing it.  The What section below has been copied to the clipboard.  Please paste it into a new issue at {0} or an email to {1}.  Please also include a clear and specific description of what you were doing at the time.",
                                                             CarnassialConfigurationSettings.GetIssuesBrowserAddress(),
                                                             CarnassialConfigurationSettings.GetDevTeamEmailLink().ToEmailAddress());
            exitNotification.Message.What = String.Format("{0}, {1}, .NET {2}{3}", typeof(CarnassialWindow).Assembly.GetName(), Environment.OSVersion, Utilities.GetDotNetVersion(), Environment.NewLine);
            if (e.ExceptionObject != null)
            {
                exitNotification.Message.What += e.ExceptionObject.ToString();
            }
            exitNotification.Message.Reason = "It's not you, it's us.  If you let us know we'll get it fixed.  If you don't tell us probably we don't know there's a problem and things won't get any better.";
            exitNotification.Message.Result = String.Format("The data file is likely OK.  If it's not you can restore from the {0} folder.  The last few changes (if any) may have to redone, though.", Constant.File.BackupFolder);
            exitNotification.Message.Hint = "\u2022 If you do the same thing this'll probably happen again.  If so, that's helpful to know as well." + Environment.NewLine;
            exitNotification.Message.Hint += "\u2022 If the automatic copy of the What content didn't take click on the What details, hit ctrl+a to select all of it, and ctrl+c to copy.";

            Clipboard.SetText(exitNotification.Message.What);
            exitNotification.ShowDialog();
        }

        public static int ToDisplayIndex(int databaseIndex)
        {
            // +1 since database file indices are zero based but display file indices are ones based
            return databaseIndex + 1;
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

        public static bool TryGetFileFromUser(string title, string defaultFilePath, string filter, out string selectedFilePath)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                AutoUpgradeEnabled = true,
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = Constant.File.TemplateFileExtension,
                Filter = filter,
                Multiselect = false,
                Title = title
            };

            if (String.IsNullOrWhiteSpace(defaultFilePath))
            {
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            else
            {
                // it would be ideal to reapply the filter and assign a new default file name when the folder changes
                // Unfortunately this is not supported by CommonOpenFileDialog, the WinForms OpenFileDialog, or the WPF OpenFileDialog.
                openFileDialog.InitialDirectory = Path.GetDirectoryName(defaultFilePath);
                openFileDialog.FileName = Path.GetFileName(defaultFilePath);
            }

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                selectedFilePath = openFileDialog.FileName;
                return true;
            }

            selectedFilePath = null;
            return false;
        }
    }
}
