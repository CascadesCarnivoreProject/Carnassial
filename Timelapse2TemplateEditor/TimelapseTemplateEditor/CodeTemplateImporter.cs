using System;
using System.Xml;
using System.Collections.Generic;
using System.Diagnostics;

using System.Data;
using System.Text.RegularExpressions;

namespace TimelapseTemplateEditor
{
    // This clase reads in the code_template.xml file (the old way that we used to specify the template) 
    // and converts it into a data template database.
    public class CodeTemplateImporter
    {
        #region constants
        // STRING PORTIONS to find the XML tags in the XML Code Template File.
        const string SLASH = "/";
        const string CODES = "Codes";

        // Paths to standard elements, always included but not always made visible
        const string _FILE = "_File";
        const string FILEPATH = CODES + SLASH + _FILE;

        const string _FOLDER = "_Folder";
        const string FOLDERPATH = CODES + SLASH + _FOLDER;

        const string _DATE = "_Date";
        const string DATEPATH = CODES + SLASH + _DATE;

        const string _TIME = "_Time";
        const string TIMEPATH = CODES + SLASH + _TIME;

        const string _IMAGEQUALITY = "_ImageQuality";
        const string IMAGEQUALITYPATH = CODES + SLASH + _IMAGEQUALITY;

        const string DATA = "Data";             // the data describing the attributes of that code
        const string LIST = "List";             // List for fixed choices
        const string ITEM = "Item";             // and item in a list



        // Paths to Notes, counters, and fixed choices
        const string NOTEPATH = CODES + SLASH + Constants.NOTE;
        const string COUNTERPATH = CODES + SLASH + Constants.COUNTER;
        const string FIXEDCHOICES = "FixedChoices";
 
        const string FIXEDCHOICEPATH = CODES + SLASH + Constants.CHOICE;

        #endregion

        #region Static Variables
        // Counters for tracking how many of each item we have
        static int counterCount = 0;
        static int noteCount = 0;
        static int choiceCount = 0;
        #endregion

        #region Read the Codes
        static public DataTable Convert(MainWindow win, string filePath, DataTable templateTable, ref List<string> error_messages)
        {
                          // String holding a user-created text log

            DataTable tempTable = templateTable.Copy();
            XmlDocument xmlDoc = new XmlDocument();
            XmlNodeList nodelist;
            XmlNodeList nodeData;

            // Collect all the data labels as we come across them, as we have to ensure that a new data label doesn't have the same name as an existing one
            List<string> data_label_list = new List<string>();
                

            int index = -1;

            win.generateControlsAndSpreadsheet = false;

            xmlDoc.Load(filePath);  // Load the XML document (the code template file)

            nodelist = xmlDoc.SelectNodes(FILEPATH); // Convert the File type 
            nodeData = nodelist[0].SelectNodes(DATA);
            index = FindRow(nodelist, tempTable, Constants.FILE);
            UpdateRow(win, nodeData, tempTable, Constants.FILE, index, ref error_messages, ref data_label_list);

            nodelist = xmlDoc.SelectNodes(FOLDERPATH); // Convert the Folder type
            nodeData = nodelist[0].SelectNodes(DATA);
            index = FindRow(nodelist, tempTable, Constants.FOLDER);
            UpdateRow(win, nodeData, tempTable, Constants.FOLDER, index, ref error_messages, ref data_label_list);

            nodelist = xmlDoc.SelectNodes(DATEPATH); // Convert the Date type
            nodeData = nodelist[0].SelectNodes(DATA);
            index = FindRow(nodelist, tempTable, Constants.DATE);
            UpdateRow(win, nodeData, tempTable, Constants.DATE, index, ref error_messages, ref data_label_list);

            nodelist = xmlDoc.SelectNodes(TIMEPATH); // Convert the Time type
            nodeData = nodelist[0].SelectNodes(DATA);
            index = FindRow(nodelist, tempTable, Constants.TIME);
            UpdateRow(win, nodeData, tempTable, Constants.TIME, index, ref error_messages, ref data_label_list);

            nodelist = xmlDoc.SelectNodes(IMAGEQUALITYPATH); // Convert the Image Quality type
            nodeData = nodelist[0].SelectNodes(DATA);
            index = FindRow(nodelist, tempTable, Constants.IMAGEQUALITY);
            UpdateRow(win, nodeData, tempTable, Constants.IMAGEQUALITY, index, ref error_messages, ref data_label_list);

            // Convert the Notes types, if any
            nodelist = xmlDoc.SelectNodes(NOTEPATH);
            for (int i = 0; i < nodelist.Count; i++)
            {
                // Get the XML section containing values for each note
                nodeData = nodelist[i].SelectNodes(DATA);
                AddRow(nodeData, tempTable, Constants.NOTE);
                UpdateRow(win, nodeData, tempTable, Constants.NOTE, tempTable.Rows.Count - 1, ref error_messages, ref data_label_list);
            }

            // Convert the Choices types, if any
            nodelist = xmlDoc.SelectNodes(FIXEDCHOICEPATH);
            for (int i = 0; i < nodelist.Count; i++)
            {
                // Get the XML section containing values for each choice
                nodeData = nodelist[i].SelectNodes(DATA);
                AddRow(nodeData, tempTable, Constants.CHOICE);
                UpdateRow(win, nodeData, tempTable, Constants.CHOICE, tempTable.Rows.Count - 1, ref error_messages, ref data_label_list);
            }

            // Convert the Counts types, if any
            nodelist = xmlDoc.SelectNodes(COUNTERPATH);
            for (int i = 0; i < nodelist.Count; i++)
            {
                // Get the XML section containing values for each note
                nodeData = nodelist[i].SelectNodes(DATA);
                AddRow(nodeData, tempTable, Constants.COUNTER);
                UpdateRow(win, nodeData, tempTable, Constants.NOTE, tempTable.Rows.Count - 1, ref error_messages, ref data_label_list);
            } 
            return tempTable;
           
        }
        #endregion

