using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml.Serialization;

namespace INVFFID.Utility.Lib.RealTimeDataLib
{
    public class DataFormatter
    {
        private Type dataType;

        public Type DataType
        {
            get { return dataType; }
        }

        public DataFormatter(Type dataType)
        {
            this.dataType = dataType;
        }

        public byte[] Format(object dataObject)
        {
            return Format(dataType, dataObject);
        }

        public byte[] FormattedMessage(string msg)
        {
            return this.Format(typeof(string), msg);
        }

        private byte[] Format(Type type, object obj)
        {
            XmlSerializer formatter = new XmlSerializer(type);
            MemoryStream serializedObjectStream = new MemoryStream();
            formatter.Serialize(serializedObjectStream, obj);

            // Append message delimiter tailer byte
            byte[] dataBytes = new byte[] { (byte)Protocol.MessageDelimiter };
            serializedObjectStream.Write(dataBytes, 0, dataBytes.Length);

            return serializedObjectStream.ToArray();
        }
    }
}
