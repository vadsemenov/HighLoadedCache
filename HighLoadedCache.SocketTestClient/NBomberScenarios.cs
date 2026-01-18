using System.Text;
using System.Text.Json;
using HighLoadedCache.Domain.Dto;
using NBomber;
using NBomber.Contracts;
using NBomber.CSharp;

namespace HighLoadedCache.SocketTestClient;

public static class NBomberScenarios
{
    // Пул клиентов и пул данных
    private static readonly ClientPool<SimpleTcpClient> ClientPool = new();
    private static readonly List<byte[]> PayloadPool = new();
    private const int PoolSize = 10000;
    // Пул из 100 соединений для 500 000 RPS
    private const int MaxConnections = 100;

    static NBomberScenarios()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            var payload = $"SET k_{i} {JsonSerializer.Serialize(new UserProfile { Id = i, Username = "User" })}\n";
            PayloadPool.Add(Encoding.UTF8.GetBytes(payload));
        }
    }

    public static ScenarioProps CreateScenario(string host = "127.0.0.1", int port = 8081)
    {
        return Scenario.Create("tcp_set_scenario", async context =>
            {
                // Получаем клиента из пула по номеру инстанса (автоматический Round-Robin)
                var client = ClientPool.GetClient(context.ScenarioInfo.InstanceNumber);
                var payload = PayloadPool[(int)(context.InvocationNumber % PoolSize)];

                try
                {
                    await client.SendAsync(payload);
                    return Response.Ok();
                }
                catch (Exception ex)
                {
                    context.Logger.Error(ex, "Send failed");
                    return Response.Fail();
                }
            })
            .WithInit(async context =>
            {
                // Инициализируем пул соединений перед тестом
                for (int i = 0; i < MaxConnections; i++)
                {
                    var client = new SimpleTcpClient(host, port);
                    await client.ConnectAsync();
                    // Добавляем в пул NBomber
                    ClientPool.AddClient(client);
                }
            })
            .WithClean(async context =>
            {
                // Закрываем все соединения после теста
                ClientPool.DisposeClients();
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(5))
            .WithLoadSimulations(
                Simulation.Inject(
                    rate: 500_000,
                    interval: TimeSpan.FromSeconds(1),
                    during: TimeSpan.FromSeconds(30))
            );
    }
}