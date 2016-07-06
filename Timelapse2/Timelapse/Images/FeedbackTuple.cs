namespace Timelapse.Images
{
    public class FeedbackTuple
    {
        public string FileName { get; set; }
        public string Message { get; set; }

        public FeedbackTuple(string fileName, string message)
        {
            this.FileName = fileName;
            this.Message = message;
        }
    }
}
