using Carnassial.Database;
using Microsoft.Win32;
using System;
using System.Windows;

namespace Carnassial.Util
{
    public class CarnassialUserRegistrySettings : UserRegistrySettings
    {
        public bool AudioFeedback { get; set; }
        public Point CarnassialWindowLocation { get; set; }
        public Size CarnassialWindowSize { get; set; }
        public CustomSelectionOperator CustomSelectionTermCombiningOperator { get; set; }
        public int DarkPixelThreshold { get; set; }
        public double DarkPixelRatioThreshold { get; set; }
        public MostRecentlyUsedList<string> MostRecentImageSets { get; private set; }
        public bool OrderFilesByDateTime { get; set; }
        public bool SkipDarkImagesCheck { get; set; }
        public bool SuppressAmbiguousDatesDialog { get; set; }
        public bool SuppressCsvExportDialog { get; set; }
        public bool SuppressCsvImportPrompt { get; set; }
        public bool SuppressFileCountOnImportDialog { get; set; }
        public bool SuppressSelectedAmbiguousDatesPrompt { get; set; }
        public bool SuppressSelectedCsvExportPrompt { get; set; }
        public bool SuppressSelectedDarkThresholdPrompt { get; set; }
        public bool SuppressSelectedDateTimeFixedCorrectionPrompt { get; set; }
        public bool SuppressSelectedDateTimeLinearCorrectionPrompt { get; set; }
        public bool SuppressSelectedDaylightSavingsCorrectionPrompt { get; set; }
        public bool SuppressSelectedPopulateFieldFromMetadataPrompt { get; set; }
        public bool SuppressSelectedRereadDatesFromFilesPrompt { get; set; }
        public bool SuppressSelectedSetTimeZonePrompt { get; set; }
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
                this.CarnassialWindowLocation = registryKey.ReadPoint(Constants.Registry.CarnassialKey.CarnassialWindowLocation, new Point(0, 0));
                this.CarnassialWindowSize = registryKey.ReadSize(Constants.Registry.CarnassialKey.CarnassialWindowSize, new Size(1350, 900));
                this.CustomSelectionTermCombiningOperator = registryKey.ReadEnum<CustomSelectionOperator>(Constants.Registry.CarnassialKey.CustomSelectionTermCombiningOperator, CustomSelectionOperator.And);
                this.DarkPixelRatioThreshold = registryKey.ReadDouble(Constants.Registry.CarnassialKey.DarkPixelRatio, Constants.Images.DarkPixelRatioThresholdDefault);
                this.DarkPixelThreshold = registryKey.ReadInteger(Constants.Registry.CarnassialKey.DarkPixelThreshold, Constants.Images.DarkPixelThresholdDefault);
                this.MostRecentImageSets = registryKey.ReadMostRecentlyUsedList(Constants.Registry.CarnassialKey.MostRecentlyUsedImageSets);
                this.OrderFilesByDateTime = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.OrderFilesByDateTime, false);
                this.SkipDarkImagesCheck = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SkipDarkImagesCheck, false);
                this.SuppressAmbiguousDatesDialog = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressAmbiguousDatesDialog, false);
                this.SuppressCsvExportDialog = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressCsvExportDialog, false);
                this.SuppressCsvImportPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressCsvImportPrompt, false);
                this.SuppressFileCountOnImportDialog = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressFileCountOnImportDialog, false);
                this.SuppressSelectedAmbiguousDatesPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressSelectedAmbiguousDatesPrompt, false);
                this.SuppressSelectedCsvExportPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressSelectedCsvExportPrompt, false);
                this.SuppressSelectedDarkThresholdPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressSelectedDarkThresholdPrompt, false);
                this.SuppressSelectedDateTimeFixedCorrectionPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressSelectedDateTimeFixedCorrectionPrompt, false);
                this.SuppressSelectedDateTimeLinearCorrectionPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressSelectedDateTimeLinearCorrectionPrompt, false);
                this.SuppressSelectedDaylightSavingsCorrectionPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressSelectedDaylightSavingsCorrectionPrompt, false);
                this.SuppressSelectedPopulateFieldFromMetadataPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressSelectedPopulateFieldFromMetadataPrompt, false);
                this.SuppressSelectedRereadDatesFromFilesPrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressSelectedRereadDatesFromFilesPrompt, false);
                this.SuppressSelectedSetTimeZonePrompt = registryKey.ReadBoolean(Constants.Registry.CarnassialKey.SuppressSelectedSetTimeZonePrompt, false);
                this.Throttles.SetDesiredImageRendersPerSecond(registryKey.ReadDouble(Constants.Registry.CarnassialKey.DesiredImageRendersPerSecond, Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault));
            }
        }

        public void WriteToRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(Constants.Registry.CarnassialKey.AudioFeedback, this.AudioFeedback);
                registryKey.Write(Constants.Registry.CarnassialKey.CarnassialWindowLocation, this.CarnassialWindowLocation);
                registryKey.Write(Constants.Registry.CarnassialKey.CarnassialWindowSize, this.CarnassialWindowSize);
                registryKey.Write(Constants.Registry.CarnassialKey.CustomSelectionTermCombiningOperator, this.CustomSelectionTermCombiningOperator.ToString());
                registryKey.Write(Constants.Registry.CarnassialKey.DarkPixelRatio, this.DarkPixelRatioThreshold);
                registryKey.Write(Constants.Registry.CarnassialKey.DarkPixelThreshold, this.DarkPixelThreshold);
                registryKey.Write(Constants.Registry.CarnassialKey.DesiredImageRendersPerSecond, this.Throttles.DesiredImageRendersPerSecond);
                registryKey.Write(Constants.Registry.CarnassialKey.MostRecentlyUsedImageSets, this.MostRecentImageSets);
                registryKey.Write(Constants.Registry.CarnassialKey.OrderFilesByDateTime, this.OrderFilesByDateTime);
                registryKey.Write(Constants.Registry.CarnassialKey.SkipDarkImagesCheck, this.SkipDarkImagesCheck);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressAmbiguousDatesDialog, this.SuppressAmbiguousDatesDialog);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressCsvExportDialog, this.SuppressCsvExportDialog);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressCsvImportPrompt, this.SuppressCsvImportPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressFileCountOnImportDialog, this.SuppressFileCountOnImportDialog);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressSelectedAmbiguousDatesPrompt, this.SuppressSelectedAmbiguousDatesPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressSelectedCsvExportPrompt, this.SuppressSelectedCsvExportPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressSelectedDarkThresholdPrompt, this.SuppressSelectedDarkThresholdPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressSelectedDateTimeFixedCorrectionPrompt, this.SuppressSelectedDateTimeFixedCorrectionPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressSelectedDateTimeLinearCorrectionPrompt, this.SuppressSelectedDateTimeLinearCorrectionPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressSelectedDaylightSavingsCorrectionPrompt, this.SuppressSelectedDaylightSavingsCorrectionPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressSelectedPopulateFieldFromMetadataPrompt, this.SuppressSelectedPopulateFieldFromMetadataPrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressSelectedSetTimeZonePrompt, this.SuppressSelectedSetTimeZonePrompt);
                registryKey.Write(Constants.Registry.CarnassialKey.SuppressSelectedRereadDatesFromFilesPrompt, this.SuppressSelectedRereadDatesFromFilesPrompt);
            }
        }
    }
}
