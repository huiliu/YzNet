using System;
using System.Net;

namespace Base.Network
{
    public delegate void ResolveCallback(bool succ, string msg, IPAddress address);

    // 解析主机名
    public static class DNS
    {
        public static void ResolveHost(string host, ResolveCallback cb)
        {
            IPAddress address;
            try
            {
                // 解析地址
                if (!IPAddress.TryParse(host, out address))
                {
                    // 解析地址失败，查询DNS
                    Dns.BeginGetHostEntry(host, getHostEntryCallback, cb);
                }
                else
                {
                    // 解析成功，开始连接
                    cb.Invoke(true, "", address);
                }
            }
            catch (Exception e)
            {
                Utils.logger.Error(string.Format("解析地址[host: {0}]失败！\nMessage: {1}\nStackTrace: {2}", host, e.Message, e.StackTrace), "DNS");
                cb.Invoke(false, e.Message, null);
            }
        }

        private static void getHostEntryCallback(IAsyncResult ar)
        {
            ResolveCallback cb = ar.AsyncState as ResolveCallback;
            try
            {
                var entries = Dns.EndGetHostEntry(ar);

                cb.Invoke(true, "", entries.AddressList[0]);
            }
            catch (Exception e)
            {
                Utils.logger.Error(string.Format("解析地址失败！\nMessage: {0}\nStackTrace: {1}", e.Message, e.StackTrace), "DNS");
                cb.Invoke(false, e.Message, null);
            }
        }
    }
}
