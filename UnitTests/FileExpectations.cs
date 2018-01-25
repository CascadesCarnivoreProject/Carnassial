using Carnassial.Data;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;

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
        public FileSelection Quality { get; set; }
        public string RelativePath { get; set; }
        public bool SkipDateTimeVerification { get; set; }
        public Dictionary<string, string> UserDefinedColumnsByDataLabel { get; private set; }

        public FileExpectations(TimeZoneInfo timeZone)
        {
            this.RelativePath = String.Empty;
            this.UserDefinedColumnsByDataLabel = new Dictionary<string, string>();
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

            foreach (KeyValuePair<string, string> columnExpectation in other.UserDefinedColumnsByDataLabel)
            {
                this.UserDefinedColumnsByDataLabel.Add(columnExpectation.Key, columnExpectation.Value);
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
            // this.DarkPixelFraction isn't applicable
            Assert.IsTrue(file.UtcDateTime.Kind == DateTimeKind.Utc);
            Assert.IsTrue(file.DeleteFlag == this.DeleteFlag);
            Assert.IsTrue(file.FileName == this.FileName, "{0}: Expected FileName '{1}' but found '{2}'.", this.FileName, this.FileName, file.FileName);
            Assert.IsTrue(file.ID == this.ID, "{0}: Expected ID '{1}' but found '{2}'.", this.FileName, this.ID, file.ID);
            Assert.IsTrue(file.ImageQuality == this.Quality, "{0}: Expected ImageQuality '{1}' but found '{2}'.", this.FileName, this.Quality, file.ImageQuality);
            // this.IsColor isn't applicable
            Assert.IsTrue(file.RelativePath == this.RelativePath, "{0}: Expected RelativePath '{1}' but found '{2}'.", this.FileName, this.RelativePath, file.RelativePath);
            // this.UserDefinedColumnsByDataLabel isn't current applicable

            // bypass checking of Date and Time properties if requested, for example if the camera didn't generate image taken metadata
            if (this.SkipDateTimeVerification == false)
            {
                DateTimeOffset fileDateTime = file.DateTimeOffset;
                DateTimeOffset expectedDateTime = this.ConvertDateTimeToTimeZone(timeZone);
                Assert.IsTrue(fileDateTime.UtcDateTime == expectedDateTime.UtcDateTime, "{0}: Expected date time '{1}' but found '{2}'.", this.FileName, DateTimeHandler.ToDatabaseDateTimeString(expectedDateTime), DateTimeHandler.ToDatabaseDateTimeString(fileDateTime.UtcDateTime));
                Assert.IsTrue(fileDateTime.Offset == expectedDateTime.Offset, "{0}: Expected date time offset '{1}' but found '{2}'.", this.FileName, expectedDateTime.Offset, fileDateTime.Offset);
                Assert.IsTrue(file.UtcDateTime == expectedDateTime.UtcDateTime, "{0}: Expected DateTime '{1}' but found '{2}'.", this.FileName, DateTimeHandler.ToDatabaseDateTimeString(expectedDateTime), DateTimeHandler.ToDatabaseDateTimeString(file.UtcDateTime));
                Assert.IsTrue(file.UtcOffset == expectedDateTime.Offset, "{0}: Expected UtcOffset '{1}' but found '{2}'.", this.FileName, expectedDateTime.Offset, file.UtcOffset);
            }
        }
    }
}
