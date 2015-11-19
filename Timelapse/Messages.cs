using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace Timelapse
{
    // A convenience class just to let us print out different kinds of stock  messages in dialog boxes
    class Messages
    {

        // While we read in the images from the XML file,there weren't acually any images in it!. 
        public static void NoImagesInXMLFile(string filename, string path)
        {
            string messageBoxText = "No images were found in the file '" + filename + "'in your image folder:\n" + path + ".\n\n";
            messageBoxText += "Likely cause: There could be something wrong with that file, or it was created when no images were in that folder\n";
            messageBoxText += "Remedy: Check the ImageData.xml file in your image directory.\n";
            messageBoxText += "Or just remove that file, although it does mean your previous coding data (if any) will be lost.";
            string caption = "No Images found!";
            MessageBoxButton button = MessageBoxButton.OK;
            MessageBoxImage icon = MessageBoxImage.Error;
            MessageBoxResult result = MessageBox.Show(messageBoxText, caption, button, icon);
        }

        // Can't open the excel file
        public static void CantWriteExcelFile(string path)
        {
            string messageBoxText = "We can't write the spreadsheet: " + path + ".\n\n";
            messageBoxText += "Likely cause: you may already have it open in Excel.\n";
            messageBoxText += "Remedy: Close the file in Excel and try again.\n";
            string caption = "Can't write the spreadsheet file!";
            MessageBoxButton button = MessageBoxButton.OK;
            MessageBoxImage icon = MessageBoxImage.Error;
            MessageBoxResult result = MessageBox.Show(messageBoxText, caption, button, icon);
        }

        // Can't export the emage
        public static void CantExportThisImage()
        {
            string messageBoxText = "We can't export the currently displayed image.\n\n";
            messageBoxText += "Likely cause: It is a corrupted or missing image.\n";
            messageBoxText += "Remedy: Navigate to a valid image.\n";
            string caption = "Can't export this image!";
            MessageBoxButton button = MessageBoxButton.OK;
            MessageBoxImage icon = MessageBoxImage.Error;
            MessageBoxResult result = MessageBox.Show(messageBoxText, caption, button, icon);
        }


        // Ask the user which folder name should be used if the two differ
        public static MessageBoxResult UseNewFolderNameIfTheyDiffer(string thisfolder, string recordedfolder)
        {
            string messageBoxText = "Which folder name should we use?\n\n";
            messageBoxText += thisfolder + " is the folder name you just opened\n";
            messageBoxText += recordedfolder + " is the folder name where these  images were last analyzed.\n";
            messageBoxText += "The two are different.\n\n";
            messageBoxText += "Likely cause: You probably renamed the folder since its images were last analyzed.\n";
            messageBoxText += "Solution:\n";
            messageBoxText += "  Yes to use the new folder name " + thisfolder + ", or \n";
            messageBoxText += "  No to keep using the old folder name " + recordedfolder;
            string caption = "Which folder name should we use?";
            MessageBoxButton button = MessageBoxButton.YesNo;
            MessageBoxImage icon = MessageBoxImage.Question;
            return (MessageBox.Show(messageBoxText, caption, button, icon));
        }

        // Can't swap the day or month
        public static void CantSwapDayMonth()
        {
            string messageBoxText = "We can't swap the day or month.\n\n";
            messageBoxText += "Likely cause:\n";
            messageBoxText += " - One of the current date values is > 12.\n";
            messageBoxText += "   Changing it to a month would produce a non-existant month.\n\n";
            messageBoxText += "Remedy: You will likely have to set the dates manually.\n";
            string caption = "Can't swap the day or month!";
            MessageBoxButton button = MessageBoxButton.OK;
            MessageBoxImage icon = MessageBoxImage.Error;
            MessageBoxResult result = MessageBox.Show(messageBoxText, caption, button, icon);
        }

        // Ask the user if they really want to swap the dates
        public static MessageBoxResult SwapDayMonth()
        {
            string messageBoxText = "Timelapse always shows the date in Day / Month order.\n";
            messageBoxText += "Yet some cameras record dates in reverse, in Month /Day order.\n\n";
            messageBoxText += "If you continue, Timelapse will swap the day / month number values for every image.\n\n";
            messageBoxText += "For example, a date currently set to 05-Dec-2012) \n will be changed to 12-May-2012)\n\n";
            messageBoxText += "Confirm that this is what you want to do:\n";
            messageBoxText += "-       Ok  confirms swapping the day and month for all images, or \n";
            messageBoxText += "-   Cancel  keeps things as they are. " ;
            string caption = "Swap the month and date values in the Date field?";
            MessageBoxButton button = MessageBoxButton.OKCancel ;
            MessageBoxImage icon = MessageBoxImage.Question;
            return (MessageBox.Show(messageBoxText, caption, button, icon));
        }

        // Ask the user to confirm value propagation
        public static MessageBoxResult CopyCurrentValueToAll(String text, string number, bool checkForZero)
        {
            text = text.Trim();
            string messageBoxText;
            if (!checkForZero && text.Equals(""))
            {
                messageBoxText = "Clear this field across every image in this set";
            }
            else if (checkForZero && text.Equals("0"))
            {
                messageBoxText = "Zero this field across every image in this set ";
            } else {
                messageBoxText = "The value \u00AB" + text + "\u00BB will be copied from";
                messageBoxText += Environment.NewLine;
                messageBoxText += "\u2022 here to every image in this set"; ;
            }
            messageBoxText += Environment.NewLine + Environment.NewLine;
            messageBoxText += "What will happen:";
            messageBoxText += Environment.NewLine;
            messageBoxText += "\u2022 data values in those other images will be over-written";
            messageBoxText += Environment.NewLine;
            messageBoxText += "\u2022 " + number + " images will be affected";
           
            string caption = "Confirm Copying to All of this Field?";
            MessageBoxButton button = MessageBoxButton.YesNo;
            MessageBoxImage icon = MessageBoxImage.Question;
            return (MessageBox.Show(messageBoxText, caption, button, icon));
        }

        // Ask the user to confirm value propagation from the last value
        public static MessageBoxResult PropagateFromLastValue(String text, string numer_images_affected, string how_far_back)
        {
            text = text.Trim();
            string messageBoxText;
            messageBoxText = "The last non-empty value \u00AB" + text + "\u00BB was seen " + how_far_back + " images back";
            messageBoxText += Environment.NewLine;
            messageBoxText += "It will be copied from ";
            messageBoxText += Environment.NewLine;
            messageBoxText += "\u2022 " + how_far_back + " images back";
            messageBoxText += Environment.NewLine;
            messageBoxText += "\u2022 " + "up to this image in this set";
            messageBoxText += Environment.NewLine + Environment.NewLine;
            messageBoxText += "What will happen:";
            messageBoxText += Environment.NewLine;
            messageBoxText += "\u2022 data values in those other images will be over-written";
            messageBoxText += Environment.NewLine;
            messageBoxText += "\u2022 " + numer_images_affected + " images will be affected";

            string caption = "Confirm Propagating to this field?";
            MessageBoxButton button = MessageBoxButton.YesNo;
            MessageBoxImage icon = MessageBoxImage.Question;
            return (MessageBox.Show(messageBoxText, caption, button, icon));
        }

        // Ask the user to confirm value propagation from the last value
        public static MessageBoxResult CopyForward(String text, string number, bool checkForZero)
        {
            text = text.Trim();
            string messageBoxText;
            if (!checkForZero && text.Equals(""))
            {
                messageBoxText = "Clear this field from:";
            }
            else if (checkForZero && text.Equals("0"))
            {
                messageBoxText = "Zero this field from:";
            }
            else {
                messageBoxText = "The value \u00AB" + text + "\u00BB will be copied forward from";
            }
            messageBoxText += Environment.NewLine;
            messageBoxText += "\u2022 here to the last image in this set"; ;
            messageBoxText += Environment.NewLine + Environment.NewLine;
            messageBoxText += "What will happen:";
            messageBoxText += Environment.NewLine;
            messageBoxText += "\u2022 data values in those other images will be over-written";
            messageBoxText += Environment.NewLine;
            messageBoxText += "\u2022 " + number + " images will be affected";
            string caption = "Confirm Copy Forward of this field?";
            MessageBoxButton button = MessageBoxButton.YesNo;
            MessageBoxImage icon = MessageBoxImage.Question;
            return (MessageBox.Show(messageBoxText, caption, button, icon));
        }
        
        // Related to above; nothing to propagate
        public static MessageBoxResult PropagateNothingToPropagate()
        {
            string messageBoxText = "None of the earlier images have anything in this field,";
            messageBoxText += Environment.NewLine;
            messageBoxText += "so there is nothing to propagate.";
            string caption = "Nothing to do!";
            MessageBoxButton button = MessageBoxButton.OK;
            MessageBoxImage icon = MessageBoxImage.Information;
            return MessageBox.Show(messageBoxText, caption, button, icon);
        }

        // Related to above; nothing to propagate
        public static MessageBoxResult PropagateNothingToCopyForward()
        {
            string messageBoxText = "We are already on the last image,";
            messageBoxText += Environment.NewLine;
            messageBoxText += "so there is nothing to copy forward.";
            string caption = "Nothing to do!";
            MessageBoxButton button = MessageBoxButton.OK;
            MessageBoxImage icon = MessageBoxImage.Information;
            return MessageBox.Show(messageBoxText, caption, button, icon);
        }

        // Ask if you really want to swap the data in the controls labeled label1 and label 2 with each other
        public static MessageBoxResult DoYouWantToSwapData(string label1, string label2)
        {
            string messageBoxText = "This operation will swap all the data across all images across these two controls:\n";
            messageBoxText += " - " + label1 + " with " + label2 + "\n\n";
            messageBoxText += "The result will be that the data appearing under " + label1 + " will now appear under ";

            messageBoxText += label2 + " and vice-versa. \n\n" ;
            messageBoxText += "Is this what you really want to do?\n";
            messageBoxText += "  Yes to swap the two, or \n";
            messageBoxText += "  No to keep things as they are";
            string caption = "Do you really want to swap your data in " + label1 + " with " + " label2\n\n";;
            MessageBoxButton button = MessageBoxButton.YesNo;
            MessageBoxImage icon = MessageBoxImage.Question;
            return (MessageBox.Show(messageBoxText, caption, button, icon));
        }

        // Related to above; nothing to propagate
        public static MessageBoxResult Exception(string message)
        {
            string messageBoxText = "Timelapse encountered a serious problem and is shutting down because: " + message;
            string caption = "Timelapse encountered a serious problem";
            MessageBoxButton button = MessageBoxButton.OK;
            MessageBoxImage icon = MessageBoxImage.Error;
            return MessageBox.Show(messageBoxText, caption, button, icon);
        }
    }
}
