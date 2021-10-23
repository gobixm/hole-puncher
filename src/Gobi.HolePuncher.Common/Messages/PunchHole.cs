using MessagePack;

namespace Gobi.HolePuncher.Common.Messages
{
    [MessagePackObject]
    public sealed record PunchHole
    {
        [Key(0)] public string Id { get; init; }
        [Key(1)] public PeerEndpoint PublicEndpoint { get; init; }
        [Key(2)] public PeerEndpoint PrivateEndpoint { get; init; }
    }
}