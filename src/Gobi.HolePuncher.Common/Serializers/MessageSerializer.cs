using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gobi.HolePuncher.Common.Messages;
using MessagePack;

namespace Gobi.HolePuncher.Common.Serializers
{
    public sealed class MessageSerializer : IMessageSerializer
    {
        private static readonly Dictionary<Type, short> MessagesMap;

        private static readonly Dictionary<short, Type> InvertedMessagesMap;

        static MessageSerializer()
        {
            MessagesMap = new Dictionary<Type, short>
            {
                {typeof(RegisterPeer), 0x1},
                {typeof(PunchHoleRequest), 0x2},
                {typeof(PunchHole), 0x3},
                {typeof(EchoRequest), 0x4},
                {typeof(EchoResponse), 0x5},
            };
            InvertedMessagesMap = MessagesMap.ToDictionary(x => x.Value, x => x.Key);
        }

        public ReadOnlySpan<byte> Serialize<T>(T message)
        {
            using var memoryStream = new MemoryStream(256);
            SerializeToStream(message, memoryStream);
            return new ReadOnlySpan<byte>(memoryStream.GetBuffer(), 0, (int) memoryStream.Length);
        }

        public byte[] SerializeBytes<T>(T message)
        {
            using var memoryStream = new MemoryStream(256);
            if (!MessagesMap.TryGetValue(typeof(T), out var typeCode)) throw new ArgumentException(nameof(message));

            memoryStream.WriteByte((byte) (typeCode >> 8));
            memoryStream.WriteByte((byte) ((typeCode << 8) >> 8));

            MessagePackSerializer.Serialize(memoryStream, message);
            return memoryStream.ToArray();
        }

        public object Deserialize(ReadOnlyMemory<byte> data)
        {
            if (data.Length < 2) throw new ArgumentException(nameof(data));

            var typeCode = (short) ((data.Span[0] << 8) + data.Span[1]);

            if (!InvertedMessagesMap.TryGetValue(typeCode, out var type))
                throw new ArgumentException($"Unexpected message type {typeCode}");

            return MessagePackSerializer.Deserialize(type, data.Slice(2, data.Length - 2));
        }

        private static void SerializeToStream<T>(T message, MemoryStream memoryStream)
        {
            if (!MessagesMap.TryGetValue(typeof(T), out var typeCode)) throw new ArgumentException(nameof(message));
            SerializeToStream(message, memoryStream);

            MessagePackSerializer.Serialize(memoryStream, message);
        }
    }
}