using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml.Serialization;

namespace RealTimeDataLib
{
    public class Protocol
    {
        public const char MessageDelimiter = (char)((byte)255);

        public static Encoding Big5Encoding
        {
            get
            {
                return Encoding.GetEncoding("Big5");
            }
        }

        public static byte[] Bye
        {
            get
            {
                XmlSerializer formatter = new XmlSerializer(typeof(Bye));
                MemoryStream msBye = new MemoryStream();
                formatter.Serialize(msBye, new Bye());

                // Append message delimiter tailer byte
                byte[] dataBytes = new byte[] { (byte)Protocol.MessageDelimiter };
                msBye.Write(dataBytes, 0, dataBytes.Length);

                return msBye.ToArray();
            }
        }

        public static bool IsIgnorableSocketError(int ErrorCode)
        {
            //10053: 連線已被您主機上的軟體中止

            //10054: 遠端主機已強制關閉一個現存的連線
            //10004: 中止操作被 WSACancelBlockingCall 呼叫打斷
            //10058: 不允許傳送或接收資料的要求，因為通訊端已經被先前的關機呼叫關閉

            if (ErrorCode == 10053 ||
                ErrorCode == 10054 ||
                ErrorCode == 10004 ||
                ErrorCode == 10058)
            {
                return true;
            }

            return false;
        }
    }

    public class Bye { }
}
