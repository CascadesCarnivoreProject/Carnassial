using Carnassial.Database;
using Microsoft.Win32;
using System;
using System.Windows;

namespace Carnassial.Util
{
    public class CarnassialUserRegistrySettings : UserRegistrySettings
    {
        public bool AudioFeedback { get; set; }
        public Rect CarnassialWindowPosition { get; set; }
        public CustomSelectionOperator CustomSelectionTermCombiningOperator { get; set; }
        public int DarkPixelThreshold { get; set; }
        public double DarkPixelRatioThreshold { get; set; }
        public DateTime MostRecentCheckForUpdates { get; set; }
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
            this(Constant.Registry.RootKey)
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
                this.AudioFeedback = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.AudioFeedback, false);
                this.CarnassialWindowPosition = registryKey.ReadRect(Constant.Registry.CarnassialKey.CarnassialWindowPosition, new Rect(0.0, 0.0, 1350.0, 900.0));
                this.CustomSelectionTermCombiningOperator = registryKey.ReadEnum<CustomSelectionOperator>(Constant.Registry.CarnassialKey.CustomSelectionTermCombiningOperator, CustomSelectionOperator.And);
                this.DarkPixelRatioThreshold = registryKey.ReadDouble(Constant.Registry.CarnassialKey.DarkPixelRatio, Constant.Images.DarkPixelRatioThresholdDefault);
                this.DarkPixelThreshold = registryKey.ReadInteger(Constant.Registry.CarnassialKey.DarkPixelThreshold, Constant.Images.DarkPixelThresholdDefault);
                this.MostRecentCheckForUpdates = registryKey.ReadDateTime(Constant.Registry.CarnassialKey.MostRecentCheckForUpdates, DateTime.UtcNow);
                this.MostRecentImageSets = registryKey.ReadMostRecentlyUsedList(Constant.Registry.CarnassialKey.MostRecentlyUsedImageSets);
                this.OrderFilesByDateTime = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.OrderFilesByDateTime, false);
                this.SkipDarkImagesCheck = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SkipDarkImagesCheck, false);
                this.SuppressAmbiguousDatesDialog = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressAmbiguousDatesDialog, false);
                this.SuppressCsvExportDialog = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressCsvExportDialog, false);
                this.SuppressCsvImportPrompt = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressCsvImportPrompt, false);
                this.SuppressFileCountOnImportDialog = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressFileCountOnImportDialog, false);
                this.SuppressSelectedAmbiguousDatesPrompt = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressSelectedAmbiguousDatesPrompt, false);
                this.SuppressSelectedCsvExportPrompt = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressSelectedCsvExportPrompt, false);
                this.SuppressSelectedDarkThresholdPrompt = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressSelectedDarkThresholdPrompt, false);
                this.SuppressSelectedDateTimeFixedCorrectionPrompt = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressSelectedDateTimeFixedCorrectionPrompt, false);
                this.SuppressSelectedDateTimeLinearCorrectionPrompt = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressSelectedDateTimeLinearCorrectionPrompt, false);
                this.SuppressSelectedDaylightSavingsCorrectionPrompt = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressSelectedDaylightSavingsCorrectionPrompt, false);
                this.SuppressSelectedPopulateFieldFromMetadataPrompt = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressSelectedPopulateFieldFromMetadataPrompt, false);
                this.SuppressSelectedRereadDatesFromFilesPrompt = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressSelectedRereadDatesFromFilesPrompt, false);
                this.SuppressSelectedSetTimeZonePrompt = registryKey.ReadBoolean(Constant.Registry.CarnassialKey.SuppressSelectedSetTimeZonePrompt, false);
                this.Throttles.SetDesiredImageRendersPerSecond(registryKey.ReadDouble(Constant.Registry.CarnassialKey.DesiredImageRendersPerSecond, Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault));
            }
        }

        public void WriteToRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(Constant.Registry.CarnassialKey.AudioFeedback, this.AudioFeedback);
                registryKey.Write(Constant.Registry.CarnassialKey.CarnassialWindowPosition, this.CarnassialWindowPosition);
                registryKey.Write(Constant.Registry.CarnassialKey.CustomSelectionTermCombiningOperator, this.CustomSelectionTermCombiningOperator.ToString());
                registryKey.Write(Constant.Registry.CarnassialKey.DarkPixelRatio, this.DarkPixelRatioThreshold);
                registryKey.Write(Constant.Registry.CarnassialKey.DarkPixelThreshold, this.DarkPixelThreshold);
                registryKey.Write(Constant.Registry.CarnassialKey.DesiredImageRendersPerSecond, this.Throttles.DesiredImageRendersPerSecond);
                registryKey.Write(Constant.Registry.CarnassialKey.MostRecentCheckForUpdates, this.MostRecentCheckForUpdates);
                registryKey.Write(Constant.Registry.CarnassialKey.MostRecentlyUsedImageSets, this.MostRecentImageSets);
                registryKey.Write(Constant.Registry.CarnassialKey.OrderFilesByDateTime, this.OrderFilesByDateTime);
                registryKey.Write(Constant.Registry.CarnassialKey.SkipDarkImagesCheck, this.SkipDarkImagesCheck);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressAmbiguousDatesDialog, this.SuppressAmbiguousDatesDialog);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressCsvExportDialog, this.SuppressCsvExportDialog);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressCsvImportPrompt, this.SuppressCsvImportPrompt);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressFileCountOnImportDialog, this.SuppressFileCountOnImportDialog);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressSelectedAmbiguousDatesPrompt, this.SuppressSelectedAmbiguousDatesPrompt);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressSelectedCsvExportPrompt, this.SuppressSelectedCsvExportPrompt);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressSelectedDarkThresholdPrompt, this.SuppressSelectedDarkThresholdPrompt);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressSelectedDateTimeFixedCorrectionPrompt, this.SuppressSelectedDateTimeFixedCorrectionPrompt);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressSelectedDateTimeLinearCorrectionPrompt, this.SuppressSelectedDateTimeLinearCorrectionPrompt);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressSelectedDaylightSavingsCorrectionPrompt, this.SuppressSelectedDaylightSavingsCorrectionPrompt);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressSelectedPopulateFieldFromMetadataPrompt, this.SuppressSelectedPopulateFieldFromMetadataPrompt);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressSelectedSetTimeZonePrompt, this.SuppressSelectedSetTimeZonePrompt);
                registryKey.Write(Constant.Registry.CarnassialKey.SuppressSelectedRereadDatesFromFilesPrompt, this.SuppressSelectedRereadDatesFromFilesPrompt);
            }
        }
    }
}