        #region Find, update and add rows 
        // Given a typeWanted (i.e., which should be one of the default types as only one of them exists), find its first occurance. 
        // If and only if its found, update the row with the XML information.
        private static int FindRow(XmlNodeList nodelist, DataTable tempTable, string typeWanted)
        {
            int index = -1;
            DataRow row = null;

            // Update the File type 
            if (nodelist.Count == 1) // There should be only one
            {
                // Find the row of a given type
                for (int i = 0; i < tempTable.Rows.Count; i++)
                {
                    row = tempTable.Rows[i];
                    string type = row[Constants.TYPE].ToString();
                    if (type == typeWanted)
                    {
                        index = i;
                        break;
                    }
                }
               
            }
            return index;
        }

        //currently used to update the default table with new values
        private static void UpdateRow(MainWindow win, XmlNodeList nodeData, DataTable tempTable, string typeWanted, int index, ref List<string> error_messages, ref List<string> data_label_list)
        {
            string str_datalabel;
            tempTable.Rows[index][Constants.DEFAULT] = GetColumn(nodeData, Constants.DEFAULT);             // Default
            tempTable.Rows[index][Constants.TXTBOXWIDTH] = GetColumn(nodeData, Constants.TXTBOXWIDTH);      // Width

            // The tempTable should have defaults filled in at this point for labels, datalabels, and tooltips
            // Thus if we just get empty values, we should use those defaults rather than clearing them
            string str_label = GetColumn(nodeData, Constants.LABEL);                                            // Label
            if (!String.IsNullOrEmpty(str_label))
                tempTable.Rows[index][Constants.LABEL] = str_label;

            if (typeWanted.Equals(Constants.FILE)) str_datalabel = Constants.FILE;
            else if (typeWanted.Equals(Constants.FOLDER)) str_datalabel = Constants.FOLDER;
            else if (typeWanted.Equals(Constants.DATE)) str_datalabel = Constants.DATE;
            else if (typeWanted.Equals(Constants.TIME)) str_datalabel = Constants.TIME;
            else if (typeWanted.Equals(Constants.IMAGEQUALITY)) str_datalabel = Constants.IMAGEQUALITY;
            else if (typeWanted.Equals(Constants.DELETEFLAG)) str_datalabel = Constants.DELETEFLAG;
            else
            {
                str_datalabel = GetColumn(nodeData, Constants.DATALABEL);
                if (str_datalabel.Trim().Equals("")) str_datalabel = str_label; // If there is no data label, use the label's value into it. 

                string datalabel = Regex.Replace(str_datalabel, @"\s+", "");    // remove any white space that may be there
                datalabel = Regex.Replace(str_datalabel, "[^a-zA-Z0-9_]", "");  // only allow alphanumeric and '_'. 
                if (!datalabel.Equals(str_datalabel))
                {
                    error_messages.Add("illicit characters: '" + str_datalabel + "' changed to '" + datalabel + "'");
                    str_datalabel = datalabel;
                }

                datalabel = str_datalabel.ToUpper();
                foreach (string s in win.RESERVED_KEYWORDS)
                {
                    if (s.Equals(datalabel))
                    {
                        error_messages.Add("reserved word:    '" + str_datalabel + "' changed to '" + str_datalabel + "_'");
                        str_datalabel += "_";
                        break;
                    }
                }
            }

            // Now set the actual data label

            // First, check to see if the datalabel already exsists in the list, i.e., its not a unique key
            // If it doesn't, keep trying to add an integer to its end to make it unique.
            int j = 0;
            string temp_datalabel = str_datalabel;
            while (data_label_list.Contains(temp_datalabel))
            {
                temp_datalabel = str_datalabel + j.ToString();
            }
            if (!str_datalabel.Equals (temp_datalabel))
            {
                error_messages.Add("duplicate data label:" + Environment.NewLine + "   '" + str_datalabel + "' changed to '" + temp_datalabel + "'");
                str_datalabel = temp_datalabel;
            }

            if (!String.IsNullOrEmpty(str_datalabel))
            {
                if (str_datalabel.Equals("Delete")) str_datalabel = "DeleteLabel"; // Delete is a reserved word!
                tempTable.Rows[index][Constants.DATALABEL] = str_datalabel;
            }
            else
            {
                // If the data label was empty, the priority is to use the non-empty label contents
                // otherwise we stay with the default contents of the data label filled in previously 
                str_label = Regex.Replace(str_label, @"\s+", "");
                if (str_label != "") tempTable.Rows[index][Constants.DATALABEL] = str_label;
            }
            data_label_list.Add(str_datalabel); // and add it to the list of data labels seen

            string str_tooltip = GetColumn(nodeData, Constants.TOOLTIP);
            if (!String.IsNullOrEmpty(str_tooltip))
                tempTable.Rows[index][Constants.TOOLTIP] = str_tooltip;


            // If there is no value supplied for Copyable, default is false for these data types (as they are already filled in by the system). 
            // Counters are also not copyable be default, as we expect counts to change image by image. But there are cases where they user may want to alter this.
            if (typeWanted == Constants.DATE || typeWanted == Constants.TIME || typeWanted == Constants.IMAGEQUALITY || typeWanted == Constants.FOLDER || typeWanted == Constants.FILE || typeWanted == Constants.COUNTER)
            {
                tempTable.Rows[index][Constants.COPYABLE] = MyConvertToBool(TextFromNode(nodeData, 0, Constants.COPYABLE), "false");
            }
            else
            {
                tempTable.Rows[index][Constants.COPYABLE] = MyConvertToBool(TextFromNode(nodeData, 0, Constants.COPYABLE), "true");
            }

            // If there is no value supplied for Visibility, default is true (i.e., the control will be visible in the interface)
            tempTable.Rows[index][Constants.VISIBLE] = MyConvertToBool(TextFromNode(nodeData, 0, Constants.VISIBLE), "true");

            //if the type has a list, we have to do more work.
            if (typeWanted == Constants.IMAGEQUALITY)  // Load up the menu items
            {
                tempTable.Rows[index][Constants.LIST] = Constants.LIST_IMAGEQUALITY; // For Image Quality, use the new list (longer than the one in old templates)
            }
            else if (typeWanted == Constants.IMAGEQUALITY || typeWanted == Constants.CHOICE)  // Load up the menu items
            {
                tempTable.Rows[index][Constants.LIST] = ""; // FOr others, generate the list from what is stored

                XmlNodeList nItems = nodeData[0].SelectNodes(Constants.LIST + SLASH + ITEM);
                bool firsttime = true;
                foreach (XmlNode nodeItem in nItems)
                {
                    if (firsttime)
                    {
                        tempTable.Rows[index][Constants.LIST] = nodeItem.InnerText; //also clears the list's default values
                    }
                    else
                    {
                        tempTable.Rows[index][Constants.LIST] += "|" + nodeItem.InnerText;
                    }
                    firsttime = false;
                }
            }
        }

