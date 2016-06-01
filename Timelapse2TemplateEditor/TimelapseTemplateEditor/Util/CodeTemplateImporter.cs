using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;
using Timelapse;

namespace TimelapseTemplateEditor.Util
{
    // This clase reads in the code_template.xml file (the old way that we used to specify the template) 
    // and converts it into a data template database.
    public class CodeTemplateImporter
    {
        // Counters for tracking how many of each item we have
        private int counterCount = 0;
        private int noteCount = 0;
        private int choiceCount = 0;

        #region Read the Codes
        public DataTable Convert(MainWindow win, string filePath, DataTable templateTable, ref List<string> error_messages)
        {
            // string holding a user-created text log
            DataTable tempTable = templateTable.Copy();
            XmlDocument xmlDoc = new XmlDocument();
            XmlNodeList nodelist;
            XmlNodeList nodeData;

            // Collect all the data labels as we come across them, as we have to ensure that a new data label doesn't have the same name as an existing one
            List<string> data_label_list = new List<string>();

            win.GenerateControlsAndSpreadsheet = false;

            xmlDoc.Load(filePath);  // Load the XML document (the code template file)

            nodelist = xmlDoc.SelectNodes(Constants.ImageXml.FilePath); // Convert the File type 
            nodeData = nodelist[0].SelectNodes(Constants.ImageXml.Data);
            int index = this.FindFirstRowOfType(nodelist, tempTable, Constants.DatabaseColumn.File);
            this.UpdateRow(win, nodeData, tempTable, Constants.DatabaseColumn.File, index, ref error_messages, ref data_label_list);

            nodelist = xmlDoc.SelectNodes(Constants.ImageXml.FolderPath); // Convert the Folder type
            nodeData = nodelist[0].SelectNodes(Constants.ImageXml.Data);
            index = this.FindFirstRowOfType(nodelist, tempTable, Constants.DatabaseColumn.Folder);
            this.UpdateRow(win, nodeData, tempTable, Constants.DatabaseColumn.Folder, index, ref error_messages, ref data_label_list);

            nodelist = xmlDoc.SelectNodes(Constants.ImageXml.DatePath); // Convert the Date type
            nodeData = nodelist[0].SelectNodes(Constants.ImageXml.Data);
            index = this.FindFirstRowOfType(nodelist, tempTable, Constants.DatabaseColumn.Date);
            this.UpdateRow(win, nodeData, tempTable, Constants.DatabaseColumn.Date, index, ref error_messages, ref data_label_list);

            nodelist = xmlDoc.SelectNodes(Constants.ImageXml.TimePath); // Convert the Time type
            nodeData = nodelist[0].SelectNodes(Constants.ImageXml.Data);
            index = this.FindFirstRowOfType(nodelist, tempTable, Constants.DatabaseColumn.Time);
            this.UpdateRow(win, nodeData, tempTable, Constants.DatabaseColumn.Time, index, ref error_messages, ref data_label_list);

            nodelist = xmlDoc.SelectNodes(Constants.ImageXml.ImageQualityPath); // Convert the Image Quality type
            nodeData = nodelist[0].SelectNodes(Constants.ImageXml.Data);
            index = this.FindFirstRowOfType(nodelist, tempTable, Constants.DatabaseColumn.ImageQuality);
            this.UpdateRow(win, nodeData, tempTable, Constants.DatabaseColumn.ImageQuality, index, ref error_messages, ref data_label_list);

            // Convert the Notes types, if any
            nodelist = xmlDoc.SelectNodes(Constants.ImageXml.NotePath);
            for (int i = 0; i < nodelist.Count; i++)
            {
                // Get the XML section containing values for each note
                nodeData = nodelist[i].SelectNodes(Constants.ImageXml.Data);
                this.AddRow(nodeData, tempTable, Constants.Control.Note);
                this.UpdateRow(win, nodeData, tempTable, Constants.Control.Note, tempTable.Rows.Count - 1, ref error_messages, ref data_label_list);
            }

            // Convert the Choices types, if any
            nodelist = xmlDoc.SelectNodes(Constants.ImageXml.FixedChoicePath);
            for (int i = 0; i < nodelist.Count; i++)
            {
                // Get the XML section containing values for each choice
                nodeData = nodelist[i].SelectNodes(Constants.ImageXml.Data);
                this.AddRow(nodeData, tempTable, Constants.Control.FixedChoice);
                this.UpdateRow(win, nodeData, tempTable, Constants.Control.FixedChoice, tempTable.Rows.Count - 1, ref error_messages, ref data_label_list);
            }

            // Convert the Counts types, if any
            nodelist = xmlDoc.SelectNodes(Constants.ImageXml.CounterPath);
            for (int i = 0; i < nodelist.Count; i++)
            {
                // Get the XML section containing values for each note
                nodeData = nodelist[i].SelectNodes(Constants.ImageXml.Data);
                this.AddRow(nodeData, tempTable, Constants.Control.Counter);
                this.UpdateRow(win, nodeData, tempTable, Constants.Control.Note, tempTable.Rows.Count - 1, ref error_messages, ref data_label_list);
            }

            return tempTable;
        }
        #endregion

