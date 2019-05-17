using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.IO;

namespace INVFFID.Utility.Lib.RealTimeDataLib
{
    public class DataParser
    {
        private Type dataType;

        public Type DataType
        {
            get { return dataType; }
        }

        public DataParser(Type dataType)
        {
            this.dataType = dataType;
        }

        public object Parse(byte[] dataBytes)
        {
            try
            {
                // Parse 看看是不是結束連線(Bye)訊息(如果不是會造成Exception 拋出)
                XmlSerializer byeParser = new XmlSerializer(typeof(Bye));
                object byeObject = byeParser.Deserialize(new MemoryStream(dataBytes));

                return byeObject;
            }
            catch (Exception e)
            {
                try
                {
                    // Parse 看看是不是string 型態資料(如果不是會造成Exception 拋出)
                    XmlSerializer stringParser = new XmlSerializer(typeof(string));
                    object byeObject = stringParser.Deserialize(new MemoryStream(dataBytes));

                    return byeObject;
                }
                catch (Exception ex)
                {
                    try
                    {
                        // Parse 成此Parser 被指定的DataType 型態的物件
                        return (new XmlSerializer(dataType).Deserialize(new MemoryStream(dataBytes)));
                    }
                    catch (Exception exc)
                    {
                        // 無法Parse
                        throw new Exception("將資料位元還原成" + dataType.Name + " 物件時發生" + exc.GetType().Name + ": " + exc.Message);
                    }
                }
            }
        }
    }
}
