using System;
using System.IO;
using System.Text;

namespace Server.Message
{
    using MessagePack;

    [MessagePackObject]
    public class MsgPong
    {
        public MsgPong(UInt32 t)
        {
            TimeStamp = t;
        }

        [Key(0)]
        public UInt32 TimeStamp;
    }
}
