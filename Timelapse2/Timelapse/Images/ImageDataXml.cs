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
        // TODOSAUL: support periodic insert of chunks rather than one large block?
        public static void Read(string filePath, ImageDatabase imageDatabase)
        {
            // XML Preparation
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(filePath);

            // Import the old log (if any)
            XmlNodeList nodeLog = xmlDoc.SelectNodes(Constants.ImageXml.Images + Constants.ImageXml.Slash + Constants.DatabaseColumn.Log);
            if (nodeLog.Count > 0)
            {
                XmlNode nlog = nodeLog[0];
                imageDatabase.SetImageSetLog(nlog.InnerText);
            }

            // Create three lists, each one representing the datalabels (in order found in the template) of notes, counters and choices
            // We will use these to find the matching ones in the xml data table.
            List<string> noteControlNames = new List<string>();
            List<string> counterControlNames = new List<string>();
            List<string> choiceControlNames = new List<string>();
            for (int control = 0; control < imageDatabase.TemplateTable.Rows.Count; control++)
            {
                string dataLabel = imageDatabase.TemplateTable.Rows[control].GetStringField(Constants.Control.DataLabel);
                switch (imageDatabase.TemplateTable.Rows[control].GetStringField(Constants.Control.Type))
                {
                    case Constants.Control.Counter:
                        counterControlNames.Add(dataLabel);
                        break;
                    case Constants.Control.FixedChoice:
                        choiceControlNames.Add(dataLabel);
                        break;
                    case Constants.Control.Note:
                        noteControlNames.Add(dataLabel);
                        break;
                    default:
                        // TODOSAUL: why no support for flag controls?
                        break;
                }
            }

            XmlNodeList nodeList = xmlDoc.SelectNodes(Constants.ImageXml.Images + Constants.ImageXml.Slash + Constants.DatabaseColumn.Image);
            int imageID = 0;
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            List<ColumnTuplesWithWhere> markersToUpdate = new List<ColumnTuplesWithWhere>();
            foreach (XmlNode node in nodeList)
            {
                imageID++;

                List<ColumnTuple> columnsToUpdate = new List<ColumnTuple>(); // Populate the data 
                // File Field - We use the file name as a key into a particular database row. We don't change the database field as it is our key.
                string imageFileName = node[Constants.ImageXml.File].InnerText;

                // If the Folder Path differs from where we had previously loaded it, 
                // warn the user that the new path will be substituted in its place

                // Folder - this field is left unchanged but, since an image ID is not available here, is used to form a unique identifier to
                // constrain which image is updated
                string imageFolder = node[Constants.DatabaseColumn.Folder].InnerText;

                // Date - We use the original date, as the analyst may have adjusted them 
                string date = node[Constants.ImageXml.Date].InnerText;
                columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Date, date));

                // Date - We use the original time, although its almost certainly identical
                string time = node[Constants.ImageXml.Time].InnerText;
                columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Time, time));

                // We don't use the imagequality, as the new system may have altered how quality is determined (e.g., deleted files)
                // imagequality = n[IMAGEQUALITY].InnerText;
                // dataline.Add(Constants.IMAGEQUALITY, imagequality);
                // System.Diagnostics.Debug.Print("----" + filename + " " + date + " " + time + " " + imagequality);

                // Notes: Iterate through 
                int innerNodeIndex = 0;
                XmlNodeList innerNodeList = node.SelectNodes(Constants.Control.Note);
                foreach (XmlNode innerNode in innerNodeList)
                {
                    columnsToUpdate.Add(new ColumnTuple(noteControlNames[innerNodeIndex++], innerNode.InnerText));
                }

                // Choices: Iterate through 
                innerNodeIndex = 0;
                innerNodeList = node.SelectNodes(Constants.Control.FixedChoice);
                foreach (XmlNode innerNode in innerNodeList)
                {
                    columnsToUpdate.Add(new ColumnTuple(choiceControlNames[innerNodeIndex++], innerNode.InnerText));
                }

                // Counters: Iterate through  
                List<ColumnTuple> counterCoordinates = new List<ColumnTuple>();
                innerNodeIndex = 0;
                innerNodeList = node.SelectNodes(Constants.Control.Counter);
                string where = String.Empty;
                foreach (XmlNode innerNode in innerNodeList)
                {
                    // Add the value of each counter to the dataline 
                    XmlNodeList dataNode = innerNode.SelectNodes(Constants.DatabaseColumn.Data);
                    columnsToUpdate.Add(new ColumnTuple(counterControlNames[innerNodeIndex], dataNode[0].InnerText));

                    // For each counter, find the points associated with it and compose them together as x1,y1|x2,y2|...|xn,yn 
                    XmlNodeList pointNodeList = innerNode.SelectNodes(Constants.DatabaseColumn.Point);
                    string countercoord = String.Empty;
                    foreach (XmlNode pointNode in pointNodeList)
                    {
                        String x = pointNode.SelectSingleNode(Constants.DatabaseColumn.X).InnerText;
                        if (x.Length > 5)
                        {
                            x = x.Substring(0, 5);
                        }
                        String y = pointNode.SelectSingleNode(Constants.DatabaseColumn.Y).InnerText;
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
                    counterCoordinates.Add(new ColumnTuple(counterControlNames[innerNodeIndex], countercoord));
                    innerNodeIndex++;
                }

                // add this image's updates to the update lists
                ColumnTuplesWithWhere imageToUpdate = new ColumnTuplesWithWhere(columnsToUpdate);
                imageToUpdate.SetWhere(imageFolder, null, imageFileName);
                imagesToUpdate.Add(imageToUpdate);

                ColumnTuplesWithWhere markerToUpdate = new ColumnTuplesWithWhere(counterCoordinates, imageID);
                markersToUpdate.Add(markerToUpdate);
            }

            // batch update both tables
            imageDatabase.UpdateImages(imagesToUpdate);
            imageDatabase.UpdateMarkers(markersToUpdate);
        }
    }
}
