using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Base.Network
{
    // |01234567|01234567|01234567|01234567|
    // |             RequesID              |  -> RPC响应头(只包含一个请求ID)
    // |           Response Body           |  -> RPC响应内容
    // |                                   |

    // RPC响应消息
    class RpcResponse
    {
        public int RequestID { get; private set; }
        public byte[] Content { get; private set; }


        public static byte[] Encode(int id, byte[] data)
        {
            using (var ms = new MemoryStream(data.Length + 2))
            {
                BinaryWriter bw = new BinaryWriter(ms);
                bw.Write(id);
                bw.Write(data);
                bw.Flush();

                return ms.GetBuffer();
            }
        }

        public static RpcResponse Decode(byte[] data)
        {
            Debug.Assert(data.Length > 4, "无效的RPC请求！", "RPC");
            RpcResponse request = new RpcResponse();
            request.RequestID = BitConverter.ToInt32(data, 0);

            byte[] buff = new byte[data.Length - 4];
            Array.Copy(data, 4, buff, 0, data.Length - 4);
            request.Content = buff;

            return request;
        }
    }
}
