using System.Windows;
using System.Windows.Controls;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog lets the user edit text notes attached to this image set, ideally to keep a log of what is going on, if needed.
    /// The log is persisted.
    /// </summary>
    public partial class EditLog : Window
    {
        /// <summary>
        /// Gets or sets the modified text that can be accessed immediately after the dialog exits.
        /// </summary>
        public string LogContents { get; set; }

        /// <summary>
        /// Raise a dialog that lets the user edit text given to it as a parameter  
        /// If the dialog returns true, the property LogContents will contain the modified text. 
        /// </summary>
        public EditLog(string text)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);
            this.LogContents = text;
            this.tbLog.Text = this.LogContents;
            this.OkButton.IsEnabled = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.LogContents = this.tbLog.Text;
            this.DialogResult = true;
        }

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.OkButton.IsEnabled = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
