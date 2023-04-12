using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace AzureDownloader
{
    internal class Program
    {
        private const string AppConfigFile = "appsettings.json";

        private static async Task Main()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();
            var app = serviceProvider.GetService<AzureArtifactDownloader>();

            if (app != null) await app.Run();
        }

        private static void ConfigureServices(IServiceCollection services)
        {

            services.AddTransient<IConfiguration>(_ =>
            {
                IConfigurationBuilder configurationBuilder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory());
                configurationBuilder.AddJsonFile(AppConfigFile);

                return configurationBuilder.Build();
            });
            services.AddLogging(configure => configure.AddConsole())
                .AddTransient<AzureArtifactDownloader>();

        }
    }

}
