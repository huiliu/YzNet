using System;
using System.Diagnostics;
using System.Text;
using System.Net;

namespace Server
{
    /// <summary>
    /// 环形缓冲区，支持动态扩容和线程安全的读写
    /// </summary>
    public class RingBuffer
    {
        public RingBuffer()
            : this(false, false)
        {
        }

        public RingBuffer(bool readFromNetOrder, bool writeToNetOrder)
        {
            ReadFromNetOrder = readFromNetOrder;
            WriteToNetOrder = writeToNetOrder;
        }

        // 读操作是否执行从网络序到本地序的转换
        public bool ReadFromNetOrder
        {
            get;
            set;
        }

        // 写操作是否执行从本地序到网络序的转换
        public bool WriteToNetOrder
        {
            get;
            set;
        }

        // 有效的数据长度
        public int Available
        {
            get
            {
                lock (this)
                {
                    return available;
                }
            }
        }

        #region 写操作部分

        public void Write(byte v)
        {
            lock (this)
            {
                MakeSureCapacity(1);
                int wPos = WritePos;
                data[wPos] = v;
                available++;
            }
        }

        public void Write(byte[] src, int offset, int cnt)
        {
            lock (this)
            {
                MakeSureCapacity(cnt);
                int wPos = WritePos;
                if (data.Length - wPos >= cnt)
                    Array.Copy(src, offset, data, wPos, cnt);
                else
                {
                    int len1 = data.Length - wPos;
                    int len2 = cnt - len1;
                    Array.Copy(src, offset, data, wPos, len1);
                    Array.Copy(src, offset + len1, data, 0, len2);
                }

                available += cnt;
            }
        }

        public void Write(byte[] src)
        {
            lock (this)
            {
                Write(src, 0, src.Length);
            }
        }

        public void Write(bool v)
        {
            lock (this)
            {
                MakeSureCapacity(sizeof(bool));
                Write(BitConverter.GetBytes(v));
            }
        }

        public void Write(bool[] arr)
        {
            lock (this)
            {
                if (arr == null)
                    Write(-1);
                else
                {
                    Write(arr.Length);
                    foreach (bool v in arr)
                        Write(v);
                }
            }
        }

        public void Write(short v)
        {
            lock (this)
            {
                MakeSureCapacity(sizeof(short));
                Write(BitConverter.GetBytes(WriteToNetOrder ? IPAddress.HostToNetworkOrder(v) : v));
            }
        }

        public void Write(short[] arr)
        {
            lock (this)
            {
                if (arr == null)
                    Write(-1);
                else
                {
                    Write(arr.Length);
                    foreach (short v in arr)
                        Write(v);
                }
            }
        }

        public void Write(int v)
        {
            lock (this)
            {
                MakeSureCapacity(sizeof(int));
                Write(BitConverter.GetBytes(WriteToNetOrder ? IPAddress.HostToNetworkOrder(v) : v));
            }
        }

        public void Write(int[] arr)
        {
            lock (this)
            {
                if (arr == null)
                    Write(-1);
                else
                {
                    Write(arr.Length);
                    foreach (int v in arr)
                        Write(v);
                }
            }
        }

        public void Write(long v)
        {
            lock (this)
            {
                MakeSureCapacity(sizeof(long));
                Write(BitConverter.GetBytes(WriteToNetOrder ? IPAddress.HostToNetworkOrder(v) : v));
            }
        }

        public void Write(long[] arr)
        {
            lock (this)
            {
                if (arr == null)
                    Write(-1);
                else
                {
                    Write(arr.Length);
                    foreach (long v in arr)
                        Write(v);
                }
            }
        }

        public void Write(float v)
        {
            lock (this)
            {
                MakeSureCapacity(sizeof(float));
                Write(BitConverter.GetBytes(v));
            }
        }

        public void Write(float[] arr)
        {
            lock (this)
            {
                if (arr == null)
                    Write(-1);
                else
                {
                    Write(arr.Length);
                    foreach (float v in arr)
                        Write(v);
                }
            }
        }

        public void Write(double v)
        {
            lock (this)
            {
                MakeSureCapacity(sizeof(double));
                Write(BitConverter.GetBytes(v));
            }
        }

        public void Write(double[] arr)
        {
            lock (this)
            {
                if (arr == null)
                    Write(-1);
                else
                {
                    Write(arr.Length);
                    foreach (double v in arr)
                        Write(v);
                }
            }
        }

        public void Write(char v)
        {
            lock (this)
            {
                MakeSureCapacity(sizeof(char));
                Write(BitConverter.GetBytes(v));
            }
        }

        public void Write(char[] arr)
        {
            lock (this)
            {
                if (arr == null)
                    Write(-1);
                else
                {
                    Write(arr.Length);
                    foreach (char v in arr)
                        Write(v);
                }
            }
        }

