using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Gobi.HolePuncher.Relay
{
    public sealed class RelayHostedService : IHostedService
    {
        private readonly RelayService _relayService;

        public RelayHostedService(RelayService relayService, IConfiguration configuration)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
            _relayService = relayService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _relayService.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _relayService.Stop();
            return Task.CompletedTask;
        }
    }
}