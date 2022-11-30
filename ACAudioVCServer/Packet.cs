using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net.Sockets;

namespace ACAudioVCServer
{
    public class Packet : Smith.ZipUtil_Stream
    {
        private const int MAGIC = 0x0ACA6D10;
        private const int MAX_BYTES = 100 * 1024;// i dunno, some number of kilobytes for a single packet

        public const int DefaultTimeoutMsec = 1000;

        public static Packet Receive(TcpClient client, int headerTimeoutMsec=DefaultTimeoutMsec, int dataTimeoutMsec=DefaultTimeoutMsec)
        {
            try
            {
                DateTime start;
                
                
                start = DateTime.Now;
                for (; ; )
                {
                    // need 8 bytes:  magic and packet len
                    if (client.Available >= 8)
                        break;

                    System.Threading.Thread.Sleep(1);

                    if (DateTime.Now.Subtract(start).TotalMilliseconds >= headerTimeoutMsec)
                        return null;
                }

                BinaryReader br = new BinaryReader(client.GetStream());
                if (br.ReadInt32() != MAGIC)
                    return null;

                int len = br.ReadInt32();
                if (len <= 0 || len > MAX_BYTES)
                    return null;


                start = DateTime.Now;
                for (; ; )
                {
                    if (client.Available >= len)
                        break;

                    System.Threading.Thread.Sleep(1);

                    if (DateTime.Now.Subtract(start).TotalMilliseconds >= dataTimeoutMsec)
                        return null;
                }


                byte[] buf = br.ReadBytes(len);

                if (buf.Length != len)
                    return null;

                return new Packet(buf);
            }
            catch
            {
                return null;
            }
        }

        private Packet(byte[] buf)
            : base(new MemoryStream(buf))
        {

        }

        public Packet()
            : base(new MemoryStream())
        {

        }

        public void Send(TcpClient client)
        {
            stream.Flush();

            byte[] buf = (stream as MemoryStream).ToArray();

            BinaryWriter bw = new BinaryWriter(client.GetStream());
            bw.Write(MAGIC);
            bw.Write(buf.Length);
            bw.Write(buf);

            bw.Flush();

            client.GetStream().Flush();
        }
    }
}
