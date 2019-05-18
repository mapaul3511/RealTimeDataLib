using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.ComponentModel;
using System.Threading;

namespace RealTimeDataLib
{
    public delegate void DataServerStartedDelegate(DataServer sender);
    public delegate void DataServerStoppedDelegate(DataServer sender);
    public delegate void DataServerExceptionDelegate(object sender, Exception e);
    public delegate void ClientConnectedDelegate(DataServer sender, ClientConnectedEventArgs args);
    public delegate void ClientDisconnectedDelegate(DataServer sender, ClientDisconnectedEventArgs args);

    /// <summary>
    /// 資料伺服器，可接受多個Client 連線並傳送資料物件

    /// </summary>
    public class DataServer
    {
        private delegate void ClientHandlerDelegate(Socket client);

        private object synclock;

        private TcpListener listener = null;

        #region 成員事件
        public event DataServerStartedDelegate OnStarted;
        public event DataServerStoppedDelegate OnStopped;
        public event DataServerExceptionDelegate OnException;

        public event ClientConnectedDelegate OnClientConnected;
        public event ClientDisconnectedDelegate OnClientDisconnected;
        public event DataReceivedDelegate OnDataReceived;
        public event MessageReceivedDelegate OnMessageReceived;
        #endregion

        private string name;

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        private BindingList<Socket> clientsList;

        public BindingList<Socket> ClientsList
        {
            get { return clientsList; }
        }

        private Encoding asciiEncoding;

        private bool stopped;

        /// <summary>
        /// 是否已停止監聽連線要求
        /// </summary>
        public bool Stopped
        {
            get { return stopped; }
            set { stopped = value; }
        }

        private DataParser dataParser;

        public DataParser DataParser
        {
            get { return dataParser; }
        }

        private DataFormatter dataFormatter;

        public DataFormatter DataFormatter
        {
            get { return dataFormatter; }
        }

        public int ListenPort { get; set; }

        public DataServer(string serverName, int port, DataParser dataParser, DataFormatter dataFormatter)
        {
            synclock = new object();

            this.name = serverName;
            this.ListenPort = port;
            listener = new TcpListener(IPAddress.Any, port);
            asciiEncoding = Encoding.ASCII;
            stopped = true;

            this.dataParser = dataParser;
            this.dataFormatter = dataFormatter;
        }

        public void Start()
        {
            clientsList = new BindingList<Socket>();
            clientsList.RaiseListChangedEvents = true;

            AsyncOperationDelegate asyncDel = new AsyncOperationDelegate(StartServer);
            asyncDel.BeginInvoke(new AsyncCallback(EndServer), asyncDel);
        }

        private void StartServer()
        {
            stopped = false;
            listener.Start();

            if (OnStarted != null)
            {
                OnStarted(this);
            }

            Socket client = null;

            do
            {
                try
                {
                    client = listener.AcceptSocket();
                    lock (synclock)
                    {
                        clientsList.Add(client);
                    }

                    ClientHandlerDelegate del = new ClientHandlerDelegate(ClientHandler);
                    del.BeginInvoke(client, new AsyncCallback(ClientHandlerEnd), del);
                }
                catch (Exception e)
                {
                    // Do nothing....
                }
            } while (!stopped);
        }

        private void EndServer(IAsyncResult result)
        {
            lock (synclock)
            {
                clientsList.Clear();
            }

            AsyncOperationDelegate asyncDel = result.AsyncState as AsyncOperationDelegate;
            asyncDel.EndInvoke(result);
        }

