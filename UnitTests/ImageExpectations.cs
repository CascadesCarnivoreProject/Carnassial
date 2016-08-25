using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Timelapse.Database;
using Timelapse.Editor;

namespace Timelapse.UnitTests
{
    public class ImageExpectations
    {
        public ImageExpectations()
        {
            this.RelativePath = String.Empty;
            this.UserDefinedColumnsByDataLabel = new Dictionary<string, string>();
        }

        public ImageExpectations(ImageExpectations other)
            : this()
        {
            this.Date = other.Date;
            this.DarkPixelFraction = other.DarkPixelFraction;
            this.FileName = other.FileName;
            this.ID = other.ID;
            this.InitialRootFolderName = other.InitialRootFolderName;
            this.IsColor = other.IsColor;
            this.DeleteFlag = other.DeleteFlag;
            this.Quality = other.Quality;
            this.RelativePath = other.RelativePath;
            this.SkipDateTimeVerification = other.SkipDateTimeVerification;
            this.Time = other.Time;

            foreach (KeyValuePair<string, string> columnExpectation in other.UserDefinedColumnsByDataLabel)
            {
                this.UserDefinedColumnsByDataLabel.Add(columnExpectation.Key, columnExpectation.Value);
            }
        }

        public string Date { get; set; }

        public double DarkPixelFraction { get; set; }

        public string FileName { get; set; }

        public long ID { get; set; }

        public string InitialRootFolderName { get; set; }

        public bool IsColor { get; set; }

        public bool DeleteFlag { get; set; }

        public ImageFilter Quality { get; set; }

        public string RelativePath { get; set; }

        public bool SkipDateTimeVerification { get; set; }

        public string Time { get; set; }

        public Dictionary<string, string> UserDefinedColumnsByDataLabel { get; private set; }

        public ImageRow GetImageProperties(ImageDatabase imageDatabase)
        {
            string imageFilePath;
            if (String.IsNullOrEmpty(this.RelativePath))
            {
                imageFilePath = Path.Combine(imageDatabase.FolderPath, this.FileName);
            }
            else
            {
                imageFilePath = Path.Combine(imageDatabase.FolderPath, this.RelativePath, this.FileName);
            }

            ImageRow imageProperties;
            imageDatabase.GetOrCreateImage(new FileInfo(imageFilePath), out imageProperties);
            // imageProperties.Date is defaulted by constructor
            // imageProperties.FileName is set by constructor
            // imageProperties.ID is set when images are refreshed after being added to the database
            // imageProperties.ImageQuality is defaulted by constructor
            // imageProperties.ImageTaken is set by constructor
            // imageProperties.InitialRootFolderName is set by constructor
            // imageProperties.RelativePath is set by constructor
            // imageProperties.Time is defaulted by constructor
            return imageProperties;
        }

        public void Verify(DataRow image)
        {
            // this.DarkPixelFraction isn't applicable
            Assert.IsTrue(image.GetStringField(Constants.DatabaseColumn.File) == this.FileName, "{0}: Expected FileName '{1}' but found '{2}'.", this.FileName, this.FileName, image[Constants.DatabaseColumn.File]);
            Assert.IsTrue(image.GetID() == this.ID, "{0}: Expected ID '{1}' but found '{2}'.", this.FileName, this.ID, image.GetID());
            // this.IsColor isn't applicable
            Assert.IsTrue(image.GetBooleanField(Constants.DatabaseColumn.DeleteFlag) == this.DeleteFlag, "{0}: Expected DeleteFlag '{1}' but found '{2}'.", this.FileName, this.DeleteFlag, image.GetStringField(Constants.DatabaseColumn.DeleteFlag));
            Assert.IsTrue(image.GetStringField(Constants.DatabaseColumn.Folder) == this.InitialRootFolderName, "{0}: Expected InitialRootFolderName '{1}' but found '{2}'.", this.FileName, this.InitialRootFolderName, image.GetStringField(Constants.DatabaseColumn.Folder));
            Assert.IsTrue(image.GetStringField(Constants.DatabaseColumn.ImageQuality) == this.Quality.ToString(), "{0}: Expected ImageQuality '{1}' but found '{2}'.", this.FileName, this.Quality, image.GetStringField(Constants.DatabaseColumn.ImageQuality));
            Assert.IsTrue(image.GetStringField(Constants.DatabaseColumn.RelativePath) == this.RelativePath, "{0}: Expected RelativePath '{1}' but found '{2}'.", this.FileName, this.RelativePath, image.GetStringField(Constants.DatabaseColumn.RelativePath));
            // this.UserDefinedColumnsByDataLabel isn't current applicable

            // bypass checking of Date and Time properties if requested, for example if the camera didn't generate image taken metadata
            if (this.SkipDateTimeVerification == false)
            {
                Assert.IsTrue(image.GetStringField(Constants.DatabaseColumn.Date) == this.Date, "{0}: Expected Date '{1}' but found '{2}'.", this.FileName, this.Date, image[Constants.DatabaseColumn.Date]);
                Assert.IsTrue(image.GetStringField(Constants.DatabaseColumn.Time) == this.Time, "{0}: Expected Time '{1}' but found '{2}'.", this.FileName, this.Time, image.GetStringField(Constants.DatabaseColumn.Time));
            }

            foreach (KeyValuePair<string, string> userFieldExpectation in this.UserDefinedColumnsByDataLabel)
            {
                Assert.IsTrue(image.GetStringField(userFieldExpectation.Key) == userFieldExpectation.Value, "{0}: Expected {1} to be '{2}' but found '{3}'.", this.FileName, userFieldExpectation.Key, userFieldExpectation.Value, image.GetStringField(userFieldExpectation.Key));
            }
        }

        public void Verify(ImageRow imageProperties)
        {
            // this.DarkPixelFraction isn't applicable
            Assert.IsTrue(imageProperties.FileName == this.FileName, "{0}: Expected FileName '{1}' but found '{2}'.", this.FileName, this.FileName, imageProperties.FileName);
            Assert.IsTrue(imageProperties.ID == this.ID, "{0}: Expected ID '{1}' but found '{2}'.", this.FileName, this.ID, imageProperties.ID);
            Assert.IsTrue(imageProperties.ImageQuality == this.Quality, "{0}: Expected ImageQuality '{1}' but found '{2}'.", this.FileName, this.Quality, imageProperties.ImageQuality);
            Assert.IsTrue(imageProperties.InitialRootFolderName == this.InitialRootFolderName, "{0}: Expected InitialRootFolderName '{1}' but found '{2}'.", this.FileName, this.InitialRootFolderName, imageProperties.InitialRootFolderName);
            // this.IsColor isn't applicable
            Assert.IsTrue(imageProperties.RelativePath == this.RelativePath, "{0}: Expected RelativePath '{1}' but found '{2}'.", this.FileName, this.RelativePath, imageProperties.RelativePath);
            // this.UserDefinedColumnsByDataLabel isn't current applicable

            // bypass checking of Date and Time properties if requested, for example if the camera didn't generate image taken metadata
            if (this.SkipDateTimeVerification == false)
            {
                Assert.IsTrue(imageProperties.Date == this.Date, "{0}: Expected Date '{1}' but found '{2}'.", this.FileName, this.Date, imageProperties.Date);
                Assert.IsTrue(imageProperties.Time == this.Time, "{0}: Expected Time '{1}' but found '{2}'.", this.FileName, this.Time, imageProperties.Time);
            }
        }
    }
}
