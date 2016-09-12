﻿using System;
using System.Collections.Generic;
using System.Windows;
using Timelapse.Database;

namespace Timelapse.Dialog
{
    public partial class AdvancedImageSetOptions : Window
    {
        private TimeZoneInfo currentImageSetTimeZone;
        private ImageDatabase database;

        public AdvancedImageSetOptions(ImageDatabase database, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.database = database;

            this.currentImageSetTimeZone = database.ImageSet.GetTimeZone();
            this.TimeZones.SelectedItem = this.currentImageSetTimeZone.DisplayName;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.database.ImageSet.TimeZone = this.TimeZones.TimeZonesByDisplayName[(string)this.TimeZones.SelectedItem].Id;
            this.database.SyncImageSetToDatabase();

            this.DialogResult = true;
        }

        private void ResetTimeZone_Click(object sender, RoutedEventArgs e)
        {
            this.TimeZones.SelectedItem = this.currentImageSetTimeZone;
        }
    }
}