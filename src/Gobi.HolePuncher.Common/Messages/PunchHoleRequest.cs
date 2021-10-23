using MessagePack;

namespace Gobi.HolePuncher.Common.Messages
{
    [MessagePackObject]
    public sealed record PunchHoleRequest
    {
        [Key(0)] public string SourcePeerId { get; init; }
        [Key(1)] public string TargetPeerId { get; init; }
    }
}