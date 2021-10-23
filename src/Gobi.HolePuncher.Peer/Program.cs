using System.Threading.Tasks;
using CommandLine;
using Gobi.HolePuncher.Common.Serializers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Gobi.HolePuncher.Peer
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var options = Parser.Default.ParseArguments<Options>(args)
                .MapResult(x => x, errors => new Options());

            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true)
                .AddEnvironmentVariables()
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            await Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(x =>
                    x.AddJsonFile("appsettings.json", false, true)
                        .AddEnvironmentVariables())
                .ConfigureServices((b, c) =>
                    c.AddHostedService<PeerHostedService>()
                        .AddTransient(_ => options)
                        .AddTransient<PeerService>()
                        .AddTransient<IMessageSerializer, MessageSerializer>()
                        .AddLogging(x => x.AddSerilog()))
                .RunConsoleAsync();
        }
    }
}