using CommandLine;

namespace Gobi.HolePuncher.Peer
{
    public sealed class Options
    {
        [Option('i', "ip", Required = true, HelpText = "Private IP address")]
        public string PrivateIp { get; set; }

        [Option('p', "port", Required = true, HelpText = "Private Port")]
        public int PrivatePort { get; set; }

        [Option("rip", Required = true, HelpText = "Relay ip address")]
        public string RelayIp { get; set; }

        [Option("rport", Required = true, HelpText = "Relay port")]
        public int RelayPort { get; set; }

        [Option('a', "active", HelpText = "Is this node try to init communication with target peer")]
        public bool Active { get; set; }

        [Option("id", Required = true, HelpText = "Id of this node")]
        public string Id { get; set; }

        [Option("tid", Required = true, HelpText = "Id of target node, to communicate with")]
        public string TargetId { get; set; }
    }
}