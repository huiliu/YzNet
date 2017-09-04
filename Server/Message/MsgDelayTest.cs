﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Message
{
    using MessagePack;

    [MessagePackObject(keyAsPropertyName:true)]
    public class MsgDelayTest
    {
        public MsgDelayTest()
        {
            ClientReceiveTime = 0;
            ClientSendTime = 0;
            ServerReceiveTime = 0;
            ServerSendTime = 0;
        }

        public UInt32 ClientSendTime    { get; set; }
        public UInt32 ClientReceiveTime { get; set; }
        public UInt32 ServerSendTime    { get; set; }
        public UInt32 ServerReceiveTime { get; set; }

        public override string ToString()
        {
            return string.Format("Client SendTime: {0} Client ReceiveTime: {1} Server ReceiveTime: {2} Server SendTime: {3}",
                ClientSendTime,
                ClientReceiveTime,
                ServerSendTime,
                ServerReceiveTime);
        }
    }
}
