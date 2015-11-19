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
    /// Dialog box to ask the user if the date is in month/day or day/month order
    /// </summary>
    public partial class DlgGetDateOrder : Window
    {
        #region Public methods
        /// <summary>
        /// Dialog to ask the user if the date is in month/day or day/month order
        /// If it returns true, then it is in month/day
        /// If it returns fals, then it is day/month
        /// </summary>
        /// <param name="date1"></param>
        /// <param name="date2"></param>
        public DlgGetDateOrder(string date1, string date2)
        {
            InitializeComponent();
        }
        #endregion

        #region Private methods
        private void btnOkay_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        #endregion
    }
}
