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

        // Установите желаемое время отправки (например, 9:00 утра)
        TimeSpan sendTime = new TimeSpan(9, 0, 0); // Час, минута, секунда
        bool messageSentToday = false;

        // Запускаем фейковый HTTP-сервер для Render
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://*:{port}/");
        listener.Start();
        Console.WriteLine($"Fake HTTP server started on port {port}");

        // Обрабатываем запросы в фоновом режиме
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

        // Основной цикл проверки времени
        while (true)
        {
            var now = DateTime.UtcNow;
            var currentTime = now.TimeOfDay;

            // Если текущее время после установленного времени отправки
            // и сообщение еще не отправлено сегодня
            if (currentTime >= sendTime && !messageSentToday)
            {
                int index = (now.DayOfYear - 1) % compliments.Length;
                string compliment = compliments[index];

                try
                {
                    using var client = new HttpClient();
                    string url = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(compliment)}";
                    await client.GetAsync(url);
                    Console.WriteLine($"Sent: {compliment} at {now}");
                    messageSentToday = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending message: {ex.Message}");
                }
            }
            // Если наступил новый день, сбрасываем флаг отправки
            else if (currentTime < sendTime && messageSentToday)
            {
                messageSentToday = false;
            }

            // Проверяем время каждую минуту (можно уменьшить интервал)
            await Task.Delay(TimeSpan.FromMinutes(1));
        }
    }
}