        // A helper routine to make sure that no values are ever null
        private static string GetColumn(XmlNodeList nodeData, string what)
        {
            string s = TextFromNode(nodeData, 0, what);
            if (null == s) s = "";
            return s;
        }

        // Convert a string to a boolean, where its set to defaultReturn if it cannot be converted by its value
        private static string MyConvertToBool(string value, string defaultReturn)
        {
            string s = value.ToLower ();
            if (s == "true") return "true";
            if (s == "false") return "false";
            return defaultReturn;
        }

        // Add a new row onto the table
        private static void AddRow(XmlNodeList nodelist, DataTable tempTable, string typeWanted)
        {
            // First, populate the row with default values
            tempTable.Rows.Add();

            int index = tempTable.Rows.Count - 1;
            tempTable.Rows[index][Constants.CONTROLORDER] = tempTable.Rows.Count;
            tempTable.Rows[index][Constants.SPREADSHEETORDER] = tempTable.Rows.Count;
            if (typeWanted.Equals(Constants.COUNTER))
            {
                tempTable.Rows[index][Constants.DEFAULT] = "0";
                tempTable.Rows[index][Constants.TYPE] = Constants.COUNTER;
                tempTable.Rows[index][Constants.TXTBOXWIDTH] = Constants.TXTBOXWIDTH_COUNTER;
                tempTable.Rows[index][Constants.COPYABLE] = false;
                tempTable.Rows[index][Constants.VISIBLE] = true;
                tempTable.Rows[index][Constants.LABEL] = Constants.LABEL_COUNTER + counterCount.ToString();
                tempTable.Rows[index][Constants.DATALABEL] = Constants.LABEL_COUNTER + counterCount.ToString();
                tempTable.Rows[index][Constants.TOOLTIP] = Constants.TOOLTIP_COUNTER;
                tempTable.Rows[index][Constants.LIST] = "";
                counterCount++;
            }
            else if (typeWanted.Equals(Constants.NOTE))
            {
                tempTable.Rows[index][Constants.DEFAULT] = "";
                tempTable.Rows[index][Constants.TYPE] = Constants.NOTE;
                tempTable.Rows[index][Constants.TXTBOXWIDTH] = Constants.TXTBOXWIDTH_NOTE;
                tempTable.Rows[index][Constants.COPYABLE] = true;
                tempTable.Rows[index][Constants.VISIBLE] = true;
                tempTable.Rows[index][Constants.LABEL] = Constants.LABEL_NOTE + noteCount.ToString();
                tempTable.Rows[index][Constants.DATALABEL] = Constants.LABEL_NOTE + noteCount.ToString();
                tempTable.Rows[index][Constants.TOOLTIP] = Constants.TOOLTIP_NOTE;
                tempTable.Rows[index][Constants.LIST] = "";
            }
            else if (typeWanted.Equals(Constants.CHOICE))
            {
                tempTable.Rows[index][Constants.DEFAULT] = "";
                tempTable.Rows[index][Constants.TYPE] = Constants.CHOICE;
                tempTable.Rows[index][Constants.TXTBOXWIDTH] = Constants.TXTBOXWIDTH_CHOICE;
                tempTable.Rows[index][Constants.COPYABLE] = true;
                tempTable.Rows[index][Constants.VISIBLE] = true;
                tempTable.Rows[index][Constants.LABEL] = Constants.LABEL_CHOICE + choiceCount.ToString();
                tempTable.Rows[index][Constants.DATALABEL] = Constants.LABEL_CHOICE + choiceCount.ToString();
                tempTable.Rows[index][Constants.TOOLTIP] = Constants.TOOLTIP_CHOICE;
                tempTable.Rows[index][Constants.LIST] = "";
            }

            // Now update the templatetable with the new values
            //UpdateRow(win, nodelist, tempTable, typeWanted, index);
        }

