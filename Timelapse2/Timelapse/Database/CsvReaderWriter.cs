﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace Timelapse.Database
{
    /// <summary>
    /// Import and export .csv files.
    /// </summary>
    internal class CsvReaderWriter
    {
        #region Public methods
        /// <summary>
        /// Export all the database data associated with the filtered view to the CSV file indicated in the file path so that spreadsheet applications (like Excel) can display it.
        /// </summary>
        public void ExportToCsv(ImageDatabase database, string filePath)
        {
            TextWriter fileWriter = new StreamWriter(filePath, false);
            try
            {
                // Write the header as defined by the data labels in the template file
                // If the data label is an empty string, we use the label instead.
                // The append sequence results in a trailing comma which is retained when writing the line.
                StringBuilder header = new StringBuilder();
                List<string> dataLabels = this.GetDataLabels(database);
                foreach (string dataLabel in dataLabels)
                {
                    header.Append(this.AddColumnValue(dataLabel));
                }
                fileWriter.WriteLine(header.ToString());

                // For each row in the data table, write out the columns in the same order as the 
                // data labels in the template file
                for (int i = 0; i < database.ImageCount; i++)
                {
                    StringBuilder row = new StringBuilder();
                    foreach (string dataLabel in dataLabels)
                    {
                        row.Append(this.AddColumnValue((string)database.ImageDataTable.Rows[i][dataLabel]));
                    }
                    fileWriter.WriteLine(row.ToString());
                }
            }
            catch
            {
                // Can't write the spreadsheet file
                DialogMessageBox messageBox = new DialogMessageBox();
                messageBox.IconType = MessageBoxImage.Error;
                messageBox.ButtonType = MessageBoxButton.OK;

                messageBox.MessageTitle = "Can't write the spreadsheet file.";
                messageBox.MessageProblem = "The following file can't be written: " + filePath + ".";
                messageBox.MessageReason = "You may already have it open in Excel or another  application.";
                messageBox.MessageSolution = "If the file is open in another application, close it and try again.";
                messageBox.ShowDialog();
            }
            finally
            {
                fileWriter.Close();
            }
        }

        public void ImportFromCsv(ImageDatabase database, string filePath)
        {
            List<string> dataLabels = this.GetDataLabels(database);
            StreamReader csvReader = new StreamReader(filePath);
            try
            {
                List<string> header = this.ReadAndParseLine(csvReader);

                List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
                for (List<string> row = this.ReadAndParseLine(csvReader); row != null; row = this.ReadAndParseLine(csvReader))
                {
                    if (row.Count == dataLabels.Count - 1)
                    {
                        // .csv files are ambiguous in the sense a trailing comma may or may not be present at the end of the line
                        // if the final field has a value this case isn't a concern, but if the final field has no value then there's
                        // no way for the parser to know the exact number of fields in the line
                        row.Add(null);
                    }
                    else if (row.Count != dataLabels.Count)
                    {
                        Debug.Assert(false, String.Format("Expected {0} fields in line {1} but found {2}.", dataLabels.Count, String.Join(",", row), row.Count));
                    }

                    string imageFileName = null;
                    string folder = null;
                    ColumnTuplesWithWhere imageToUpdate = new ColumnTuplesWithWhere();
                    for (int field = 0; field < row.Count; ++field)
                    {
                        string dataLabel = dataLabels[field];
                        string value = row[field];
                        imageToUpdate.Columns.Add(new ColumnTuple(dataLabel, value));

                        if (dataLabel == Constants.DatabaseColumn.File)
                        {
                            imageFileName = value;
                        }
                        if (dataLabel == Constants.DatabaseColumn.Folder)
                        {
                            folder = value;
                        }
                    }

                    Debug.Assert(String.IsNullOrWhiteSpace(imageFileName) == false, "Image's file name was not loaded.");
                    imageToUpdate.SetWhere(folder, imageFileName);
                    imagesToUpdate.Add(imageToUpdate);

                    if (imagesToUpdate.Count >= 100)
                    {
                        database.UpdateImages(imagesToUpdate);
                        imagesToUpdate.Clear();
                    }
                }

                database.UpdateImages(imagesToUpdate);
            }
            catch
            {
                DialogMessageBox messageBox = new DialogMessageBox();
                messageBox.IconType = MessageBoxImage.Error;
                messageBox.ButtonType = MessageBoxButton.OK;

                messageBox.MessageTitle = "Can't read the spreadsheet file.";
                messageBox.ShowDialog();
            }
            finally
            {
                csvReader.Close();
            }
        }
        #endregion

        #region Private methods

        // Returms the dataLabel if it isn't empty, otherwise the label
        private string GetLabel(string label, string dataLabel)
        {
            return (dataLabel == String.Empty) ? label : dataLabel;
        }

        // Check if there is any Quotation Mark '"', a Comma ',', a Line Feed \x0A,  or Carriage Return \x0D
        // and escape it as needed
        private string AddColumnValue(string value)
        {
            if (value == null)
            {
                return ",";
            }
            if (value.IndexOfAny("\",\x0A\x0D".ToCharArray()) > -1)
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"" + ",";
            }
            else
            {
                return value + ",";
            }
        }

        private List<string> GetDataLabels(ImageDatabase database)
        {
            List<string> dataLabels = new List<string>();
            for (int i = 0; i < database.TemplateTable.Rows.Count; i++)
            {
                string label = (string)database.TemplateTable.Rows[i][Constants.Control.Label];
                string dataLabel = (string)database.TemplateTable.Rows[i][Constants.Control.DataLabel];
                dataLabel = this.GetLabel(label, dataLabel);

                // get a list of datalabels so we can add columns in the order that matches the current template table order
                if (Constants.Database.ID != dataLabel)
                {
                    dataLabels.Add(dataLabel);
                }
            }
            return dataLabels;
        }

        private List<string> ReadAndParseLine(StreamReader csvReader)
        {
            string unparsedLine = csvReader.ReadLine();
            if (unparsedLine == null)
            {
                return null;
            }

            List<string> parsedLine = new List<string>();
            bool isFieldEscaped = false;
            int fieldStart = 0;
            bool inField = false;
            for (int index = 0; index < unparsedLine.Length; ++index)
            {
                char currentCharacter = unparsedLine[index];
                if (inField == false)
                {
                    if (currentCharacter == '\"')
                    {
                        // start of escaped field
                        isFieldEscaped = true;
                        fieldStart = index + 1;
                    }
                    else if (currentCharacter == ',')
                    {
                        // empty field
                        parsedLine.Add(null);
                        continue;
                    }
                    else
                    {
                        // start of unescaped field
                        fieldStart = index;
                    }

                    inField = true;
                }
                else
                {
                    if (currentCharacter == ',' && isFieldEscaped == false)
                    {
                        // end of unescaped field
                        inField = false;
                        string field = unparsedLine.Substring(fieldStart, index - fieldStart);
                        parsedLine.Add(field);
                    }
                    else if (currentCharacter == '\"' && isFieldEscaped)
                    {
                        // escaped character encountered; check for end of escaped field
                        int nextIndex = index + 1;
                        if (nextIndex < unparsedLine.Length && unparsedLine[nextIndex] == ',')
                        {
                            // end of escaped field
                            inField = false;
                            isFieldEscaped = false;
                            string field = unparsedLine.Substring(fieldStart, index - fieldStart);
                            parsedLine.Add(field);
                            ++index;
                        }
                    }
                }
            }
            return parsedLine;
        }
        #endregion 
    }
}
