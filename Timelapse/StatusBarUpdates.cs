using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls.Primitives;

namespace Timelapse
{
    /// <summary>
    ///  The Status Bar convenience class that collects methods to update different parts of the status bar
    /// </summary>
    class StatusBarUpdate
    {
     
        //Display a message in the message portion of the status bar
        static public void Message (StatusBar statusBar, string message)
        {
            StatusBarItem item = (StatusBarItem) statusBar.Items[5];
            item.Content = message;
        }

        //Clear the message portion of the status bar
        static public void ClearMessage(StatusBar statusBar)
        {
            StatusBarUpdate.Message(statusBar, "");
        }

        //Display a vkiew  in the View portion of the status bar
        static public void View(StatusBar statusBar, string view)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[4];
            item.Content = "of " + view;
        }

        // Set the total counts in the total coutns portion of the status bar
        static public void TotalCount(StatusBar statusBar, int totalCount)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[3];
            item.Content = totalCount.ToString();
        }

        // Set the total counts in the total coutns portion of the status bar
        static public void CurrentImageNumber (StatusBar statusBar, int imageNumber)
        {
            StatusBarItem item = (StatusBarItem)statusBar.Items[1];
            item.Content = imageNumber.ToString();
        }
    }
}