        private static void AddDeletedFlag(DataTable tempTable)
         {
             tempTable.Rows.Add();

             int index = tempTable.Rows.Count - 1;
             tempTable.Rows[index][Constants.CONTROLORDER] = tempTable.Rows.Count;
             tempTable.Rows[index][Constants.SPREADSHEETORDER] = tempTable.Rows.Count;

             //tempTable.Rows[index][Constants.SPREADSHEETORDER] = spreadsheetOrder++);
             tempTable.Rows[index][Constants.DEFAULT] = Constants.DEFAULT_FLAG;
             tempTable.Rows[index][Constants.TYPE] = Constants.DELETEFLAG;
             tempTable.Rows[index][Constants.TXTBOXWIDTH] = Constants.CHKBOXWIDTH_FLAG;
             tempTable.Rows[index][Constants.COPYABLE] = false;
             tempTable.Rows[index][Constants.VISIBLE] = true;
             tempTable.Rows[index][Constants.LABEL] = Constants.LABEL_DELETEFLAG;
             tempTable.Rows[index][Constants.DATALABEL] = Constants.DATALABEL_DELETEFLAG;
             tempTable.Rows[index][Constants.TOOLTIP] = Constants.TOOLTIP_DELETEFLAG; 
         }
        #endregion Find, update and add rows

        #region Utilities
        // Given a nodelist, get the text associated with it 
        private static string TextFromNode(XmlNodeList node, int nodeIndex, string nodeToFind)
        {
            XmlNodeList n = node[nodeIndex].SelectNodes(nodeToFind);
            if (n.Count == 0) return ""; //The node doesn't exist
            return n[0].InnerText;
        }
        #endregion
    }
}
