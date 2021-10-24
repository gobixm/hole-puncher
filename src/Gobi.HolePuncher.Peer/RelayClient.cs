using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Gobi.HolePuncher.Common;
using Gobi.HolePuncher.Common.Messages;
using Gobi.HolePuncher.Common.Serializers;

namespace Gobi.HolePuncher.Peer
{
    public sealed class RelayClient
    {
        private readonly IPEndPoint _relay;
        private readonly IMessageSerializer _serializer;
        private readonly UdpSocket _socket = new();

        public RelayClient(IPEndPoint relay, IMessageSerializer serializer)
        {
            _relay = relay;
            _serializer = serializer;
            _socket.Connect(relay);
        }

        public void Listen(Func<object, IPEndPoint, Task> onMessage,
            CancellationToken cancellationToken)
        {
            var reader = _socket.Listen(100, cancellationToken);
            Task.Run(() => ProcessMessages(reader, onMessage, cancellationToken));
        }

        private void ProcessMessages(ChannelReader<UdpResult> reader, Func<object, IPEndPoint, Task> onMessage,
            CancellationToken cancellationToken)
        {
            var messages = reader.ReadAllAsync(cancellationToken);
            messages.ForEachAwaitAsync(x => ProcessMessageAsync(x, onMessage), cancellationToken);
        }

        private async Task ProcessMessageAsync(UdpResult receive, Func<object, IPEndPoint, Task> onMessage)
        {
            try
            {
                var message = _serializer.Deserialize(receive.Data);
                await onMessage(message, receive.Remote);
            }
            catch (Exception ex)
            {
            }
        }

        public async Task RegisterAsync(RegisterPeer registerPeer)
        {
            var message = _serializer.SerializeBytes(registerPeer);
            await _socket.SendToAsync(message, _relay);
        }

        public async Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request,
            CancellationToken cancellationToken) where TResponse : class
        {
            var message = _serializer.SerializeBytes(request);
            await _socket.SendToAsync(message, _relay);
            var reply = new byte[0xffff];
            var result = await _socket.ReceiveAsync(_relay, new ArraySegment<byte>(reply), cancellationToken);
            return _serializer.Deserialize(result.Data) as TResponse;
        }

        public async Task PunchAsync(PunchHoleRequest punchHoleRequest)
        {
            await _socket.SendToAsync(_serializer.SerializeBytes(punchHoleRequest), _relay);
        }
    }
}