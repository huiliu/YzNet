using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace YezhStudio.Base.Network
{
    // |01234567|01234567|01234567|01234567|
    // |SYNCODE1|SYNCODE2|    CtrlCode     | <--- Message Header(消息长度不包含消息头, 12字节: 1 + 1 + 1 + 1 + 4 + 2 + 2)
    // |               Cookie              | <--- Message Header
    // |     MessageID   |  MessageLength  | <--- Message Header
    // |           Message Data            | <--- Message Body

    // 添加和解开消息头
    public static class MessageHeader
    {
        public enum MessageCtrlType
        {
            RPCRequest  = 1 << 0,   // RPC请求
            RPCResponse = 1 << 1,   // RPC响应
            Compress    = 1 << 2,   // 压缩
        }

        // 消息头长度
        public const UInt16 HeaderLength = 12;

        // 同步码
        public static byte SYN_CODE1 = 83;
        public static byte SYN_CODE2 = 77;

        // 加上消息头
        public static ByteBuffer Encoding(int MsgID, ByteBuffer data, int cookie = 0, bool needCompress = false)
        {
            var length = data.ReadableBytes + HeaderLength;
            if (length == 0 || length > NetworkCommon.MaxPackageSize)
            {
                Debug.Assert(length > 0 && length <= NetworkCommon.MaxPackageSize, string.Format("消息长度不符合要求！[{0}/{1}]", length, NetworkCommon.MaxPackageSize), "MessageHeader");
                return null;
            }

            Int16 ctrlCode = 0;
            try
            {
                if (needCompress)
                {
                    ctrlCode |= (Int16)MessageCtrlType.Compress;

                    using (var ms = new MemoryStream())
                    {
                        DeflateStream deflateStream = new DeflateStream(ms, CompressionMode.Compress);
                        deflateStream.Write(data.Buffer, data.ReadIndex, data.ReadableBytes);
                        deflateStream.Flush();

                        data.RetrieveAll();
                        data.WriteBytes(ms.GetBuffer());
                    }
                }

            }
            catch (Exception e)
            {
                Utils.logger.Error(string.Format("打包消息[MessageID: {2}]出错！原因：{0}\nStackTrace: {1}\n", e.Message, e.StackTrace, MsgID), "MessageEncode");
                data.RetrieveAll();
            }

            data.PrependInt16((Int16)data.ReadableBytes);
            data.PrependInt16((Int16)MsgID);
            data.PrependInt32(cookie);
            data.PrependInt16(ctrlCode);
            data.PrependByte(SYN_CODE2);
            data.PrependByte(SYN_CODE1);

            return data;
        }

        // 尝试剥除消息头，返回消息内容
        public static byte[] TryDecode(ByteBuffer buff, out int MsgID, out int cookie)
        {
            MsgID = 0;
            cookie = 0;
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

            // 解析消息头
            Int16 ctrlCode = BitConverter.ToInt16(head, 2);
            cookie         = BitConverter.ToInt32(head, 4);
            MsgID          = BitConverter.ToInt16(head, 8);
            Int16 msgLen   = BitConverter.ToInt16(head, 10);

            // 检查消息长度
            if (totalLength - HeaderLength < msgLen)
            {
                // 消息不完整
                return null;
            }

            // 跳过消息头
            buff.Retrieve(HeaderLength);

            // 消息内容长度为0
            if (msgLen == 0)
            {
                Utils.logger.Warn(string.Format("收到消息[ID: {0}]内容长度为0！", MsgID), "Message Decode");
                return null;
            }

            // 读取消息内容
            byte[] msg = buff.ReadBytes(msgLen);
            buff.Retrieve(msgLen);
            try
            {
                if ((ctrlCode & (int)MessageCtrlType.Compress) != 0)
                {
                    using (var ms = new MemoryStream())
                    {
                        DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress);
                        ds.Write(msg, 0, msgLen);
                        ds.Flush();

                        msg = ms.GetBuffer();
                    }
                }
                
            }
            catch (Exception e)
            {
                Utils.logger.Error(string.Format("解码消息[MessageID: {2}]出错！原因：{0}\nStackTrace: {1}\n", e.Message, e.StackTrace, MsgID), "MessageEncode");
                msg = null;
            }

            return msg;
        }
    }
}
