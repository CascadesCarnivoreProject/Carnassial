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
    /// Dialog to show the user some statistics about the images
    /// </summary>
    public partial class DlgStatisticsOfImageCounts : Window
    {

        /// <summary>
        ///  Show the user some statistics about the images in a dialog box
        /// </summary>
        /// <param name="total"></param>
        /// <param name="dark"></param>
        /// <param name="corrupted"></param>
        /// <param name="allValid"></param>
        public DlgStatisticsOfImageCounts(int ok, int dark, int corrupted, int missing)
        {
            InitializeComponent();

            // Fill in the counts
            this.Ok.Text = String.Format("{0,5}", ok) + this.Ok.Text;
            this.Missing.Text = String.Format("{0,5}", missing) + this.Missing.Text;
            this.Dark.Text = String.Format("{0,5}", dark) + this.Dark.Text;
            this.Corrupted.Text = String.Format("{0,5}", corrupted) + this.Corrupted.Text;

            int total = ok + dark + corrupted + missing;
            this.Total.Text = String.Format("{0,5}", total) + this.Total.Text;   
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}

