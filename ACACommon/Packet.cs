using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net.Sockets;
using Smith;

namespace ACACommon
{
    public class Packet : ZipUtil_Stream
    {
        private const int MAGIC = 0x0ACA6D10;//never change magic unless this internal packet structure changes

        public const int ProtocolVersion = 0x20000000;//update this if our messages or expected number of read/writes changes between versions

        private const int MAX_BYTES = 100 * 1024;// i dunno, some number of kilobytes for a single packet

        public const int DefaultTimeoutMsec = 1000;

        public const int HeartbeatMsec = 1000;

        public enum MessageType
        {
            Heartbeat,                      // any                  no info; just a keep-alive if nothing else has been sent  (so lost sockets can be detected)
            Disconnect,                     // any                  signal that the connection will be terminated
            PlayerConnect,                  // client->server       sent during initial connection to provide player info
            StreamInfo,                     // server->client       specifies the currently accepted audio format
            RawAudio,                       // client->server       anonymous audio fragment
            DetailAudio,                    // server->client       audio fragment with appropriate source information
        }

        public readonly MessageType Message;

        public static Packet InternalReceive(TcpClient client, int headerTimeoutMsec=DefaultTimeoutMsec, int dataTimeoutMsec=DefaultTimeoutMsec)
        {
            try
            {
                DateTime start;
                
                
                start = DateTime.Now;
                for (; ; )
                {
                    // need 16 bytes:  magic, version, messagetype and packet len
                    if (client.Available >= 16)
                        break;

                    System.Threading.Thread.Sleep(1);

                    if (DateTime.Now.Subtract(start).TotalMilliseconds >= headerTimeoutMsec)
                        return null;
                }

                BinaryReader br = new BinaryReader(client.GetStream());
                if (br.ReadInt32() != MAGIC)
                    return null;

                int version = br.ReadInt32();

                MessageType message = (MessageType)br.ReadInt32();

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

                // after we've read/skipped past the remainder of the message, check protocol version to see if we even care
                if (version != ProtocolVersion)
                    return null;

                return new Packet(message, buf);
            }
            catch
            {
                return null;
            }
        }

        private Packet(MessageType _Message, byte[] buf)
            : base(new MemoryStream(buf))
        {
            Message = _Message;
        }

        public Packet(MessageType _Message)
            : base(new MemoryStream())
        {
            Message = _Message;
        }

        public void InternalSend(TcpClient client)
        {
            try
            {
                //stream.Flush();//shouldnt be necessary for memorystream

                byte[] buf = (stream as MemoryStream).ToArray();

                BinaryWriter bw = new BinaryWriter(client.GetStream());
                bw.Write(MAGIC);
                bw.Write(ProtocolVersion);
                bw.Write((int)Message);
                bw.Write(buf.Length);
                bw.Write(buf);

                bw.Flush();

                //client.GetStream().Flush();// supposedly does nothing for network stream
            }
            catch
            {

            }
        }
    }
}
