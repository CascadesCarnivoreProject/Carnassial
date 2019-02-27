using Carnassial.Util;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Threading;
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial
{
    public class ApplicationWindow : WindowWithSystemMenu
    {
        internal string GetDotNetVersion()
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

        protected void ShowExceptionReportingDialog(string alternativeTitle, string databasePath, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox exitNotification = MessageBox.FromResource(Constant.ResourceKey.ApplicationWindowException, this, 
                                                                  CarnassialConfigurationSettings.GetIssuesBrowserAddress(),
                                                                  CarnassialConfigurationSettings.GetDevTeamEmailLink().ToEmailAddress(),
                                                                  typeof(CarnassialWindow).Assembly.GetName(), 
                                                                  Environment.OSVersion, 
                                                                  this.GetDotNetVersion(), 
                                                                  databasePath,
                                                                  e.Exception);
            Clipboard.SetText(exitNotification.Message.GetWhat());
            exitNotification.ShowDialog();
        }
    }
}
