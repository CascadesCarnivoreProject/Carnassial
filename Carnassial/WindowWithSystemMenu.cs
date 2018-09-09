using Carnassial.Util;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Input;
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial
{
    public class WindowWithSystemMenu : Window
    {
        protected void CloseWindow_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.CloseWindow(this);
        }

        protected void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private string GetDotNetVersion()
        {
            // adapted from https://msdn.microsoft.com/en-us/library/hh925568.aspx
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

            if (release >= 461808)
            {
                return "4.7.2 or later";
            }
            if (release >= 461308)
            {
                return "4.7.1";
            }
            if (release >= 460798)
            {
                return "4.7";
            }
            if (release >= 394802)
            {
                return "4.6.2";
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

            return "4.0 or earlier";
        }

        protected void Instructions_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            if (this.IsSingleTemplateFileDrag(dragEvent, out string templateDatabaseFilePath))
            {
                dragEvent.Effects = DragDropEffects.All;
            }
            else
            {
                dragEvent.Effects = DragDropEffects.None;
            }
            dragEvent.Handled = true;
        }

        protected bool IsSingleTemplateFileDrag(DragEventArgs dragEvent, out string templateDatabasePath)
        {
            if (dragEvent.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFiles = (string[])dragEvent.Data.GetData(DataFormats.FileDrop);
                if (droppedFiles != null && droppedFiles.Length == 1)
                {
                    templateDatabasePath = droppedFiles[0];
                    if (templateDatabasePath.EndsWith(Constant.File.TemplateFileExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            templateDatabasePath = null;
            return false;
        }

        protected void MaximizeWindow_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MaximizeWindow(this);
        }

        protected void MinimizeWindow_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        protected void RestoreWindow_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.RestoreWindow(this);
        }

        protected void ShowExceptionReportingDialog(string title, UnhandledExceptionEventArgs e, Window owner)
        {
            MessageBox exitNotification = new MessageBox("Uh-oh.  We're so sorry.", owner);
            exitNotification.Message.StatusImage = MessageBoxImage.Error;
            exitNotification.Message.Title = title;
            exitNotification.Message.Problem = String.Format("This is a bug and we'd appreciate your help in fixing it.  The What section below has been copied to the clipboard.  Please paste it into a new issue at {0} or an email to {1}.  Please also include a clear and specific description of what you were doing at the time.",
                                                             CarnassialConfigurationSettings.GetIssuesBrowserAddress(),
                                                             CarnassialConfigurationSettings.GetDevTeamEmailLink().ToEmailAddress());
            exitNotification.Message.What = String.Format("{0}, {1}, .NET {2}{3}", typeof(CarnassialWindow).Assembly.GetName(), Environment.OSVersion, this.GetDotNetVersion(), Environment.NewLine);
            if ((owner is CarnassialWindow carnassial) && (carnassial.DataHandler != null) && (carnassial.DataHandler.FileDatabase != null))
            {
                exitNotification.Message.What = String.Format("{0}{1}", carnassial.DataHandler.FileDatabase.FilePath, Environment.NewLine);
            }
            if (e.ExceptionObject != null)
            {
                exitNotification.Message.What += e.ExceptionObject.ToString();
            }
            exitNotification.Message.Reason = "It's not you, it's us.  If you let us know we'll get it fixed.  If you don't tell us probably we won't know there's a problem.";
            exitNotification.Message.Result = "The data file is likely OK.  If it's not you can restore it from its backup.  The last few changes (if any) may have to redone, though.";
            exitNotification.Message.Hint = "\u2022 If you do the same thing this'll probably happen again.  If so, that's helpful to know as well." + Environment.NewLine;
            exitNotification.Message.Hint += "\u2022 If the automatic copy of the What content didn't take click on the What details, hit ctrl+a to select all of it, and ctrl+c to copy.";

            Clipboard.SetText(exitNotification.Message.What);
            exitNotification.ShowDialog();
        }

        protected void ShowSystemMenu_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.ShowSystemMenu(this, Mouse.GetPosition(this));
        }
    }
}
