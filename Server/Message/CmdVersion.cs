using System.IO;

namespace Server.Message
{
    using MsgPack.Serialization;

    class CmdVersion
    {
        // 协议版本号
        public CmdVersion(int version)
        {
            this.version = version;
        }

        public static byte[] Pack(int version)
        {
            var stream = new MemoryStream();
            var serializer = MessagePackSerializer.Get<CmdVersion>();
            serializer.Pack(stream, new CmdVersion(version));

            return stream.GetBuffer();
        }

        public static CmdVersion Unpack(byte[] buff)
        {
            var stream = new MemoryStream();
            var serializer = MessagePackSerializer.Get<CmdVersion>();

            stream.Write(buff, 0, buff.Length);
            stream.Position = 0;

            return serializer.Unpack(stream);
        }

        private int version;
    }
}
