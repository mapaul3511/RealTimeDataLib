using System;
using System.Collections.Generic;
using System.Text;

namespace RealTimeDataLib
{
    public delegate void AsyncOperationDelegate();
    public delegate void DataReceivedDelegate(object sender, object dataObject, System.Net.Sockets.Socket from);
    public delegate void MessageReceivedDelegate(object sender, string msg, System.Net.Sockets.Socket from);
}