        #region Find, update and add rows 
        // Given a typeWanted (i.e., which should be one of the default types as only one of them exists), find its first occurance. 
        // If and only if its found, update the row with the XML information.
        private int FindFirstRowOfType(XmlNodeList nodelist, DataTable tempTable, string typeWanted)
        {
            // Update the File type 
            // There should be only one node
            if (nodelist.Count == 1)
            {
                // Find the row of a given type
                for (int rowIndex = 0; rowIndex < tempTable.Rows.Count; rowIndex++)
                {
                    DataRow row = tempTable.Rows[rowIndex];
                    string type = row[Constants.DatabaseColumn.Type].ToString();
                    if (type == typeWanted)
                    {
                        return rowIndex;
                    }
                }
            }
            return -1;
        }

        // currently used to update the default table with new values
        private void UpdateRow(MainWindow win, XmlNodeList nodeData, DataTable tempTable, string typeWanted, int index, ref List<string> error_messages, ref List<string> data_label_list)
        {
            tempTable.Rows[index][Constants.Control.DefaultValue] = GetColumn(nodeData, Constants.Control.DefaultValue);             // Default
            tempTable.Rows[index][Constants.Control.TextBoxWidth] = GetColumn(nodeData, Constants.Control.TextBoxWidth);      // Width

            // The tempTable should have defaults filled in at this point for labels, datalabels, and tooltips
            // Thus if we just get empty values, we should use those defaults rather than clearing them
            string label = GetColumn(nodeData, Constants.Control.Label);                                            // Label
            if (!String.IsNullOrEmpty(label))
            {
                tempTable.Rows[index][Constants.Control.Label] = label;
            }

            string dataLabel;
            if (typeWanted.Equals(Constants.DatabaseColumn.File))
            {
                dataLabel = Constants.DatabaseColumn.File;
            }
            else if (typeWanted.Equals(Constants.DatabaseColumn.Folder))
            {
                dataLabel = Constants.DatabaseColumn.Folder;
            }
            else if (typeWanted.Equals(Constants.DatabaseColumn.Date))
            {
                dataLabel = Constants.DatabaseColumn.Date;
            }
            else if (typeWanted.Equals(Constants.DatabaseColumn.Time))
            {
                dataLabel = Constants.DatabaseColumn.Time;
            }
            else if (typeWanted.Equals(Constants.DatabaseColumn.ImageQuality))
            {
                dataLabel = Constants.DatabaseColumn.ImageQuality;
            }
            else if (typeWanted.Equals(Constants.Control.DeleteFlag))
            {
                dataLabel = Constants.Control.DeleteFlag;
            }
            else
            {
                dataLabel = GetColumn(nodeData, Constants.Control.DataLabel);
                if (dataLabel.Trim().Equals(String.Empty))
                {
                    dataLabel = label; // If there is no data label, use the label's value into it. 
                }

                string datalabel = Regex.Replace(dataLabel, @"\s+", String.Empty);    // remove any white space that may be there
                datalabel = Regex.Replace(dataLabel, "[^a-zA-Z0-9_]", String.Empty);  // only allow alphanumeric and '_'. 
                if (!datalabel.Equals(dataLabel))
                {
                    error_messages.Add("illicit characters: '" + dataLabel + "' changed to '" + datalabel + "'");
                    dataLabel = datalabel;
                }

                datalabel = dataLabel.ToUpper();
                foreach (string s in EditorConstant.ReservedSqlKeywords)
                {
                    if (s.Equals(datalabel))
                    {
                        error_messages.Add("reserved word:    '" + dataLabel + "' changed to '" + dataLabel + "_'");
                        dataLabel += "_";
                        break;
                    }
                }
            }

            // Now set the actual data label

            // First, check to see if the datalabel already exsists in the list, i.e., its not a unique key
            // If it doesn't, keep trying to add an integer to its end to make it unique.
            int j = 0;
            string temp_datalabel = dataLabel;
            while (data_label_list.Contains(temp_datalabel))
            {
                temp_datalabel = dataLabel + j.ToString();
            }
            if (!dataLabel.Equals(temp_datalabel))
            {
                error_messages.Add("duplicate data label:" + Environment.NewLine + "   '" + dataLabel + "' changed to '" + temp_datalabel + "'");
                dataLabel = temp_datalabel;
            }

            if (!String.IsNullOrEmpty(dataLabel))
            {
                if (dataLabel.Equals("Delete"))
                {
                    dataLabel = "DeleteLabel"; // Delete is a reserved word!
                }
                tempTable.Rows[index][Constants.Control.DataLabel] = dataLabel;
            }
            else
            {
                // If the data label was empty, the priority is to use the non-empty label contents
                // otherwise we stay with the default contents of the data label filled in previously 
                label = Regex.Replace(label, @"\s+", String.Empty);
                if (label != String.Empty)
                {
                    tempTable.Rows[index][Constants.Control.DataLabel] = label;
                }
            }
            data_label_list.Add(dataLabel); // and add it to the list of data labels seen

            string str_tooltip = GetColumn(nodeData, Constants.Control.Tooltip);
            if (!String.IsNullOrEmpty(str_tooltip))
            {
                tempTable.Rows[index][Constants.Control.Tooltip] = str_tooltip;
            }

            // If there is no value supplied for Copyable, default is false for these data types (as they are already filled in by the system). 
            // Counters are also not copyable be default, as we expect counts to change image by image. But there are cases where they user may want to alter this.
            if (typeWanted == Constants.DatabaseColumn.Date || typeWanted == Constants.DatabaseColumn.Time || typeWanted == Constants.DatabaseColumn.ImageQuality || typeWanted == Constants.DatabaseColumn.Folder || typeWanted == Constants.DatabaseColumn.File || typeWanted == Constants.Control.Counter)
            {
                tempTable.Rows[index][Constants.Control.Copyable] = MyConvertToBool(TextFromNode(nodeData, 0, Constants.Control.Copyable), "false");
            }
            else
            {
                tempTable.Rows[index][Constants.Control.Copyable] = MyConvertToBool(TextFromNode(nodeData, 0, Constants.Control.Copyable), "true");
            }

            // If there is no value supplied for Visibility, default is true (i.e., the control will be visible in the interface)
            tempTable.Rows[index][Constants.Control.Visible] = MyConvertToBool(TextFromNode(nodeData, 0, Constants.Control.Visible), "true");

            // if the type has a list, we have to do more work.
            if (typeWanted == Constants.DatabaseColumn.ImageQuality)
            {
                // For Image Quality, use the new list (longer than the one in old templates)
                tempTable.Rows[index][Constants.Control.List] = Constants.ImageQuality.ListOfValues;
            }
            else if (typeWanted == Constants.DatabaseColumn.ImageQuality || typeWanted == Constants.Control.FixedChoice)
            {
                // Load up the menu items
                tempTable.Rows[index][Constants.Control.List] = String.Empty; // FOr others, generate the list from what is stored

                XmlNodeList nodes = nodeData[0].SelectNodes(Constants.Control.List + Constants.ImageXml.Slash + Constants.ImageXml.Item);
                bool firsttime = true;
                foreach (XmlNode node in nodes)
                {
                    if (firsttime)
                    {
                        tempTable.Rows[index][Constants.Control.List] = node.InnerText; // also clears the list's default values
                    }
                    else
                    {
                        tempTable.Rows[index][Constants.Control.List] += "|" + node.InnerText;
                    }
                    firsttime = false;
                }
            }
        }

