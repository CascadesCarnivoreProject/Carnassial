using System;

namespace Timelapse.Database
{
    /// <summary>
    /// A SearchTerm stores the search criteria for each column
    /// </summary>
    public class SearchTerm
    {
        public string DataLabel { get; set; }
        public string Label { get; set; }
        public string List { get; set; }
        public string Operator { get; set; }
        public string Type { get; set; }
        public bool UseForSearching { get; set; }
        public string Value { get; set; }

        public SearchTerm()
        {
            // TODOSAUL: can these be made consistent with this.List by defaulting to null?
            this.DataLabel = String.Empty;
            this.Label = String.Empty;
            this.Operator = String.Empty;
            this.Type = String.Empty;
            this.UseForSearching = false;
            this.Value = String.Empty;
        }
    }
}
