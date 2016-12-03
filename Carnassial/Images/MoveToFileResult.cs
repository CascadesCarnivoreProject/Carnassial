namespace Carnassial.Images
{
    public class MoveToFileResult
    {
        public bool NewFileToDisplay { get; set; }
        public bool Succeeded { get; set; }

        public MoveToFileResult()
        {
            this.NewFileToDisplay = false;
            this.Succeeded = false;
        }

        public MoveToFileResult(bool newFileToDisplay)
        {
            this.NewFileToDisplay = newFileToDisplay;
            this.Succeeded = true;
        }
    }
}
