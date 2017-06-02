using Carnassial.Control;
using Carnassial.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class DataEntryTests : CarnassialTest
    {
        [TestMethod]
        public void CreateReuseControlsAndPropagate()
        {
            List<DatabaseExpectations> databaseExpectations = new List<DatabaseExpectations>()
            {
                new DatabaseExpectations()
                {
                    FileName = Constant.File.DefaultFileDatabaseFileName,
                    TemplateDatabaseFileName = TestConstant.File.DefaultTemplateDatabaseFileName,
                    ExpectedColumns = TestConstant.DefaultFileDataColumns,
                    ExpectedControls = TestConstant.DefaultFileDataColumns.Count - 6
                }
            };

            foreach (DatabaseExpectations databaseExpectation in databaseExpectations)
            {
                FileDatabase fileDatabase = this.CreateFileDatabase(databaseExpectation.TemplateDatabaseFileName, databaseExpectation.FileName);
                DataEntryHandler dataHandler = new DataEntryHandler(fileDatabase);

                DataEntryControls controls = new DataEntryControls();
                controls.CreateControls(fileDatabase, dataHandler, (string dataLabel) => { return fileDatabase.GetDistinctValuesInFileDataColumn(dataLabel); });
                Assert.IsTrue(controls.ControlsByDataLabel.Count == databaseExpectation.ExpectedControls, "Expected {0} controls to be generated but {1} were.", databaseExpectation.ExpectedControls, controls.ControlsByDataLabel.Count);

                // check copies aren't possible when the image enumerator's not pointing to an image
                List<DataEntryControl> copyableControls = controls.Controls.Where(control => control.Copyable).ToList();
                foreach (DataEntryControl control in copyableControls)
                {
                    Assert.IsFalse(dataHandler.IsCopyForwardPossible(control));
                    Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                }

                // check only copy forward is possible when enumerator's on first image
                List<FileExpectations> fileExpectations = this.PopulateDefaultDatabase(fileDatabase);
                Assert.IsTrue(dataHandler.ImageCache.MoveNext());

                foreach (DataEntryControl control in copyableControls)
                {
                    Assert.IsTrue(dataHandler.IsCopyForwardPossible(control));
                    Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                }

                // check only copy last is possible when enumerator's on last image
                // check also copy last is not possible if no previous instance of the field has been filled out
                while (dataHandler.ImageCache.CurrentRow < fileExpectations.Count - 1)
                {
                    Assert.IsTrue(dataHandler.ImageCache.MoveNext());
                }

                foreach (DataEntryControl control in copyableControls)
                {
                    Assert.IsFalse(dataHandler.IsCopyForwardPossible(control));
                    if (control.DataLabel == TestConstant.CarnivoreDatabaseColumn.Pelage ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.ChoiceNotVisible ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.Choice3 ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.Counter3 ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.Flag0 ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel)
                    {
                        Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control), control.DataLabel);
                    }
                    else
                    {
                        Assert.IsTrue(dataHandler.IsCopyFromLastNonEmptyValuePossible(control), control.DataLabel);
                    }
                }

                // propagation methods not covered due to requirement of UX interaction
                // dataHandler.CopyForward(control);
                // dataHandler.CopyFromLastValue(control);
                // dataHandler.CopyToAll(control);

                // verify roundtrip of fields subject to copy/paste and analysis assignment
                // AsDictionary() returns a dictionary with one fewer values than there are columns as the DateTime and UtcOffset columns are merged to DateTimeOffset.
                Assert.IsTrue(dataHandler.ImageCache.TryMoveToFile(0));
                ImageRow firstFile = fileDatabase.Files[0];
                FileExpectations firstFileExpectations = fileExpectations[0];

                Dictionary<string, object> firstFileValuesByPropertyName = firstFile.AsDictionary();
                Assert.IsTrue(firstFileValuesByPropertyName.Count == databaseExpectation.ExpectedColumns.Count - 1);
                foreach (KeyValuePair<string, object> singlePropertyEdit in firstFileValuesByPropertyName)
                {
                    if (singlePropertyEdit.Key == Constant.DatabaseColumn.ID)
                    {
                        continue;
                    }
                    firstFile[singlePropertyEdit.Key] = singlePropertyEdit.Value;
                }

                TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZone();
                firstFileExpectations.Verify(firstFile, imageSetTimeZone);

                // verify roundtrip of fields via display string
                foreach (DataEntryControl control in controls.Controls)
                {
                    string displayString = firstFile.GetDisplayString(control);
                    control.SetValue(displayString);
                }

                firstFileExpectations.Verify(firstFile, imageSetTimeZone);

                // verify availability of database strings
                foreach (string dataLabel in databaseExpectation.ExpectedColumns)
                {
                    string databaseString = firstFile.GetDatabaseString(dataLabel);
                }

                // verify counter increment and decrement
                // UI thread isn't running to perform data binding (and controls aren't visible) so most checks are against the underlying ImageRow.
                DataEntryCounter counter0 = (DataEntryCounter)controls.ControlsByDataLabel[TestConstant.DefaultDatabaseColumn.Counter0];
                counter0.DataContext = dataHandler.ImageCache.Current;
                Assert.IsTrue(counter0.Content == "1");
                dataHandler.DecrementOrResetCounter(counter0);
                Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "0");
                dataHandler.DecrementOrResetCounter(counter0);
                Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "0");

                dataHandler.IncrementOrResetCounter(counter0);
                Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "1");
                dataHandler.DecrementOrResetCounter(counter0);
                Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "0");

                dataHandler.IncrementOrResetCounter(counter0);
                Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "1");
                dataHandler.DecrementOrResetCounter(counter0);
                Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "0");

                DataEntryCounter counter3 = (DataEntryCounter)controls.ControlsByDataLabel[TestConstant.DefaultDatabaseColumn.Counter3];
                counter3.DataContext = dataHandler.ImageCache.Current;
                Assert.IsTrue(counter3.Content == "0");
                dataHandler.IncrementOrResetCounter(counter0);
                dataHandler.IncrementOrResetCounter(counter3);
                dataHandler.IncrementOrResetCounter(counter3);
                dataHandler.IncrementOrResetCounter(counter0);
                Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "2");
                Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter3] == "2");
                dataHandler.DecrementOrResetCounter(counter0);
                dataHandler.DecrementOrResetCounter(counter3);
                dataHandler.DecrementOrResetCounter(counter0);
                dataHandler.DecrementOrResetCounter(counter3);
                dataHandler.IncrementOrResetCounter(counter3);
                dataHandler.IncrementOrResetCounter(counter3);
                dataHandler.IncrementOrResetCounter(counter3);
                dataHandler.IncrementOrResetCounter(counter3);
                dataHandler.DecrementOrResetCounter(counter3);
                Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "0");
                Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter3] == "3");
            }
        }
    }
}
