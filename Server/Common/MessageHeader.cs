using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    // |01234567|01234567|01234567|01234567|
    // |SYNCODE1|SYNCODE2| MessageLength   | <--- Message Header(消息长度指Body长度，不包括消息头)
    // | Message Data                      | <--- Message Body
    // 添加和解开消息头
    public static class MessageHeader
    {
        // 消息头长度
        public const UInt16 HeaderLength = 4;

        // 消息最大长度
        // 64KB - HeaderLength
        public const UInt16 MessageMaxLength = 1024 * 16 - HeaderLength;

        // 同步码
        public static byte SYN_CODE1 = 83;
        public static byte SYN_CODE2 = 77;

        // 加上消息头
        public static byte[] Encoding(byte[] data)
        {
            var length = data.Length;
            if (length == 0 || length > MessageMaxLength)
            {
                Debug.Assert(length > 0 && length <= MessageMaxLength, string.Format("消息长度不符合要求！[{0}/{1}]", length, MessageMaxLength), "MessageHeader");
                return null;
            }

            using (var ms = new MemoryStream(data.Length + 4))
            {
                var bw = new BinaryWriter(ms);

                // 消息长度
                bw.Write(SYN_CODE1);
                bw.Write(SYN_CODE2);
                bw.Write((UInt16)length);
                // 消息内容
                bw.Write(data);
                bw.Flush();

                return ms.GetBuffer();
            }
        }

        // 尝试剥除消息头，返回消息内容
        public static byte[] TryDecode(ByteBuffer buff)
        {
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
                // TODO: 打印内容
                Debug.Assert(false, string.Format("同步码错误! {0}", head), "MessageHeader");
                buff.Retrieve(2);
                return null;
            }

            // 检查消息长度
            UInt16 bodyLength = BitConverter.ToUInt16(head, 2);
            if (totalLength - HeaderLength < bodyLength)
            {
                // 消息不完整
                return null;
            }

            // 跳过消息头
            buff.Retrieve(HeaderLength);

            // 读取消息内容
            return buff.ReadBytes(bodyLength);
        }
    }
}
