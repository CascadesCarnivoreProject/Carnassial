using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Data;
namespace Timelapse
{
    class ImageDataXML
    {
        public static void ImportIntoDatabase()
        {
        }
        #region constants
 
        // XML Labels
        const string SLASH = "/";
        const string IMAGES = "Images";             // There are multiple images
        const string LOG = "Log";                   // String holding a user-created text log
        const string IMAGE = "Image";               // A single image and its associated data
        const string DATA = "Data";                 // the data describing the attributes of that code

        // Paths to standard elements, always included but not always made visible
        const string FILE = "_File";        
        const string FOLDER = "_Folder";
        const string DATE = "_Date";
        const string TIME = "_Time";
        const string IMAGEQUALITY = "_ImageQuality";

        const string NOTE = "Note";                 // A note
        const string FIXEDCHOICE = "FixedChoice";   // a fixed choice

        const string COUNTER = "Counter";           // a counter
        const string POINTS = "Points";             // There may be multiple points per counter
        const string POINT = "Point";               // a single point
        const string X = "X";                       // Every point has an X and Y
        const string Y = "Y";                       
        #endregion

        // Read all the data into the imageData structure from the XML file in the filepath.
        // Note that we need to know the code controls,as we have to associate any points read in with a particular counter control
        
        public static void Read(string filePath, DataTable template, DBData dbData)
        {
            // XML Preparation
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(filePath);

            // Import the old log (if any)
            XmlNodeList nodeLog = xmlDoc.SelectNodes(IMAGES + SLASH + LOG);
            if (nodeLog.Count > 0)
            {
                XmlNode nlog = nodeLog[0];
                dbData.Log = nlog.InnerText;
            }

            XmlNodeList nodelist = xmlDoc.SelectNodes(IMAGES + SLASH + IMAGE);
            XmlNodeList innerNodeList;
          
            string filename = "";
            string date;
            string time;

            List<string> counts = new List<string>();
            ColumnTupleList countercoords = new ColumnTupleList();
            Dictionary<String, Object> dataline;
            Dictionary<Dictionary<String, Object>, String> control_values_to_update = new Dictionary<Dictionary<String, Object>, String>();
            List<ColumnTupleListWhere> marker_value_to_update = new List<ColumnTupleListWhere>();

            int id = 0;
            List<string> notenames = new List<string>();
            List<string> counternames = new List<string>();
            List<string> choicenames = new List<string>();


            // Create three lists, each one representing the datalabels (in order found in the template) of notes, counters and choices
            // We will use these to find the matching ones in the xml data table.
            for (int i = 0; i < template.Rows.Count; i++ )
            {
                if (template.Rows[i][Constants.TYPE].Equals (Constants.NOTE) )
                {
                    notenames.Add((string) template.Rows[i][Constants.DATALABEL]);
                }
                else if  (template.Rows[i][Constants.TYPE].Equals (Constants.COUNTER) )
                {
                    counternames.Add((string) template.Rows[i][Constants.DATALABEL]);
                }
                else if (template.Rows[i][Constants.TYPE].Equals(Constants.FIXEDCHOICE))
                {
                    choicenames.Add((string)template.Rows[i][Constants.DATALABEL]);
                }
            }
            
            foreach (XmlNode n in nodelist)
            {
                int idx;

                id++;
                dataline = new Dictionary<String, Object>(); // Populate the data 

                // File Field - We use the file name as a key into a particular database row. We don't change the database field as it is our key.
                filename = n[FILE].InnerText;

                // If the Folder Path differs from where we had previously loaded it, 
                // warn the user that the new path will be substituted in its place

                // Folder - We are going to leave this field unchanged, so ignore it
                //string folder = n[FOLDER].InnerText);

                // Date - We use the original date, as the analyst may have adjusted them 
                date = n[DATE].InnerText;
                dataline.Add(Constants.DATE, date);

                // Date - We use the original time, although its almost certainly identical
                time = n[TIME].InnerText;
                dataline.Add(Constants.TIME, time);

                // We don't use the imagequality, as the new system may have altered how quality is determined (e.g., deleted files)
                // imagequality = n[IMAGEQUALITY].InnerText;
                // dataline.Add(Constants.IMAGEQUALITY, imagequality);
                // System.Diagnostics.Debug.Print("----" + filename + " " + date + " " + time + " " + imagequality);

                // Notes: Iterate through 
                idx = 0; 
                innerNodeList = n.SelectNodes(NOTE);
                foreach (XmlNode node in innerNodeList)
                {
                    dataline.Add(notenames[idx++], node.InnerText);
                }

                // Choices: Iterate through 
                idx = 0;
                innerNodeList = n.SelectNodes(FIXEDCHOICE);
                foreach (XmlNode node in innerNodeList)
                {
                    dataline.Add(choicenames[idx++], node.InnerText);
                }


                // Counters: Iterate through  
                idx = 0;
                innerNodeList = n.SelectNodes(COUNTER);

                // For each counter control 
                string where = "";
                foreach (XmlNode node in innerNodeList)
                {
                    // Add the value of each counter to the dataline 
                    XmlNodeList dataNode = node.SelectNodes(DATA);
                    dataline.Add(counternames[idx], dataNode[0].InnerText);

                    // For each counter, find the points associated with it and compose them together as x1,y1|x2,y2|...|xn,yn 
                    XmlNodeList pointNodeList = node.SelectNodes(POINT);
                    string countercoord = "";
                    foreach (XmlNode pnode in pointNodeList)
                    {
                        String x = pnode.SelectSingleNode(X).InnerText;
                        if (x.Length > 5) x = x.Substring(0, 5);
                        String y = pnode.SelectSingleNode(Y).InnerText;
                        if (y.Length > 5) y = y.Substring(0, 5);
                        countercoord += x + "," + y + "|";
                    }

                    // Remove the last "|" from the point list
                    if (!countercoord.Equals(""))  countercoord = countercoord.Remove(countercoord.Length - 1); // Remove the last "|"

                    // Countercoords will have a list of points (possibly empty) with each list entry representing a control
                    countercoords.Add(new ColumnTuple (counternames[idx], countercoord));
                    idx++;
                }
                // Add this update to the list of all updates for the Datatable, and then update it
                control_values_to_update.Add(dataline, Constants.FILE + "='" + filename + "'");

                where = Constants.ID + "='" + id.ToString() + "'";
                marker_value_to_update.Add(new ColumnTupleListWhere (countercoords, where));
                //dbData.UpdateMarkersInRow (id, counternames, countercoords); // Update the marker table

                // Create a list of all updates for the MarkerTable, and then update it

                countercoords = new ColumnTupleList ();
            }
            // Update the various tables in one fell swoop
            dbData.RowsUpdateRowsFromFilenames(control_values_to_update);
            dbData.RowsUpdateMarkerRows(marker_value_to_update);
            dbData.UpdateMarkersInRows(marker_value_to_update);
        }
    }
}
