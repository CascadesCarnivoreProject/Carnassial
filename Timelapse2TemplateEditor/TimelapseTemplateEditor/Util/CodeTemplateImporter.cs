using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;
using Timelapse;
using Timelapse.Database;

namespace Timelapse.Editor.Util
{
    // This clase reads in the code_template.xml file (the old way that we used to specify the template) 
    // and converts it into a data template database.
    public class CodeTemplateImporter
    {
        public void Import(string filePath, TemplateDatabase templateDatabase, out List<string> conversionErrors)
        {
            conversionErrors = new List<string>();

            // Collect all the data labels as we come across them, as we have to ensure that a new data label doesn't have the same name as an existing one
            List<string> dataLabels = new List<string>();

            // Load the XML document (the code template file)
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(filePath);

            // merge standard controls which existed in code templates
            // MarkForDeletion and Relative path weren't available in code templates
            // SAUL TODO: UPDATE THIS TO NEWER DELETEFLAG
            XmlNodeList selectedNodes = xmlDoc.SelectNodes(Constants.ImageXml.FilePath); // Convert the File type 
            this.UpdateStandardControl(selectedNodes, templateDatabase, Constants.DatabaseColumn.File, ref conversionErrors, ref dataLabels);

            selectedNodes = xmlDoc.SelectNodes(Constants.ImageXml.FolderPath); // Convert the Folder type
            this.UpdateStandardControl(selectedNodes, templateDatabase, Constants.DatabaseColumn.Folder, ref conversionErrors, ref dataLabels);

            selectedNodes = xmlDoc.SelectNodes(Constants.ImageXml.DatePath); // Convert the Date type
            this.UpdateStandardControl(selectedNodes, templateDatabase, Constants.DatabaseColumn.Date, ref conversionErrors, ref dataLabels);

            selectedNodes = xmlDoc.SelectNodes(Constants.ImageXml.TimePath); // Convert the Time type
            this.UpdateStandardControl(selectedNodes, templateDatabase, Constants.DatabaseColumn.Time, ref conversionErrors, ref dataLabels);

            selectedNodes = xmlDoc.SelectNodes(Constants.ImageXml.ImageQualityPath); // Convert the Image Quality type
            this.UpdateStandardControl(selectedNodes, templateDatabase, Constants.DatabaseColumn.ImageQuality, ref conversionErrors, ref dataLabels);

            // no flag controls to import
            // import notes
            selectedNodes = xmlDoc.SelectNodes(Constants.ImageXml.NotePath);
            for (int index = 0; index < selectedNodes.Count; index++)
            {
                ControlRow note = templateDatabase.AddUserDefinedControl(Constants.Control.Note);
                this.UpdateControl(selectedNodes[index], templateDatabase, Constants.Control.Note, note, ref conversionErrors, ref dataLabels);
            }

            // import choices
            selectedNodes = xmlDoc.SelectNodes(Constants.ImageXml.FixedChoicePath);
            for (int index = 0; index < selectedNodes.Count; index++)
            {
                ControlRow choice = templateDatabase.AddUserDefinedControl(Constants.Control.FixedChoice);
                this.UpdateControl(selectedNodes[index], templateDatabase, Constants.Control.FixedChoice, choice, ref conversionErrors, ref dataLabels);
            }

            // import counters
            selectedNodes = xmlDoc.SelectNodes(Constants.ImageXml.CounterPath);
            for (int index = 0; index < selectedNodes.Count; index++)
            {
                ControlRow counter = templateDatabase.AddUserDefinedControl(Constants.Control.Counter);
                this.UpdateControl(selectedNodes[index], templateDatabase, Constants.Control.Counter, counter, ref conversionErrors, ref dataLabels);
            }
        }

