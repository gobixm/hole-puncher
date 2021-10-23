using MessagePack;

namespace Gobi.HolePuncher.Common.Messages
{
    [MessagePackObject]
    public sealed record EchoResponse
    {
        [Key(0)] public string Payload { get; init; }
        [Key(1)] public string Id { get; init; }
    }
}