        // A helper routine to make sure that no values are ever null
        private static string GetColumn(XmlNodeList nodeData, string what)
        {
            string s = TextFromNode(nodeData, 0, what);
            if (null == s)
            {
                s = String.Empty;
            }
            return s;
        }

        // Convert a string to a boolean, where its set to defaultReturn if it cannot be converted by its value
        private static string MyConvertToBool(string value, string defaultReturn)
        {
            string s = value.ToLower();
            if (s == "true")
            {
                return "true";
            }
            if (s == "false")
            {
                return "false";
            }
            return defaultReturn;
        }

        // Add a new row onto the table
        // TODOTODD: dup code, merge to TemplateDatabase
        private void AddRow(XmlNodeList nodelist, DataTable tempTable, string typeWanted)
        {
            // First, populate the row with default values
            tempTable.Rows.Add();

            int index = tempTable.Rows.Count - 1;
            tempTable.Rows[index][Constants.Control.ControlOrder] = tempTable.Rows.Count;
            tempTable.Rows[index][Constants.Control.SpreadsheetOrder] = tempTable.Rows.Count;
            if (typeWanted.Equals(Constants.Control.Counter))
            {
                tempTable.Rows[index][Constants.Control.DefaultValue] = "0";
                tempTable.Rows[index][Constants.DatabaseColumn.Type] = Constants.Control.Counter;
                tempTable.Rows[index][Constants.Control.TextBoxWidth] = Constants.ControlDefault.CounterWidth;
                tempTable.Rows[index][Constants.Control.Copyable] = false;
                tempTable.Rows[index][Constants.Control.Visible] = true;
                tempTable.Rows[index][Constants.Control.Label] = Constants.Control.Counter + this.counterCount.ToString();
                tempTable.Rows[index][Constants.Control.DataLabel] = Constants.Control.Counter + this.counterCount.ToString();
                tempTable.Rows[index][Constants.Control.Tooltip] = Constants.ControlDefault.CounterTooltip;
                tempTable.Rows[index][Constants.Control.List] = String.Empty;
                this.counterCount++;
            }
            else if (typeWanted.Equals(Constants.Control.Note))
            {
                tempTable.Rows[index][Constants.Control.DefaultValue] = String.Empty;
                tempTable.Rows[index][Constants.DatabaseColumn.Type] = Constants.Control.Note;
                tempTable.Rows[index][Constants.Control.TextBoxWidth] = Constants.ControlDefault.NoteWidth;
                tempTable.Rows[index][Constants.Control.Copyable] = true;
                tempTable.Rows[index][Constants.Control.Visible] = true;
                tempTable.Rows[index][Constants.Control.Label] = Constants.Control.Note + this.noteCount.ToString();
                tempTable.Rows[index][Constants.Control.DataLabel] = Constants.Control.Note + this.noteCount.ToString();
                tempTable.Rows[index][Constants.Control.Tooltip] = Constants.ControlDefault.NoteTooltip;
                tempTable.Rows[index][Constants.Control.List] = String.Empty;
            }
            else if (typeWanted.Equals(Constants.Control.FixedChoice))
            {
                tempTable.Rows[index][Constants.Control.DefaultValue] = String.Empty;
                tempTable.Rows[index][Constants.DatabaseColumn.Type] = Constants.Control.FixedChoice;
                tempTable.Rows[index][Constants.Control.TextBoxWidth] = Constants.ControlDefault.FixedChoiceWidth;
                tempTable.Rows[index][Constants.Control.Copyable] = true;
                tempTable.Rows[index][Constants.Control.Visible] = true;
                tempTable.Rows[index][Constants.Control.Label] = Constants.Control.Choice + this.choiceCount.ToString();
                // TODOSAUL: shouldn't this be Constants.Control.FixedChoice?
                tempTable.Rows[index][Constants.Control.DataLabel] = Constants.Control.Choice + this.choiceCount.ToString();
                tempTable.Rows[index][Constants.Control.Tooltip] = Constants.ControlDefault.FixedChoiceTooltip;
                tempTable.Rows[index][Constants.Control.List] = String.Empty;
            }
        }

