using Carnassial.Util;
using System;
using System.Windows.Controls.Primitives;

namespace Carnassial.Control
{
    /// <summary>
    /// Wrapping methods to update different items in the status bar.
    /// </summary>
    internal static class StatusBarExtensions
    {
        // clear the message portion of the status bar
        public static void ClearMessage(this StatusBar statusBar)
        {
            statusBar.SetMessage(String.Empty);
        }

        // set the current file index
        public static void SetCurrentFile(this StatusBar statusBar, int currentFile)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[1];
            item.Content = Utilities.ToDisplayIndex(currentFile).ToString();
        }

        // set the total number of files
        public static void SetFileCount(this StatusBar statusBar, int selectedFileCount)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[3];
            item.Content = selectedFileCount.ToString();
        }

        // display a message in the message portion of the status bar
        public static void SetMessage(this StatusBar statusBar, string message)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[5];
            item.Content = message;
        }

        public static void SetMessage(this StatusBar statusBar, string format, params object[] arguments)
        {
            statusBar.SetMessage(String.Format(format, arguments));
        }

        // display a view in the View portion of the status bar
        public static void SetView(this StatusBar statusBar, string view)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[4];
            item.Content = view;
        }
    }
}
