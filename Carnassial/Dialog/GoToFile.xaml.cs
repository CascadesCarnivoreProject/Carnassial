using Carnassial.Util;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Carnassial.Dialog
{
    /// <summary>
    /// This dialog lets the user edit text notes attached to this image set, ideally to keep a log of what is going on, if needed.
    /// The log is persisted.
    /// </summary>
    public partial class GoToFile : Window
    {
        private int selectedFiles;

        public int FileIndex { get; private set; }

        /// <summary>
        /// Raise a dialog that lets the user edit text given to it as a parameter  
        /// If the dialog returns true, the property LogContents will contain the modified text. 
        /// </summary>
        public GoToFile(int currentFileIndex, int selectedFiles, Window owner)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);
            this.Owner = owner;
            this.selectedFiles = selectedFiles;

            this.FileIndex = currentFileIndex;
            this.FileIndexAsText.Text = (currentFileIndex + 1).ToString();
            this.FileIndexAsText.SelectionStart = 0;
            this.FileIndexAsText.SelectionLength = this.FileIndexAsText.Text.Length;
            this.FileIndexAsText.TextChanged += this.FileIndexAsText_TextChanged;
            this.FileNumberRange.Text = String.Format("File number (1 - {0}):", selectedFiles);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void FileIndexAsText_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.Back:
                    case Key.D0:
                    case Key.D1:
                    case Key.D2:
                    case Key.D3:
                    case Key.D4:
                    case Key.D5:
                    case Key.D6:
                    case Key.D7:
                    case Key.D8:
                    case Key.D9:
                    case Key.Delete:
                    case Key.Enter:
                    case Key.Escape:
                    case Key.Left:
                    case Key.NumPad0:
                    case Key.NumPad1:
                    case Key.NumPad2:
                    case Key.NumPad3:
                    case Key.NumPad4:
                    case Key.NumPad5:
                    case Key.NumPad6:
                    case Key.NumPad7:
                    case Key.NumPad8:
                    case Key.NumPad9:
                    case Key.Right:
                    case Key.System:
                    case Key.Tab:
                        // leave event unhandled so key is accepted as input
                        return;
                    default:
                        // block all other keys as they're neither navigation, editing, or digits
                        break;
                }
            }
            e.Handled = true;
        }

        private void FileIndexAsText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (String.IsNullOrEmpty(this.FileIndexAsText.Text))
            {
                this.OkButton.IsEnabled = false;
                return;
            }

            this.FileIndex = Int32.Parse(this.FileIndexAsText.Text) - 1;
            this.OkButton.IsEnabled = (this.FileIndex > Constant.Database.InvalidRow) && (this.FileIndex < this.selectedFiles);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // marking the OK button IsDefault to associate it with dialog completion also gives it initial focus
            // It's more helpful to put focus on the file number.
            this.FileIndexAsText.Focus();
        }
    }
}
