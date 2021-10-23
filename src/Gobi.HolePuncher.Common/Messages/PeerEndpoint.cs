using MessagePack;

namespace Gobi.HolePuncher.Common.Messages
{
    [MessagePackObject]
    public sealed record PeerEndpoint
    {
        [Key(1)] public byte[] Ip { get; init; }
        [Key(2)] public int Port { get; init; }
    }
}