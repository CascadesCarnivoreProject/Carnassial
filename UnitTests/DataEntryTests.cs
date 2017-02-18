using Carnassial.Controls;
using Carnassial.Database;
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
                controls.CreateControls(fileDatabase, dataHandler);
                Assert.IsTrue(controls.ControlsByDataLabel.Count == databaseExpectation.ExpectedControls, "Expected {0} controls to be generated but {1} were.", databaseExpectation.ExpectedControls, controls.ControlsByDataLabel.Count);

                // check copies aren't possible when the image enumerator's not pointing to an image
                foreach (DataEntryControl control in controls.Controls)
                {
                    Assert.IsFalse(dataHandler.IsCopyForwardPossible(control));
                    Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                }

                // check only copy forward is possible when enumerator's on first image
                List<FileExpectations> fileExpectations = this.PopulateDefaultDatabase(fileDatabase);
                Assert.IsTrue(dataHandler.ImageCache.MoveNext());

                List<DataEntryControl> copyableControls = controls.Controls.Where(control => control.Copyable).ToList();
                foreach (DataEntryControl control in copyableControls)
                {
                    Assert.IsTrue(dataHandler.IsCopyForwardPossible(control));
                    Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                }

                List<DataEntryControl> notCopyableControls = controls.Controls.Where(control => control.Copyable == false).ToList();
                foreach (DataEntryControl control in notCopyableControls)
                {
                    Assert.IsFalse(dataHandler.IsCopyForwardPossible(control));
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
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel)
                    {
                        Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                    }
                    else
                    {
                        Assert.IsTrue(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                    }
                }

                // propagation methods not covered due to requirement of UX interaction
                // dataHandler.CopyForward(control);
                // dataHandler.CopyFromLastValue(control);
                // dataHandler.CopyToAll(control);

                // verify roundtrip of fields subject to copy/paste and analysis assignment
                Assert.IsTrue(dataHandler.ImageCache.TryMoveToFile(0));
                ImageRow firstFile = fileDatabase.Files[0];
                FileExpectations firstFileExpectations = fileExpectations[0];

                Dictionary<string, object> firstFileValuesByDataLabel = firstFile.AsDictionary();
                Assert.IsTrue(firstFileValuesByDataLabel.Count == databaseExpectation.ExpectedColumns.Count);
                foreach (KeyValuePair<string, object> fileValue in firstFileValuesByDataLabel)
                {
                    DataEntryControl control;
                    if (controls.ControlsByDataLabel.TryGetValue(fileValue.Key, out control))
                    {
                        control.SetValue(firstFileValuesByDataLabel[fileValue.Key]);
                    }
                }

                TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZone();
                firstFileExpectations.Verify(firstFile, imageSetTimeZone);

                // verify roundtrip of fields via display string
                foreach (KeyValuePair<string, DataEntryControl> control in controls.ControlsByDataLabel)
                {
                    string displayString = firstFile.GetValueDisplayString(control.Value);
                    control.Value.SetContentAndTooltip(displayString);
                }

                firstFileExpectations.Verify(firstFile, imageSetTimeZone);

                // verify availability of database strings
                foreach (string dataLabel in databaseExpectation.ExpectedColumns)
                {
                    string databaseString = firstFile.GetValueDatabaseString(dataLabel);
                }

                // verify counter increment and decrement
                DataEntryCounter counter0 = (DataEntryCounter)controls.ControlsByDataLabel[TestConstant.DefaultDatabaseColumn.Counter0];
                Assert.IsTrue(counter0.Content == "1");
                Assert.IsTrue(dataHandler.TryDecrementOrResetCounter(counter0));
                Assert.IsTrue(counter0.Content == "0");
                Assert.IsFalse(dataHandler.TryDecrementOrResetCounter(counter0));
                Assert.IsTrue(counter0.Content == "0");

                dataHandler.IncrementOrResetCounter(counter0);
                Assert.IsTrue(counter0.Content == "1");
                Assert.IsTrue(dataHandler.TryDecrementOrResetCounter(counter0));
                Assert.IsTrue(counter0.Content == "0");

                dataHandler.IncrementOrResetCounter(counter0);
                Assert.IsTrue(counter0.Content == "1");
                Assert.IsTrue(dataHandler.TryDecrementOrResetCounter(counter0));
                Assert.IsTrue(counter0.Content == "0");

                DataEntryCounter counter3 = (DataEntryCounter)controls.ControlsByDataLabel[TestConstant.DefaultDatabaseColumn.Counter3];
                Assert.IsTrue(counter3.Content == "0");
                dataHandler.IncrementOrResetCounter(counter0);
                dataHandler.IncrementOrResetCounter(counter3);
                dataHandler.IncrementOrResetCounter(counter3);
                dataHandler.IncrementOrResetCounter(counter0);
                Assert.IsTrue(counter0.Content == "2");
                Assert.IsTrue(counter3.Content == "2");
                Assert.IsTrue(dataHandler.TryDecrementOrResetCounter(counter0));
                Assert.IsTrue(dataHandler.TryDecrementOrResetCounter(counter3));
                Assert.IsTrue(dataHandler.TryDecrementOrResetCounter(counter0));
                Assert.IsTrue(dataHandler.TryDecrementOrResetCounter(counter3));
                dataHandler.IncrementOrResetCounter(counter3);
                dataHandler.IncrementOrResetCounter(counter3);
                dataHandler.IncrementOrResetCounter(counter3);
                dataHandler.IncrementOrResetCounter(counter3);
                Assert.IsTrue(dataHandler.TryDecrementOrResetCounter(counter3));
                Assert.IsTrue(counter0.Content == "0");
                Assert.IsTrue(counter3.Content == "3");
            }
        }
    }
}
