using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carnassial.Data
{
    public class FileTableSpreadsheetMap
    {
        public int ClassificationIndex { get; set; }
        public int DateTimeIndex { get; set; }
        public int DeleteFlagIndex { get; set; }
        public int FileNameIndex { get; set; }
        public int RelativePathIndex { get; set; }
        public int UtcOffsetIndex { get; set; }

        public List<int> UserChoiceDataIndices { get; private set; }
        public List<int> UserChoiceIndices { get; private set; }
        public List<FileTableUserColumn> UserChoices { get; private set; }
        public List<List<string>> UserChoiceValues { get; private set; }
        public List<int> UserCounterIndices { get; private set; }
        public List<FileTableUserColumn> UserCounters { get; private set; }
        public List<int> UserFlagIndices { get; private set; }
        public List<FileTableUserColumn> UserFlags { get; private set; }
        public List<int> UserMarkerIndices { get; private set; }
        public List<int> UserNoteDataIndices { get; private set; }
        public List<int> UserNoteIndices { get; private set; }
        public List<FileTableUserColumn> UserNotes { get; private set; }

        public FileTableSpreadsheetMap()
        {
            this.ClassificationIndex = -1;
            this.DateTimeIndex = -1;
            this.DeleteFlagIndex = -1;
            this.FileNameIndex = -1;
            this.RelativePathIndex = -1;
            this.UtcOffsetIndex = -1;

            this.UserChoiceDataIndices = new List<int>();
            this.UserChoiceIndices = new List<int>();
            this.UserChoices = new List<FileTableUserColumn>();
            this.UserChoiceValues = new List<List<string>>();

            this.UserCounterIndices = new List<int>();
            this.UserCounters = new List<FileTableUserColumn>();
            this.UserFlagIndices = new List<int>();
            this.UserFlags = new List<FileTableUserColumn>();
            this.UserMarkerIndices = new List<int>();

            this.UserNoteDataIndices = new List<int>();
            this.UserNoteIndices = new List<int>();
            this.UserNotes = new List<FileTableUserColumn>();
        }
    }
}
