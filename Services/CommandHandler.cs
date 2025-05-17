using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SteamStoreBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SteamStoreBot.Services
{
    internal class CommandHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ApiClient _apiClient;
        private readonly IUserService _userService;
        private readonly Dictionary<long, string> _userStates = new Dictionary<long, string>();
        private readonly Dictionary<long, int> _userMessageToDelete = new Dictionary<long, int>();

        public CommandHandler(
            ITelegramBotClient botClient,
            ApiClient apiClient,
            IUserService userService
        )
        {
            _botClient = botClient;
            _apiClient = apiClient;
            _userService = userService;
        }

        public async Task HandleCommandAsync(Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery?.Data != null)
            {
                await HandleCallbackAsync(update.CallbackQuery, cancellationToken);
                return;
            }

            if (update.Type != UpdateType.Message || update.Message?.Text == null)
                return;

            var chatId = update.Message.Chat.Id;
            var text = update.Message.Text.Trim();

            try
            {
                if (_userStates.TryGetValue(chatId, out var state))
                {
                    await HandleUserStateAsync(chatId, text, state, cancellationToken);
                }
                else
                {
                    await HandleCommandTextAsync(chatId, text, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"Сталася помилка: {ex.Message}",
                    cancellationToken: cancellationToken
                );
            }
        }

        private async Task HandleCallbackAsync(CallbackQuery cb, CancellationToken token)
        {
            var chatId = cb.Message.Chat.Id;
            var messageId = cb.Message.MessageId;
            var data = cb.Data;

            if (data.StartsWith("add_to_wishlist_"))
            {
                var appId = int.Parse(data.Substring("add_to_wishlist_".Length));
                await _userService.AddToWishlistAsync(chatId, appId);

                var updated = new InlineKeyboardMarkup(
                    new[] { InlineKeyboardButton.WithCallbackData("✅ У вішлісті", "noop") }
                );

                await _botClient.EditMessageReplyMarkup(
                    chatId: chatId,
                    messageId: messageId,
                    replyMarkup: updated,
                    cancellationToken: token
                );
            }
        }

        private async Task HandleUserStateAsync(
            long chatId,
            string message,
            string state,
            CancellationToken cancellationToken
        )
        {
            _userStates.Remove(chatId);

            switch (state)
            {
                case "WaitingForName":
                    var games = await _apiClient.SearchGamesAsync(message);
                    if (!games.Any())
                    {
                        await _botClient.SendMessage(
                            chatId: chatId,
                            text: "Ігор не знайдено.",
                            cancellationToken: cancellationToken
                        );
                        return;
                    }

                    var kb = new ReplyKeyboardMarkup(
                        games
                            .Select(g => new[] { new KeyboardButton($"{g.Name} (ID: {g.Id})") })
                            .ToArray()
                    )
                    {
                        ResizeKeyboard = true,
                    };

                    var sent = await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Оберіть гру з результатів:",
                        replyMarkup: kb,
                        cancellationToken: cancellationToken
                    );

                    _userMessageToDelete[chatId] = sent.MessageId;
                    _userStates[chatId] = "WaitingForGameSelection";
                    break;

                case "WaitingForGenre":
                    // ЗАГЛУШКА
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Пошук по жанру поки не реалізований.",
                        replyMarkup: KeyboardManager.GetMainKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;

                case "WaitingForBudget":
                    // ЗАГЛУШКА
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Пошук за бюджетом поки не реалізований.",
                        replyMarkup: KeyboardManager.GetMainKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;

                case "WaitingForGameSelection":
                    if (_userMessageToDelete.TryGetValue(chatId, out var msgId))
                    {
                        _userMessageToDelete.Remove(chatId);
                    }

                    var idPart = message
                        .Split(new[] { "(ID:" }, StringSplitOptions.None)
                        .Last()
                        .TrimEnd(')')
                        .Trim();

                    if (int.TryParse(idPart, out var appId))
                        await SendGameDetailsAsync(chatId, appId, cancellationToken);
                    break;

                default:
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Щось пішло не так, повернімося в меню.",
                        replyMarkup: KeyboardManager.GetMainKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;
            }
        }

        private async Task HandleCommandTextAsync(
            long chatId,
            string message,
            CancellationToken cancellationToken
        )
        {
            switch (message)
            {
                case "/start":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Привіт! Я бот для Steam-ігри. Оберіть дію:",
                        replyMarkup: KeyboardManager.GetMainKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;

                case "Список бажань":
                    var settings = await _userService.GetSettingsAsync(chatId);
                    var reply = settings.Wishlist.Any()
                        ? "Ваш список бажань:\n"
                            + string.Join("\n", settings.Wishlist.Select(id => $"Айді гри ({id})"))
                        : "Список бажань порожній.";
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: reply,
                        cancellationToken: cancellationToken
                    );
                    break;

                case "Пошук ігор":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Оберіть тип пошуку:",
                        replyMarkup: KeyboardManager.GetSearchKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;

                case "Пошук по назві":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Введіть назву гри:",
                        replyMarkup: new ReplyKeyboardRemove(),
                        cancellationToken: cancellationToken
                    );
                    _userStates[chatId] = "WaitingForName";
                    break;

                case "Пошук по жанру":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Введіть жанр:",
                        replyMarkup: new ReplyKeyboardRemove(),
                        cancellationToken: cancellationToken
                    );
                    _userStates[chatId] = "WaitingForGenre";
                    break;

                case "Пошук по бюджету":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Введіть бюджет у гривнях:",
                        replyMarkup: new ReplyKeyboardRemove(),
                        cancellationToken: cancellationToken
                    );
                    _userStates[chatId] = "WaitingForBudget";
                    break;

                case "/help":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Доступні команди:\n"
                            + "/start\n"
                            + "Список бажань\n"
                            + "Пошук по назві\n"
                            + "Пошук по жанру\n"
                            + "Пошук по бюджету",
                        replyMarkup: KeyboardManager.GetMainKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;

                default:
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Невідома команда. Використайте меню.",
                        replyMarkup: KeyboardManager.GetMainKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;
            }
        }

        public async Task SendGameDetailsAsync(
            long chatId,
            int appId,
            CancellationToken cancellationToken
        )
        {
            var settings = await _userService.GetSettingsAsync(chatId);
            var wishlist = settings.Wishlist;
            var data = await _apiClient.GetGameDetailsAsync(appId);

            if (
                data == null
                || !data.TryGetValue("data", out var raw)
                || !(raw is JsonElement json)
            )
            {
                await _botClient.SendMessage(
                    chatId,
                    "Не вдалося завантажити інформацію.",
                    cancellationToken: cancellationToken
                );
                return;
            }

            var details = GameDetails.FromJson(json, appId, wishlist);

            await _botClient.SendMessage(
                chatId: chatId,
                text: details.ToHtmlCaption(),
                parseMode: ParseMode.Html,
                replyMarkup: details.ToInlineKeyboard(),
                cancellationToken: cancellationToken
            );

            await _botClient.SendMessage(
                chatId: chatId,
                text: "Що далі?",
                replyMarkup: KeyboardManager.GetMainKeyboard(),
                cancellationToken: cancellationToken
            );
        }
    }
}
