using System;
using System.IO;
using System.Net.Http;
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

        // Ждём бесконечно — чтобы контейнер не закрылся
        await Task.Delay(-1);
    }
}
