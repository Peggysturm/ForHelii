using System;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static System.Timers.Timer timer;

    static async Task Main()
    {
        string[] compliments = await File.ReadAllLinesAsync("compliments.txt");

        string token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        string chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");
        string ownerChatId = Environment.GetEnvironmentVariable("OWNER_CHAT_ID"); // Новый chat ID для уведомлений владельцу
        string sendTime = Environment.GetEnvironmentVariable("SEND_TIME") ?? "11:30"; // Default to 11:30 AM UTC

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatId))
        {
            Console.WriteLine("Environment variables TELEGRAM_BOT_TOKEN or TELEGRAM_CHAT_ID not set");
            return;
        }

        // Parse the send time
        if (!TimeSpan.TryParse(sendTime, out TimeSpan targetTime))
        {
            Console.WriteLine("Invalid SEND_TIME format. Using default 11:30");
            targetTime = TimeSpan.Parse("11:30");
        }

        // Set up timer to send compliment daily
        timer = new System.Timers.Timer();
        timer.Elapsed += async (sender, e) => await SendCompliment(compliments, token, chatId, ownerChatId);
        timer.Interval = CalculateInitialDelay(targetTime);
        timer.AutoReset = true; // Repeat daily
        timer.Start();

        Console.WriteLine($"Scheduled to send compliments daily at {targetTime}");

        // Start fake HTTP server for Render
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://*:{port}/");
        listener.Start();

        Console.WriteLine($"Fake HTTP server started on port {port}");

        // Handle empty requests for Render
        // Добавьте это в Main после запуска HTTP сервера
        _ = Task.Run(async () =>
        {
            var appUrl = Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL");
            if (!string.IsNullOrEmpty(appUrl))
            {
                while (true)
                {
                    try
                    {
                        await client.GetAsync($"{appUrl}/health");
                        Console.WriteLine($"[{DateTime.UtcNow}] Self-ping successful");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Self-ping error: {ex.Message}");
                    }

                    await Task.Delay(TimeSpan.FromMinutes(10)); // Пинг каждые 10 минут
                }
            }
        });


        // Keep container alive
        await Task.Delay(-1);
    }

    private static async Task SendCompliment(string[] compliments, string token, string chatId, string ownerChatId)
    {
        try
        {
            int index = (DateTime.UtcNow.DayOfYear - 1) % compliments.Length;
            string compliment = compliments[index];

            string url = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(compliment)}";
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Sent: {compliment}");

                // Отправка уведомления владельцу
                if (!string.IsNullOrEmpty(ownerChatId))
                {
                    string notification = $"Комплимент отправлен: {compliment}";
                    string ownerUrl = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={ownerChatId}&text={Uri.EscapeDataString(notification)}";
                    var ownerResponse = await client.GetAsync(ownerUrl);

                    if (ownerResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Notification sent to owner: {notification}");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to send notification to owner: {ownerResponse.StatusCode}");
                    }
                }
                else
                {
                    Console.WriteLine("OWNER_CHAT_ID not set, skipping notification");
                }
            }
            else
            {
                Console.WriteLine($"Failed to send compliment: {response.StatusCode}");
            }

            // Reset timer for next day
            timer.Interval = TimeSpan.FromDays(1).TotalMilliseconds;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending compliment: {ex.Message}");
        }
    }

    private static double CalculateInitialDelay(TimeSpan targetTime)
    {
        var now = DateTime.UtcNow;
        var target = DateTime.Today.Add(targetTime);

        // If target time has passed today, schedule for tomorrow
        if (target < now)
        {
            target = target.AddDays(1);
        }

        return (target - now).TotalMilliseconds;
    }
}
