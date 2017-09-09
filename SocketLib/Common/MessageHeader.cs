using System;
using System.Diagnostics;
using System.IO;

namespace YezhStudio.Base.Network
{
    // |01234567|01234567|01234567|01234567|
    // |SYNCODE1|SYNCODE2|  MessageLength  | <--- Message Header(消息长度包含消息头, 12字节: 1 + 1 + 2 + 8)
    // |            Timestamp              | <--- Message Header
    // |                                   | <--- Message Header
    // |           Message Data            | <--- Message Body

    // 添加和解开消息头
    public static class MessageHeader
    {
        // 消息头长度
        public const UInt16 HeaderLength = 12;


        // 同步码
        public static byte SYN_CODE1 = 83;
        public static byte SYN_CODE2 = 77;

        // 加上消息头
        public static byte[] Encoding(byte[] data)
        {
            var length = data.Length + HeaderLength;
            if (length == 0 || length > NetworkCommon.MaxPackageSize)
            {
                Debug.Assert(length > 0 && length <= NetworkCommon.MaxPackageSize, string.Format("消息长度不符合要求！[{0}/{1}]", length, NetworkCommon.MaxPackageSize), "MessageHeader");
                return null;
            }

            using (var ms = new MemoryStream(length))
            {
                var bw = new BinaryWriter(ms);

                // 消息长度
                bw.Write(SYN_CODE1);
                bw.Write(SYN_CODE2);
                bw.Write((short)length);    // 消息长度最多为32KB
                bw.Write((long)0);          // TODO: 时间戳
                // 消息内容
                bw.Write(data);
                bw.Flush();

                return ms.GetBuffer();
            }
        }

        // 尝试剥除消息头，返回消息内容
        public static byte[] TryDecode(ByteBuffer buff)
        {
            // 检查消息长度
            var totalLength = buff.ReadableBytes;
            if (totalLength < HeaderLength)
            {
                // 消息不完整
                return null;
            }


            // 验证消息头同步码
            var head = buff.PeekBytes(HeaderLength);
            if (head[0] != SYN_CODE1 || head[1] != SYN_CODE2)
            {
                Debug.Assert(false, string.Format("同步码错误! {0}", head), "MessageHeader");
                buff.Retrieve(2);
                return null;
            }

            // 检查消息长度
            Int16 msgLength = BitConverter.ToInt16(head, 2);
            if (totalLength < msgLength)
            {
                // 消息不完整
                return null;
            }

            // 时间戳被忽略

            // 跳过消息头
            buff.Retrieve(HeaderLength);

            // 读取消息内容
            return buff.ReadBytes(msgLength - HeaderLength);
        }
    }
}
