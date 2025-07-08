using System;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        string[] compliments = await File.ReadAllLinesAsync("compliments.txt");

        string token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        string chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatId))
        {
            Console.WriteLine("Env variables not set");
            return;
        }

        int index = (DateTime.UtcNow.DayOfYear - 1) % compliments.Length;
        string compliment = compliments[index];

        using var client = new HttpClient();
        string url = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(compliment)}";
        await client.GetAsync(url);

        Console.WriteLine($"Sent: {compliment}");

        // Запускаем фейковый HTTP-сервер для Render
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://*:{port}/");
        listener.Start();

        Console.WriteLine($"Fake HTTP server started on port {port}");

        // Обрабатываем пустые запросы, чтобы Render считал, что всё работает
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var context = await listener.GetContextAsync();
                var response = context.Response;
                var buffer = Encoding.UTF8.GetBytes("Bot is running");
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.Close();
            }
        });

        // Ждём бесконечно — контейнер жив, Render доволен
        await Task.Delay(-1);
    }
}
