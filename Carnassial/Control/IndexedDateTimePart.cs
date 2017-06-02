namespace Carnassial.Control
{
    internal class IndexedDateTimePart
    {
        public char Format { get; set; }
        public int FormatStart { get; set; }
        public int FormatLength { get; set; }
        public bool IsSelectionLengthVariable { get; set; }
        public bool IsSelectionStartVariable { get; set; }
        public int SelectionLength { get; set; }
        public int SelectionStart { get; set; }

        public IndexedDateTimePart(char format, int formatStart, int selectionStart)
        {
            this.Format = format;
            this.FormatStart = formatStart;
            this.FormatLength = 1;
            this.IsSelectionLengthVariable = false;
            this.IsSelectionStartVariable = false;
            this.SelectionLength = 1;
            this.SelectionStart = selectionStart;
        }
    }
}
