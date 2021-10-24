using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Gobi.HolePuncher.Common;
using Gobi.HolePuncher.Common.Messages;
using Gobi.HolePuncher.Common.Serializers;
using Gobi.HolePuncher.Relay.Models;
using Serilog;

namespace Gobi.HolePuncher.Relay
{
    public sealed class RelayService
    {
        private static readonly ILogger Logger = Log.ForContext<RelayService>();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly ConcurrentDictionary<string, Peer> _peers = new();
        private readonly IMessageSerializer _serializer;
        private readonly UdpSocket _socket = new();

        public RelayService(IMessageSerializer serializer)
        {
            _serializer = serializer;
        }

        public void Start()
        {
            _socket.Bind(new IPEndPoint(IPAddress.Any, 6000));
            var reader = _socket.Listen(100, _cancellationTokenSource.Token);

            Task.Run(() => ProcessMessages(reader));
        }

        private void ProcessMessages(ChannelReader<UdpResult> reader)
        {
            var messages = reader.ReadAllAsync(_cancellationTokenSource.Token);
            messages.ForEachAwaitAsync(ProcessMessageAsync);
        }

        private async Task ProcessMessageAsync(UdpResult receive)
        {
            Logger.Information("Got message from {Remote} size: {Size}", receive.Remote, receive.Data.Length);
            try
            {
                var message = _serializer.Deserialize(receive.Data);
                switch (message)
                {
                    case RegisterPeer registerPeer:
                        ProcessRegisterPeer(registerPeer, receive.Remote);
                        break;
                    case PunchHoleRequest getPeerRequest:
                        await ProcessPunchHoleRequestAsync(getPeerRequest, receive.Remote);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to process message from {Remote}", receive.Remote);
            }

            await Task.CompletedTask;
        }

        private async Task ProcessPunchHoleRequestAsync(PunchHoleRequest request, IPEndPoint sender)
        {
            var sourcePeer = _peers.GetValueOrDefault(request.SourcePeerId);
            var targetPeer = _peers.GetValueOrDefault(request.TargetPeerId);

            Logger.Information("Punch hole Request {Request} from {Endpoint}", request, sender);
            if (sourcePeer == null || targetPeer == null)
            {
                Logger.Information("Peer not found");
                await _socket.SendToAsync(_serializer.SerializeBytes(new PunchHole
                {
                    Id = request.TargetPeerId,
                    PrivateEndpoint = null,
                    PublicEndpoint = null
                }), sender);
                return;
            }

            await SendPunchHoleAsync(targetPeer, sender);
            await SendPunchHoleAsync(sourcePeer, targetPeer.PublicEndpoint);
        }

        private async Task SendPunchHoleAsync(Peer peer, IPEndPoint target)
        {
            var response = new PunchHole
            {
                Id = peer.Id,
                PrivateEndpoint = new PeerEndpoint
                {
                    Ip = peer.PrivateEndpoint.Address.GetAddressBytes(),
                    Port = peer.PrivateEndpoint.Port
                },
                PublicEndpoint = new PeerEndpoint
                {
                    Ip = peer.PublicEndpoint.Address.GetAddressBytes(),
                    Port = peer.PublicEndpoint.Port
                }
            };
            Logger.Information("Sending punch response to {Target}", target);
            await _socket.SendToAsync(_serializer.SerializeBytes(response), target);
        }

        private void ProcessRegisterPeer(RegisterPeer registerPeer, IPEndPoint sender)
        {
            var peer = new Peer(
                registerPeer.Id,
                new IPEndPoint(new IPAddress(registerPeer.Endpoint.Ip), registerPeer.Endpoint.Port),
                sender
            );
            Logger.Information("Register Peer id={Id} private={Private} public={Public}", peer.Id, peer.PrivateEndpoint,
                peer.PublicEndpoint);

            _peers[registerPeer.Id] = peer;
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
        }
    }
}