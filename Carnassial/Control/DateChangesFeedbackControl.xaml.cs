﻿using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Control
{
    /// <summary>
    /// This user control contains a grid with four columns, labeled File Name, Status, Original date/time, New date/time, and Difference
    /// It is used to display feedback about any date changes that  may be made or that have been made
    /// The primary way to invoke this is to add the control to the UI, and then invoke
    /// AddFeedbackRow (...), which will add a row to the grid whose contents reflect the contents of the various parameters.
    /// </summary>
    public partial class DateChangesFeedbackControl : UserControl
    {
        private readonly ObservableCollection<FeedbackRowTuple> feedbackRows;

        public bool ShowDifferenceColumn { get; set; }

        public DateChangesFeedbackControl()
        {
            this.InitializeComponent();
            this.feedbackRows = [];
            this.feedbackGrid.ItemsSource = this.feedbackRows;
        }

        // Add a row to the tuple, which in turn will update the grid. 
        // Also ensures the latest added row is in view.
        public void AddFeedbackRow(string fileName, string status, string oldDateTime, string newDateTime, string? difference)
        {
            FeedbackRowTuple row = new(fileName, status, oldDateTime, newDateTime, difference);
            this.feedbackRows.Add((FeedbackRowTuple)row);
        }

        // Label the datagrid feedback columns with the appropriate headers
        private void DatagridFeedback_AutoGeneratedColumns(object sender, EventArgs e)
        {
            this.feedbackGrid.Columns[0].Header = "File Name";
            this.feedbackGrid.Columns[1].Header = "Status";
            this.feedbackGrid.Columns[2].Header = "Original date/time";
            this.feedbackGrid.Columns[3].Header = "New date/time";
            this.feedbackGrid.Columns[4].Header = "Difference";
            if (this.ShowDifferenceColumn == false)
            {
                this.feedbackGrid.Columns[4].Visibility = Visibility.Collapsed;
            }
        }

        // This class defines a row tuple containing 5 elements, which is used as a row in the datagrid.
        private class FeedbackRowTuple
        {
            public string FileName { get; set; }
            public string Status { get; set; }
            public string OldDateTime { get; set; }
            public string NewDateTime { get; set; }
            public string? Difference { get; set; }

            public FeedbackRowTuple(string fileName, string status, string oldDateTime, string newDateTime, string? difference)
            {
                this.FileName = fileName;
                this.Status = status;
                this.OldDateTime = oldDateTime;
                this.NewDateTime = newDateTime;
                this.Difference = difference;
            }
        }
    }
}
