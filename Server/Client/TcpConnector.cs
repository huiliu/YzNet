using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server
{
    public class ClientCfg
    {
        public string IP;
        public int Port; 
    }

    // 用于与服务端建立连接
    public static class TcpConnector
    {
        #region TCP
        // 以TCP协议连接服务器
        public static async Task<TcpSession> ConnectTcpServer(ClientCfg cfg)
        {
            bool result = false;
            Socket s = null;
            try
            {
                IPAddress address;
                IPAddress[] addresses;
                if (!IPAddress.TryParse(cfg.IP, out address))
                {
                    var ipHostEntry = await Dns.GetHostEntryAsync(cfg.IP);
                    addresses = ipHostEntry.AddressList;

                    for (var i = 0; i < addresses.Length; ++i)
                    {
                        if ((result = tryToConnect(addresses[i], cfg.Port, out s)))
                        {
                            break;
                        }
                    }
                }
                else
                {
                    result = tryToConnect(address, cfg.Port, out s);
                }
            }
            catch (Exception e)
            {
                Debug.Write(string.Format("连接[{0}/{1}]失败！Message: {2}", cfg.IP, cfg.Port, e.Message), "Connector");
                return null;
            }

            if (result)
            {
                Debug.Assert(s != null && s.Connected, "Socket连接没有建立!", "Connector");
                var session = TcpSession.Create(s);
                TcpSessionMgr.Instance.Register(session.GetId(), session);

                return session;
            }

            return null;
        }

        private static bool tryToConnect(IPAddress ipAddress, int port, out Socket s)
        {
            s = null;
            try
            {
                // 同步连接
                s = new Socket(ipAddress.AddressFamily, SocketType.Stream,  ProtocolType.Tcp);
                s.Connect(ipAddress, port);
                Debug.Assert(s.Connected, string.Format("连接{0}/{1}失败！", ipAddress.ToString(), port), "TcpClient");

                return true;
            }
            catch (Exception e)
            {
                Debug.Write(string.Format("连接[{0}/{1}]失败！Message: {2}", ipAddress.ToString(), port, e.Message), "Connector");

                if (s != null)
                {
                    s.Close();
                }

                return false;
            }
        }
        #endregion

        #region UDP

        #endregion
    }
}
