using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;

namespace Carnassial.Editor.Dialog
{
    /// <summary>
    /// This dialog displays a list of metadata found in a selected image. 
    /// </summary>
    // Note: Lots of commonalities between this dialog and PopulateFieldWithMetadata, but its not clear if it's worth the effort of factoring the two.
    public partial class InspectMetadata : WindowWithSystemMenu
    {
        private string filePath;

        public InspectMetadata(Window owner)
        {
            this.InitializeComponent();
            this.Message.SetVisibility();

            this.Owner = owner;
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            string filter = String.Format(CultureInfo.CurrentCulture, "Images and videos (*{0};*{1};*{2})|*{0};*{1};*{2}", Constant.File.JpgFileExtension, Constant.File.AviFileExtension, Constant.File.Mp4FileExtension);
            if (CommonUserInterface.TryGetFileFromUser("Select a typical file to inspect", Constant.File.CurrentDirectory, filter, out this.filePath))
            {
                this.ImageName.Content = Path.GetFileName(this.filePath);
                if (JpegImage.IsJpeg(this.filePath))
                {
                    this.DataGrid.ItemsSource = JpegImage.LoadMetadata(this.filePath).SelectMany(directory => directory.Tags);
                    this.DataGrid.SortByFirstTwoColumnsAscending();
                }
                else
                {
                    this.DataGrid.ItemsSource = null;
                }
                this.DataGrid.Items.Refresh();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);
        }
    }
}
