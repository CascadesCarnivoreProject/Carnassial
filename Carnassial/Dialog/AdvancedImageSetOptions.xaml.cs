using Carnassial.Data;
using System;
using System.Windows;

namespace Carnassial.Dialog
{
    public partial class AdvancedImageSetOptions : WindowWithSystemMenu
    {
        private TimeZoneInfo currentImageSetTimeZone;
        private TemplateDatabase database;

        public AdvancedImageSetOptions(TemplateDatabase database, Window owner)
        {
            this.InitializeComponent();
            this.Message.SetVisibility();
            this.Owner = owner;
            this.database = database;

            this.currentImageSetTimeZone = database.ImageSet.GetTimeZoneInfo();
            this.TimeZones.SelectTimeZone(this.currentImageSetTimeZone);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.database.ImageSet.TimeZone = this.TimeZones.TimeZonesByDisplayIdentifier[(string)this.TimeZones.SelectedItem].Id;
            this.database.TrySyncImageSetToDatabase();

            this.DialogResult = true;
        }

        private void ResetTimeZone_Click(object sender, RoutedEventArgs e)
        {
            this.TimeZones.SelectTimeZone(this.currentImageSetTimeZone);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // marking the OK button IsDefault to associate it with dialog completion also gives it initial focus
            // It's more helpful to put focus on the time zone list as this saves having to tab to the list as a first step.
            this.TimeZones.Focus();
        }
    }
}