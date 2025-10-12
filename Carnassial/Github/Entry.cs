using System;
using System.Xml;

namespace Carnassial.Github
{
    internal class Entry : XmlSerializable
    {
        public string? ID { get; private set; }

        public Entry(XmlReader reader)
        {
            this.ReadXml(reader);
        }

        public Version GetVersion()
        {
            if (String.IsNullOrWhiteSpace(this.ID))
            {
                throw new NotSupportedException($"{nameof(this.ID)} property is null or whitespace.");
            }

            int vIndex = this.ID.LastIndexOf('v'); // could also use last slash
            if ((vIndex == -1) || (vIndex == this.ID.Length))
            {
                throw new NotSupportedException($"{nameof(this.ID)} is '{this.ID}', which does not end in a version number.");
            }

            return Version.Parse(this.ID[(vIndex + 1)..]);
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (String.Equals(reader.Name, "id", StringComparison.Ordinal))
            {
                this.ID = reader.ReadElementContentAsString();
            }
            else
            {
                // for now, ignore everything besides ID element
                reader.Read();
            }
        }
    }
}
