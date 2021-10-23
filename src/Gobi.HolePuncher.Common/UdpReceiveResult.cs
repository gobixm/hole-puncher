using System;
using System.Net;

namespace Gobi.HolePuncher.Common
{
    public sealed record UdpReceiveResult(IPEndPoint Remote, ReadOnlyMemory<byte> Data);
}