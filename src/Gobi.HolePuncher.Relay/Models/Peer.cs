using System.Net;

namespace Gobi.HolePuncher.Relay.Models
{
    public sealed record Peer(string Id, IPEndPoint PrivateEndpoint, IPEndPoint PublicEndpoint);
}