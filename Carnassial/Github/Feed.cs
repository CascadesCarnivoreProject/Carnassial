using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Carnassial.Github
{
    internal class Feed
    {
        public List<Entry> Entries { get; private init; }

        public Feed(string xml, int maxEntries)
        {
            this.Entries = new List<Entry>(maxEntries);

            using StringReader stringReader = new(xml);
            using XmlReader reader = XmlTextReader.Create(stringReader);
            reader.MoveToContent();

            while (reader.EOF == false)
            {
                if (reader.IsStartElement())
                {
                    if (String.Equals(reader.Name, "entry", StringComparison.Ordinal))
                    {
                        Entry entry = new(reader);
                        this.Entries.Add(entry);
                        if (this.Entries.Count == maxEntries)
                        {
                            break;
                        }
                    }
                    else
                    {
                        // for now, ignore everything besides release entries
                        reader.Read();
                    }
                }
                else
                {
                    reader.Read();
                }
            }
        }
    }
}