        private void ClientHandler(Socket client)
        {
            string clientIpAddress = client.RemoteEndPoint.ToString();

            if (OnClientConnected != null)
            {
                ClientConnectedEventArgs args = new ClientConnectedEventArgs(clientIpAddress, DateTime.Now);
                OnClientConnected(this, args);
            }

            byte[] buff = new byte[1024];
            byte[] tempBuff;
            int bytesReaded;
            MemoryStream receivedBytesBuffer = new MemoryStream();

            try
            {
                #region 從Client socket 讀取資料流
                while ((bytesReaded = client.Receive(buff, buff.Length, SocketFlags.None)) > 0)
                {
                    tempBuff = new byte[bytesReaded];
                    Array.Copy(buff, tempBuff, bytesReaded);

                    for (int i = 0, n = tempBuff.Length; i < n; i++)
                    {
                        if ((char)tempBuff[i] != Protocol.MessageDelimiter)
                        {
                            receivedBytesBuffer.WriteByte(tempBuff[i]);
                        }
                        else
                        {
                            byte[] dataBytes = receivedBytesBuffer.ToArray();
                            object recvObject = dataParser.Parse(dataBytes);

                            if (!(recvObject is Bye))
                            {
                                if (recvObject is string)
                                {
                                    if (OnMessageReceived != null)
                                    {
                                        OnMessageReceived(this, recvObject as string, client);
                                    }
                                }
                                else
                                {
                                    if (OnDataReceived != null)
                                    {
                                        OnDataReceived(this, recvObject, client);
                                    }
                                }
                            }
                            else
                            {
                                // 從Data Source Client 端收到準備離線訊息

                                client.Send(Protocol.Bye);
                                client.Shutdown(SocketShutdown.Send);
                            }

                            receivedBytesBuffer.Close();
                            receivedBytesBuffer.Dispose();
                            receivedBytesBuffer = new MemoryStream();
                        }
                    }
                }
                #endregion

                client.Shutdown(SocketShutdown.Receive);
            }
            catch (Exception e)
            {
                #region Client Handler 例外處理
                if (OnException != null)
                {
                    if (e is SocketException)
                    {
                        if (!Protocol.IsIgnorableSocketError((e as SocketException).ErrorCode))
                        {
                            OnException(this, new Exception("Client Handler 發生" + e.GetType().Name + ": " + e.Message + ", Client IP 為 " + clientIpAddress));
                        }
                    }
                    else
                    {
                        OnException(this, new Exception("Client Handler 發生" + e.GetType().Name + ": " + e.Message + ", Client IP 為 " + clientIpAddress));
                    }
                }
                #endregion

                client.Shutdown(SocketShutdown.Both);
            }
            finally
            {
                client.Close();

                lock (synclock)
                {
                    clientsList.Remove(client);
                }
            }

            // 通知ClientDisconnected 事件
            if (OnClientDisconnected != null)
            {
                ClientDisconnectedEventArgs args = new ClientDisconnectedEventArgs(clientIpAddress, DateTime.Now);
                OnClientDisconnected(this, args);
            }
        }

        private void ClientHandlerEnd(IAsyncResult result)
        {
            ClientHandlerDelegate asyncDel = result.AsyncState as ClientHandlerDelegate;
            asyncDel.EndInvoke(result);
        }

        public void Broadcast(object dataObject)
        {
            lock (synclock)
            {
                string clientIpAddr;

                foreach (Socket client in clientsList)
                {
                    clientIpAddr =string.Empty;

                    try
                    {
                        clientIpAddr = client.RemoteEndPoint.ToString();
                    }
                    catch (Exception e)
                    {
                        continue;
                    }

                    try
                    {
                        client.Send(dataFormatter.Format(dataObject));
                    }
                    catch (Exception e)
                    {
                        if (OnException != null)
                        {
                            OnException(this, new Exception("傳送" + dataObject.GetType().Name + " 資料物件到" + clientIpAddr + " 時發生" + e.GetType().Name + ": " + e.Message));
                        }
                    }
                }
            }
        }

        public void Stop()
        {
            stopped = true;
            listener.Stop();

            #region 結束所有的Clients 連線
            lock (synclock)
            {
                foreach (Socket client in clientsList)
                {
                    if (client != null && client.Connected)
                    {
                        string clientIpAddr = string.Empty;

                        try
                        {
                            clientIpAddr = client.RemoteEndPoint.ToString();
                            client.Send(Protocol.Bye);
                            client.Shutdown(SocketShutdown.Send);
                        }
                        catch (Exception e)
                        {
                            if (OnException != null)
                            {
                                OnException(this, new Exception("結束與Client 端[" + clientIpAddr + "]的連線時發生" + e.GetType().Name + ": " + e.Message));
                            }
                        }
                    }
                }
            }
            #endregion

            if (OnStopped != null)
            {
                OnStopped(this);
            }
        }
    }

    public class ClientConnectedEventArgs : EventArgs
    {
        private string clientIpAddress;

        public string ClientIpAddress
        {
            get { return clientIpAddress; }
        }

        private DateTime connectedTime;

        public DateTime ConnectedTime
        {
            get { return connectedTime; }
            set { connectedTime = value; }
        }

        public ClientConnectedEventArgs() : base() { }

        public ClientConnectedEventArgs(string clientIpAddress, DateTime time)
            : this()
        {
            this.clientIpAddress = clientIpAddress;
            this.connectedTime = time;
        }
    }

    public class ClientDisconnectedEventArgs : EventArgs
    {
        private string clientIpAddress;

        public string ClientIpAddress
        {
            get { return clientIpAddress; }
        }
                
        private DateTime disconnectedTime;

        public DateTime DisconnectedTime
        {
            get { return disconnectedTime; }
            set { disconnectedTime = value; }
        }

        public ClientDisconnectedEventArgs() : base() { }

        public ClientDisconnectedEventArgs(string clientIpAddress, DateTime time)
            : this()
        {
            this.clientIpAddress = clientIpAddress;
            this.disconnectedTime = time;
        }
    }
}