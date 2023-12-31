namespace Carnassial.Images
{
    public class MoveToFileResult
    {
        public bool NewFileToDisplay { get; private init; }
        public bool Succeeded { get; private init; }

        public MoveToFileResult(bool newFileToDisplay)
            : this(newFileToDisplay, true)
        {
        }

        public MoveToFileResult(bool newFileToDisplay, bool succeeded) 
        {
            this.NewFileToDisplay = newFileToDisplay;
            this.Succeeded = succeeded;
        }
    }
}