        public void Write(string v)
        {
            lock (this)
            {
                byte[] arr = ASCIIEncoding.UTF8.GetBytes(v);
                MakeSureCapacity(sizeof(int) + arr.Length);
                Write(BitConverter.GetBytes(WriteToNetOrder ? IPAddress.HostToNetworkOrder(arr.Length) : arr.Length));
                Write(arr);
            }
        }

        public void Write(string[] arr)
        {
            lock (this)
            {
                if (arr == null)
                    Write(-1);
                else
                {
                    Write(arr.Length);
                    foreach (string v in arr)
                        Write(v);
                }
            }
        }

        #endregion

        #region 读操作部分

        // 从读位置开始，丢弃指定数量的数据。一般是表示这部分数据已经读过了
        public void Skip(int cnt)
        {
            lock (this)
            {
                if (available < cnt)
                    throw new Exception("RingBuffer skip overflow");

                rPos += cnt;
                available -= cnt;

                while (rPos >= data.Length)
                    rPos -= data.Length;

                while (rPos < 0)
                    rPos += data.Length;
            }
        }

        public bool PeekInt(ref int v)
        {
            lock (this)
            {
                int cnt = sizeof(int);
                if (available < cnt)
                    return false;
                else
                {
                    v = ReadFromNetOrder
                        ? IPAddress.NetworkToHostOrder(Peek<int>(BitConverter.ToInt32, cnt))
                        : Peek<int>(BitConverter.ToInt32, cnt);
                    return true;
                }
            }
        }

        // 拷贝cnt个字节，但不移动游标
        public byte[] PeekBytes(int cnt)
        {
            lock (this)
            {
                if (available < cnt)
                    throw new Exception("RingBuffer reading overflow");

                byte[] dst = new byte[cnt];
                if (rPos + cnt > data.Length)
                {
                    int len1 = data.Length - rPos;
                    int len2 = cnt - len1;
                    Array.Copy(data, rPos, dst, 0, len1);
                    Array.Copy(data, 0, dst, len1, len2);
                }
                else
                    Array.Copy(data, rPos, dst, 0, cnt);

                //Skip(cnt);

                return dst;
            }
        }

        public byte ReadByte()
        {
            lock (this)
            {
                if (available < 1)
                    throw new Exception("RingBuffer reading overflow");

                byte v = data[rPos];
                Skip(1);
                return v;
            }
        }

        public byte[] ReadBytes(int cnt)
        {
            lock (this)
            {
                if (available < cnt)
                    throw new Exception("RingBuffer reading overflow");

                byte[] dst = new byte[cnt];
                if (rPos + cnt > data.Length)
                {
                    int len1 = data.Length - rPos;
                    int len2 = cnt - len1;
                    Array.Copy(data, rPos, dst, 0, len1);
                    Array.Copy(data, 0, dst, len1, len2);
                }
                else
                    Array.Copy(data, rPos, dst, 0, cnt);

                Skip(cnt);

                return dst;
            }
        }

        public bool ReadBool()
        {
            lock (this)
            {
                return Read<bool>(BitConverter.ToBoolean, sizeof(bool));
            }
        }

        public bool[] ReadBoolArr()
        {
            lock (this)
            {
                int len = ReadInt();
                if (len == -1)
                    return null;
                else
                {
                    bool[] arr = new bool[len];
                    for (int i = 0; i < len; i++)
                        arr[i] = ReadBool();
                    return arr;
                }
            }
        }

        public short ReadShort()
        {
            lock (this)
            {
                return ReadFromNetOrder
                    ? IPAddress.NetworkToHostOrder(Read<short>(BitConverter.ToInt16, sizeof(short)))
                        : Read<short>(BitConverter.ToInt16, sizeof(short));
            }
        }

        public short[] ReadShortArr()
        {
            lock (this)
            {
                int len = ReadInt();
                if (len == -1)
                    return null;
                else
                {
                    short[] arr = new short[len];
                    for (int i = 0; i < len; i++)
                        arr[i] = ReadShort();
                    return arr;
                }
            }
        }

        public int ReadInt()
        {
            lock (this)
            {
                return ReadFromNetOrder
                    ? IPAddress.NetworkToHostOrder(Read<int>(BitConverter.ToInt32, sizeof(int)))
                        : Read<int>(BitConverter.ToInt32, sizeof(int));
            }
        }

        public int[] ReadIntArr()
        {
            lock (this)
            {
                int len = ReadInt();
                if (len == -1)
                    return null;
                else
                {
                    int[] arr = new int[len];
                    for (int i = 0; i < len; i++)
                        arr[i] = ReadInt();
                    return arr;
                }
            }
        }

        public long ReadLong()
        {
            lock (this)
            {
                return ReadFromNetOrder
                    ? IPAddress.NetworkToHostOrder(Read<long>(BitConverter.ToInt64, sizeof(long)))
                        : Read<long>(BitConverter.ToInt64, sizeof(long));
            }
        }

