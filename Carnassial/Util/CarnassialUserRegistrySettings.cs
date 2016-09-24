using Microsoft.Win32;
using System;
using System.Windows;

namespace Carnassial.Util
{
    public class CarnassialUserRegistrySettings : UserRegistrySettings
    {
        public bool AudioFeedback { get; set; }
        public bool ControlsInSeparateWindow { get; set; }
        public Point ControlWindowSize { get; set; }
        public int DarkPixelThreshold { get; set; }
        public double DarkPixelRatioThreshold { get; set; }
        public MostRecentlyUsedList<string> MostRecentImageSets { get; private set; }
        public bool SuppressAmbiguousDatesDialog { get; set; }
        public bool SuppressCsvExportDialog { get; set; }
        public bool SuppressCsvImportPrompt { get; set; }
        public bool SuppressFileCountOnImportDialog { get; set; }
        public bool SuppressFilteredAmbiguousDatesPrompt { get; set; }
        public bool SuppressFilteredCsvExportPrompt { get; set; }
        public bool SuppressFilteredDarkThresholdPrompt { get; set; }
        public bool SuppressFilteredDateTimeFixedCorrectionPrompt { get; set; }
        public bool SuppressFilteredDateTimeLinearCorrectionPrompt { get; set; }
        public bool SuppressFilteredDaylightSavingsCorrectionPrompt { get; set; }
        public bool SuppressFilteredPopulateFieldFromMetadataPrompt { get; set; }
        public bool SuppressFilteredRereadDatesFromFilesPrompt { get; set; }
        public bool SuppressFilteredSetTimeZonePrompt { get; set; }
        public Throttles Throttles { get; private set; }

        public CarnassialUserRegistrySettings() :
            this(Constants.Registry.RootKey)
        {
        }

        internal CarnassialUserRegistrySettings(string registryKey)
            : base(registryKey)
        {
            this.Throttles = new Throttles();

            this.ReadFromRegistry();
        }

        public void ReadFromRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                this.AudioFeedback = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.AudioFeedback, false);
                this.ControlsInSeparateWindow = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.ControlsInSeparateWindow, false);
                double controlWindowWidth = registryKey.ReadDouble(Constants.Registry.CarnassialKey.ControlWindowWidth, 0);
                double controlWindowHeight = registryKey.ReadDouble(Constants.Registry.CarnassialKey.ControlWindowHeight, 0);
                this.ControlWindowSize = new Point(controlWindowWidth, controlWindowHeight);
                this.DarkPixelRatioThreshold = registryKey.ReadDouble(Constants.Registry.CarnassialKey.DarkPixelRatio, Constants.Images.DarkPixelRatioThresholdDefault);
                this.DarkPixelThreshold = registryKey.ReadInteger(Constants.Registry.CarnassialKey.DarkPixelThreshold, Constants.Images.DarkPixelThresholdDefault);
                this.MostRecentImageSets = registryKey.ReadMostRecentlyUsedList(Constants.Registry.CarnassialKey.MostRecentlyUsedImageSets);
                this.SuppressAmbiguousDatesDialog = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressAmbiguousDatesDialog, false);
                this.SuppressCsvExportDialog = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressCsvExportDialog, false);
                this.SuppressCsvImportPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressCsvImportPrompt, false);
                this.SuppressFileCountOnImportDialog = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressFileCountOnImportDialog, false);
                this.SuppressFilteredAmbiguousDatesPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressFilteredAmbiguousDatesPrompt, false);
                this.SuppressFilteredCsvExportPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressFilteredCsvExportPrompt, false);
                this.SuppressFilteredDarkThresholdPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressFilteredDarkThresholdPrompt, false);
                this.SuppressFilteredDateTimeFixedCorrectionPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressFilteredDateTimeFixedCorrectionPrompt, false);
                this.SuppressFilteredDateTimeLinearCorrectionPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressFilteredDateTimeLinearCorrectionPrompt, false);
                this.SuppressFilteredDaylightSavingsCorrectionPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressFilteredDaylightSavingsCorrectionPrompt, false);
                this.SuppressFilteredPopulateFieldFromMetadataPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressFilteredPopulateFieldFromMetadataPrompt, false);
                this.SuppressFilteredRereadDatesFromFilesPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressFilteredRereadDatesFromFilesPrompt, false);
                this.SuppressFilteredSetTimeZonePrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressFilteredSetTimeZonePrompt, false);
                this.Throttles.SetDesiredImageRendersPerSecond(registryKey.ReadDouble(Constants.Registry.CarnassialKey.DesiredImageRendersPerSecond, Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault));
            }
        }

        public void WriteToRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(Constants.Registry.CarnassialKey.AudioFeedback, this.AudioFeedback);
                registryKey.Write(Constants.Registry.CarnassialKey.ControlsInSeparateWindow, this.ControlsInSeparateWindow);
                double controlWindowWidth = Double.IsNaN(this.ControlWindowSize.X) ? 0.0 : this.ControlWindowSize.X;
                double controlWindowHeight = Double.IsNaN(this.ControlWindowSize.Y) ? 0.0 : this.ControlWindowSize.Y;
                registryKey.Write(Constants.Registry.CarnassialKey.ControlWindowWidth, controlWindowWidth);
                registryKey.Write(Constants.Registry.CarnassialKey.ControlWindowHeight, controlWindowHeight);
                registryKey.Write(Constants.Registry.CarnassialKey.DarkPixelRatio, this.DarkPixelRatioThreshold);
                registryKey.Write(Constants.Registry.CarnassialKey.DarkPixelThreshold, this.DarkPixelThreshold);
                registryKey.Write(Constants.Registry.CarnassialKey.DesiredImageRendersPerSecond, this.Throttles.DesiredImageRendersPerSecond);
                registryKey.Write(Constants.Registry.CarnassialKey.MostRecentlyUsedImageSets, this.MostRecentImageSets);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressAmbiguousDatesDialog, this.SuppressAmbiguousDatesDialog);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressCsvExportDialog, this.SuppressCsvExportDialog);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressCsvImportPrompt, this.SuppressCsvImportPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressFileCountOnImportDialog, this.SuppressFileCountOnImportDialog);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressFilteredAmbiguousDatesPrompt, this.SuppressFilteredAmbiguousDatesPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressFilteredCsvExportPrompt, this.SuppressFilteredCsvExportPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressFilteredDarkThresholdPrompt, this.SuppressFilteredDarkThresholdPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressFilteredDateTimeFixedCorrectionPrompt, this.SuppressFilteredDateTimeFixedCorrectionPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressFilteredDateTimeLinearCorrectionPrompt, this.SuppressFilteredDateTimeLinearCorrectionPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressFilteredDaylightSavingsCorrectionPrompt, this.SuppressFilteredDaylightSavingsCorrectionPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressFilteredPopulateFieldFromMetadataPrompt, this.SuppressFilteredPopulateFieldFromMetadataPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressFilteredSetTimeZonePrompt, this.SuppressFilteredSetTimeZonePrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressFilteredRereadDatesFromFilesPrompt, this.SuppressFilteredRereadDatesFromFilesPrompt);
            }
        }
    }
}
