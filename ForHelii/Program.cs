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
        timer.Elapsed += async (sender, e) => await SendCompliment(compliments, token, chatId);
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

        // Keep container alive
        await Task.Delay(-1);
    }

    private static async Task SendCompliment(string[] compliments, string token, string chatId)
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