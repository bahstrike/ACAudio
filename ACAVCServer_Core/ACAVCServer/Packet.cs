using System;
using System.Text;
using System.IO;
using System.Net.Sockets;

namespace ACAVCServer
{
    internal class Packet
    {
        private const int MAGIC = 0x0ACA6D10;//never change magic unless this internal packet structure changes

        public const int ProtocolVersion = 0x20000000;//update this if our messages or expected number of read/writes changes between versions

        private const int MAX_BYTES = 100 * 1024;// i dunno, some number of kilobytes for a single packet

        public const int DefaultTimeoutMsec = 1000;

        public const int HeartbeatMsec = 1000;

        // determined upon read or send
        private int _FinalSizeBytes = 0;
        public int FinalSizeBytes
        {
            get
            {
                return _FinalSizeBytes;
            }
        }

        public enum MessageType
        {
            Heartbeat,                      // any                  no info; just a keep-alive if nothing else has been sent  (so lost sockets can be detected)
            Disconnect,                     // any                  signal that the connection will be terminated
            PlayerConnect,                  // client->server       sent during initial connection to provide player info
            StreamInfo,                     // server->client       specifies the currently accepted audio format
            RawAudio,                       // client->server       anonymous audio fragment
            DetailAudio,                    // server->client       audio fragment with appropriate source information
            ClientStatus,                   // client->server       sent often by clients to inform server where players are and whatnot
            ServerStatus,                   // server->client       sent often by server to inform client if there are any potential listeners and whatnot
        }

        public readonly MessageType Message;

        public static Packet InternalReceive(TcpClient client, int timeoutMsec=DefaultTimeoutMsec, bool allowTimeoutToCancelPartial=false)
        {
            try
            {
                DateTime start;
                StagedInfo stagedInfo = null;
                
                
                start = DateTime.Now;
                for (; ; )
                {
                    Packet p = InternalReceive(client, ref stagedInfo);
                    if (p != null)
                        return p;

                    System.Threading.Thread.Sleep(1);

                    // we can only honor the timeout during the initial header stage.  once we have gotten a header then we cant skip the rest without corrupting tcp stream
                    if ((allowTimeoutToCancelPartial || stagedInfo == null) && DateTime.Now.Subtract(start).TotalMilliseconds >= timeoutMsec)
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public class StagedInfo
        {
            public int Version;
            public MessageType Message;
            public int Length;
        }

        // alternative that will never wait.  rather, if a packet header is received but payload is not ready, then stagedInfo will be populated
        // with the "in progress" packet which you must pass in again on next receive attempt.
        // set your stagedInfo to null upon startup or connection termination,  but otherwise never use or manipulate it.
        public static Packet InternalReceive(TcpClient client, ref StagedInfo stagedInfo)
        {
            try
            {
                // if we have no staged packet, attempt to construct one if we have enough data available for a header
                if(stagedInfo == null)
                {
                    if (client.Available < 16)
                        return null;

                    BinaryReader br = new BinaryReader(client.GetStream());
                    if (br.ReadInt32() != MAGIC)
                        return null;

                    stagedInfo = new StagedInfo();

                    stagedInfo.Version = br.ReadInt32();
                    stagedInfo.Message = (MessageType)br.ReadInt32();
                    stagedInfo.Length = br.ReadInt32();

                    if (stagedInfo.Length < 0 || stagedInfo.Length > MAX_BYTES)
                    {
                        stagedInfo = null;// yuck.  reject this attempt
                        return null;
                    }
                }


                // now that we have a staged packet, attempt to complete it if we have enough data available to finish out its payload
                if (client.Available < stagedInfo.Length)
                    return null;



                byte[] buf;
                if (stagedInfo.Length == 0)
                    buf = new byte[0];
                else
                {
                    buf = new BinaryReader(client.GetStream()).ReadBytes(stagedInfo.Length);

                    if (buf.Length != stagedInfo.Length)
                    {
                        stagedInfo = null;// shrug.. i guess whole packet is bad
                        return null;
                    }
                }

                // after we've read/skipped past the remainder of the message, check protocol version to see if we even care
                if (stagedInfo.Version != ProtocolVersion)
                    return null;

                // ok we've received all data for this packet. promote the staged packet to final
                Packet p = new Packet(stagedInfo.Message, buf);
                p._FinalSizeBytes = 16 + buf.Length;

                stagedInfo = null;

                return p;
            }
            catch
            {
                return null;
            }
        }

        public readonly MemoryStream Stream;

        public int ReadInt()
        {
            byte[] b = new byte[4];
            Stream.Read(b, 0, 4);
            return BitConverter.ToInt32(b, 0);
        }
        public bool ReadBool()
        {
            return (Stream.ReadByte() != 0);
        }

        public string ReadString()
        {
            byte[] buf = ReadBuffer();
            if (buf.Length == 0)
                return string.Empty;

            return Encoding.UTF8.GetString(buf);
        }

        public byte[] ReadBuffer()
        {
            int len = ReadInt();
            if (len <= 0 || len > MAX_BYTES)
                return new byte[0];

            byte[] buf = new byte[len];
            Stream.Read(buf, 0, len);
            return buf;
        }
        
        public void WriteInt(int i)
        {
            Stream.Write(BitConverter.GetBytes(i), 0, 4);
        }

        public void WriteBool(bool b)
        {
            Stream.WriteByte((byte)(b ? 1 : 0));
        }

        public void WriteString(string s)
        {
            WriteBuffer(Encoding.UTF8.GetBytes(s));
        }

        public void WriteBuffer(byte[] buf)
        {
            WriteInt(buf.Length);

            if (buf.Length > 0)
                Stream.Write(buf, 0, buf.Length);
        }

        private Packet(MemoryStream ms)
        {
            Stream = ms;
        }

        private Packet(MessageType _Message, byte[] buf)
            : this(new MemoryStream(buf))
        {
            Message = _Message;
        }

        public Packet(MessageType _Message)
            : this(new MemoryStream())
        {
            Message = _Message;
        }

        public void InternalSend(TcpClient client)
        {
            try
            {
                //stream.Flush();//shouldnt be necessary for memorystream

                byte[] buf = Stream.ToArray();

                BinaryWriter bw = new BinaryWriter(client.GetStream());
                bw.Write(MAGIC);
                bw.Write(ProtocolVersion);
                bw.Write((int)Message);
                bw.Write(buf.Length);
                bw.Write(buf);

                bw.Flush();

                //client.GetStream().Flush();// supposedly does nothing for network stream
                _FinalSizeBytes = 16 + buf.Length;
            }
            catch
            {

            }
        }
    }
}
