using System;
using System.IO;

namespace Server.Message
{
    using MsgPack.Serialization;

    public class MsgUdpKey
    {
        public MsgUdpKey(UInt32 conv)
        {
            this.Conv = conv;
        }

        public UInt32 Conv;

        public static byte[] Pack(UInt32 conv)
        {
            var stream = new MemoryStream();

            var serializer = MessagePackSerializer.Get<MsgUdpKey>();
            serializer.Pack(stream, new MsgUdpKey(conv));

            stream.Flush();
            return stream.GetBuffer();
        }

        public static MsgUdpKey UnPack(byte[] buff)
        {
            var stream = new MemoryStream();
            var serializer = MessagePackSerializer.Get<MsgUdpKey>();

            stream.Write(buff, 0, buff.Length);
            stream.Position = 0;
            return serializer.Unpack(stream);
        }
    }
}
