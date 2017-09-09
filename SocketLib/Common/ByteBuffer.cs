using System;
using System.Diagnostics;

// 参考https://github.com/chenshuo/muduo中Buffer的实现

namespace YezhStudio.Base.Network
{
    public class ByteBuffer
    {
        const int kCheapPrepend = 8;
        const int kInitialSize = 1024;

        private byte[] buffer;
        public byte[] Buffer { get { return buffer; } }

        public int ReadIndex { get; private set; }
        public int WriteIndex { get; private set; }
        public int ReadableBytes { get { return WriteIndex - ReadIndex; } }
        public int WriteableBytes { get { return buffer.Length - WriteIndex; } }
        public int PrependableBytes { get { return ReadIndex; } }

        public ByteBuffer(int initialSize = kInitialSize)
        {
            buffer = new byte[kCheapPrepend + initialSize];
            ReadIndex = kCheapPrepend;
            WriteIndex = kCheapPrepend;

            Debug.Assert(ReadableBytes == 0);
            Debug.Assert(WriteableBytes == initialSize);
            Debug.Assert(PrependableBytes == kCheapPrepend);
        }

        #region 读取操作
        public byte PeekByte()
        {
            return buffer[ReadIndex];
        }

        public byte[] PeekBytes(int len)
        {
            Debug.Assert(len <= ReadableBytes);
            var buff = new byte[len];
            Array.Copy(buffer, ReadIndex, buff, 0, len);

            return buff;
        }

        public Int16 PeekInt16()
        {
            return BitConverter.ToInt16(buffer, ReadIndex);
        }

        public UInt16 PeekUInt16()
        {
            return BitConverter.ToUInt16(buffer, ReadIndex);
        }

        public Int32 PeekInt32()
        {
            return BitConverter.ToInt32(buffer, ReadIndex);
        }

        public Int64 PeekInt64()
        {
            return BitConverter.ToInt64(buffer, ReadIndex);
        }

        public byte ReadByte()
        {
            var result = PeekByte();
            Retrieve(1);
            return result;
        }

        public byte[] ReadBytes(int len)
        {
            var result = PeekBytes(len);
            Retrieve(len);
            return result;
        }

        public byte[] ReadAll()
        {
            return ReadBytes(ReadableBytes);
        }

        public Int16 ReadInt16()
        {
            var result = PeekInt16();
            RetrieveInt16();
            return result;
        }

        public UInt16 ReadUInt16()
        {
            var result = PeekUInt16();
            RetrieveUInt16();
            return result;
        }

        public Int32 ReadInt32()
        {
            var result = PeekInt32();
            RetrieveInt32();
            return result;
        }

        public Int64 ReadInt64()
        {
            var result = PeekInt64();
            RetrieveInt64();
            return result;
        }
        #endregion

        #region 移动游标
        public void Retrieve(int len)
        {
            Debug.Assert(len <= ReadableBytes, "没有足够数据");
            if (len < ReadableBytes)
            {
                ReadIndex += len;
            }
            else
            {
                RetrieveAll();
            }
        }

        public void RetrieveInt16()
        {
            Retrieve(sizeof(Int16));
        }

        public void RetrieveUInt16()
        {
            Retrieve(sizeof(UInt16));
        }

        public void RetrieveInt32()
        {
            Retrieve(sizeof(Int32));
        }

        public void RetrieveInt64()
        {
            Retrieve(sizeof(Int64));
        }

        public void RetrieveAll()
        {
            ReadIndex = kCheapPrepend;
            WriteIndex = kCheapPrepend;
        }
        #endregion

        #region 尾部添加数据
        public void WriteByte(byte data)
        {
            ensureWritableBytes(1);
            buffer[WriteIndex] = data;
            hasWritten(1);
        }

        public void WriteBytes(byte[] data)
        {
            Append(data);
        }

        public void WriteInt16(Int16 x)
        {
            Append(BitConverter.GetBytes(x));
        }

        public void WriteUInt16(UInt16 x)
        {
            Append(BitConverter.GetBytes(x));
        }

