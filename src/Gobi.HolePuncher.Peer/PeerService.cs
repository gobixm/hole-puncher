using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Gobi.HolePuncher.Common;
using Gobi.HolePuncher.Common.Messages;
using Gobi.HolePuncher.Common.Serializers;
using Serilog;

namespace Gobi.HolePuncher.Peer
{
    public sealed class PeerService
    {
        private static readonly ILogger Logger = Log.ForContext<PeerService>();
        private readonly bool _active;
        private readonly string _id;
        private readonly IPEndPoint _privateEndpoint;
        private readonly RelayClient _relayClient;
        private readonly IMessageSerializer _serializer;
        private readonly string _targetId;

        public PeerService(Options options, IMessageSerializer messageSerializer)
        {
            _serializer = messageSerializer;
            _privateEndpoint = new IPEndPoint(IPAddress.Parse(options.PrivateIp), options.PrivatePort);
            _id = options.Id;
            _targetId = options.TargetId;
            _active = options.Active;

            _relayClient = new RelayClient(
                new IPEndPoint(IPAddress.Parse(options.RelayIp), options.RelayPort),
                _serializer);
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            _relayClient.Listen(OnMessage, _privateEndpoint, cancellationToken);

            await _relayClient.RegisterAsync(new RegisterPeer
            {
                Id = _id,
                Endpoint = new PeerEndpoint
                {
                    Ip = _privateEndpoint.Address.GetAddressBytes(),
                    Port = _privateEndpoint.Port
                }
            });

            if (_active) await SendPunchRequest();

            await Task.Delay(-1, cancellationToken);
        }

        private async Task SendPunchRequest()
        {
            await _relayClient.PunchAsync(new PunchHoleRequest
            {
                SourcePeerId = _id,
                TargetPeerId = _targetId
            });
        }

        private async Task OnMessage(object message, IPEndPoint sender)
        {
            switch (message)
            {
                case PunchHole punchHole:
                    await ProcessPunchHole(punchHole);
                    return;
                case EchoRequest echoRequest:
                    await ProcessEchoRequest(echoRequest, sender);
                    return;
            }
        }

        private async Task ProcessEchoRequest(EchoRequest echoRequest, IPEndPoint sender)
        {
            Logger.Information("Got echo request from {Endpoint}", sender);
            using var socket = new UdpSocket();
            await socket.SendAsync(_serializer.SerializeBytes(new EchoResponse
            {
                Id = _id,
                Payload = $"echo reply from {_id} to {_targetId}"
            }), sender);
        }

        private async Task ProcessPunchHole(PunchHole punchHole)
        {
            if (punchHole.PrivateEndpoint == null || punchHole.PublicEndpoint == null)
            {
                Logger.Information("Unknown peer {Id}", punchHole.Id);
                if (_active)
                {
                    await Task.Delay(5_000);
                    await SendPunchRequest();
                }

                return;
            }

            // try send to private address
            var privateEndpoint =
                new IPEndPoint(new IPAddress(punchHole.PrivateEndpoint.Ip), punchHole.PrivateEndpoint.Port);
            var privateTask = RequestEchoAsync(privateEndpoint);

            var publicEndpoint =
                new IPEndPoint(new IPAddress(punchHole.PublicEndpoint.Ip), punchHole.PublicEndpoint.Port);
            var publicTask = RequestEchoAsync(publicEndpoint);

            await Task.WhenAny(privateTask, publicTask);
        }

        private async Task RequestEchoAsync(IPEndPoint endpoint)
        {
            Logger.Information("Sending echo to {Endpoint}", endpoint);
            using var socket = new UdpSocket();
            await socket.SendAsync(_serializer.SerializeBytes(new EchoRequest
            {
                Payload = $"echo request from {_id} to {_targetId}"
            }), endpoint);
            var buffer = new byte[0xffff];
            var reply = await socket.ReceiveAsync(endpoint, buffer, new CancellationTokenSource(10_000).Token);
            var replyMessage = _serializer.Deserialize(reply.Data);
            Logger.Information("Got reply {Reply}", replyMessage);
        }
    }
}