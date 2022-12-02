using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace ACACommon
{
    public abstract class ZipUtil
    {
        public abstract bool IsValid
        {
            get;
        }

        public abstract int Position
        {
            get;

            set;
        }

        public abstract int Length
        {
            get;
        }

        public ZipUtil()
        {

        }

        ~ZipUtil()
        {
            Close();
        }

        public abstract void Close();

        public abstract int Write(IntPtr ptr, int bytes);

        public abstract int Read(IntPtr ptr, int bytes);

        public T ReadStruct<T>()
        {
            int sz = Marshal.SizeOf(typeof(T));
            IntPtr p = Marshal.AllocHGlobal(sz);
            Read(p, sz);
            T t = (T)Marshal.PtrToStructure(p, typeof(T));
            Marshal.FreeHGlobal(p);
            return t;
        }

        public unsafe void WriteByte(byte b)
        {
            Write((IntPtr)(&b), sizeof(byte));
        }

        public void WriteBool(bool b)
        {
            WriteByte((byte)(b ? 1 : 0));
        }

        public unsafe void WriteInt(int i)
        {
            Write((IntPtr)(&i), sizeof(int));
        }

        public unsafe void WriteUInt(uint i)
        {
            Write((IntPtr)(&i), sizeof(uint));
        }

        public unsafe void WriteShort(ushort s)
        {
            Write((IntPtr)(&s), sizeof(ushort));
        }

        public unsafe void WriteFloat(float f)
        {
            Write((IntPtr)(&f), sizeof(float));
        }

        public unsafe void WriteDouble(double d)
        {
            Write((IntPtr)(&d), sizeof(double));
        }

        public unsafe void WriteInt64(Int64 i)
        {
            Write((IntPtr)(&i), sizeof(Int64));
        }

        public unsafe void WriteString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                WriteInt(0);
                return;
            }

            WriteInt(str.Length);

            if (str.Length > 0)
            {
                byte[] bytes = new System.Text.ASCIIEncoding().GetBytes(str);
                fixed (byte* pBytes = bytes)
                {
                    Write((IntPtr)pBytes, str.Length);
                }
            }
        }

        public unsafe void Write(byte[] array, int offset, int count)
        {
            if (count <= 0)
                return;

            fixed (byte* pArray = array)
            {
                Write((IntPtr)(pArray + offset), count);
            }
        }

        public void WriteBuffer(byte[] buf)
        {
            if (buf == null)
            {
                WriteInt(-1);
                return;
            }

            WriteInt(buf.Length);
            Write(buf, 0, buf.Length);
        }

        public unsafe byte ReadByte()
        {
            byte b;
            Read((IntPtr)(&b), sizeof(byte));
            return b;
        }

        public unsafe byte[] ReadBytes(int count)
        {
            byte[] buf = new byte[count];
            fixed (byte* pbuf = buf)
                Read((IntPtr)pbuf, count);
            return buf;
        }

        public unsafe ushort[] ReadShorts(int count)
        {
            ushort[] buf = new ushort[count];
            fixed (ushort* pbuf = buf)
                Read((IntPtr)pbuf, count * 2);
            return buf;
        }

        public bool ReadBool()
        {
            return (ReadByte() != 0);
        }

        public unsafe int ReadInt()
        {
            int i;
            Read((IntPtr)(&i), sizeof(int));
            return i;
        }

        public unsafe uint ReadUInt()
        {
            uint i;
            Read((IntPtr)(&i), sizeof(uint));
            return i;
        }

        public unsafe uint ReadUIntBE()
        {
            uint i;
            byte* pi = (byte*)&i;
            pi[3] = ReadByte();
            pi[2] = ReadByte();
            pi[1] = ReadByte();
            pi[0] = ReadByte();
            return i;
        }

        public unsafe ushort ReadShort()
        {
            ushort i;
            Read((IntPtr)(&i), sizeof(ushort));
            return i;
        }

        public unsafe float ReadFloat()
        {
            float f;
            Read((IntPtr)(&f), sizeof(float));
            return f;
        }

        public unsafe double ReadDouble()
        {
            double d;
            Read((IntPtr)(&d), sizeof(double));
            return d;
        }

        public unsafe Int64 ReadInt64()
        {
            Int64 i;
            Read((IntPtr)(&i), sizeof(Int64));
            return i;
        }

        public unsafe string ReadString(int numChars, bool trimNulls = true)
        {
            sbyte[] str = new sbyte[numChars];
            fixed (sbyte* pstr = str)
            {
                Read((IntPtr)pstr, numChars);

                string ret = new string(pstr, 0, numChars);

                if (trimNulls)
                {
                    int i = ret.IndexOf('\0');
                    if (i != -1)
                        ret = ret.Substring(0, i);
                }

                return ret;
            }
        }

        public string ReadStringSZ()
        {
            StringBuilder sb = new StringBuilder();

            for (; ; )
            {
                char c = (char)ReadByte();
                if (c == 0)
                    break;

                sb.Append(c);
            }

            return sb.ToString();
        }

        public unsafe string ReadString()
        {
            int l = ReadInt();
            if (l == 0)
                return string.Empty;


            const int maxStackBytes = 8 * 1024; /* anything above 8kb lets do on heap */
            if (l < maxStackBytes)
            {
                sbyte* pStr = stackalloc sbyte[l + 1];

                Read((IntPtr)pStr, l);
                pStr[l] = 0;

                return new string(pStr);
            }
            else
            {
                sbyte* pStr = (sbyte*)Marshal.AllocHGlobal(l + 1);

                Read((IntPtr)pStr, l);
                pStr[l] = 0;

                string str = new string(pStr);

                Marshal.FreeHGlobal((IntPtr)pStr);

                return str;
            }
        }

        public unsafe int Read(byte[] array, int offset, int count)
        {
            if (count <= 0)
                return 0;

            fixed (byte* pArray = array)
            {
                return Read((IntPtr)(pArray + offset), count);
            }
        }

        public byte[] ReadBuffer()
        {
            int len = ReadInt();
            if (len < 0)
                return null;

            byte[] buf = new byte[len];
            Read(buf, 0, len);

            return buf;
        }
    }

    public class ZipUtil_Stream : ZipUtil
    {
        protected Stream stream;

        public override bool IsValid
        {
            get
            {
                return (stream != null);
            }
        }

        public override int Position
        {
            get
            {
                if (!IsValid)
                    return 0;

                return (int)stream.Position;
            }

            set
            {
                if (!IsValid)
                    return;

                stream.Position = (int)value;
            }
        }

        public Stream Stream
        {
            get
            {
                return stream;
            }
        }

        public override int Length
        {
            get
            {
                if (!IsValid)
                    return 0;

                return (int)stream.Length;
            }
        }

        public ZipUtil_Stream(Stream _stream)
        {
            stream = _stream;
        }

        public override void Close()
        {
            if (IsValid)
            {
                try
                {
                    stream.Flush();
                }
                catch
                {

                }
                stream = null;
            }
        }

        public unsafe override int Write(IntPtr ptr, int bytes)
        {
            if (!IsValid)
                return 0;

            byte[] buf = new byte[bytes];
            Marshal.Copy(ptr, buf, 0, bytes);

            stream.Write(buf, 0, bytes);

            return bytes;
        }

        public unsafe override int Read(IntPtr ptr, int bytes)
        {
            if (!IsValid)
                return 0;

            byte[] buf = new byte[bytes];
            int read = stream.Read(buf, 0, bytes);

            Marshal.Copy(buf, 0, ptr, read);

            return (int)read;
        }
    }
}
