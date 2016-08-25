using Microsoft.Win32;
using System;
using System.Windows;

namespace Timelapse.Util
{
    public class TimelapseUserRegistrySettings : UserRegistrySettings
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
        public Throttles Throttles { get; private set; }

        public TimelapseUserRegistrySettings() :
            this(Constants.Registry.RootKey)
        {
        }

        internal TimelapseUserRegistrySettings(string registryKey)
            : base(registryKey)
        {
            this.Throttles = new Throttles();

            this.ReadFromRegistry();
        }

        public void ReadFromRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                this.AudioFeedback = registryKey.ReadBoolean(Constants.Registry.TimelapseKey.AudioFeedback, false);
                this.ControlsInSeparateWindow = registryKey.ReadBoolean(Constants.Registry.TimelapseKey.ControlsInSeparateWindow, false);
                double controlWindowWidth = registryKey.ReadDouble(Constants.Registry.TimelapseKey.ControlWindowWidth, 0);
                double controlWindowHeight = registryKey.ReadDouble(Constants.Registry.TimelapseKey.ControlWindowHeight, 0);
                this.ControlWindowSize = new Point(controlWindowWidth, controlWindowHeight);
                this.DarkPixelRatioThreshold = registryKey.ReadDouble(Constants.Registry.TimelapseKey.DarkPixelRatio, Constants.Images.DarkPixelRatioThresholdDefault);
                this.DarkPixelThreshold = registryKey.ReadInteger(Constants.Registry.TimelapseKey.DarkPixelThreshold, Constants.Images.DarkPixelThresholdDefault);
                this.MostRecentImageSets = registryKey.ReadMostRecentlyUsedList(Constants.Registry.TimelapseKey.MostRecentlyUsedImageSets);
                this.SuppressAmbiguousDatesDialog = registryKey.ReadBoolean(Constants.Registry.TimelapseKey.SuppressAmbiguousDatesDialog, false);
                this.SuppressCsvExportDialog = registryKey.ReadBoolean(Constants.Registry.TimelapseKey.SuppressCsvExportDialog, false);
                this.SuppressCsvImportPrompt = registryKey.ReadBoolean(Constants.Registry.TimelapseKey.SuppressCsvImportPrompt, false);
                this.SuppressFileCountOnImportDialog = registryKey.ReadBoolean(Constants.Registry.TimelapseKey.SuppressFileCountOnImportDialog, false);
                this.SuppressFilteredAmbiguousDatesPrompt = registryKey.ReadBoolean(Constants.Registry.TimelapseKey.SuppressFilteredAmbiguousDatesPrompt, false);
                this.SuppressFilteredCsvExportPrompt = registryKey.ReadBoolean(Constants.Registry.TimelapseKey.SuppressFilteredCsvExportPrompt, false);
                this.SuppressFilteredDarkThresholdPrompt = registryKey.ReadBoolean(Constants.Registry.TimelapseKey.SuppressFilteredDarkThresholdPrompt, false);
                this.SuppressFilteredDateTimeFixedCorrectionPrompt = registryKey.ReadBoolean(Constants.Registry.TimelapseKey.SuppressFilteredDateTimeFixedCorrectionPrompt, false);
                this.SuppressFilteredDateTimeLinearCorrectionPrompt = registryKey.ReadBoolean(Constants.Registry.TimelapseKey.SuppressFilteredDateTimeLinearCorrectionPrompt, false);
                this.SuppressFilteredDaylightSavingsCorrectionPrompt = registryKey.ReadBoolean(Constants.Registry.TimelapseKey.SuppressFilteredDaylightSavingsCorrectionPrompt, false);
                this.SuppressFilteredPopulateFieldFromMetadataPrompt = registryKey.ReadBoolean(Constants.Registry.TimelapseKey.SuppressFilteredPopulateFieldFromMetadataPrompt, false);
                this.SuppressFilteredRereadDatesFromFilesPrompt = registryKey.ReadBoolean(Constants.Registry.TimelapseKey.SuppressFilteredRereadDatesFromFilesPrompt, false);
                this.Throttles.SetDesiredImageRendersPerSecond(registryKey.ReadDouble(Constants.Registry.TimelapseKey.DesiredImageRendersPerSecond, Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault));
            }
        }

        public void WriteToRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(Constants.Registry.TimelapseKey.AudioFeedback, this.AudioFeedback);
                registryKey.Write(Constants.Registry.TimelapseKey.ControlsInSeparateWindow, this.ControlsInSeparateWindow);
                double controlWindowWidth = Double.IsNaN(this.ControlWindowSize.X) ? 0.0 : this.ControlWindowSize.X;
                double controlWindowHeight = Double.IsNaN(this.ControlWindowSize.Y) ? 0.0 : this.ControlWindowSize.Y;
                registryKey.Write(Constants.Registry.TimelapseKey.ControlWindowWidth, controlWindowWidth);
                registryKey.Write(Constants.Registry.TimelapseKey.ControlWindowHeight, controlWindowHeight);
                registryKey.Write(Constants.Registry.TimelapseKey.DarkPixelRatio, this.DarkPixelRatioThreshold);
                registryKey.Write(Constants.Registry.TimelapseKey.DarkPixelThreshold, this.DarkPixelThreshold);
                registryKey.Write(Constants.Registry.TimelapseKey.DesiredImageRendersPerSecond, this.Throttles.DesiredImageRendersPerSecond);
                registryKey.Write(Constants.Registry.TimelapseKey.MostRecentlyUsedImageSets, this.MostRecentImageSets);
                registryKey.Write(Constants.Registry.TimelapseKey.SuppressAmbiguousDatesDialog, this.SuppressAmbiguousDatesDialog);
                registryKey.Write(Constants.Registry.TimelapseKey.SuppressCsvExportDialog, this.SuppressCsvExportDialog);
                registryKey.Write(Constants.Registry.TimelapseKey.SuppressCsvImportPrompt, this.SuppressCsvImportPrompt);
                registryKey.Write(Constants.Registry.TimelapseKey.SuppressFileCountOnImportDialog, this.SuppressFileCountOnImportDialog);
                registryKey.Write(Constants.Registry.TimelapseKey.SuppressFilteredAmbiguousDatesPrompt, this.SuppressFilteredAmbiguousDatesPrompt);
                registryKey.Write(Constants.Registry.TimelapseKey.SuppressFilteredCsvExportPrompt, this.SuppressFilteredCsvExportPrompt);
                registryKey.Write(Constants.Registry.TimelapseKey.SuppressFilteredDarkThresholdPrompt, this.SuppressFilteredDarkThresholdPrompt);
                registryKey.Write(Constants.Registry.TimelapseKey.SuppressFilteredDateTimeFixedCorrectionPrompt, this.SuppressFilteredDateTimeFixedCorrectionPrompt);
                registryKey.Write(Constants.Registry.TimelapseKey.SuppressFilteredDateTimeLinearCorrectionPrompt, this.SuppressFilteredDateTimeLinearCorrectionPrompt);
                registryKey.Write(Constants.Registry.TimelapseKey.SuppressFilteredDaylightSavingsCorrectionPrompt, this.SuppressFilteredDaylightSavingsCorrectionPrompt);
                registryKey.Write(Constants.Registry.TimelapseKey.SuppressFilteredPopulateFieldFromMetadataPrompt, this.SuppressFilteredPopulateFieldFromMetadataPrompt);
                registryKey.Write(Constants.Registry.TimelapseKey.SuppressFilteredRereadDatesFromFilesPrompt, this.SuppressFilteredRereadDatesFromFilesPrompt);
            }
        }
    }
}