        private static void AddDeletedFlag(DataTable tempTable)
        {
            tempTable.Rows.Add();

            int index = tempTable.Rows.Count - 1;
            tempTable.Rows[index][Constants.Control.ControlOrder] = tempTable.Rows.Count;
            tempTable.Rows[index][Constants.Control.SpreadsheetOrder] = tempTable.Rows.Count;
            tempTable.Rows[index][Constants.Control.DefaultValue] = Constants.ControlDefault.FlagValue;
            tempTable.Rows[index][Constants.DatabaseColumn.Type] = Constants.Control.DeleteFlag;
            tempTable.Rows[index][Constants.Control.TextBoxWidth] = Constants.ControlDefault.FlagWidth;
            tempTable.Rows[index][Constants.Control.Copyable] = false;
            tempTable.Rows[index][Constants.Control.Visible] = true;
            tempTable.Rows[index][Constants.Control.Label] = EditorConstant.Control.MarkForDeletionLabel;
            tempTable.Rows[index][Constants.Control.DataLabel] = EditorConstant.Control.MarkForDeletion;
            tempTable.Rows[index][Constants.Control.Tooltip] = Constants.ControlDefault.MarkForDeletionTooltip;
        }
        #endregion Find, update and add rows

        #region Utilities
        // Given a nodelist, get the text associated with it 
        private static string TextFromNode(XmlNodeList node, int nodeIndex, string nodeToFind)
        {
            XmlNodeList n = node[nodeIndex].SelectNodes(nodeToFind);
            if (n.Count == 0)
            {
                return String.Empty; // The node doesn't exist
            }
            return n[0].InnerText;
        }
        #endregion
    }
}
