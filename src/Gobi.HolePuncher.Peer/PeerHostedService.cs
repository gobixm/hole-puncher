using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Gobi.HolePuncher.Peer
{
    public sealed class PeerHostedService : IHostedService
    {
        private readonly PeerService _peerService;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        public PeerHostedService(PeerService peerService, IConfiguration configuration)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
            _peerService = peerService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _peerService.RunAsync(_cancellationTokenSource.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }
    }
}