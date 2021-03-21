using MetadataExtractor;
using System;
using System.Collections.Generic;
using MetadataDirectory = MetadataExtractor.Directory;

namespace Carnassial.Images
{
    public static class IReadOnlyListExtensions
    {
        public static bool TryGetMetadataValue(this IReadOnlyList<MetadataDirectory> metadata, Tag metadataField, out string? metadataValue)
        {
            foreach (MetadataDirectory directory in metadata)
            {
                if (String.Equals(directory.Name, metadataField.DirectoryName, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (Tag tag in directory.Tags)
                    {
                        if (String.Equals(tag.Name, metadataField.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            metadataValue = tag.Description;
                            return true;
                        }
                    }
                }
            }

            metadataValue = null;
            return false;
        }
    }
}
