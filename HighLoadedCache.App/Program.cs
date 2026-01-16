using HighLoadedCache.Domain;
using HighLoadedCache.Infrastructure;
using HighLoadedCache.Services.Abstraction;
using HighLoadedCache.Services.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(DiagnosticsConfig.ServiceName, serviceVersion: "1.0.0");

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(DiagnosticsConfig.ServiceName)
    .AddConsoleExporter()
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddMeter(DiagnosticsConfig.ServiceName)
    .AddConsoleExporter()
    .Build();

var builder = Host.CreateApplicationBuilder(args);

IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

builder.Services.AddOptions<TcpSettings>().Bind(configuration.GetSection("TcpSettings"));

builder.Services.AddSingleton<ISimpleStore, SimpleStore>();
builder.Services.AddSingleton<ITcpServer, TcpServer>();

var host = builder.Build();

await RunTcpServerAsync(host);

await host.RunAsync();

await host.WaitForShutdownAsync();

async Task RunTcpServerAsync(IHost hostProvider)
{
    using var server = hostProvider.Services.GetService<ITcpServer>();

    try
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        await server!.StartAsync(cancellationTokenSource.Token);

        Console.WriteLine("Сервер запущен. Нажмите Enter для остановки...");
        Console.ReadLine();

        await cancellationTokenSource.CancelAsync();

        Console.WriteLine("Остановка сервера...");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка при работе сервера: {ex.Message}");
    }
}