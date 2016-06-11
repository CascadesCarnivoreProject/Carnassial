using System;

namespace Timelapse.Database
{
    /// <summary>
    /// Class SearchTerms stores the search term expressions for each field
    /// </summary>
    public class SearchTerm
    {
        public string DataLabel { get; set; }
        public string Expression { get; set; }
        public string Label { get; set; }
        public string List { get; set; }
        public string Type { get; set; }
        public bool UseForSearching { get; set; }
        public string Value { get; set; }

        public SearchTerm()
        {
            this.DataLabel = String.Empty;
            this.Expression = String.Empty;
            this.Label = String.Empty;
            // TODOSAUL: should this.List not follow pattern by defaulting to null?
            this.Type = String.Empty;
            this.UseForSearching = false;
            this.Value = String.Empty;
        }
    }
}
