using System;
using Gobi.HolePuncher.Common.Messages;

namespace Gobi.HolePuncher.Common.Serializers
{
    public interface IMessageSerializer
    {
        ReadOnlySpan<byte> Serialize<T>(T message);
        byte[] SerializeBytes<T>(T message);
        object Deserialize(ReadOnlyMemory<byte> data);
    }
}