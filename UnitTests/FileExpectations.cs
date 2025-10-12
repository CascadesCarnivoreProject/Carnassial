using Carnassial.Data;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Carnassial.UnitTests
{
    public class FileExpectations
    {
        private readonly TimeZoneInfo timeZoneForDateTime;

        public FileClassification Classification { get; set; }
        public double Coloration { get; init; }
        public DateTimeOffset DateTime { get; set; }
        public bool DeleteFlag { get; set; }
        public string? FileName { get; set; }
        public long ID { get; init; }
        public double Luminosity { get; init; }
        public string? RelativePath { get; set; }
        public bool SkipDateTimeVerification { get; init; }
        public bool SkipMarkerByteVerification { get; set; }
        public bool SkipUserControlVerification { get; init; }
        public Dictionary<string, object> UserControlsByDataLabel { get; private init; }

        public FileExpectations(TimeZoneInfo timeZone)
        {
            this.Classification = (FileClassification)(-1);
            this.Coloration = -1.0;
            this.DateTime = DateTimeOffset.MinValue;
            this.DeleteFlag = false;
            this.FileName = null;
            this.ID = Constant.Database.InvalidID;
            this.Luminosity = -1.0;
            this.RelativePath = String.Empty;
            this.SkipDateTimeVerification = false;
            this.SkipMarkerByteVerification = false;
            this.SkipUserControlVerification = false;
            this.UserControlsByDataLabel = new Dictionary<string, object>(StringComparer.Ordinal);
            this.timeZoneForDateTime = timeZone;
        }

        public FileExpectations(FileExpectations other)
            : this(other.timeZoneForDateTime)
        {
            this.Classification = other.Classification;
            this.Coloration = other.Coloration;
            this.DateTime = other.DateTime;
            this.DeleteFlag = other.DeleteFlag;
            this.FileName = other.FileName;
            this.ID = other.ID;
            this.Luminosity = other.Luminosity;
            this.RelativePath = other.RelativePath;
            this.SkipDateTimeVerification = other.SkipDateTimeVerification;
            this.SkipMarkerByteVerification = other.SkipMarkerByteVerification;
            this.SkipUserControlVerification = other.SkipUserControlVerification;

            foreach (KeyValuePair<string, object> columnExpectation in other.UserControlsByDataLabel)
            {
                this.UserControlsByDataLabel.Add(columnExpectation.Key, columnExpectation.Value);
            }
        }

        private DateTimeOffset ConvertDateTimeToTimeZone(TimeZoneInfo imageSetDateTime)
        {
            bool currentlyDaylightSavings = this.timeZoneForDateTime.IsDaylightSavingTime(this.DateTime);
            TimeSpan utcOffsetChange = imageSetDateTime.BaseUtcOffset - this.timeZoneForDateTime.BaseUtcOffset;
            DateTimeOffset expectedDateTime = this.DateTime.ToNewOffset(utcOffsetChange);

            bool newlyDaylightSavings = imageSetDateTime.IsDaylightSavingTime(this.DateTime);
            if (newlyDaylightSavings != currentlyDaylightSavings)
            {
                TimeSpan daylightSavingsAdjustment = TimeSpan.FromHours(-1.0);
                if (newlyDaylightSavings && (currentlyDaylightSavings == false))
                {
                    daylightSavingsAdjustment = daylightSavingsAdjustment.Duration();
                }
                expectedDateTime = expectedDateTime.ToNewOffset(daylightSavingsAdjustment);
            }

            return expectedDateTime;
        }

        public static DateTimeOffset ParseDateTimeOffsetString(string dateTimeAsString)
        {
            return DateTimeOffset.ParseExact(dateTimeAsString, TestConstant.DateTimeWithOffsetFormat, Constant.InvariantCulture);
        }

        public void Verify(ImageRow file, TimeZoneInfo timeZone)
        {
            Assert.IsTrue(file.Classification == this.Classification, $"{this.FileName}: Expected Classification '{this.Classification}' but found '{file.Classification}'.");
            Assert.IsTrue(file.DeleteFlag == this.DeleteFlag, $"{this.FileName}: Expected DeleteFlag '{this.DeleteFlag}' but found '{file.DeleteFlag}'.");
            Assert.IsTrue(file.FileName == this.FileName, $"{this.FileName}: Expected FileName '{this.FileName}' but found '{file.FileName}'.");
            Assert.IsTrue(file.ID == this.ID, $"{this.FileName}: Expected ID '{this.ID}' but found '{file.ID}'.");
            Assert.IsTrue((file.IsVideo == file is VideoRow) && (file.IsVideo == (file.Classification == FileClassification.Video)));
            Assert.IsTrue(file.RelativePath == this.RelativePath, $"{this.FileName}: Expected RelativePath '{this.RelativePath}' but found '{file.RelativePath}'.");
            Assert.IsTrue(file.UtcDateTime.Kind == DateTimeKind.Utc, $"{this.FileName}: Expected UtcDateTime.Kind '{DateTimeKind.Utc}' but found '{file.UtcDateTime.Kind}'.");

            // bypass checking of DateTimeOffset if requested, for example if the camera didn't generate image taken metadata
            if (this.SkipDateTimeVerification == false)
            {
                DateTimeOffset fileDateTime = file.DateTimeOffset;
                DateTimeOffset expectedDateTime = this.ConvertDateTimeToTimeZone(timeZone);
                Assert.IsTrue(fileDateTime.UtcDateTime == expectedDateTime.UtcDateTime, $"{this.FileName}: Expected date time '{DateTimeHandler.ToDatabaseDateTimeString(expectedDateTime)}' but found '{DateTimeHandler.ToDatabaseDateTimeString(fileDateTime.UtcDateTime)}'.");
                Assert.IsTrue(fileDateTime.Offset == expectedDateTime.Offset, $"{this.FileName}: Expected date time offset '{expectedDateTime.Offset}' but found '{fileDateTime.Offset}'.");
                Assert.IsTrue(file.UtcDateTime == expectedDateTime.UtcDateTime, $"{this.FileName}: Expected DateTime '{DateTimeHandler.ToDatabaseDateTimeString(expectedDateTime)}' but found '{DateTimeHandler.ToDatabaseDateTimeString(file.UtcDateTime)}'.");
                Assert.IsTrue(file.UtcOffset == expectedDateTime.Offset, $"{this.FileName}: Expected UtcOffset '{expectedDateTime.Offset}' but found '{file.UtcOffset}'.");
            }

            if (this.SkipUserControlVerification == false)
            {
                int actualUserControlCount = file.UserCounters.Length + file.UserFlags.Length + file.UserMarkerPositions.Length + file.UserNotesAndChoices.Length;
                Assert.IsTrue(actualUserControlCount == this.UserControlsByDataLabel.Count, $"{this.FileName}: Expected '{this.UserControlsByDataLabel.Count}' user control values but found '{actualUserControlCount}'.");
                foreach (KeyValuePair<string, object> userControlExpectation in this.UserControlsByDataLabel)
                {
                    string dataLabel = userControlExpectation.Key;
                    object? actualValue = file[dataLabel];
                    object expectedValue = userControlExpectation.Value;
                    if (expectedValue is bool expectedBool)
                    {
                        Assert.IsTrue((actualValue != null) && ((bool)actualValue == expectedBool), $"{this.FileName}: Expected {userControlExpectation.Key} to be '{expectedBool}' but found '{actualValue}'.");
                    }
                    else if (expectedValue is byte[] expectedBytes)
                    {
                        byte[]? actualBytes = (byte[]?)actualValue;
                        Assert.IsTrue((actualBytes  != null) && (actualBytes.Length == expectedBytes.Length));
                        if (this.SkipMarkerByteVerification == false)
                        {
                            for (int byteIndex = 0; byteIndex < expectedBytes.Length; ++byteIndex)
                            {
                                Assert.IsTrue(actualBytes[byteIndex] == expectedBytes[byteIndex]);
                            }
                        }
                    }
                    else if (expectedValue is int expectedInt)
                    {
                        Assert.IsTrue((actualValue != null) && ((int)actualValue == expectedInt), $"{this.FileName}: Expected {userControlExpectation.Key} to be '{expectedInt}' but found '{actualValue}'.");
                        MarkersForCounter markersForCounter = file.GetMarkersForCounter(dataLabel);
                        this.Verify(dataLabel, markersForCounter);
                    }
                    else
                    {
                        Assert.IsTrue(String.Equals((string?)actualValue, (string)expectedValue, StringComparison.Ordinal), $"{this.FileName}: Expected {userControlExpectation.Key} to be '{expectedValue}' but found '{actualValue}'.");
                    }
                }
            }
        }

        private void Verify(string dataLabel, MarkersForCounter markersForCounter)
        {
            // basic checks
            int expectedCount = (int)this.UserControlsByDataLabel[dataLabel];
            Assert.IsTrue(markersForCounter.Count == expectedCount);

            string? spreadsheetPositions = markersForCounter.MarkerPositionsToSpreadsheetString();
            string[] spreadsheetTokens = spreadsheetPositions == null ? [] : spreadsheetPositions.Split(Constant.Excel.MarkerPositionSeparator);

            string markerColummn = FileTable.GetMarkerPositionColumnName(dataLabel);
            byte[] expectedPositions = (byte[])this.UserControlsByDataLabel[markerColummn];
            MarkersForCounter expectedMarkersForCounter = new(dataLabel, expectedCount);
            expectedMarkersForCounter.MarkerPositionsFromFloatArray(expectedPositions);

            Assert.IsTrue(expectedMarkersForCounter.Markers.Count == spreadsheetTokens.Length);
            Assert.IsTrue(expectedMarkersForCounter.Markers.Count == markersForCounter.Markers.Count);

            // marker positions
            for (int markerIndex = 0; markerIndex < expectedMarkersForCounter.Markers.Count; ++markerIndex)
            {
                Marker expectedMarker = expectedMarkersForCounter.Markers[markerIndex];
                Marker marker = markersForCounter.Markers[markerIndex];

                Assert.IsTrue(String.Equals(marker.DataLabel, dataLabel, StringComparison.Ordinal));
                Assert.IsFalse(marker.Emphasize);
                Assert.IsFalse(marker.Highlight);
                Assert.IsTrue(marker.LabelShownPreviously);
                Vector positionDifference = marker.Position - expectedMarker.Position;
                Assert.IsTrue(positionDifference.Length < 0.5E-6);
                Assert.IsFalse(marker.ShowLabel);
                Assert.IsNull(marker.Tooltip);
            }
        }
    }
}