        public void WriteInt32(Int32 x)
        {
            Append(BitConverter.GetBytes(x));
        }

        public void WriteInt64(Int64 x)
        {
            Append(BitConverter.GetBytes(x));
        }

        private void Append(byte[] data)
        {
            Append(data, 0, data.Length);
        }

        private void Append(byte[] data, int offset, int count)
        {
            ensureWritableBytes(count);
            Array.Copy(data, offset, buffer, WriteIndex, count);
            hasWritten(count);
        }
        #endregion

        private void ensureWritableBytes(int len)
        {
            if (WriteableBytes < len)
            {
                makeSpace(len);
            }

            Debug.Assert(WriteableBytes >= len);
        }

        private void hasWritten(int len)
        {
            Debug.Assert(WriteableBytes >= len);
            WriteIndex += len;
        }

        private void makeSpace(int len)
        {
            if (WriteableBytes + PrependableBytes < len + kCheapPrepend)
            {
                // 扩容
                byte[] newBuffer = new byte[WriteIndex + len];
                Array.Copy(buffer, ReadIndex, newBuffer, 0, ReadableBytes);
                buffer = newBuffer;
            }
            else
            {
                // 移动数据
                Debug.Assert(kCheapPrepend < ReadIndex);
                var readable = ReadableBytes;
                Array.Copy(buffer, ReadIndex, buffer, kCheapPrepend, readable);

                ReadIndex = kCheapPrepend;
                WriteIndex = ReadIndex + readable;
                Debug.Assert(readable == ReadableBytes);
            }
        }

        public void UnWrite(int len)
        {
            Debug.Assert(len <= ReadableBytes);
            WriteIndex -= len;
        }

        public void Swap(ByteBuffer rhs)
        {
            var temp   = buffer;
            buffer     = rhs.buffer;
            rhs.buffer = temp;

            var value     = ReadIndex;
            ReadIndex     = rhs.ReadIndex;
            rhs.ReadIndex = value;

            value         = WriteIndex;
            WriteIndex    = rhs.WriteIndex;
            rhs.ReadIndex = value;
        }

        public void Shrink(int reserve)
        {
            var readableBytes = ReadableBytes;

            byte[] newBuffer = new byte[kCheapPrepend + ReadableBytes + reserve];
            Array.Copy(buffer, ReadIndex, newBuffer, kCheapPrepend, ReadableBytes);

            buffer     = newBuffer;
            ReadIndex  = kCheapPrepend;
            WriteIndex = ReadIndex + readableBytes;
        }

        #region 头部添加消息头
        // 在头部添加数据
        public void Prepend(byte[] data)
        {
            Debug.Assert(data.Length <= PrependableBytes);
            ReadIndex -= data.Length;
            Array.Copy(data, 0, buffer, ReadIndex, data.Length);
        }

        public void PrependByte(byte x)
        {
            Debug.Assert(1 <= PrependableBytes);
            ReadIndex -= 1;
            buffer[ReadIndex] = x;
        }

        public void PrependInt16(Int16 x)
        {
            Prepend(BitConverter.GetBytes(x));
        }

        public void PrependUInt16(UInt16 x)
        {
            Prepend(BitConverter.GetBytes(x));
        }

        public void PrependInt32(Int32 x)
        {
            Prepend(BitConverter.GetBytes(x));
        }

        public void PrependInt64(Int64 x)
        {
            Prepend(BitConverter.GetBytes(x));
        }
        #endregion

        #region 额外添加

        // 手动移动WriteIndex
        public void MoveWriteIndex(int len)
        {
            WriteIndex += len;
        }

        // 当尾部数据少于头部时，将数据整体向前移动
        // 如果一个协议包大于buffer.Length
        public void TryDefragment()
        {
            if (WriteableBytes < PrependableBytes - kCheapPrepend && WriteableBytes < 100)
            {
                var readableBytes = ReadableBytes;

                Array.Copy(Buffer, ReadIndex, Buffer, kCheapPrepend, ReadableBytes);

                ReadIndex  = kCheapPrepend;
                WriteIndex = ReadIndex + readableBytes;
            }
        }
        #endregion
    }
}
