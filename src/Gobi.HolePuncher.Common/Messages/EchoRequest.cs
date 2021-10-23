using MessagePack;

namespace Gobi.HolePuncher.Common.Messages
{
    [MessagePackObject]
    public sealed record EchoRequest
    {
        [Key(0)] public string Payload { get; init; }
    }
}