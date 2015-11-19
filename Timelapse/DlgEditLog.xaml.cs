using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Timelapse
{
    /// <summary>
    /// This dialog lets the user edit text notes attached to this image set, 
    /// ideally to keep a log of what is going on, if needed. The log is saved 
    /// </summary>
    public partial class DlgEditLog : Window
    {
        #region Public  Properties and Methods

        /// <summary>
        /// Contains the modified text that can be accessed immediately after the dialog exits
        /// </summary>
        public string LogContents { get; set; } 
 

        /// <summary>
        /// Raise a dialog that lets the user edit text given to it as a parameter  
        /// If the dialog returns true, the property LogContents will contain the modified text. 
        /// </summary>
        /// <param name="text"></param>
        public DlgEditLog(string text)
        {
            InitializeComponent();
            this.LogContents = text;
            this.tbLog.Text = this.LogContents;
            this.OkButton.IsEnabled = false;
        }
        #endregion

        #region Private Methods
        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            this.LogContents = tbLog.Text;
            this.DialogResult = true;
        }

        private void tbLog_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.OkButton.IsEnabled = true;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
