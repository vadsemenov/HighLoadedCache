using System.Net.Sockets;
using System.Text;

try
{
    using var client = new TcpClient();
    await client.ConnectAsync("127.0.0.1", 8081);

    await using var stream = client.GetStream();
    var commands = new[]
    {
        "SET key1 value1",
        "GET key1",
        "DEL key1",
        "PING"
    };

    var buffer = new byte[1024];
    var byteCount = await stream.ReadAsync(buffer);
    var response = Encoding.UTF8.GetString(buffer, 0, byteCount);
    Console.WriteLine($"Получено: {response}");

    while (Console.ReadKey().Key != ConsoleKey.Escape)
    {
        foreach (var command in commands)
        {
            var data = Encoding.UTF8.GetBytes(command);
            await stream.WriteAsync(data);
            Console.WriteLine($"Отправлено: {command}");

            // ИСПРАВЬ!

            byteCount = await stream.ReadAsync(buffer);
            response = Encoding.UTF8.GetString(buffer, 0, byteCount);
            Console.WriteLine($"Получено: {response}");

            Console.ReadKey();
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка клиента: {ex.Message}");
}