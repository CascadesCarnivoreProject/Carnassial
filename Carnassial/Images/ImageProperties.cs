using Carnassial.Data;
using System;

namespace Carnassial.Images
{
    public class ImageProperties
    {
        public double Coloration { get; private set; }
        public double Luminosity { get; private set; }
        public MetadataReadResult MetadataResult { get; set; }

        public ImageProperties(MetadataReadResult metadataResult)
        {
            this.MetadataResult = metadataResult;
            this.Coloration = -1.0;
            this.Luminosity = -1.0;
        }

        public ImageProperties(double luminosity, double coloration)
        {
            this.MetadataResult = MetadataReadResult.None;
            this.Coloration = coloration;
            this.Luminosity = luminosity;
        }

        public bool CanClassify
        {
            get { return (this.Coloration >= 0.0) && (this.Luminosity >= 0.0); }
        }

        public FileSelection EvaluateNewClassification(double darkLuminosityThreshold)
        {
            if (this.CanClassify == false)
            {
                switch (this.MetadataResult)
                {
                    case MetadataReadResult.Failed:
                        return FileSelection.Corrupt;
                    case MetadataReadResult.None:
                        return FileSelection.NoLongerAvailable;
                    default:
                        throw new NotSupportedException("Unhandled metadata result " + this.MetadataResult.ToString());
                }
            }

            // a truly greyscale pixel has r = g = b but allow for some color cast
            // Color cast in greyscale can result from
            // - pick up of manufacturer logos or info bar color not excluded in the skip above
            // - jpeg quantization
            // - not quite greyscale output from the camera
            if ((this.Coloration < Constant.Images.GreyscaleColorationThreshold) &&
                (this.Luminosity < darkLuminosityThreshold))
            {
                // color images are never considered to be dark
                return FileSelection.Dark;
            }
            return FileSelection.Ok;
        }

        public string GetClassificationDescription()
        {
            if (this.CanClassify == false)
            {
                return "File could not be loaded because it is missing or corrupt.";
            }

            if (this.Coloration >= Constant.Images.GreyscaleColorationThreshold)
            {
                return "File is in color and therefore not dark.";
            }
            return String.Format("Normalized brightness is {0:P1}.", this.Luminosity);
        }
    }
}
