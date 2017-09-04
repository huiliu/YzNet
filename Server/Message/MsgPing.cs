using System;
using System.IO;
using System.Text;

namespace Server.Message
{
    using MessagePack;

    [MessagePackObject]
    public class MsgPing
    {
        public MsgPing(UInt32 t)
        {
            this.TimeStamp = t;
        }

        [Key(0)]
        public UInt32 TimeStamp;
    }

}
