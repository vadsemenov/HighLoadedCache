using HighLoadedCache.SocketTestClient;
using NBomber.CSharp;
using NBomber.Http;
using NBomber.Plugins.Network.Ping;

NBomberRunner
    .RegisterScenarios(NBomberScenarios.CreateScenario())
    .WithWorkerPlugins(
        new PingPlugin(PingPluginConfig.CreateDefault("mysite.com")),
        new HttpMetricsPlugin([HttpVersion.Version1])
    )
    .Run();