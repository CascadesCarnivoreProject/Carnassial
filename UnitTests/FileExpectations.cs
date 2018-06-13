using Carnassial.Data;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace Carnassial.UnitTests
{
    public class FileExpectations
    {
        private TimeZoneInfo timeZoneForDateTime;

        public double Coloration { get; set; }
        public DateTimeOffset DateTime { get; set; }
        public bool DeleteFlag { get; set; }
        public string FileName { get; set; }
        public long ID { get; set; }
        public double Luminosity { get; set; }
        public FileClassification Quality { get; set; }
        public string RelativePath { get; set; }
        public bool SkipDateTimeVerification { get; set; }
        public bool SkipUserControlVerification { get; set; }
        public Dictionary<string, string> UserControlsByDataLabel { get; private set; }

        public FileExpectations(TimeZoneInfo timeZone)
        {
            this.Coloration = -1.0;
            this.DateTime = DateTimeOffset.MinValue;
            this.DeleteFlag = false;
            this.FileName = null;
            this.ID = Constant.Database.InvalidID;
            this.Luminosity = -1.0;
            this.Quality = (FileClassification)(-1);
            this.RelativePath = String.Empty;
            this.SkipDateTimeVerification = false;
            this.SkipUserControlVerification = false;
            this.UserControlsByDataLabel = new Dictionary<string, string>();
            this.timeZoneForDateTime = timeZone;
        }

        public FileExpectations(FileExpectations other)
            : this(other.timeZoneForDateTime)
        {
            this.Coloration = other.Coloration;
            this.DateTime = other.DateTime;
            this.DeleteFlag = other.DeleteFlag;
            this.FileName = other.FileName;
            this.ID = other.ID;
            this.Luminosity = other.Luminosity;
            this.Quality = other.Quality;
            this.RelativePath = other.RelativePath;
            this.SkipDateTimeVerification = other.SkipDateTimeVerification;

            foreach (KeyValuePair<string, string> columnExpectation in other.UserControlsByDataLabel)
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
            return DateTimeOffset.ParseExact(dateTimeAsString, TestConstant.DateTimeWithOffsetFormat, CultureInfo.InvariantCulture);
        }

        public void Verify(ImageRow file, TimeZoneInfo timeZone)
        {
            Assert.IsTrue(file.DeleteFlag == this.DeleteFlag);
            Assert.IsTrue(file.FileName == this.FileName, "{0}: Expected FileName '{1}' but found '{2}'.", this.FileName, this.FileName, file.FileName);
            // Assert.IsFalse(file.HasChanges);
            Assert.IsTrue(file.ID == this.ID, "{0}: Expected ID '{1}' but found '{2}'.", this.FileName, this.ID, file.ID);
            Assert.IsTrue(file.Classification == this.Quality, "{0}: Expected ImageQuality '{1}' but found '{2}'.", this.FileName, this.Quality, file.Classification);
            Assert.IsTrue((file.IsVideo == file is VideoRow) && (file.IsVideo == (file.Classification == FileClassification.Video)));
            Assert.IsTrue(file.RelativePath == this.RelativePath, "{0}: Expected RelativePath '{1}' but found '{2}'.", this.FileName, this.RelativePath, file.RelativePath);
            Assert.IsTrue(file.UtcDateTime.Kind == DateTimeKind.Utc, "{0}: Expected UtcDateTime.Kind '{1}' but found '{2}'.", this.FileName, DateTimeKind.Utc, file.UtcDateTime.Kind);

            // bypass checking of DateTimeOffset if requested, for example if the camera didn't generate image taken metadata
            if (this.SkipDateTimeVerification == false)
            {
                DateTimeOffset fileDateTime = file.DateTimeOffset;
                DateTimeOffset expectedDateTime = this.ConvertDateTimeToTimeZone(timeZone);
                Assert.IsTrue(fileDateTime.UtcDateTime == expectedDateTime.UtcDateTime, "{0}: Expected date time '{1}' but found '{2}'.", this.FileName, DateTimeHandler.ToDatabaseDateTimeString(expectedDateTime), DateTimeHandler.ToDatabaseDateTimeString(fileDateTime.UtcDateTime));
                Assert.IsTrue(fileDateTime.Offset == expectedDateTime.Offset, "{0}: Expected date time offset '{1}' but found '{2}'.", this.FileName, expectedDateTime.Offset, fileDateTime.Offset);
                Assert.IsTrue(file.UtcDateTime == expectedDateTime.UtcDateTime, "{0}: Expected DateTime '{1}' but found '{2}'.", this.FileName, DateTimeHandler.ToDatabaseDateTimeString(expectedDateTime), DateTimeHandler.ToDatabaseDateTimeString(file.UtcDateTime));
                Assert.IsTrue(file.UtcOffset == expectedDateTime.Offset, "{0}: Expected UtcOffset '{1}' but found '{2}'.", this.FileName, expectedDateTime.Offset, file.UtcOffset);
            }

            if (this.SkipUserControlVerification == false)
            {
                Assert.IsTrue(file.UserControlValues.Length == this.UserControlsByDataLabel.Count, "{0}: Expected '{1}' user control values but found '{2}'.", this.FileName, this.UserControlsByDataLabel.Count, file.UserControlValues.Length);
                foreach (KeyValuePair<string, string> userControlExpectation in this.UserControlsByDataLabel)
                {
                    string dataLabel = userControlExpectation.Key;
                    string actualDatabaseString = (string)file[dataLabel];
                    string expectedDatabaseString = userControlExpectation.Value;
                    Assert.IsTrue(String.Equals(actualDatabaseString, expectedDatabaseString, StringComparison.Ordinal), "{0}: Expected {1} to be '{2}' but found '{3}'.", this.ID, userControlExpectation.Key, userControlExpectation.Value, actualDatabaseString);

                    if (dataLabel.Contains("Counter"))
                    {
                        MarkersForCounter markersForCounter = file.GetMarkersForCounter(dataLabel);
                        this.Verify(dataLabel, markersForCounter);
                    }
                }
            }
        }

        private void Verify(string dataLabel, MarkersForCounter markersForCounter)
        {
            // basic checks
            string[] actualTokens = markersForCounter.ToDatabaseString().Split(Constant.Database.BarDelimiter);
            Assert.IsTrue(actualTokens.Length > 0);
            string[] expectedTokens = this.UserControlsByDataLabel[dataLabel].Split(Constant.Database.BarDelimiter);
            Assert.IsTrue(expectedTokens.Length == actualTokens.Length);
            Assert.IsTrue(String.Equals(actualTokens[0], expectedTokens[0], StringComparison.Ordinal));
            Assert.IsTrue(markersForCounter.Count == Int32.Parse(expectedTokens[0]));

            // marker positions
            for (int token = 1; token < expectedTokens.Length; ++token)
            {
                Assert.IsTrue(String.Equals(actualTokens[token], expectedTokens[token], StringComparison.Ordinal));
                Marker marker = markersForCounter.Markers[token - 1];
                Assert.IsTrue(String.Equals(marker.DataLabel, dataLabel, StringComparison.Ordinal));
                Assert.IsFalse(marker.Emphasize);
                Assert.IsFalse(marker.Highlight);
                Assert.IsTrue(marker.LabelShownPreviously);
                Point expectedPosition = Point.Parse(expectedTokens[token]);
                Assert.IsTrue(marker.Position == expectedPosition);
                Assert.IsFalse(marker.ShowLabel);
                Assert.IsNull(marker.Tooltip);
            }
        }
    }
}
