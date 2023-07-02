using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Carnassial.Github
{
    internal abstract class XmlSerializable : IXmlSerializable
    {
        XmlSchema? IXmlSerializable.GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            if (reader.IsEmptyElement)
            {
                this.ReadStartElement(reader);
            }
            else
            {
                using XmlReader elementReader = reader.ReadSubtree();
                while (elementReader.EOF == false)
                {
                    if (elementReader.IsStartElement())
                    {
                        this.ReadStartElement(elementReader);
                    }
                    else
                    {
                        elementReader.Read();
                    }
                }
            }
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            throw new NotImplementedException();
        }

        protected abstract void ReadStartElement(XmlReader reader);
    }
}