        private void UpdateControl(XmlNode selectedNode, TemplateDatabase templateDatabase, string typeWanted, ControlRow control, ref List<string> errorMessages, ref List<string> dataLabels)
        {
            XmlNodeList selectedData = selectedNode.SelectNodes(Constants.ImageXml.Data);
            control.DefaultValue = GetColumn(selectedData, Constants.Control.DefaultValue); // Default
            control.TextBoxWidth = Int32.Parse(GetColumn(selectedData, Constants.Control.TextBoxWidth)); // Width

            // The tempTable should have defaults filled in at this point for labels, datalabels, and tooltips
            // Thus if we just get empty values, we should use those defaults rather than clearing them
            string label = GetColumn(selectedData, Constants.Control.Label);
            if (!String.IsNullOrEmpty(label))
            {
                control.Label = label;
            }

            string controlType = typeWanted;
            if (EditorControls.IsStandardControlType(typeWanted) == false)
            {
                controlType = GetColumn(selectedData, Constants.Control.DataLabel);
                if (controlType.Trim().Equals(String.Empty))
                {
                    controlType = label; // If there is no data label, use the label's value into it. 
                }

                string dataLabel = Regex.Replace(controlType, @"\s+", String.Empty);    // remove any white space that may be there
                dataLabel = Regex.Replace(controlType, "[^a-zA-Z0-9_]", String.Empty);  // only allow alphanumeric and '_'. 
                if (!dataLabel.Equals(controlType))
                {
                    errorMessages.Add("illicit characters: '" + controlType + "' changed to '" + dataLabel + "'");
                    controlType = dataLabel;
                }

                foreach (string sqlKeyword in EditorConstant.ReservedSqlKeywords)
                {
                    if (String.Equals(sqlKeyword, dataLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        errorMessages.Add("reserved word:    '" + controlType + "' changed to '" + controlType + "_'");
                        controlType += "_";
                        break;
                    }
                }
            }

            // Now set the actual data label

            // First, check to see if the datalabel already exsists in the list, i.e., its not a unique key
            // If it doesn't, keep trying to add an integer to its end to make it unique.
            int j = 0;
            string temp_datalabel = controlType;
            while (dataLabels.Contains(temp_datalabel))
            {
                temp_datalabel = controlType + j.ToString();
            }
            if (!controlType.Equals(temp_datalabel))
            {
                errorMessages.Add("duplicate data label:" + Environment.NewLine + "   '" + controlType + "' changed to '" + temp_datalabel + "'");
                controlType = temp_datalabel;
            }

            if (!String.IsNullOrEmpty(controlType))
            {
                if (controlType.Equals("Delete"))
                {
                    // TODOSAUL: should this be Constants.Control.DeleteFlag?
                    controlType = "DeleteLabel"; // Delete is a reserved word!
                }
                control.DataLabel = controlType;
            }
            else
            {
                // If the data label was empty, the priority is to use the non-empty label contents
                // otherwise we stay with the default contents of the data label filled in previously 
                label = Regex.Replace(label, @"\s+", String.Empty);
                if (label != String.Empty)
                {
                    control.DataLabel = label;
                }
            }
            dataLabels.Add(controlType); // and add it to the list of data labels seen

            string tooltip = GetColumn(selectedData, Constants.Control.Tooltip);
            if (!String.IsNullOrEmpty(tooltip))
            {
                control.Tooltip = tooltip;
            }

            // If there is no value supplied for Copyable, default is false for these data types (as they are already filled in by the system). 
            // Counters are also not copyable be default, as we expect counts to change image by image. But there are cases where they user may want to alter this.
            bool defaultCopyable = true;
            if (EditorControls.IsStandardControlType(typeWanted))
            {
                defaultCopyable = false;
            }
            control.Copyable = ConvertToBool(TextFromNode(selectedData, 0, Constants.Control.Copyable), defaultCopyable);

            // If there is no value supplied for Visibility, default is true (i.e., the control will be visible in the interface)
            control.Visible = ConvertToBool(TextFromNode(selectedData, 0, Constants.Control.Visible), true);

            // if the type has a list, we have to do more work.
            if (typeWanted == Constants.DatabaseColumn.ImageQuality)
            {
                // For Image Quality, use the new list (longer than the one in old templates)
                control.List = Constants.ImageQuality.ListOfValues;
            }
            else if (typeWanted == Constants.DatabaseColumn.ImageQuality || typeWanted == Constants.Control.FixedChoice)
            {
                // Load up the menu items
                control.List = String.Empty; // For others, generate the list from what is stored

                XmlNodeList nodes = selectedData[0].SelectNodes(Constants.Control.List + Constants.ImageXml.Slash + Constants.ImageXml.Item);
                bool firstTime = true;
                foreach (XmlNode node in nodes)
                {
                    if (firstTime)
                    {
                        control.List = node.InnerText; // also clears the list's default values
                    }
                    else
                    {
                        control.List += "|" + node.InnerText;
                    }
                    firstTime = false;
                }
            }

            templateDatabase.SyncControlToDatabase(control);
        }

        private void UpdateStandardControl(XmlNodeList selectedNodes, TemplateDatabase templateDatabase, string typeWanted, ref List<string> errorMessages, ref List<string> dataLabels)
        {
            Debug.Assert(selectedNodes != null && selectedNodes.Count == 1, "Row update is supported for only a single XML element.");

            // assume the database is well formed and contains only a single row of the given standard type
            foreach (ControlRow control in templateDatabase.TemplateTable)
            {
                if (control.Type == typeWanted)
                {
                    this.UpdateControl(selectedNodes[0], templateDatabase, typeWanted, control, ref errorMessages, ref dataLabels);
                }
            }

            throw new ArgumentOutOfRangeException(String.Format("Control of type {0} could not be found in database.", typeWanted));
        }

        // A helper routine to make sure that no values are ever null
        private static string GetColumn(XmlNodeList nodeData, string what)
        {
            string s = TextFromNode(nodeData, 0, what);
            if (s == null)
            {
                s = String.Empty;
            }
            return s;
        }

        // Convert a string to a boolean, where its set to defaultValue if it cannot be converted by its value
        private static bool ConvertToBool(string value, bool defaultValue)
        {
            string s = value.ToLowerInvariant();
            if (s == Constants.Boolean.True)
            {
                return true;
            }
            if (s == Constants.Boolean.False)
            {
                return false;
            }
            return defaultValue;
        }

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
    }
}