        public long[] ReadLongArr()
        {
            lock (this)
            {
                int len = ReadInt();
                if (len == -1)
                    return null;
                else
                {
                    long[] arr = new long[len];
                    for (int i = 0; i < len; i++)
                        arr[i] = ReadLong();
                    return arr;
                }
            }
        }

        public float ReadFloat()
        {
            lock (this)
            {
                return Read<float>(BitConverter.ToSingle, sizeof(float));
            }
        }

        public float[] ReadFloatArr()
        {
            lock (this)
            {
                int len = ReadInt();
                if (len == -1)
                    return null;
                else
                {
                    float[] arr = new float[len];
                    for (int i = 0; i < len; i++)
                        arr[i] = ReadFloat();
                    return arr;
                }
            }
        }

        public double ReadDouble()
        {
            lock (this)
            {
                return Read<double>(BitConverter.ToDouble, sizeof(double));
            }
        }

        public double[] ReadDoubleArr()
        {
            lock (this)
            {
                int len = ReadInt();
                if (len == -1)
                    return null;
                else
                {
                    double[] arr = new double[len];
                    for (int i = 0; i < len; i++)
                        arr[i] = ReadDouble();
                    return arr;
                }
            }
        }

        public char ReadChar()
        {
            lock (this)
            {
                return Read<char>(BitConverter.ToChar, sizeof(char));
            }
        }

        public char[] ReadCharArr()
        {
            lock (this)
            {
                int len = ReadInt();
                if (len == -1)
                    return null;
                else
                {
                    char[] arr = new char[len];
                    for (int i = 0; i < len; i++)
                        arr[i] = ReadChar();
                    return arr;
                }
            }
        }

        public string ReadString()
        {
            lock (this)
            {
                int hd = sizeof(int);
                int len = ReadFromNetOrder
                    ? IPAddress.NetworkToHostOrder(Peek<int>(BitConverter.ToInt32, hd))
                    : Peek<int>(BitConverter.ToInt32, hd);

                if (len == -1)
                    return null;
                else if (len + hd > available)
                {
                    string msg = string.Format("in RingBuffer.ReadString(), len = {0}, hd = {1}, available = {2}, ",
                        len, hd, available);
                    Trace.WriteLine(msg, "Error.RingBuffer");

                    throw new Exception("RingBuffer reading overflow");
                }

                Skip(hd);
                string v = null;
                if (rPos + len <= data.Length)
                {
                    v = ASCIIEncoding.UTF8.GetString(data, rPos, len);
                    Skip(len);
                }
                else
                    v = ASCIIEncoding.UTF8.GetString(ReadBytes(len));

                return v;
            }
        }

        public string[] ReadStringArr()
        {
            lock (this)
            {
                int len = ReadInt();
                if (len == -1)
                    return null;
                else
                {
                    string[] arr = new string[len];
                    for (int i = 0; i < len; i++)
                        arr[i] = ReadString();
                    return arr;
                }
            }
        }

        #endregion

        #region 保护部分

        // 存放数据的裸字节数组
        byte[] data = new byte[1024];

        // 读位置
        int rPos = 0;

        // 有效数据长度
        int available = 0;

        // 包装不同基本类型的读取操作
        delegate T ConverterHandler<T>(byte[] src, int pos);
        T Read<T>(ConverterHandler<T> converter, int typeSize)
        {
            T v = Peek(converter, typeSize);
            Skip(typeSize);
            return v;
        }

        // 从当前读位置拾取一个数据，但不改变缓冲区状态
        T Peek<T>(ConverterHandler<T> converter, int typeSize)
        {
            int cnt = typeSize;
            if (cnt > available)
                throw new Exception("RingBuffer peeking overflow");

            if (rPos + cnt > data.Length)
            {
                byte[] tmp = new byte[cnt];
                int len1 = data.Length - rPos;
                int len2 = cnt - len1;
                Array.Copy(data, rPos, tmp, 0, len1);
                Array.Copy(data, 0, tmp, len1, len2);
                return converter(tmp, 0);
            }
            else
                return converter(data, rPos);
        }

        // 写位置
        int WritePos
        {
            get
            {
                if (available >= data.Length)
                    return -1;
                if (rPos + available < data.Length)
                    return rPos + available;
                else
                    return rPos + available - data.Length;
            }
        }

        // 检查并保证可用空间
        void MakeSureCapacity(int cnt)
        {
            // 空间够用了
            if (data.Length - available >= cnt)
                return;

            // 如果空间不够，则重新分配
            int times = 2;
            while (data.Length * times - available < cnt)
                times *= 2;

            byte[] newData = new byte[data.Length * times];
            if (rPos + available < data.Length)
                Array.Copy(data, rPos, newData, 0, available);
            else
            {
                int len1 = data.Length - rPos;
                int len2 = available - len1;
                Array.Copy(data, rPos, newData, 0, len1);
                Array.Copy(data, 0, newData, len1, len2);
            }

            data = newData;
            rPos = 0;
        }

        #endregion
    }
}
