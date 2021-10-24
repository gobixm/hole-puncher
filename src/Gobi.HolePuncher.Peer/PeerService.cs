using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Channels;
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
        private readonly UdpSocket _peerSocket = new();
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
            _relayClient.Listen(OnRelayMessage, cancellationToken);
            _peerSocket.Bind(_privateEndpoint);
            var reader = _peerSocket.Listen(100, cancellationToken);
            Task.Run(() => ProcessPeerMessages(reader));


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

        private void ProcessPeerMessages(ChannelReader<UdpResult> reader)
        {
            var messages = reader.ReadAllAsync();
            messages.ForEachAwaitAsync(ProcessPeerMessage);
        }

        private async Task ProcessPeerMessage(UdpResult receive)
        {
            Logger.Information("Got message from {Remote} size: {Size}", receive.Remote, receive.Data.Length);
            try
            {
                var message = _serializer.Deserialize(receive.Data);
                switch (message)
                {
                    case EchoRequest echoRequest:
                        await ProcessEchoRequest(echoRequest, receive.Remote);
                        break;
                    case EchoResponse echoResponse:
                        await ProcessEchoResponse(echoResponse, receive.Remote);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to process message from {Remote}", receive.Remote);
            }

            await Task.CompletedTask;
        }

        private Task ProcessEchoResponse(EchoResponse echoResponse, IPEndPoint receiveRemote)
        {
            Logger.Information("Got echo response {EchoResponse} from {Remote}", echoResponse, receiveRemote);

            return Task.CompletedTask;
        }

        private async Task SendPunchRequest()
        {
            await _relayClient.PunchAsync(new PunchHoleRequest
            {
                SourcePeerId = _id,
                TargetPeerId = _targetId
            });
        }

        private async Task OnRelayMessage(object message, IPEndPoint sender)
        {
            switch (message)
            {
                case PunchHole punchHole:
                    await ProcessPunchHole(punchHole);
                    return;
            }
        }

        private async Task ProcessEchoRequest(EchoRequest echoRequest, IPEndPoint sender)
        {
            Logger.Information("Got echo request {Request} from {Endpoint}", echoRequest, sender);
            var message = _serializer.SerializeBytes(new EchoResponse
            {
                Id = _id,
                Payload = $"echo reply from {_id} to {_targetId}"
            });
            await _peerSocket.SendToAsync(message, sender);
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

            foreach (var retry in Enumerable.Range(0, 3))
                try
                {
                    await socket.SendToAsync(_serializer.SerializeBytes(new EchoRequest
                    {
                        Payload = $"echo request from {_id} to {_targetId}"
                    }), endpoint);
                    var buffer = new byte[0xffff];
                    var reply = await socket.ReceiveAsync(endpoint, buffer, new CancellationTokenSource(10_000).Token);
                    var replyMessage = _serializer.Deserialize(reply.Data);
                    Logger.Information("Got reply {Reply} from {Remote}", replyMessage, reply.Remote);
                }
                catch (Exception e)
                {
                    Logger.Warning(e, "Failed to echo {Remote}", endpoint);
                    await Task.Delay(5_000);
                }
        }
    }
}