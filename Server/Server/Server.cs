using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    enum ServerState
    {
        Start,
        Closed,
    }

    public class ServerConfig
    {
        public string IP            { get; set; }
        public int Port             { get; set; }
        public int MaxConnections   { get; set; }
    }

    // 表示一个服务
    interface Server<T>
    {
        event Action<T> OnNewConnection;

        void StartServiceOn(ServerConfig cfg);
        void Stop();

        Task SendMessage(byte[] buff, object obj = null);
    }
}
