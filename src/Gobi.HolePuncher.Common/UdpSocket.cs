using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Gobi.HolePuncher.Common
{
    public sealed class UdpSocket : IDisposable
    {
        private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        public UdpSocket()
        {
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
        }

        public ChannelReader<UdpReceiveResult> Listen(
            IPEndPoint endPoint,
            int receiveBound = 100,
            CancellationToken cancellationToken = default)
        {
            var buffer = new byte[0xffff];
            _socket.Bind(endPoint);
            var remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);

            var receiveChannel = Channel.CreateBounded<UdpReceiveResult>(new BoundedChannelOptions(receiveBound)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false
            });

            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await _socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None,
                        remoteEndpoint);
                    await receiveChannel.Writer.WriteAsync(
                        new UdpReceiveResult(
                            result.RemoteEndPoint as IPEndPoint,
                            new ReadOnlyMemory<byte>(buffer, 0, result.ReceivedBytes)
                        ),
                        cancellationToken);
                }

                receiveChannel.Writer.Complete();
            }, cancellationToken);

            return receiveChannel.Reader;
        }

        public void Connect(IPEndPoint endPoint)
        {
            _socket.Connect(endPoint);
        }

        public async Task<int> SendAsync(byte[] data, IPEndPoint remote)
        {
            return await _socket.SendToAsync(data, SocketFlags.None, remote);
        }

        public async Task<UdpReceiveResult> ReceiveAsync(IPEndPoint remote, ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            var receiveTask = _socket.ReceiveFromAsync(buffer, SocketFlags.None, remote);
            var task = await Task.WhenAny(Task.Delay(-1, cancellationToken), receiveTask);
            if (task != receiveTask) throw new OperationCanceledException();
            var result = await receiveTask;
            return new UdpReceiveResult(
                result.RemoteEndPoint as IPEndPoint,
                buffer.AsMemory());
        }

        public void Dispose()
        {
            _socket?.Dispose();
        }
    }
}