using Carnassial.Data;
using Carnassial.Images;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MetadataDirectory = MetadataExtractor.Directory;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class FileTests : CarnassialTest
    {
        [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
        public static void ClassCleanup()
        {
            CarnassialTest.TryRevertToDefaultCultures();
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            CarnassialTest.TryChangeToTestCulture();
        }

        [TestMethod]
        public async Task Cache()
        {
            using FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewFileDatabaseFileName);
            List<FileExpectations> fileExpectations = this.PopulateDefaultDatabase(fileDatabase);

            using ImageCache cache = new(fileDatabase);
            Assert.IsNull(cache.Current);
            Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
            Assert.IsTrue(cache.CurrentRow == -1);

            Assert.IsTrue(cache.MoveNext());
            Assert.IsTrue(cache.MoveNext());
            Assert.IsTrue(cache.MovePrevious());
            Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
            Assert.IsTrue(cache.CurrentRow == 0);
            FileTests.VerifyCurrentImage(cache);

            MoveToFileResult moveToFile = await cache.TryMoveToFileAsync(0, 0).ConfigureAwait(false);
            Assert.IsTrue(moveToFile.Succeeded);
            Assert.IsFalse(moveToFile.NewFileToDisplay);
            moveToFile = await cache.TryMoveToFileAsync(0, 1).ConfigureAwait(false);
            Assert.IsTrue(moveToFile.Succeeded);
            Assert.IsFalse(moveToFile.NewFileToDisplay);
            moveToFile = await cache.TryMoveToFileAsync(1, 0).ConfigureAwait(false);
            Assert.IsTrue(moveToFile.Succeeded);
            Assert.IsTrue(moveToFile.NewFileToDisplay);
            moveToFile = await cache.TryMoveToFileAsync(1, -1).ConfigureAwait(false);
            Assert.IsTrue(moveToFile.Succeeded);
            Assert.IsFalse(moveToFile.NewFileToDisplay);
            FileTests.VerifyCurrentImage(cache);

            Assert.IsTrue(cache.TryInvalidate(1));
            moveToFile = await cache.TryMoveToFileAsync(0, 0).ConfigureAwait(false);
            Assert.IsTrue(moveToFile.Succeeded);
            Assert.IsTrue(moveToFile.NewFileToDisplay);
            moveToFile = await cache.TryMoveToFileAsync(1, 0).ConfigureAwait(false);
            Assert.IsTrue(moveToFile.Succeeded);
            Assert.IsTrue(moveToFile.NewFileToDisplay);
            FileTests.VerifyCurrentImage(cache);

            Assert.IsTrue(cache.TryInvalidate(2));
            moveToFile = await cache.TryMoveToFileAsync(1, 1).ConfigureAwait(false);
            Assert.IsTrue(moveToFile.Succeeded);
            Assert.IsTrue(moveToFile.NewFileToDisplay);
            moveToFile = await cache.TryMoveToFileAsync(1, 0).ConfigureAwait(false);
            Assert.IsTrue(moveToFile.Succeeded);
            Assert.IsFalse(moveToFile.NewFileToDisplay);
            moveToFile = await cache.TryMoveToFileAsync(0, -1).ConfigureAwait(false);
            Assert.IsTrue(moveToFile.Succeeded);
            Assert.IsTrue(moveToFile.NewFileToDisplay);
            FileTests.VerifyCurrentImage(cache);

            moveToFile = await cache.TryMoveToFileAsync(fileExpectations.Count, 0).ConfigureAwait(false);
            Assert.IsFalse(moveToFile.Succeeded);
            moveToFile = await cache.TryMoveToFileAsync(fileExpectations.Count, 0).ConfigureAwait(false);
            Assert.IsFalse(moveToFile.Succeeded);

            moveToFile = await cache.TryMoveToFileAsync(0, 0).ConfigureAwait(false);
            Assert.IsTrue(moveToFile.Succeeded);
            moveToFile = await cache.TryMoveToFileAsync(1, 0).ConfigureAwait(false);
            Assert.IsTrue(moveToFile.Succeeded);
            moveToFile = await cache.TryMoveToFileAsync(fileExpectations.Count, 0).ConfigureAwait(false);
            Assert.IsFalse(moveToFile.Succeeded);

            // combined differences
            cache.Reset();
            for (int file = 0; file < fileDatabase.Files.RowCount; ++file)
            {
                moveToFile = await cache.TryMoveToFileAsync(file, 1).ConfigureAwait(false);
                Assert.IsTrue(moveToFile.Succeeded);

                for (int step = 0; step < 4; ++step)
                {
                    ImageDifferenceResult combinedDifferenceResult = await cache.TryMoveToNextCombinedDifferenceImageAsync(Constant.Images.DifferenceThresholdDefault - 2).ConfigureAwait(false);
                    Assert.IsTrue((cache.CurrentDifferenceState == ImageDifference.Combined) ||
                                  (cache.CurrentDifferenceState == ImageDifference.Unaltered));
                    await FileTests.CheckDifferenceResult(combinedDifferenceResult, cache, fileDatabase).ConfigureAwait(false);

                    CachedImage? differenceImage = cache.GetCurrentImage();
                    if (combinedDifferenceResult == ImageDifferenceResult.Success)
                    {
                        Assert.IsTrue((differenceImage != null) && (differenceImage.Image != null));
                    }
                }
            }

            // next and previous differences
            cache.Reset();
            for (int file = fileDatabase.Files.RowCount - 1; file >= 0; --file)
            {
                moveToFile = await cache.TryMoveToFileAsync(file, -1).ConfigureAwait(false);
                Assert.IsTrue(moveToFile.Succeeded);

                for (int step = 0; step < 7; ++step)
                {
                    ImageDifferenceResult differenceResult = await cache.TryMoveToNextDifferenceImageAsync(Constant.Images.DifferenceThresholdDefault + 2).ConfigureAwait(false);
                    Assert.IsTrue((cache.CurrentDifferenceState == ImageDifference.Next) ||
                                  (cache.CurrentDifferenceState == ImageDifference.Previous) ||
                                  (cache.CurrentDifferenceState == ImageDifference.Unaltered));

                    await FileTests.CheckDifferenceResult(differenceResult, cache, fileDatabase).ConfigureAwait(false);

                    CachedImage? differenceImage = cache.GetCurrentImage();
                    if (differenceResult == ImageDifferenceResult.Success)
                    {
                        Assert.IsNotNull((differenceImage != null) && (differenceImage.Image != null));
                    }
                }
            }

            cache.Reset();
            Assert.IsNull(cache.Current);
            Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
            Assert.IsTrue(cache.CurrentRow == Constant.Database.InvalidRow);
        }

        [TestMethod]
        public async Task Classification()
        {
            string luminosityColorationFolder = "LuminosityColoration";
            List< FileExpectations> fileExpectations =
            [
                new(TestConstant.FileExpectation.PacificTime)
                {
                    FileName = "luminosity black.jpg",
                    RelativePath = luminosityColorationFolder,
                    Classification = FileClassification.Greyscale,
                    Coloration = 0.0,
                    Luminosity = 0.00392156862745098 // decompresses with RGB = { 37 * 1, 74 * 1, 14 * 1 } / (125 * 255) for all pixels rather than { 0, 0, 0 }; 0.00392156862745098 = 1/(125 * 255)
                },
                new(TestConstant.FileExpectation.PacificTime)
                {
                    FileName = "luminosity grey 50.jpg",
                    RelativePath = luminosityColorationFolder,
                    Classification = FileClassification.Greyscale,
                    Coloration = 0.0,
                    Luminosity = 0.50196078431372548 // RGB = { 37 * 128, 74 * 128, 14 * 128 } / (125 * 255)
                },
                new(TestConstant.FileExpectation.PacificTime)
                {
                    FileName = "luminosity white.jpg",
                    RelativePath = luminosityColorationFolder,
                    Classification = FileClassification.Greyscale,
                    Coloration = 0.0,
                    Luminosity = 1.0 // RGB = { 37 * 255, 74 * 255, 14 * 255 } / (125 * 255)
                },
                new(TestConstant.FileExpectation.PacificTime)
                {
                    FileName = "coloration red.jpg", // RGB = { 255, 0, 0 } saved at 70% quality
                    RelativePath = luminosityColorationFolder,
                    Classification = FileClassification.Color,
                    Coloration = 0.996078431372549, // (abs(2 - 0) + abs(0 - 254) + abs(254 - 2)) / (2 * 255) = 0.9960784
                    Luminosity = 0.29571764705882353 // RGB = { 37 * 254, 0, 14 * 2 } / (125 * 255) = 0.2957176
                },
                new(TestConstant.FileExpectation.PacificTime)
                {
                    FileName = "coloration green.jpg", // RGB = { 0, 255, 0 } saved at 70% quality
                    RelativePath = luminosityColorationFolder,
                    Classification = FileClassification.Color,
                    Coloration = 0.996078431372549, // (abs(2 - 255) + abs(255 - 1) + abs(1 - 2)) / (2 * 255) = 0.9960784
                    Luminosity = 0.59403921568627449 // RGB = { 37 * 2, 74 * 255, 14 * 1 } / (125 * 255) = 0.5947608
                },
                new(TestConstant.FileExpectation.PacificTime)
                {
                    FileName = "coloration blue.jpg", // RGB = { 0, 0, 255 } saved at 70% quality
                    RelativePath = luminosityColorationFolder,
                    Classification = FileClassification.Color,
                    Coloration = 0.996078431372549, // (abs(254 - 0) + abs(0 - 0) + abs(0 - 254)) / (2 * 255) = 0.9960784
                    Luminosity = 0.11156078431372549 // RGB = { 0, 0, 14 * 254 } / (125 * 255) = 0.1115608
                },
                TestConstant.FileExpectation.DaylightBobcat, // ok to use expectations from TestConstants without cloning since held const
                TestConstant.FileExpectation.DaylightCoyote,
                TestConstant.FileExpectation.DaylightMartenPair,
                TestConstant.FileExpectation.InfraredMarten
            ];

            using TemplateDatabase templateDatabase = this.CreateTemplateDatabase(TestConstant.File.DefaultNewTemplateDatabaseFileName);
            using FileDatabase fileDatabase = this.CreateFileDatabase(templateDatabase, TestConstant.File.DefaultNewFileDatabaseFileName);
            foreach (FileExpectations fileExpectation in fileExpectations)
            {
                // load the image
                Debug.Assert((fileExpectation.RelativePath != null) && (fileExpectation.FileName != null));
                FileInfo fileInfo = new(Path.Combine(fileExpectation.RelativePath, fileExpectation.FileName));
                ImageRow file = fileDatabase.Files.CreateAndAppendFile(fileInfo.Name, fileExpectation.RelativePath);
                CachedImage? image = await file.TryLoadImageAsync(this.WorkingDirectory).ConfigureAwait(false);
                Assert.IsTrue((image != null) && (image.Image != null), "Failed to load image '" + fileExpectation.FileName + "'.");

                (double luminosity, double coloration) = image.Image.GetLuminosityAndColoration(0);
                FileClassification classification = new ImageProperties(luminosity, coloration).EvaluateNewClassification(Constant.Images.DarkLuminosityThresholdDefault);
                if (Math.Abs(luminosity - fileExpectation.Luminosity) > TestConstant.LuminosityAndColorationTolerance)
                {
                    Assert.Fail(fileExpectation.FileName + ": Expected luminosity to be " + fileExpectation.Luminosity + ", but it was " + luminosity + ".");
                }
                if (Math.Abs(coloration - fileExpectation.Coloration) > TestConstant.LuminosityAndColorationTolerance)
                {
                    Assert.Fail(fileExpectation.FileName + ": Expected coloration to be " + fileExpectation.Coloration + ", but it was " + coloration + ".");
                }
                Assert.IsTrue(classification == fileExpectation.Classification, "{0}: Expected classification {1}, but it was {2}.", fileExpectation.FileName, fileExpectation.Classification, classification);
            }
        }

        private static async Task CheckDifferenceResult(ImageDifferenceResult result, ImageCache cache, FileDatabase fileDatabase)
        {
            CachedImage? currentImage = cache.GetCurrentImage();
            switch (result)
            {
                case ImageDifferenceResult.CurrentImageNotAvailable:
                case ImageDifferenceResult.NextImageNotAvailable:
                case ImageDifferenceResult.PreviousImageNotAvailable:
                    if (cache.CurrentDifferenceState == ImageDifference.Unaltered)
                    {
                        Assert.IsTrue((currentImage != null) && (currentImage.Image != null));
                    }
                    else
                    {
                        Assert.IsTrue((currentImage == null) || (currentImage.Image == null));
                    }
                    break;
                case ImageDifferenceResult.NotCalculable:
                    bool expectNullImage = false;
                    int previousOrNextImageRow = -1;
                    int otherImageRowForCombined = -1;
                    switch (cache.CurrentDifferenceState)
                    {
                        // as a default assume images are matched and expect differences to be calculable if the necessary images are available
                        case ImageDifference.Combined:
                            expectNullImage = (cache.CurrentRow == 0) || (cache.CurrentRow == fileDatabase.CurrentlySelectedFileCount - 1);
                            previousOrNextImageRow = cache.CurrentRow - 1;
                            otherImageRowForCombined = cache.CurrentRow + 1;
                            break;
                        case ImageDifference.Next:
                            expectNullImage = cache.CurrentRow == fileDatabase.CurrentlySelectedFileCount - 1;
                            previousOrNextImageRow = cache.CurrentRow + 1;
                            break;
                        case ImageDifference.Previous:
                            expectNullImage = cache.CurrentRow == 0;
                            previousOrNextImageRow = cache.CurrentRow - 1;
                            break;
                        case ImageDifference.Unaltered:
                            // result should be NotCalculable on Unaltered
                            return;
                    }

                    // check if the image to diff against is matched
                    if (fileDatabase.IsFileRowInRange(previousOrNextImageRow))
                    {
                        Assert.IsTrue(cache.Current != null);
                        CachedImage? unalteredImage = await cache.Current.TryLoadImageAsync(fileDatabase.FolderPath).ConfigureAwait(false);
                        Assert.IsTrue((unalteredImage != null) && (unalteredImage.Image != null));

                        ImageRow previousNextFile = fileDatabase.Files[previousOrNextImageRow];
                        CachedImage? previousNextImage = await previousNextFile.TryLoadImageAsync(fileDatabase.FolderPath).ConfigureAwait(false);
                        bool mismatched = true;
                        if (previousNextImage.Image != null)
                        {
                            mismatched = unalteredImage.Image.MismatchedOrNot32BitBgra(previousNextImage.Image);
                        }

                        if (fileDatabase.IsFileRowInRange(otherImageRowForCombined))
                        {
                            ImageRow otherFileForCombined = fileDatabase.Files[otherImageRowForCombined];
                            CachedImage otherImageForCombined = await otherFileForCombined.TryLoadImageAsync(fileDatabase.FolderPath).ConfigureAwait(false);
                            if (otherImageForCombined.Image != null)
                            {
                                mismatched |= unalteredImage.Image.MismatchedOrNot32BitBgra(otherImageForCombined.Image);
                            }
                            else
                            {
                                mismatched = true;
                            }
                        }

                        expectNullImage |= mismatched;
                    }

                    if (expectNullImage)
                    {
                        Assert.IsTrue((currentImage == null) || (currentImage.Image == null), "Expected a null image for difference result {0} and state {1}.", result, cache.CurrentDifferenceState);
                    }
                    else
                    {
                        Assert.IsTrue((currentImage != null) && (currentImage.Image != null), "Expected an image for difference result {0} and state {1}.", result, cache.CurrentDifferenceState);
                    }
                    break;
                case ImageDifferenceResult.Success:
                    Assert.IsTrue((currentImage != null) && (currentImage.Image != null));
                    break;
                default:
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled result {0}.", result));
            }
        }

        [TestMethod]
        public async Task CorruptFileAsync()
        {
            using FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultFileDatabaseFileName);
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
            ImageRow corruptFile = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.CorruptFieldScan, out MetadataReadResults corruptMetadataRead);
            
            CachedImage? corruptImage = await corruptFile.TryLoadImageAsync(fileDatabase.FolderPath).ConfigureAwait(false);
            Assert.IsTrue((corruptImage != null) &&
                          (corruptImage.Image != null) &&
                          (corruptImage.ImageNotDecodable == false) &&
                          (corruptImage.FileNoLongerAvailable == false) &&
                          corruptImage.Image.DecompressionError);
        }

        [TestMethod]
        public void ExifBushnell()
        {
            using FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, Constant.File.DefaultFileDatabaseFileName);
            IReadOnlyCollection<MetadataDirectory> metadata = this.LoadMetadata(TestConstant.FileExpectation.InfraredMarten);
            ExifIfd0Directory ifd0 = metadata.OfType<ExifIfd0Directory>().Single();
            ExifSubIfdDirectory subIfd = metadata.OfType<ExifSubIfdDirectory>().Single();

            Assert.IsTrue(DateTime.TryParseExact(ifd0.GetDescription(ExifIfd0Directory.TagDateTime), TestConstant.Exif.DateTimeFormat, Constant.InvariantCulture, DateTimeStyles.None, out DateTime dateTime));
            Assert.IsTrue(DateTime.TryParseExact(subIfd.GetDescription(ExifSubIfdDirectory.TagDateTimeDigitized), TestConstant.Exif.DateTimeFormat, Constant.InvariantCulture, DateTimeStyles.None, out DateTime dateTimeDigitized));
            Assert.IsTrue(DateTime.TryParseExact(subIfd.GetDescription(ExifSubIfdDirectory.TagDateTimeOriginal), TestConstant.Exif.DateTimeFormat, Constant.InvariantCulture, DateTimeStyles.None, out DateTime dateTimeOriginal));
            Assert.IsFalse(String.IsNullOrWhiteSpace(ifd0.GetDescription(ExifSubIfdDirectory.TagSoftware)));
        }

        [TestMethod]
        public void ExifReconyxHyperfire()
        {
            using FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, Constant.File.DefaultFileDatabaseFileName);
            IReadOnlyCollection<MetadataDirectory> metadata = this.LoadMetadata(TestConstant.FileExpectation.DaylightMartenPair);
            ExifIfd0Directory ifd0 = metadata.OfType<ExifIfd0Directory>().Single();
            ExifSubIfdDirectory subIfd = metadata.OfType<ExifSubIfdDirectory>().Single();
            ReconyxHyperFireMakernoteDirectory hyperfire = metadata.OfType<ReconyxHyperFireMakernoteDirectory>().Single();

            Assert.IsFalse(String.IsNullOrWhiteSpace(subIfd.GetDescription(ExifSubIfdDirectory.TagExposureTime)));

            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagAmbientTemperature)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagAmbientTemperatureFahrenheit)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagBatteryVoltage)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagBrightness)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagContrast)));
            Assert.IsTrue(DateTime.TryParseExact(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagDateTimeOriginal), TestConstant.Exif.DateTimeFormat, Constant.InvariantCulture, DateTimeStyles.None, out DateTime dateTimeOriginal));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagEventNumber)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagFirmwareVersion)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagInfraredIlluminator)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagMakernoteVersion)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagMoonPhase)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagSaturation)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagSequence)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagSerialNumber)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagSharpness)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagTriggerMode)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagUserLabel)));
        }

        private IReadOnlyCollection<MetadataDirectory> LoadMetadata(FileExpectations fileExpectation)
        {
            if ((fileExpectation.RelativePath == null) || (fileExpectation.FileName == null))
            {
                throw new ArgumentOutOfRangeException(nameof(fileExpectation));
            }

            string filePath = Path.Combine(this.WorkingDirectory, fileExpectation.RelativePath, fileExpectation.FileName);
            Assert.IsTrue(JpegImage.IsJpeg(filePath));
            IReadOnlyCollection<MetadataDirectory> metadata = JpegImage.LoadMetadata(filePath);
            Assert.IsTrue(metadata.Count >= 5, "Expected at least 5 metadata directories to be retrieved from {0}", filePath);
            // example information returned from ExifTool
            // field name                   Bushnell                                    Reconyx
            // --- fields of likely interest for image analysis ---
            // Create Date                  2016.02.24 04:59:46                         [Bushnell only]
            // Date/Time Original           2016.02.24 04:59:46                         2015.01.28 11:17:34 [but located in Makernote rather than the standard EXIF field!]
            // Modify Date                  2016.02.24 04:59:46                         [Bushnell only]
            // --- fields possibly of interest interest for image analysis ---
            // Exposure Time                0                                           1/84
            // Shutter Speed                0                                           1/84
            // Software                     BS683BWYx05209                              [Bushnell only]
            // Firmware Version             [Reconyx only]                              3.3.0
            // Trigger Mode                 [Reconyx only]                              Motion Detection
            // Sequence                     [Reconyx only]                              1 of 5
            // Ambient Temperature          [Reconyx only]                              0 C
            // Serial Number                [Reconyx only]                              H500DE01120343
            // Infrared Illuminator         [Reconyx only]                              Off
            // User Label                   [Reconyx only]                              CCPF DS02
            // --- fields of little interest (Bushnell often uses placeholder values which don't change) ---
            // ExifTool Version Number      10.14                                       10.14
            // File Name                    BushnellTrophyHD-119677C-20160224-056.JPG   Reconyx-HC500-20150128-201.JPG
            // Directory                    Carnassial/UnitTests/bin/Debug               Carnassial/UnitTests/bin/Debug/CarnivoreTestImages
            // File Size                    731 kB                                      336 kB
            // File Modification Date/Time  <file time from last build>                 <file time from last build>
            // File Access Date/Time        <file time from last build>                 <file time from last build>
            // File Creation Date/Time      <file time from last build>                 <file time from last build>
            // File Permissions             rw-rw-rw-                                   rw-rw-rw-
            // File Type                    JPEG                                        JPEG
            // File Type Extension          jpg                                         jpg
            // MIME Type                    image/jpeg                                  image/jpeg
            // Exif Byte Order              Little-endian(Intel, II)                    Little-endian(Intel, II)
            // Image Description            M2E6L0-0R350B362                            [Bushnell only]
            // Make                         [blank]                                     [Bushnell only]
            // Camera Model Name            [blank]                                     [Bushnell only]
            // Orientation                  Horizontal(normal)                          [Bushnell only]
            // X Resolution                 96                                          72
            // Y Resolution                 96                                          72
            // Resolution Unit              inches                                      inches
            // Y Cb Cr Positioning          Co-sited                                    Co-sited
            // Copyright                    Copyright 2002                              [Bushnell only]
            // F Number                     2.8                                         [Bushnell only]
            // Exposure Program             Aperture-priority AE                        [Bushnell only]
            // ISO                          100                                         50
            // Exif Version                 0210                                        0220
            // Components Configuration     Y, Cb, Cr, -                                Y, Cb, Cr, -
            // Compressed Bits Per Pixel    0.7419992711                                [Bushnell only]
            // Shutter Speed Value          1                                           [Bushnell only]
            // Aperture Value               2.6                                         [Bushnell only]
            // Exposure Compensation        +2                                          [Bushnell only]
            // Max Aperture Value           2.6                                         [Bushnell only]
            // Metering Mode                Average                                     [Bushnell only]
            // Light Source                 Daylight                                    [Bushnell only]
            // Flash                        No Flash                                    [Bushnell only]
            // Warning                      [minor]Unrecognized MakerNotes              [Bushnell only]
            // Flashpix Version             0100                                        0100
            // Color Space                  sRGB                                        sRGB
            // Exif Image Width             3264                                        2048
            // Exif Image Height            2448                                        1536
            // Related Sound File           RelatedSound                                [Bushnell only]
            // Interoperability Index       R98 - DCF basic file(sRGB)                  [Bushnell only]
            // Interoperability Version     0100                                        [Bushnell only]
            // Exposure Index               1                                           [Bushnell only]
            // Sensing Method               One-chip color area                         [Bushnell only]
            // File Source                  Digital Camera                              [Bushnell only]
            // Scene Type                   Directly photographed                       [Bushnell only]
            // Compression                  JPEG(old - style)                           [Bushnell only]
            // Thumbnail Offset             1312                                        [Bushnell only]
            // Thumbnail Length             5768                                        [Bushnell only]
            // Image Width                  3264                                        2048
            // Image Height                 2448                                        1536
            // Encoding Process             Baseline DCT, Huffman coding                Baseline DCT, Huffman coding
            // Bits Per Sample              8                                           8
            // Color Components             3                                           3
            // Y Cb Cr Sub Sampling         YCbCr4:2:2 (2 1)                            YCbCr4:2:2 (2 1)
            // Aperture                     2.8                                         [Bushnell only]
            // Image Size                   3264x2448                                   2048x1536
            // Megapixels                   8.0                                         3.1
            // Thumbnail Image              <binary data>                               [Bushnell only]
            // Ambient Temperature Fahrenheit [Reconyx only]                            31 F
            // Battery Voltage              [Reconyx only]                              8.65 V
            // Contrast                     [Reconyx only]                              142
            // Brightness                   [Reconyx only]                              238
            // Event Number                 [Reconyx only]                              39
            // Firmware Date                [Reconyx only]                              2011:01:10
            // Maker Note Version           [Reconyx only]                              0xf101
            // Moon Phase                   [Reconyx only]                              First Quarter
            // Motion Sensitivity           [Reconyx only]                              100
            // Sharpness                    [Reconyx only]                              64
            // Saturation                   [Reconyx only]                              144
            return metadata;
        }

        private static void VerifyCurrentImage(ImageCache cache)
        {
            Assert.IsNotNull(cache.Current);

            // don't dispose current image as it's owned by the cache
            CachedImage? currentImage = cache.GetCurrentImage();
            Assert.IsTrue(currentImage != null);
            Assert.IsTrue(currentImage.Image != null);
            Assert.IsTrue(currentImage.Image.DecompressionError == false);
            Assert.IsTrue(currentImage.ImageNotDecodable == false);
            Assert.IsTrue((1000 < currentImage.Image.PixelHeight) && (currentImage.Image.PixelHeight < 10000));
            Assert.IsTrue((1000 < currentImage.Image.PixelWidth) && (currentImage.Image.PixelWidth < 10000));
            Assert.IsTrue(currentImage.Image.TotalPixels > 1000 * 1000);
        }
    }
}
