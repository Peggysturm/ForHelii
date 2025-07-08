using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;

class Program
{
    static string[] compliments;
    static int dayOfYear => DateTime.UtcNow.DayOfYear;
    static string telegramToken = Environment.GetEnvironmentVariable("7609997966:AAHE8iYn2UTDTM_d5xhHr6LUoPbvm1y1uJ0");
    static string chatId = Environment.GetEnvironmentVariable("7709327999");

    static async Task Main()
    {
        compliments = File.ReadAllLines("compliments.txt");
        await SendCompliment();

        var timer = new System.Timers.Timer(TimeSpan.FromDays(1).TotalMilliseconds);
        timer.Elapsed += async (s, e) => await SendCompliment();
        timer.Start();

        Console.WriteLine("Bot is running...");
        await Task.Delay(-1); // Keep app running
    }

    static async Task SendCompliment()
    {
        if (dayOfYear <= compliments.Length)
        {
            string compliment = compliments[dayOfYear - 1];
            using var client = new HttpClient();
            string url = $"https://api.telegram.org/bot{telegramToken}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(compliment)}";
            await client.GetAsync(url);
            Console.WriteLine($"Sent: {compliment}");
        }
        else
        {
            Console.WriteLine("No compliment found for this day.");
        }
    }
}
