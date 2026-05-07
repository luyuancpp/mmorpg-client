using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace MmorpgClient.Net
{
    /// <summary>
    /// Wire format compatible with muduo ProtobufCodec used by the Gate node:
    ///
    ///   [len:int32 BE]              // bytes of (nameLen + typeName + body + checksum)
    ///   [nameLen:int32 BE]          // length of typeName including trailing '\0'
    ///   [typeName: nameLen bytes]   // proto full message name + '\0'
    ///   [body: variable]            // serialized protobuf payload
    ///   [adler32:int32 BE]          // adler32(nameLen || typeName || body)
    ///
    /// The codec dispatches inbound messages by full type name (e.g.
    /// "MessageContent", "ClientTokenVerifyResponse").
    /// </summary>
    public sealed class MuduoCodec
    {
        private const int HeaderLen = sizeof(int);
        private const int MinMessageLen = 2 * HeaderLen + 2;

        private readonly Dictionary<string, MessageParser> _parsers = new();

        /// <summary>
        /// Register a protobuf message type so the codec can decode incoming
        /// frames carrying that type name.
        /// </summary>
        public void Register<T>() where T : IMessage<T>, new()
        {
            var msg = new T();
            _parsers[msg.Descriptor.FullName] = msg.Descriptor.Parser;
            // muduo uses the *short* type name without package prefix on the
            // wire by default in some configurations -- accept both for safety.
            _parsers[msg.Descriptor.Name] = msg.Descriptor.Parser;
        }

        /// <summary>Encode a single protobuf message into a framed byte array.</summary>
        public byte[] Encode(IMessage message)
        {
            // muduo writes the *full* descriptor name here. The C++ side does:
            //   typeName = message.GetTypeName(); nameLen = typeName.size()+1;
            // GetTypeName() in protobuf C++ returns the fully qualified name
            // (package.Message). For interop we send the full name + '\0'.
            string typeName = message.Descriptor.FullName;
            byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(typeName);
            int nameLen = nameBytes.Length + 1; // include trailing '\0'

            byte[] body = message.ToByteArray();

            int payloadLen = HeaderLen + nameLen + body.Length + HeaderLen; // nameLen field + name + body + checksum
            byte[] buf = new byte[HeaderLen + payloadLen];

            // total len (big endian)
            BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(0, 4), payloadLen);

            int p = HeaderLen;
            // nameLen
            BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(p, 4), nameLen);
            p += 4;
            // typeName
            Buffer.BlockCopy(nameBytes, 0, buf, p, nameBytes.Length);
            p += nameBytes.Length;
            buf[p++] = 0; // trailing '\0'
            // body
            Buffer.BlockCopy(body, 0, buf, p, body.Length);
            p += body.Length;

            // checksum over [nameLen .. body]
            int checksumOffset = HeaderLen;
            int checksumLen = p - checksumOffset;
            uint checksum = Adler32.Compute(buf, checksumOffset, checksumLen);
            BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(p, 4), unchecked((int)checksum));

            return buf;
        }

        /// <summary>
        /// Try to parse a single message from <paramref name="buffer"/>.
        /// Returns null if not enough bytes are available; updates
        /// <paramref name="consumed"/> with the number of bytes consumed
        /// when a message is returned.
        /// </summary>
        public IMessage TryDecode(byte[] buffer, int offset, int available, out int consumed)
        {
            consumed = 0;
            if (available < MinMessageLen + HeaderLen) return null;

            int len = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(offset, 4));
            if (len < MinMessageLen || len > 64 * 1024 * 1024)
                throw new InvalidDataException($"MuduoCodec: invalid frame length {len}");

            int totalNeeded = HeaderLen + len;
            if (available < totalNeeded) return null;

            int p = offset + HeaderLen;
            int nameLen = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(p, 4));
            p += 4;
            if (nameLen <= 0 || nameLen > len - HeaderLen - HeaderLen)
                throw new InvalidDataException($"MuduoCodec: invalid nameLen {nameLen}");

            // typeName ends with '\0'
            string typeName = System.Text.Encoding.ASCII.GetString(buffer, p, nameLen - 1);
            p += nameLen;

            int bodyLen = len - HeaderLen - nameLen - HeaderLen;
            int bodyOffset = p;
            p += bodyLen;

            uint expected = unchecked((uint)BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(p, 4)));
            uint actual = Adler32.Compute(buffer, offset + HeaderLen, len - HeaderLen);
            if (expected != actual)
                throw new InvalidDataException($"MuduoCodec: adler32 mismatch type={typeName}");

            consumed = totalNeeded;

            if (!_parsers.TryGetValue(typeName, out var parser))
            {
                UnityEngine.Debug.LogWarning($"MuduoCodec: no parser registered for type '{typeName}', skipping {bodyLen} bytes");
                return null;
            }
            return parser.ParseFrom(buffer, bodyOffset, bodyLen);
        }
    }
}
