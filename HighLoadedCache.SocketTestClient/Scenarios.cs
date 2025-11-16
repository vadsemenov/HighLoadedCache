using System.Security.Cryptography;
using System.Text;
using NBomber.Contracts;
using NBomber.CSharp;

// пространство имён, где лежит Steps.CreateSetStep

namespace HighLoadedCache.SocketTestClient;

public static class Scenarios
{
    private static readonly RandomNumberGenerator NumberGenerator = RandomNumberGenerator.Create();
    private static string RandomKey(int len) => RandomFromAlphabet(len, "abcdefghijklmnopqrstuvwxyz0123456789");
    private static string RandomString(int len) => RandomFromAlphabet(len, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");

    public static ScenarioProps CreateScenario(string host = "127.0.0.1", int port = 8081)
    {
        var scenario = Scenario.Create("tcp_set_scenario", async scenarioContext =>
            {
                try
                {
                    var step = await Step.Run("tcp_set_random", scenarioContext, async () =>
                    {
                        try
                        {
                            await using var client = new SimpleTcpClient(host, port);
                            await client.ConnectAsync();

                            var key = $"k_{scenarioContext.ScenarioInfo.InstanceId}_{scenarioContext.InvocationNumber}_{RandomKey(8)}";
                            var value = RandomString(32);

                            await client.SetAsync(key, value);
                        }
                        catch (Exception exception)
                        {
                            scenarioContext.Logger.Error(exception, "tcp_set_random failed");
                            return Response.Fail();
                        }
                       
                        return Response.Ok();
                    });
                    
                    return step;
                }
                catch (Exception exception)
                {
                    scenarioContext.Logger.Error(exception, "tcp_set_random failed");
                    return Response.Fail();
                }
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(10)) // разогрев 5–10 сек
            .WithLoadSimulations(
                Simulation.Inject(
                    rate: 100, // 100 запросов/сек
                    interval: TimeSpan.FromSeconds(1), // шаг инъекции
                    during: TimeSpan.FromSeconds(30) // длительность плато
                )
            );

        return scenario;
    }

    private static string RandomFromAlphabet(int len, string alphabet)
    {
        Span<byte> bytes = stackalloc byte[len];
        NumberGenerator.GetBytes(bytes);
        var sb = new StringBuilder(len);
        for (int i = 0; i < len; i++) sb.Append(alphabet[bytes[i] % alphabet.Length]);
        return sb.ToString();
    }
}