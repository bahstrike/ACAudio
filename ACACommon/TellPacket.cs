using System;
using System.Collections.Generic;
using System.Text;
using Smith;

namespace ACACommon
{
    public class TellPacket
    {
        private readonly byte magic;
        private BitStream stream;

        private static byte GetUTCMagic()
        {
            return (byte)(((DateTime.Now.ToUniversalTime().Hour & 0b00000111) << 5) |
                        ((DateTime.Now.ToUniversalTime().Minute & 0b00000110) << 2) |
                        ((DateTime.Now.ToUniversalTime().Second & 0b00111000) >> 3));
        }

        public enum MessageType
        {
            Join,               // client->server           same as /tell join
            RequestInfo,        // server->client           after receiving a rawtext "join" tell or whatever, send this back to trigger client's automatic protocol
            ClientInfo,         // client->server           after receiving a RequestInfo, submit client details
            ServerInfo,         // server->client           if we accept the client info then send tcp connection details

        }
        private const int MessageBits = 2;//keep updated to support whatever number of MessageType we want to support

        public readonly MessageType Message;

        public void WriteBits(int v, int num)
        {
            stream.WriteBits(v, num);
        }

        public void WriteByte(byte b)
        {
            stream.WriteBits(b, 8);
        }

        public void WriteInt(int i)
        {
            stream.WriteBits(i, 32);
        }

        public void WriteString(string s)
        {
            stream.WriteString(s);
        }

        public int ReadBits(int num)
        {
            return stream.ReadBits(num);
        }

        public byte ReadByte()
        {
            return (byte)stream.ReadBits(8);
        }

        public int ReadInt()
        {
            return stream.ReadBits(32);
        }

        public string ReadString()
        {
            return stream.ReadString();
        }

        public TellPacket(MessageType _Message)
        {
            magic = GetUTCMagic();
            Message = _Message;

            stream = new BitStream();
            stream.WriteBits((int)Message, MessageBits);
        }

        private TellPacket(byte _magic, MessageType _Message, BitStream _stream)
        {
            magic = _magic;
            Message = _Message;
            stream = _stream;
        }

        public const string prefix = "ACA*";

        public string GenerateString()
        {
            return $"{prefix}{magic.ToString("X2")}{stream.GenerateString(magic)}";
        }

        public static TellPacket FromString(string str)
        {
            if (!str.StartsWith(prefix))
                return null;

            str = str.Substring(prefix.Length);

            byte magic = (byte)int.Parse(str.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            if (magic != GetUTCMagic())
                return null;

            str = str.Substring(2);

            BitStream bs = BitStream.FromString(magic, str);
            if (bs == null)
                return null;

            MessageType msg = (MessageType)bs.ReadBits(MessageBits);

            return new TellPacket(magic, msg, bs);
        }

    }
}
