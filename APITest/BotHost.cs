
using APITest.Bot;
using APITest.Util;
using APITest;
using Microsoft.Graph.Communications.Common.Telemetry;

namespace APITest
{
    public class BotHost : IBotHost
    {
        private readonly ILogger<BotHost> _logger;

        private WebApplication? app;


        public BotHost(ILogger<BotHost> logger)
        {
            _logger = logger;
        }

        public async Task StartAsync()
        {
            _logger.LogInformation("Starting bot host");
            var builder = WebApplication.CreateBuilder();

            var section = builder.Configuration.GetSection("AppSettings");
            var appSettings = section.Get<AppSettings>();
            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSingleton<IGraphLogger, GraphLogger>(_ => new GraphLogger("APITestWorker", redirectToTrace: true));
            builder.Services.AddSingleton<IBotMediaLogger, BotMediaLogger>();
            builder.Services.AddSingleton<IBotService, BotServices>();
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            builder.Logging.AddEventLog(config => config.SourceName = "APITestWorker");


            builder.Services.AddOptions<AppSettings>()
                .BindConfiguration(nameof(appSettings))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var botInternalHostingProtocol = "https";
            if (appSettings.UseLocalDevSettings)
            {
                botInternalHostingProtocol = "http";

                builder.Services.PostConfigure<AppSettings>(options =>
                {
                    options.BotInstanceExternalPort = 443;
                    options.BotInternalPort = appSettings.BotCallingInternalPort;
                });
            }
            else
            {
                builder.Services.PostConfigure<AppSettings>(options =>
                {
                    options.MediaDnsName = appSettings.MediaDnsName;
                });
            }

            var baseDomain = "+";

            var callListeningUris = new HashSet<string>
            {
                $"{botInternalHostingProtocol}://{baseDomain}:{appSettings.BotCallingInternalPort}",
                $"{botInternalHostingProtocol}://{baseDomain}:{appSettings.BotInternalPort}"
            };

            builder.WebHost.UseUrls(callListeningUris.ToArray());

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ConfigureHttpsDefaults(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = LUtilities.GetCertificateFromStore(appSettings.CertificateThumbprint);
                });
            });

            app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var bot = scope.ServiceProvider.GetRequiredService<IBotService>();
                bot.Initialize();
            }

            // Configure the HTTP request pipeline.
            // if (app.Environment.IsDevelopment())
            // {
            app.UseSwagger();
            app.UseSwaggerUI();
            // }



            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            await app.RunAsync();
        }

        public async Task StopAsync()
        {
            if (app != null)
            {
                using (var scope = app.Services.CreateScope())
                {
                    var bot = scope.ServiceProvider.GetRequiredService<IBotService>();
                    await bot.Shutdown();
                }

                await app.StopAsync();
            }
        }
    }
}