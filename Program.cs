using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SteamStoreBot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SteamStoreBot
{
    internal class Program
    {
        private static ITelegramBotClient _botClient;
        private static ReceiverOptions _receiverOptions;
        private static CommandHandler _commandHandler;

        static void Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ITelegramBotClient>(
                new TelegramBotClient("8019910175:AAHFfXbbOOCHPS77UVn3H925g6gEG0ZkZiQ")
            );
            services.AddSingleton<ApiClient>();
            services.AddSingleton<IUserService, UserService>();
            services.AddSingleton<CommandHandler>();

            var serviceProvider = services.BuildServiceProvider();

            _botClient = serviceProvider.GetRequiredService<ITelegramBotClient>();
            _commandHandler = serviceProvider.GetRequiredService<CommandHandler>();

            var cts = new CancellationTokenSource();
            _receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>(),
                DropPendingUpdates = true,
            };

            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                _receiverOptions,
                cts.Token
            );

            Console.WriteLine("Бот запущений. Натисніть Enter для зупинки...");
            Console.ReadLine();
            cts.Cancel();
        }

        private static async Task HandleUpdateAsync(
            ITelegramBotClient botClient,
            Update update,
            CancellationToken cancellationToken
        )
        {
            await _commandHandler.HandleCommandAsync(update, cancellationToken);
        }

        private static Task HandleErrorAsync(
            ITelegramBotClient botClient,
            Exception exception,
            CancellationToken cancellationToken
        )
        {
            Console.WriteLine($"Помилка: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
