using System;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json.Linq;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static System.Timers.Timer timer;
    private static Random random = new Random();

    static async Task Main()
    {
        string[] compliments = await File.ReadAllLinesAsync("compliments.txt");

        string token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        string chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");
        string ownerChatId = Environment.GetEnvironmentVariable("OWNER_CHAT_ID");
        string unsplashKey = Environment.GetEnvironmentVariable("UNSPLASH_ACCESS_KEY");
        string sendTime = Environment.GetEnvironmentVariable("SEND_TIME") ?? "16:10";

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatId))
        {
            Console.WriteLine("Environment variables TELEGRAM_BOT_TOKEN or TELEGRAM_CHAT_ID not set");
            return;
        }

        if (string.IsNullOrEmpty(unsplashKey))
        {
            Console.WriteLine("Warning: UNSPLASH_ACCESS_KEY not set. Bot will send only text messages.");
        }

        // Parse the send time
        if (!TimeSpan.TryParse(sendTime, out TimeSpan targetTime))
        {
            Console.WriteLine("Invalid SEND_TIME format. Using default 11:30");
            targetTime = TimeSpan.Parse("11:30");
        }

        // Set up timer to send compliment daily
        timer = new System.Timers.Timer();
        timer.Elapsed += async (sender, e) => await SendComplimentWithImage(compliments, token, chatId, ownerChatId, unsplashKey);
        timer.Interval = CalculateInitialDelay(targetTime);
        timer.AutoReset = true;
        timer.Start();

        Console.WriteLine($"Scheduled to send compliments with cat images daily at {targetTime}");

        // Start fake HTTP server for Render
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://*:{port}/");
        listener.Start();

        Console.WriteLine($"Fake HTTP server started on port {port}");

        // Асинхронная обработка запросов для fake HTTP сервера
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    var response = context.Response;
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentType = "text/plain";
                    string responseString = "OK";
                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                    Console.WriteLine($"Handled request: {context.Request.Url}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling request: {ex.Message}");
                }
            }
        });

        // Handle empty requests for Render
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

                    await Task.Delay(TimeSpan.FromMinutes(10));
                }
            }
        });

        // Keep container alive
        await Task.Delay(-1);
    }

    private static async Task<string> GetRandomCatImageFromUnsplash(string accessKey)
    {
        try
        {
            // Различные запросы для поиска кошек и котят
            string[] catQueries = {
                "cute cat",
                "kitten",
                "adorable kitten",
                "sleeping cat",
                "fluffy cat",
                "cat portrait",
                "playful kitten",
                "cat eyes",
                "tabby cat",
                "persian cat",
                "siamese cat",
                "british shorthair cat",
                "maine coon cat",
                "ragdoll cat",
                "cute kitty",
                "baby kitten",
                "cat paws",
                "cat playing",
                "funny cat",
                "beautiful cat"
            };

            string randomQuery = catQueries[random.Next(catQueries.Length)];

            // Добавляем параметры для получения качественных фото
            string url = $"https://api.unsplash.com/photos/random?query={randomQuery}&orientation=landscape&content_filter=high&client_id={accessKey}";

            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                JObject data = JObject.Parse(json);

                // Получаем URL изображения среднего размера
                string imageUrl = data["urls"]?["regular"]?.ToString();

                // Логируем информацию об изображении
                string photographer = data["user"]?["name"]?.ToString();
                string description = data["description"]?.ToString() ?? data["alt_description"]?.ToString() ?? "No description";
                Console.WriteLine($"Got cat image from Unsplash by {photographer}");
                Console.WriteLine($"Description: {description}");
                Console.WriteLine($"Search query used: {randomQuery}");

                return imageUrl;
            }
            else
            {
                Console.WriteLine($"Failed to get cat image from Unsplash: {response.StatusCode}");
                string errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error details: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting cat image from Unsplash: {ex.Message}");
        }

        return null;
    }

    private static async Task SendComplimentWithImage(string[] compliments, string token, string chatId, string ownerChatId, string unsplashKey)
    {
        try
        {
            int index = (DateTime.UtcNow.DayOfYear - 1) % compliments.Length;
            string compliment = compliments[index];

            // Добавляем эмодзи кошки к комплименту
            string complimentWithCat = $"🐱 {compliment} 🐾";

            bool messageSent = false;

            // Пытаемся получить изображение кошки из Unsplash
            string imageUrl = null;
            if (!string.IsNullOrEmpty(unsplashKey))
            {
                imageUrl = await GetRandomCatImageFromUnsplash(unsplashKey);
            }

            // Если есть изображение, отправляем фото с подписью
            if (!string.IsNullOrEmpty(imageUrl))
            {
                messageSent = await SendPhotoMessage(token, chatId, imageUrl, complimentWithCat);

                // Если не удалось отправить фото, пробуем отправить только текст
                if (!messageSent)
                {
                    Console.WriteLine("Failed to send photo, sending text only");
                    messageSent = await SendTextMessage(token, chatId, complimentWithCat);
                }
            }
            else
            {
                // Если нет изображения, отправляем только текст
                Console.WriteLine("No cat image available, sending text only");
                messageSent = await SendTextMessage(token, chatId, complimentWithCat);
            }

            // Отправка уведомления владельцу
            if (messageSent && !string.IsNullOrEmpty(ownerChatId))
            {
                string notification = $"✅ Комплимент отправлен: {compliment}";
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    notification += "\n📷 С фотографией кошки из Unsplash";
                }
                await SendTextMessage(token, ownerChatId, notification);
            }

            // Reset timer for next day
            timer.Interval = TimeSpan.FromDays(1).TotalMilliseconds;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending compliment: {ex.Message}");

            // Уведомляем владельца об ошибке
            if (!string.IsNullOrEmpty(ownerChatId))
            {
                string errorNotification = $"❌ Ошибка при отправке комплимента: {ex.Message}";
                await SendTextMessage(token, ownerChatId, errorNotification);
            }
        }
    }

    private static async Task<bool> SendPhotoMessage(string token, string chatId, string photoUrl, string caption)
    {
        try
        {
            string url = $"https://api.telegram.org/bot{token}/sendPhoto";

            using (var formContent = new MultipartFormDataContent())
            {
                formContent.Add(new StringContent(chatId), "chat_id");
                formContent.Add(new StringContent(caption), "caption");
                formContent.Add(new StringContent(photoUrl), "photo");
                formContent.Add(new StringContent("HTML"), "parse_mode");

                var response = await client.PostAsync(url, formContent);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Cat photo message sent successfully");
                    return true;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to send cat photo: {response.StatusCode}");
                    Console.WriteLine($"Error details: {errorContent}");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending cat photo message: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> SendTextMessage(string token, string chatId, string text)
    {
        try
        {
            string url = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(text)}&parse_mode=HTML";
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Text message sent successfully");
                return true;
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to send text message: {response.StatusCode}");
                Console.WriteLine($"Error details: {errorContent}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending text message: {ex.Message}");
            return false;
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

        Console.WriteLine($"Next send scheduled at: {target:yyyy-MM-dd HH:mm:ss} UTC");
        return (target - now).TotalMilliseconds;
    }
}