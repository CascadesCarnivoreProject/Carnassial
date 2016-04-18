using System;
using System.Collections.Generic;
using System.Data;
using System.Xml;
using Timelapse.Database;

namespace Timelapse.Images
{
    internal class ImageDataXml
    {
        // Read all the data into the imageData structure from the XML file in the filepath.
        // Note that we need to know the code controls,as we have to associate any points read in with a particular counter control
        public static void Read(string filePath, DataTable template, ImageDatabase imageDatabase)
        {
            // XML Preparation
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(filePath);

            // Import the old log (if any)
            XmlNodeList nodeLog = xmlDoc.SelectNodes(Constants.DatabaseColumn.Images + Constants.DatabaseColumn.Slash + Constants.DatabaseColumn.Log);
            if (nodeLog.Count > 0)
            {
                XmlNode nlog = nodeLog[0];
                imageDatabase.SetImageSetLog(nlog.InnerText);
            }

            XmlNodeList nodelist = xmlDoc.SelectNodes(Constants.DatabaseColumn.Images + Constants.DatabaseColumn.Slash + Constants.DatabaseColumn.Image);
            XmlNodeList innerNodeList;

            string filename = String.Empty;
            string date;
            string time;

            List<string> counts = new List<string>();
            List<ColumnTuple> countercoords = new List<ColumnTuple>();
            Dictionary<String, Object> dataline;
            Dictionary<Dictionary<String, Object>, String> control_values_to_update = new Dictionary<Dictionary<String, Object>, String>();
            List<ColumnTupleListWhere> marker_value_to_update = new List<ColumnTupleListWhere>();

            int id = 0;
            List<string> notenames = new List<string>();
            List<string> counternames = new List<string>();
            List<string> choicenames = new List<string>();

            // Create three lists, each one representing the datalabels (in order found in the template) of notes, counters and choices
            // We will use these to find the matching ones in the xml data table.
            for (int i = 0; i < template.Rows.Count; i++)
            {
                if (template.Rows[i][Constants.Database.Type].Equals(Constants.DatabaseColumn.Note))
                {
                    notenames.Add((string)template.Rows[i][Constants.Control.DataLabel]);
                }
                else if (template.Rows[i][Constants.Database.Type].Equals(Constants.DatabaseColumn.Counter))
                {
                    counternames.Add((string)template.Rows[i][Constants.Control.DataLabel]);
                }
                else if (template.Rows[i][Constants.Database.Type].Equals(Constants.DatabaseColumn.FixedChoice))
                {
                    choicenames.Add((string)template.Rows[i][Constants.Control.DataLabel]);
                }
            }

            foreach (XmlNode n in nodelist)
            {
                int idx;

                id++;
                dataline = new Dictionary<String, Object>(); // Populate the data 

                // File Field - We use the file name as a key into a particular database row. We don't change the database field as it is our key.
                filename = n[Constants.DatabaseColumn._File].InnerText;

                // If the Folder Path differs from where we had previously loaded it, 
                // warn the user that the new path will be substituted in its place

                // Folder - We are going to leave this field unchanged, so ignore it
                // string folder = n[FOLDER].InnerText);

                // Date - We use the original date, as the analyst may have adjusted them 
                date = n[Constants.DatabaseColumn._Date].InnerText;
                dataline.Add(Constants.DatabaseColumn.Date, date);

                // Date - We use the original time, although its almost certainly identical
                time = n[Constants.DatabaseColumn._Time].InnerText;
                dataline.Add(Constants.DatabaseColumn.Time, time);

                // We don't use the imagequality, as the new system may have altered how quality is determined (e.g., deleted files)
                // imagequality = n[IMAGEQUALITY].InnerText;
                // dataline.Add(Constants.IMAGEQUALITY, imagequality);
                // System.Diagnostics.Debug.Print("----" + filename + " " + date + " " + time + " " + imagequality);

                // Notes: Iterate through 
                idx = 0;
                innerNodeList = n.SelectNodes(Constants.DatabaseColumn.Note);
                foreach (XmlNode node in innerNodeList)
                {
                    dataline.Add(notenames[idx++], node.InnerText);
                }

                // Choices: Iterate through 
                idx = 0;
                innerNodeList = n.SelectNodes(Constants.DatabaseColumn.FixedChoice);
                foreach (XmlNode node in innerNodeList)
                {
                    dataline.Add(choicenames[idx++], node.InnerText);
                }

                // Counters: Iterate through  
                idx = 0;
                innerNodeList = n.SelectNodes(Constants.DatabaseColumn.Counter);

                // For each counter control 
                string where = String.Empty;
                foreach (XmlNode node in innerNodeList)
                {
                    // Add the value of each counter to the dataline 
                    XmlNodeList dataNode = node.SelectNodes(Constants.DatabaseColumn.Data);
                    dataline.Add(counternames[idx], dataNode[0].InnerText);

                    // For each counter, find the points associated with it and compose them together as x1,y1|x2,y2|...|xn,yn 
                    XmlNodeList pointNodeList = node.SelectNodes(Constants.DatabaseColumn.Point);
                    string countercoord = String.Empty;
                    foreach (XmlNode pnode in pointNodeList)
                    {
                        String x = pnode.SelectSingleNode(Constants.DatabaseColumn.X).InnerText;
                        if (x.Length > 5)
                        {
                            x = x.Substring(0, 5);
                        }
                        String y = pnode.SelectSingleNode(Constants.DatabaseColumn.Y).InnerText;
                        if (y.Length > 5)
                        {
                            y = y.Substring(0, 5);
                        }
                        countercoord += x + "," + y + "|";
                    }

                    // Remove the last "|" from the point list
                    if (!countercoord.Equals(String.Empty))
                    {
                        countercoord = countercoord.Remove(countercoord.Length - 1); // Remove the last "|"
                    }

                    // Countercoords will have a list of points (possibly empty) with each list entry representing a control
                    countercoords.Add(new ColumnTuple(counternames[idx], countercoord));
                    idx++;
                }
                // Add this update to the list of all updates for the Datatable, and then update it
                control_values_to_update.Add(dataline, Constants.DatabaseColumn.File + "='" + filename + "'");

                where = Constants.Database.ID + "='" + id.ToString() + "'";
                marker_value_to_update.Add(new ColumnTupleListWhere(countercoords, where));
                // imageDatabase.UpdateMarkersInRow(id, counternames, countercoords); // Update the marker table

                // Create a list of all updates for the MarkerTable, and then update it
                countercoords = new List<ColumnTuple>();
            }

            // Update the various tables in one fell swoop
            imageDatabase.UpdateImages(control_values_to_update);
            imageDatabase.UpdateMarkers(marker_value_to_update);
        }
    }
}
