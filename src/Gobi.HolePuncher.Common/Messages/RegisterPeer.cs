using MessagePack;

namespace Gobi.HolePuncher.Common.Messages
{
    [MessagePackObject]
    public sealed record RegisterPeer
    {
        [Key(0)] public string Id { get; init; }
        [Key(1)] public PeerEndpoint Endpoint { get; init; }
    }
}