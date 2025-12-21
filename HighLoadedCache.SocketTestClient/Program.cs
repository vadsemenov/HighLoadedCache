using System.Text.Json;
using HighLoadedCache.Domain.Dto;
using HighLoadedCache.SocketTestClient;
using NBomber.CSharp;
using NBomber.Http;
using NBomber.Plugins.Network.Ping;

// NBomberRunner
//     .RegisterScenarios(NBomberScenarios.CreateScenario())
//     .WithWorkerPlugins(
//         new PingPlugin(PingPluginConfig.CreateDefault("mysite.com")),
//         new HttpMetricsPlugin([HttpVersion.Version1])
//     )
//     .Run();

string host = "127.0.0.1";
int port = 8081;

await using var client = new SimpleTcpClient(host, port);
await client.ConnectAsync();

var key = $"k_12345";
var value = JsonSerializer.Serialize(new UserProfile { Id = 0, Username = "UserName", CreatedAt = DateTime.Now });

await client.SetAsync(key, value);

Console.Read();