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
using System.Globalization;
using System.IO;
using System.Diagnostics;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DlgDateCorrection.xaml
    /// This dialog lets the user specify a correct date and time of an image. All other image dates and times are then correctes by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// </summary>
    public partial class DlgDateCorrection : Window
    {
        DateTime original_date;
        DBData dbData;
        public DlgDateCorrection(DBData dbData)
        {

            InitializeComponent();

            this.dbData = dbData;
            bool result = DateTime.TryParse(dbData.RowGetValueFromType(Constants.DATE), out this.original_date);
            if (result)
            {
                this.datePicker.DisplayDate = original_date;
                this.lblOriginalDate.Content = this.original_date.ToLongDateString() + " " + dbData.RowGetValueFromType(Constants.TIME);
            }
            else
            {
                this.lblOriginalDate.Content = dbData.RowGetValueFromType(Constants.DATE) + " " + dbData.RowGetValueFromType(Constants.TIME);
            }
            this.lblImageName.Content = dbData.RowGetValueFromType(Constants.FILE);
            string path = System.IO.Path.Combine (dbData.FolderPath, dbData.RowGetValueFromType(Constants.FILE));
            BitmapFrame bmap;
            try
            {
                bmap = BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.None);
            }
            catch
            {
                 Debug.Print("Catch: Could not load the image");
                 bmap = BitmapFrame.Create(new Uri("pack://application:,,/Resources/corrupted.jpg"));
            }
            this.imgDateImage.Source = bmap;
           
            if (result) this.datePicker.DisplayDate = original_date;

            EventManager.RegisterClassHandler(typeof(DatePicker),
            DatePicker.LoadedEvent,
            new RoutedEventHandler(DatePicker_Loaded));
        }

        // These two routines are needed to change the default text in the date picker.
        void DatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            var dp = sender as DatePicker;
            if (dp == null) return;

            var tb = GetChildOfType<TextBox>(dp);
            if (tb == null) return;

            var wm = tb.Template.FindName("PART_Watermark", tb) as ContentControl;
            if (wm == null) return;

            wm.Content = this.original_date.ToLongDateString (); // "Choose Corrected Date";
        }

        
        private static T GetChildOfType<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);

                var result = (child as T) ?? GetChildOfType<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            DateTime tmp;
            if (DateTime.TryParse(this.datePicker.SelectedDate.ToString(), out tmp))
            {
            }
            MessageBox.Show (original_date.ToLongDateString() + " " + tmp.ToLongDateString());
            //DateTimeHandler.CorrectDate(null, original_date, this.datePicker.SelectedDate);
           // dbData.Log += "System entry: Added a correction value to all dates.\n";
            //this.main.ximageSet.log += "                        Old sample date was: " + (string)this.lblOriginalDate.Content + " and new date is " + this.tbNewDate.Text + "\n";
            //this.DialogResult = true;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }


        private void tbNewDate_TextChanged(object sender, TextChangedEventArgs e)
        {
        //    string format = "dd-MMM-yyyy hh:mm tt";
        //    CultureInfo provider = CultureInfo.InvariantCulture;
        //    string sDate = tbNewDate.Text;
        //    try
        //    {
        //        DateTime.ParseExact(sDate, format, provider);
        //        tblkStatus.Text = "\x221A"; // A checkmark

        //        BrushConverter bc = new BrushConverter();
        //        Brush brush;
        //        brush = (Brush)bc.ConvertFrom("#280EE800");
        //        tbNewDate.BorderBrush = brush;
        //        tblkStatus.Background = brush;

        //        this.OkButton.IsEnabled = true;
        //    } catch
        //    {
        //        if (tblkStatus != null)  // null check in case its not yet created
        //        {
        //            tblkStatus.Text = "x";

        //            BrushConverter bc = new BrushConverter();
        //            Brush brush;
        //            brush = (Brush)bc.ConvertFrom("#46F50000");
        //            tbNewDate.BorderBrush = brush;
        //            tblkStatus.Background = brush;

        //            this.OkButton.IsEnabled = false;
        //        }
        //    }
        }

    }
}
