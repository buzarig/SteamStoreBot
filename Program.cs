using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SteamStoreBot; // тут лежить ApiClient (namespace має бути тим самим)
using SteamStoreBot.Services; // для UserService, CommandHandler, тощо
using SteamStoreBot.Utils; // для KeyboardManager (якщо потрібен десь)
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace SteamStoreBot
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            // 1) Зчитуємо конфіг (botConfig.json + ENV-змінні)
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("botConfig.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Отримуємо основні дані з конфігу
            var botToken = configuration["TelegramBot:Token"];
            var apiBaseUrl = configuration["Api:BaseUrl"];

            if (string.IsNullOrWhiteSpace(botToken))
            {
                throw new InvalidOperationException(
                    "Не знайдено TelegramBot:Token — задайте його в botConfig.json або через ENV VARIABLE TELEGRAMBOT__TOKEN"
                );
            }

            // 2) Налаштовуємо Host та DI Container
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(
                    (context, services) =>
                    {
                        // Додаємо IConfiguration, якщо десь потрібно прямо інжектити конфіг
                        services.AddSingleton<IConfiguration>(configuration);

                        // HttpClient для ApiClient
                        services.AddHttpClient<ApiClient>(client =>
                        {
                            client.BaseAddress = new Uri(apiBaseUrl);
                            client.Timeout = TimeSpan.FromSeconds(30);
                        });

                        // Telegram BotClient
                        services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(
                            botToken
                        ));

                        // UserService (реалізація IUserService)
                        services.AddSingleton<IUserService, UserService>();

                        // Інші «сервіси» бота
                        services.AddSingleton<CallbackHandler>();
                        services.AddSingleton<StateHandler>();
                        services.AddSingleton<TextCommandHandler>();
                        services.AddSingleton<NotificationService>();

                        // **Фасад**: ICommandHandler → CommandHandler
                        services.AddSingleton<ICommandHandler, CommandHandler>();
                        // Якщо раптом потрібно діставати сам CommandHandler (а не через інтерфейс), можна додати:
                        // services.AddSingleton<CommandHandler>(sp => (CommandHandler)sp.GetRequiredService<ICommandHandler>());
                    }
                )
                .UseConsoleLifetime()
                .Build();

            // 3) Отримуємо з DI необхідні екземпляри
            var servicesProvider = host.Services;

            // Ми реєстрували саме ICommandHandler, тому дістанемо його через інтерфейс:
            var commandHandler = servicesProvider.GetRequiredService<ICommandHandler>();
            // Якщо ж у вас строка StartReceiving все ще викликає CommandHandler.HandleCommandAsync,
            // змініть її на HandleAsync (або відповідно адаптуйте).
            var botClient = servicesProvider.GetRequiredService<ITelegramBotClient>();
            var notificationService = servicesProvider.GetRequiredService<NotificationService>();

            // 4) Запускаємо Telegram-Polling
            var cts = new CancellationTokenSource();
            var receiverOptions = new ReceiverOptions
            {
                // Вказуємо, які саме оновлення ми хочемо обробляти
                AllowedUpdates = new UpdateType[] { UpdateType.Message, UpdateType.CallbackQuery },
                DropPendingUpdates = true,
            };

            botClient.StartReceiving(
                // Перший аргумент — делегат, який бере update → передаємо у наш фасад ICommandHandler
                (bot, update, token) => commandHandler.HandleAsync(update, token),
                // Другий аргумент — обробник помилок
                (bot, ex, token) =>
                {
                    Console.WriteLine($"[Telegram Error] {ex.Message}");
                    return Task.CompletedTask;
                },
                receiverOptions,
                cancellationToken: cts.Token
            );

            Console.WriteLine("Бот запущено. Натисніть Enter для зупинки...");

            // 5) Запускаємо NotificationService у фоні (якщо він у вас запускається віч-loop’ом)
            _ = Task.Run(() => notificationService.RunSchedulerAsync(), cts.Token);

            // Чекаємо, доки користувач натисне Enter
            Console.ReadLine();
            cts.Cancel();

            // Невелика затримка, щоб усі цикли встигли «прибратися»
            await Task.Delay(500);
        }
    }
}
