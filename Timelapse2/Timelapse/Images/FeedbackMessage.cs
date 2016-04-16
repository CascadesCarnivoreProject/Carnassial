namespace Timelapse.Images
{
    // A class that tracks our progress as we load the images
    public class FeedbackMessage
    {
        public string ImageName { get; set; }
        public string Message { get; set; }

        public FeedbackMessage(string imageName, string message)
        {
            this.ImageName = imageName;
            this.Message = message;
        }
    }
}
