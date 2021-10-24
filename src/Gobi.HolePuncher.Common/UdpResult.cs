using System;
using System.Net;

namespace Gobi.HolePuncher.Common
{
    public sealed record UdpResult(IPEndPoint Remote, ReadOnlyMemory<byte> Data);
}