using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace INVFFID.Utility.Lib.RealTimeDataLib
{
    #region Delegates
    public delegate void DataReceiverConnectedDelegate(string dataSrcIp, int dataSrcPort);
    public delegate void DataReceiverDisconnectedDelegate(string dataSrcIp, int dataSrcPort);
    public delegate void DataReceiverExceptionDelegate(DataClient sender, Exception exc);
    public delegate void DataReceiverStopped(DataClient sender);
    #endregion

    public class DataClient
    {
        #region 成員變數
        private string serverHost;
        private int serverPort;
        private Socket dataServerConnection;
        private DataParser dataParser;
        private DataFormatter dataFormatter;
        private bool stopped;

        public bool Stopped
        {
            get { return stopped; }
        }
        #endregion

        #region 公用屬性

        public string ServerHost
        {
            get { return serverHost; }
            set { serverHost = value; }
        }

        public int ServerPort
        {
            get { return serverPort; }
            set { serverPort = value; }
        }

        public bool Connected
        {
            get 
            {
                return dataServerConnection == null ? false : dataServerConnection.Connected;
            }
        }

        public DataFormatter DataFormatter
        {
            get { return dataFormatter; }
            set { dataFormatter = value; }
        }

        public DataParser DataParser
        {
            get { return dataParser; }
            set { dataParser = value; }
        }
        #endregion

        #region 成員事件
        public event DataReceiverConnectedDelegate OnConnected;
        public event DataReceiverDisconnectedDelegate OnDisconnected;
        public event DataReceiverExceptionDelegate OnException;
        public event DataReceiverStopped OnStopped;
        public event DataReceivedDelegate OnDataReceived;
        public event MessageReceivedDelegate OnMessageReceived;
        #endregion

        public DataClient(string host, int port, DataParser parser, DataFormatter formatter)
        {
            this.DataParser = parser;
            this.DataFormatter = formatter;

            serverHost = host;
            serverPort = port;
            dataServerConnection = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Start()
        {
            AsyncOperationDelegate asyncDel = new AsyncOperationDelegate(ConnectToServer);
            asyncDel.BeginInvoke(new AsyncCallback(EndConnect), asyncDel);
        }

        private void ConnectToServer()
        {
            byte[] buff = new byte[128];
            byte[] tempBuff;
            int bytesReaded;

            MemoryStream receivedBytesBuffer;
            BinaryFormatter bf = new BinaryFormatter();

            try
            {
                // 連線到InfoServer
                dataServerConnection.Connect(serverHost, serverPort);
                stopped = false;

                // 觸發OnConnected 事件
                if (OnConnected != null)
                {
                    OnConnected(serverHost, serverPort);
                }

                receivedBytesBuffer = new MemoryStream();

                while ((bytesReaded = dataServerConnection.Receive(buff, buff.Length, SocketFlags.None)) > 0)
                {
                    tempBuff = new byte[bytesReaded];
                    Array.Copy(buff, tempBuff, bytesReaded);

                    for (int i = 0, n = tempBuff.Length; i < n; i++)
                    {
                        if ((char)tempBuff[i] != Protocol.MessageDelimiter)
                        {
                            receivedBytesBuffer.WriteByte(tempBuff[i]);
                            continue;
                        }

                        byte[] dataBytes = receivedBytesBuffer.ToArray();
                        object recvObject = dataParser.Parse(dataBytes);
                        
                        if (!(recvObject is Bye))
                        {
                            if (recvObject is string)
                            {
                                if (OnMessageReceived != null)
                                {
                                    OnMessageReceived(this, recvObject as string, dataServerConnection);
                                }
                            }
                            else
                            {
                                if (OnDataReceived != null)
                                {
                                    OnDataReceived(this, recvObject, dataServerConnection);
                                }
                            }
                        }
                        else
                        {
                            // 收到伺服器送來的結束訊息，則終止傳送資料

                            dataServerConnection.Shutdown(SocketShutdown.Send);
                        }

                        receivedBytesBuffer.Close();
                        receivedBytesBuffer = new MemoryStream();
                    }
                }
            }
            catch (Exception e)
            {
                if (OnException != null)
                {
                    if (e is SocketException)
                    {
                        SocketException se = e as SocketException;

                        if (!Protocol.IsIgnorableSocketError(se.ErrorCode))
                        {
                            OnException(this, e);
                        }
                    }
                    else
                    {
                        OnException(this, e);
                    }
                }
            }
            finally
            {
                if (dataServerConnection != null)
                {
                    dataServerConnection.Close();
                    dataServerConnection = null;

                    if (OnDisconnected != null)
                    {
                        OnDisconnected(serverHost, serverPort);
                    }
                }
            }
        }

        private void EndConnect(IAsyncResult result)
        {
            AsyncOperationDelegate asyncDel = result.AsyncState as AsyncOperationDelegate;
            asyncDel.EndInvoke(result);

            stopped = true;

            if (OnStopped != null)
            {
                OnStopped(this);
            }
        }

        public void SendData(object dataObject)
        {
            dataServerConnection.Send(dataFormatter.Format(dataObject));
        }

        public void SendMessage(string msg)
        {
            dataServerConnection.Send(dataFormatter.FormattedMessage(msg));
        }

        public void Stop()
        {
            dataServerConnection.Send(Protocol.Bye);
        }
    }
}
