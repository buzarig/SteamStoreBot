using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SteamStoreBot.Services;
using Telegram.Bot;

namespace SteamStoreBot
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("botConfig.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) => { })
                .ConfigureServices(
                    (context, services) =>
                    {
                        services.AddSingleton<IConfiguration>(configuration);

                        var apiBaseUrl = configuration["Api:BaseUrl"];
                        services.AddHttpClient<ApiClient>(client =>
                        {
                            client.BaseAddress = new Uri(apiBaseUrl);
                            client.Timeout = TimeSpan.FromSeconds(30);
                        });

                        var botToken = configuration["TelegramBot:Token"];
                        if (string.IsNullOrWhiteSpace(botToken))
                        {
                            throw new InvalidOperationException(
                                "Не знайдено TelegramBot:Token — "
                                    + "попередньо задайте його у botConfig.json або через змінну середовища TELEGRAMBOT__TOKEN"
                            );
                        }

                        services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(
                            botToken
                        ));

                        services.AddSingleton<IUserService, UserService>();
                        services.AddSingleton<CommandHandler>();
                        services.AddSingleton<NotificationService>();
                    }
                )
                .UseConsoleLifetime()
                .Build();

            var servicesProvider = host.Services;
            var botClient = servicesProvider.GetRequiredService<ITelegramBotClient>();
            var commandHandler = servicesProvider.GetRequiredService<CommandHandler>();
            var notificationService = servicesProvider.GetRequiredService<NotificationService>();

            var cts = new CancellationTokenSource();
            var receiverOptions = new Telegram.Bot.Polling.ReceiverOptions
            {
                AllowedUpdates = Array.Empty<Telegram.Bot.Types.Enums.UpdateType>(),
                DropPendingUpdates = true,
            };

            botClient.StartReceiving(
                (bot, update, token) => commandHandler.HandleCommandAsync(update, token),
                (bot, ex, token) =>
                {
                    Console.WriteLine($"[Telegram Error] {ex.Message}");
                    return Task.CompletedTask;
                },
                receiverOptions,
                cancellationToken: cts.Token
            );

            Console.WriteLine("Бот запущено. Натисніть Enter для зупинки...");

            _ = Task.Run(
                () =>
                {
                    return notificationService.RunSchedulerAsync();
                },
                cts.Token
            );

            Console.ReadLine();
            cts.Cancel();

            await Task.Delay(500);
        }
    }
}
