using System;
using System.Threading;
using System.Threading.Tasks;
using SteamStoreBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SteamStoreBot.Services
{
    internal class CommandHandler : ICommandHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly TextCommandHandler _textHandler;
        private readonly StateHandler _stateHandler;
        private readonly CallbackHandler _callbackHandler;

        public CommandHandler(
            ITelegramBotClient botClient,
            TextCommandHandler textHandler,
            StateHandler stateHandler,
            CallbackHandler callbackHandler
        )
        {
            _botClient = botClient;
            _textHandler = textHandler;
            _stateHandler = stateHandler;
            _callbackHandler = callbackHandler;
        }

        public async Task HandleAsync(Update update, CancellationToken cancellationToken)
        {
            // Обробка CallbackQuery
            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery?.Data != null)
            {
                await _callbackHandler.HandleCallbackAsync(update.CallbackQuery, cancellationToken);
                return;
            }

            // Обробка текстових повідомлень
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                var chatId = update.Message.Chat.Id;
                var text = update.Message.Text.Trim();

                try
                {
                    // Якщо є поточний стан у користувача
                    if (_stateHandler.HasState(chatId))
                    {
                        await _stateHandler.HandleStateAsync(chatId, text, cancellationToken);
                    }
                    else
                    {
                        // Інакше – це нова команда
                        await _textHandler.HandleCommandTextAsync(chatId, text, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: $"❗ Сталася помилка: {ex.Message}",
                        cancellationToken: cancellationToken
                    );
                }
            }
        }
    }
}




//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net.Http;
//using System.Text;
//using System.Text.Json;
//using System.Text.RegularExpressions;
//using System.Threading;
//using System.Threading.Tasks;
//using SteamStoreBot.Models;
//using Telegram.Bot;
//using Telegram.Bot.Types;
//using Telegram.Bot.Types.Enums;
//using Telegram.Bot.Types.ReplyMarkups;

//namespace SteamStoreBot.Utils
//{
//    internal class CommandHandler
//    {
//        private readonly ITelegramBotClient _botClient;
//        private readonly ApiClient _apiClient;
//        private readonly IUserService _userService;
//        private readonly Dictionary<long, string> _userStates = new Dictionary<long, string>();
//        private readonly Dictionary<long, int> _userMessageToDelete = new Dictionary<long, int>();

//        private readonly Dictionary<long, ReplyKeyboardMarkup> _gameKeyboards =
//            new Dictionary<long, ReplyKeyboardMarkup>();

//        public CommandHandler(
//            ITelegramBotClient botClient,
//            ApiClient apiClient,
//            IUserService userService
//        )
//        {
//            _botClient = botClient;
//            _apiClient = apiClient;
//            _userService = userService;
//        }

//        public async Task HandleCommandAsync(Update update, CancellationToken cancellationToken)
//        {
//            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery?.Data != null)
//            {
//                await HandleCallbackAsync(update.CallbackQuery, cancellationToken);
//                return;
//            }

//            if (update.Type != UpdateType.Message || update.Message?.Text == null)
//                return;

//            var chatId = update.Message.Chat.Id;
//            var text = update.Message.Text.Trim();

//            try
//            {
//                if (_userStates.TryGetValue(chatId, out var state))
//                {
//                    await HandleUserStateAsync(chatId, text, state, cancellationToken);
//                }
//                else
//                {
//                    await HandleCommandTextAsync(chatId, text, cancellationToken);
//                }
//            }
//            catch (Exception ex)
//            {
//                await _botClient.SendMessage(
//                    chatId: chatId,
//                    text: $"Сталася помилка: {ex.Message}",
//                    cancellationToken: cancellationToken
//                );
//            }
//        }

//        private async Task HandleCallbackAsync(CallbackQuery cb, CancellationToken token)
//        {
//            var chatId = cb.Message.Chat.Id;
//            var messageId = cb.Message.MessageId;
//            var data = cb.Data;

//            if (data.StartsWith("addwishlist:"))
//            {
//                var parts = data.Split(':');
//                if (parts.Length != 3)
//                    return;

//                if (!int.TryParse(parts[1], out var appId))
//                    return;

//                var currency = parts[2].ToUpper();

//                await _userService.AddToWishlistAsync(chatId, appId);

//                var settings = await _userService.GetSettingsAsync(chatId);
//                var details = await _apiClient.GetGameDetailsAsync(appId, currency, "ukrainian");

//                if (
//                    details != null
//                    && details.TryGetValue("data", out var raw)
//                    && raw is JsonElement json
//                )
//                {
//                    var gameDetails = GameDetails.FromJson(json, appId, settings.Wishlist);
//                    var updatedMarkup = gameDetails.ToInlineKeyboard(
//                        currency,
//                        settings.SubscribedGames
//                    );

//                    await _botClient.EditMessageText(
//                        chatId: chatId,
//                        messageId: messageId,
//                        text: gameDetails.ToHtmlCaption(),
//                        parseMode: ParseMode.Html,
//                        replyMarkup: updatedMarkup,
//                        cancellationToken: token
//                    );
//                }
//            }
//            if (data.StartsWith("subscribe_news:") || data.StartsWith("unsubscribe_news:"))
//            {
//                var parts = data.Split(':');
//                if (parts.Length == 3 && int.TryParse(parts[1], out var appId))
//                {
//                    var action = parts[0];
//                    var currency = parts[2];

//                    var settings = await _userService.GetSettingsAsync(chatId);

//                    if (action == "subscribe_news")
//                    {
//                        await _userService.SubscribeToGameNewsAsync(chatId, appId);
//                        settings = await _userService.GetSettingsAsync(chatId);

//                        var details = await _apiClient.GetGameDetailsAsync(
//                            appId,
//                            currency,
//                            "ukrainian"
//                        );
//                        if (
//                            details != null
//                            && details.TryGetValue("data", out var raw)
//                            && raw is JsonElement json
//                        )
//                        {
//                            var game = GameDetails.FromJson(json, appId, settings.Wishlist);
//                            var newMarkup = game.ToInlineKeyboard(
//                                currency,
//                                settings.SubscribedGames
//                            );

//                            await _botClient.EditMessageReplyMarkup(
//                                chatId,
//                                messageId,
//                                replyMarkup: newMarkup,
//                                cancellationToken: token
//                            );
//                            await _botClient.AnswerCallbackQuery(cb.Id, "✅ Підписка активна!");
//                        }
//                        return;
//                    }
//                    else // action == "unsubscribe_news"
//                    {
//                        if (settings.SubscribedGames.Remove(appId))
//                        {
//                            await _apiClient.UpdateUserSettingsAsync(settings);

//                            var details = await _apiClient.GetGameDetailsAsync(
//                                appId,
//                                currency,
//                                "ukrainian"
//                            );
//                            if (
//                                details != null
//                                && details.TryGetValue("data", out var raw2)
//                                && raw2 is JsonElement json2
//                            )
//                            {
//                                var game = GameDetails.FromJson(json2, appId, settings.Wishlist);
//                                var newMarkup = game.ToInlineKeyboard(
//                                    currency,
//                                    settings.SubscribedGames
//                                );

//                                await _botClient.EditMessageReplyMarkup(
//                                    chatId,
//                                    messageId,
//                                    replyMarkup: newMarkup,
//                                    cancellationToken: token
//                                );
//                                await _botClient.AnswerCallbackQuery(
//                                    cb.Id,
//                                    "🔕 Підписку скасовано"
//                                );
//                            }
//                        }
//                        else
//                        {
//                            await _botClient.AnswerCallbackQuery(cb.Id, "Ви не були підписані");
//                        }
//                        return;
//                    }
//                }
//            }
//            if (data.StartsWith("convert_to_usd_") || data.StartsWith("convert_to_uah_"))
//            {
//                // Розберемо appId і дію
//                if (
//                    data.StartsWith("convert_to_usd_")
//                    && int.TryParse(data.Substring("convert_to_usd_".Length), out var appIdUsd)
//                )
//                {
//                    // Користувач натиснув «показати в $»
//                    var settings = await _userService.GetSettingsAsync(chatId);
//                    var detailsUsd = await _apiClient.GetGameDetailsAsync(
//                        appIdUsd,
//                        "US",
//                        "ukrainian"
//                    );
//                    if (
//                        detailsUsd != null
//                        && detailsUsd.TryGetValue("data", out var rawUsd)
//                        && rawUsd is JsonElement jsonUsd
//                    )
//                    {
//                        var gameUsd = GameDetails.FromJson(jsonUsd, appIdUsd, settings.Wishlist);

//                        // При конвертації в долари, передаємо currency = "US"
//                        var markupUsd = gameUsd.ToInlineKeyboard("US", settings.SubscribedGames);
//                        await _botClient.EditMessageText(
//                            chatId: chatId,
//                            messageId: messageId,
//                            text: gameUsd.ToHtmlCaption(),
//                            parseMode: ParseMode.Html,
//                            replyMarkup: markupUsd,
//                            cancellationToken: token
//                        );
//                    }
//                }
//                else if (
//                    data.StartsWith("convert_to_uah_")
//                    && int.TryParse(data.Substring("convert_to_uah_".Length), out var appIdUah)
//                )
//                {
//                    // Користувач натиснув «показати в грн»
//                    var settings = await _userService.GetSettingsAsync(chatId);
//                    var detailsUah = await _apiClient.GetGameDetailsAsync(
//                        appIdUah,
//                        "UA",
//                        "ukrainian"
//                    );
//                    if (
//                        detailsUah != null
//                        && detailsUah.TryGetValue("data", out var rawUah)
//                        && rawUah is JsonElement jsonUah
//                    )
//                    {
//                        var gameUah = GameDetails.FromJson(jsonUah, appIdUah, settings.Wishlist);
//                        var markupUah = gameUah.ToInlineKeyboard("UA", settings.SubscribedGames);
//                        await _botClient.EditMessageText(
//                            chatId: chatId,
//                            messageId: messageId,
//                            text: gameUah.ToHtmlCaption(),
//                            parseMode: ParseMode.Html,
//                            replyMarkup: markupUah,
//                            cancellationToken: token
//                        );
//                    }
//                }
//                return;
//            }
//            if (data == "subscribe_sales" || data == "unsubscribe_sales")
//            {
//                var settings = await _userService.GetSettingsAsync(chatId);

//                bool nowEnable = data == "subscribe_sales";
//                await _userService.ToggleSalesSubscriptionAsync(chatId, nowEnable);

//                settings = await _userService.GetSettingsAsync(chatId);

//                var newInline = new InlineKeyboardMarkup(
//                    new[]
//                    {
//                        new[]
//                        {
//                            InlineKeyboardButton.WithCallbackData(
//                                settings.SubscriptionOnSales
//                                    ? "🔕 Відписатися від знижок"
//                                    : "🔔 Підписатися на знижки",
//                                settings.SubscriptionOnSales
//                                    ? "unsubscribe_sales"
//                                    : "subscribe_sales"
//                            ),
//                        },
//                    }
//                );

//                await _botClient.EditMessageReplyMarkup(
//                    chatId: chatId,
//                    messageId: messageId,
//                    replyMarkup: newInline,
//                    cancellationToken: token
//                );

//                if (nowEnable)
//                    await _botClient.AnswerCallbackQuery(cb.Id, "✅ Підписка на знижки активована");
//                else
//                    await _botClient.AnswerCallbackQuery(cb.Id, "🔕 Підписку скасовано");

//                return;
//            }
//        }

//        private async Task HandleUserStateAsync(
//            long chatId,
//            string message,
//            string state,
//            CancellationToken cancellationToken
//        )
//        {
//            switch (state)
//            {
//                case "WaitingForName":
//                    List<GameSearchResult> games;
//                    try
//                    {
//                        games = await _apiClient.SearchGamesAsync(message);
//                    }
//                    catch (HttpRequestException)
//                    {
//                        await _botClient.SendMessage(
//                            chatId,
//                            "❗ Сталася помилка при пошуку гри. Спробуйте іншу назву або перевірте правильність введення.",
//                            cancellationToken: cancellationToken
//                        );
//                        _userStates[chatId] = "WaitingForName";
//                        return;
//                    }

//                    if (games == null || !games.Any())
//                    {
//                        await _botClient.SendMessage(
//                            chatId,
//                            "😕 Гру з такою назвою не знайдено. Спробуйте ще раз.",
//                            cancellationToken: cancellationToken
//                        );
//                        _userStates[chatId] = "WaitingForName";
//                        return;
//                    }

//                    var kb = new ReplyKeyboardMarkup(
//                        games
//                            .Select(g => new[] { new KeyboardButton($"{g.Name} (ID: {g.Id})") })
//                            .ToArray()
//                    )
//                    {
//                        ResizeKeyboard = true,
//                    };

//                    var sent = await _botClient.SendMessage(
//                        chatId: chatId,
//                        text: "Оберіть гру з результатів:",
//                        replyMarkup: kb,
//                        cancellationToken: cancellationToken
//                    );

//                    _userMessageToDelete[chatId] = sent.MessageId;
//                    _userStates[chatId] = "WaitingForGameSelection";
//                    break;

//                case "WaitingForGenre":
//                {
//                    var genreSearch = message.Trim();

//                    if (string.IsNullOrWhiteSpace(genreSearch) || genreSearch.Length < 2)
//                    {
//                        await _botClient.SendMessage(
//                            chatId,
//                            "❗️ Введіть жанр гри англійською. Наприклад: RPG, Action, Indie, MMO",
//                            cancellationToken: cancellationToken
//                        );
//                        _userStates[chatId] = "WaitingForGenre";
//                        return;
//                    }

//                    var genreGames = await _apiClient.GetGamesByGenreSpyAsync(genreSearch);

//                    if (genreGames == null || !genreGames.Any())
//                    {
//                        await _botClient.SendMessage(
//                            chatId,
//                            $"😕 Ігор з жанром \"{genreSearch}\" не знайдено. Спробуйте інший жанр. Наприклад: Strategy, Racing.",
//                            cancellationToken: cancellationToken
//                        );
//                        _userStates[chatId] = "WaitingForGenre";
//                        return;
//                    }

//                    var genreGameButtons = genreGames
//                        .Take(10)
//                        .Select(g => new KeyboardButton($"{g.Name} (ID: {g.Id})"))
//                        .Select(b => new[] { b })
//                        .ToList();

//                    await _botClient.SendMessage(
//                        chatId,
//                        $"🎮 Ось ігри у жанрі {genreSearch}:",
//                        replyMarkup: new ReplyKeyboardMarkup(genreGameButtons)
//                        {
//                            ResizeKeyboard = true,
//                        },
//                        cancellationToken: cancellationToken
//                    );

//                    _userStates[chatId] = "WaitingForGameSelection";
//                    break;
//                }

//                case "WaitingForRemoveId":
//                    if (message == "⬅️ Назад")
//                    {
//                        _userStates.Remove(chatId);
//                        await _botClient.SendMessage(
//                            chatId,
//                            "Головне меню:",
//                            replyMarkup: KeyboardManager.GetMainKeyboard(),
//                            cancellationToken: cancellationToken
//                        );
//                        return;
//                    }

//                    if (!int.TryParse(message.Trim(), out var removeId))
//                    {
//                        await _botClient.SendMessage(
//                            chatId,
//                            "❗ Некоректний ID. Введіть, будь ласка, правильний ID гри (тільки цифри), "
//                                + "або натисніть «⬅️ Назад», щоб скасувати:",
//                            replyMarkup: new ReplyKeyboardMarkup(
//                                new[] { new[] { new KeyboardButton("⬅️ Назад") } }
//                            )
//                            {
//                                ResizeKeyboard = true,
//                            },
//                            cancellationToken: cancellationToken
//                        );
//                        return;
//                    }

//                    var userSettings = await _userService.GetSettingsAsync(chatId);

//                    if (!userSettings.Wishlist.Contains(removeId))
//                    {
//                        await _botClient.SendMessage(
//                            chatId,
//                            "❌ Гри з таким ID немає у вашому вішлісті. Введіть інший ID або натисніть «⬅️ Назад», щоб скасувати:",
//                            replyMarkup: new ReplyKeyboardMarkup(
//                                new[] { new[] { new KeyboardButton("⬅️ Назад") } }
//                            )
//                            {
//                                ResizeKeyboard = true,
//                            },
//                            cancellationToken: cancellationToken
//                        );
//                        return;
//                    }

//                    await _userService.RemoveFromWishlistAsync(chatId, removeId);

//                    await _botClient.SendMessage(
//                        chatId,
//                        $"✅ Гру з ID {removeId} успішно видалено з вішліста.",
//                        replyMarkup: KeyboardManager.GetMainKeyboard(),
//                        cancellationToken: cancellationToken
//                    );

//                    _userStates.Remove(chatId);
//                    break;

//                case "WaitingForBudget":
//                {
//                    if (message == "⬅️ Назад")
//                    {
//                        _userStates.Remove(chatId);
//                        _gameKeyboards.Remove(chatId);

//                        await _botClient.SendMessage(
//                            chatId: chatId,
//                            text: "Головне меню:",
//                            replyMarkup: KeyboardManager.GetMainKeyboard(),
//                            cancellationToken: cancellationToken
//                        );
//                        return;
//                    }

//                    if (
//                        !double.TryParse(
//                            message.Trim().Replace(',', '.'),
//                            System.Globalization.NumberStyles.Any,
//                            System.Globalization.CultureInfo.InvariantCulture,
//                            out var maxDollars
//                        )
//                        || double.IsInfinity(maxDollars)
//                        || maxDollars <= 0
//                    )
//                    {
//                        await _botClient.SendMessage(
//                            chatId: chatId,
//                            text: "❗ Введіть коректну суму в доларах (наприклад: 1.99 або 0.5).",
//                            cancellationToken: cancellationToken
//                        );
//                        _userStates[chatId] = "WaitingForBudget";
//                        return;
//                    }

//                    List<GameSearchResult> budgetGames;
//                    try
//                    {
//                        budgetGames = await _apiClient.GetGamesByBudgetSpyAsync(maxDollars);
//                    }
//                    catch (HttpRequestException)
//                    {
//                        await _botClient.SendMessage(
//                            chatId: chatId,
//                            text: "❗ Сталася помилка при зверненні до сервера. Спробуйте ввести менший бюджет або пізніше.",
//                            cancellationToken: cancellationToken
//                        );
//                        _userStates[chatId] = "WaitingForBudget";
//                        return;
//                    }

//                    if (!budgetGames.Any())
//                    {
//                        await _botClient.SendMessage(
//                            chatId: chatId,
//                            text: "😕 Ігор у цьому бюджеті не знайдено. Спробуйте вказати інший ліміт.",
//                            cancellationToken: cancellationToken
//                        );
//                        _userStates[chatId] = "WaitingForBudget";
//                        return;
//                    }

//                    var gameButtons = budgetGames
//                        .Take(10)
//                        .Select(g =>
//                        {
//                            var priceFormatted =
//                                g.Price > 0 ? $" – {g.Price / 100.0:0.00}$" : " – Free";
//                            return new KeyboardButton($"{g.Name} (ID: {g.Id}){priceFormatted}");
//                        })
//                        .Select(b => new[] { b })
//                        .ToArray();

//                    var keyboard = new ReplyKeyboardMarkup(gameButtons) { ResizeKeyboard = true };

//                    _gameKeyboards[chatId] = keyboard;

//                    await _botClient.SendMessage(
//                        chatId: chatId,
//                        text: $"🎮 Ось ігри до ${maxDollars:0.##}:",
//                        replyMarkup: keyboard,
//                        cancellationToken: cancellationToken
//                    );

//                    _userStates[chatId] = "WaitingForGameSelection";
//                    break;
//                }

//                case "WaitingForGameSelection":
//                {
//                    if (_userMessageToDelete.TryGetValue(chatId, out var msgId))
//                    {
//                        _userMessageToDelete.Remove(chatId);
//                    }

//                    if (message == "⬅️ Назад")
//                    {
//                        _userStates.Remove(chatId);
//                        _gameKeyboards.Remove(chatId);

//                        await _botClient.SendMessage(
//                            chatId: chatId,
//                            text: "Головне меню:",
//                            replyMarkup: KeyboardManager.GetMainKeyboard(),
//                            cancellationToken: cancellationToken
//                        );
//                        break;
//                    }

//                    var match = Regex.Match(message, @"\(ID:\s*(\d+)\)");
//                    if (match.Success && int.TryParse(match.Groups[1].Value, out var appId))
//                    {
//                        await SendGameDetailsAsync(chatId, appId, cancellationToken);
//                        _userStates.Remove(chatId);
//                        _gameKeyboards.Remove(chatId);
//                    }
//                    else
//                    {
//                        if (_gameKeyboards.TryGetValue(chatId, out var prevKeyboard))
//                        {
//                            await _botClient.SendMessage(
//                                chatId: chatId,
//                                text: "❗ Будь ласка, оберіть гру зі списку нижче, натиснувши на її кнопку.",
//                                replyMarkup: prevKeyboard,
//                                cancellationToken: cancellationToken
//                            );
//                        }
//                        else
//                        {
//                            _userStates.Remove(chatId);
//                            await _botClient.SendMessage(
//                                chatId: chatId,
//                                text: "❗ Сталася помилка. Повертаємося в меню.",
//                                replyMarkup: KeyboardManager.GetMainKeyboard(),
//                                cancellationToken: cancellationToken
//                            );
//                        }
//                    }

//                    break;
//                }

//                case "WaitingForUnsubscribeId":
//                {
//                    if (message == "⬅️ Назад")
//                    {
//                        _userStates.Remove(chatId);
//                        await _botClient.SendMessage(
//                            chatId,
//                            "Головне меню:",
//                            replyMarkup: KeyboardManager.GetMainKeyboard(),
//                            cancellationToken: cancellationToken
//                        );
//                        return;
//                    }

//                    if (!int.TryParse(message.Trim(), out var unsubId))
//                    {
//                        await _botClient.SendMessage(
//                            chatId,
//                            "❗ Некоректний ID. Введіть, будь ласка, правильний ID гри (тільки цифри), або натисніть «⬅️ Назад», щоб скасувати:",
//                            replyMarkup: new ReplyKeyboardMarkup(
//                                new[] { new[] { new KeyboardButton("⬅️ Назад") } }
//                            )
//                            {
//                                ResizeKeyboard = true,
//                            },
//                            cancellationToken: cancellationToken
//                        );
//                        return;
//                    }

//                    var user = await _userService.GetSettingsAsync(chatId);

//                    if (!user.SubscribedGames.Contains(unsubId))
//                    {
//                        await _botClient.SendMessage(
//                            chatId,
//                            "❌ Ви не були підписані на новини цієї гри. Введіть інший ID або натисніть «⬅️ Назад», щоб скасувати:",
//                            replyMarkup: new ReplyKeyboardMarkup(
//                                new[] { new[] { new KeyboardButton("⬅️ Назад") } }
//                            )
//                            {
//                                ResizeKeyboard = true,
//                            },
//                            cancellationToken: cancellationToken
//                        );
//                        return;
//                    }

//                    user.SubscribedGames.Remove(unsubId);
//                    await _apiClient.UpdateUserSettingsAsync(user);

//                    await _botClient.SendMessage(
//                        chatId,
//                        $"🔕 Ви успішно відписалися від новин гри з ID {unsubId}.",
//                        replyMarkup: KeyboardManager.GetMainKeyboard(),
//                        cancellationToken: cancellationToken
//                    );

//                    _userStates.Remove(chatId);
//                    break;
//                }
//                default:
//                {
//                    _userStates.Remove(chatId);
//                    await _botClient.SendMessage(
//                        chatId,
//                        "Щось пішло не так. Повернімося в меню.",
//                        replyMarkup: KeyboardManager.GetMainKeyboard(),
//                        cancellationToken: cancellationToken
//                    );
//                    break;
//                }
//            }
//        }

//        private async Task HandleCommandTextAsync(
//            long chatId,
//            string message,
//            CancellationToken cancellationToken
//        )
//        {
//            switch (message)
//            {
//                case "/start":
//                    await _botClient.SendMessage(
//                        chatId: chatId,
//                        text: "Привіт! Я бот для допомоги зі Steam - магазином. Оберіть дію:",
//                        replyMarkup: KeyboardManager.GetMainKeyboard(),
//                        cancellationToken: cancellationToken
//                    );
//                    break;

//                case "Список бажань":
//                    var settings = await _userService.GetSettingsAsync(chatId);

//                    if (!settings.Wishlist.Any())
//                    {
//                        await _botClient.SendMessage(
//                            chatId: chatId,
//                            text: "Список бажань порожній.",
//                            cancellationToken: cancellationToken
//                        );
//                        return;
//                    }

//                    var tasks = settings.Wishlist.Select(appId =>
//                        _apiClient.GetGameDetailsAsync(appId)
//                    );
//                    var gameDetailsList = await Task.WhenAll(tasks);

//                    var wishlistText = "Ваш список бажань:\n";
//                    foreach (var details in gameDetailsList)
//                    {
//                        if (details.TryGetValue("data", out var raw) && raw is JsonElement json)
//                        {
//                            var name = json.GetProperty("name").GetString();
//                            var id = json.GetProperty("steam_appid").GetInt32();
//                            wishlistText += $"🎮 {name} (ID: {id})\n";
//                        }
//                    }
//                    await _botClient.SendMessage(
//                        chatId: chatId,
//                        text: wishlistText,
//                        replyMarkup: KeyboardManager.GetWishlistKeyboard(),
//                        cancellationToken: cancellationToken
//                    );
//                    break;

//                case "❌ Видалити гру з вішліста":
//                    await _botClient.SendMessage(
//                        chatId,
//                        "Введіть <b>ID гри</b>, яку хочете видалити:",
//                        parseMode: ParseMode.Html,
//                        replyMarkup: new ReplyKeyboardMarkup(
//                            new[] { new[] { new KeyboardButton("⬅️ Назад") } }
//                        )
//                        {
//                            ResizeKeyboard = true,
//                        },
//                        cancellationToken: cancellationToken
//                    );
//                    _userStates[chatId] = "WaitingForRemoveId";
//                    break;

//                case "⬅️ Назад":
//                    await _botClient.SendMessage(
//                        chatId,
//                        "Головне меню:",
//                        replyMarkup: KeyboardManager.GetMainKeyboard(),
//                        cancellationToken: cancellationToken
//                    );
//                    break;

//                case "Підписка на новини":
//                {
//                    var user = await _userService.GetSettingsAsync(chatId);

//                    if (!user.SubscribedGames.Any())
//                    {
//                        await _botClient.SendMessage(
//                            chatId,
//                            "❗ Ви ще не підписані на новини жодної гри.",
//                            replyMarkup: KeyboardManager.GetMainKeyboard(),
//                            cancellationToken: cancellationToken
//                        );
//                        return;
//                    }

//                    var sb = new StringBuilder("📬 Ви підписані на новини цих ігор:\n\n");

//                    foreach (var appId in user.SubscribedGames)
//                    {
//                        var details = await _apiClient.GetGameDetailsAsync(appId);
//                        if (details.TryGetValue("data", out var raw) && raw is JsonElement json)
//                        {
//                            var name = json.GetProperty("name").GetString();
//                            sb.AppendLine($"▪️ {name} (ID: {appId})");
//                        }
//                        else
//                        {
//                            sb.AppendLine($"▪️ Гра з ID {appId}");
//                        }
//                    }

//                    await _botClient.SendMessage(
//                        chatId,
//                        sb.ToString(),
//                        replyMarkup: KeyboardManager.GetSubscriptionKeyboard(),
//                        cancellationToken: cancellationToken
//                    );
//                    break;
//                }

//                case "❌ Відписатись від гри":
//                    await _botClient.SendMessage(
//                        chatId,
//                        "Введіть <b>ID гри</b>, від новин якої хочете відписатись:",
//                        parseMode: ParseMode.Html,
//                        replyMarkup: new ReplyKeyboardRemove(),
//                        cancellationToken: cancellationToken
//                    );
//                    _userStates[chatId] = "WaitingForUnsubscribeId";
//                    break;

//                case "Щоденні знижки":
//                {
//                    var games = await _apiClient.GetDiscountedGamesAsync();

//                    if (!games.Any())
//                    {
//                        await _botClient.SendMessage(
//                            chatId,
//                            "😕 Сьогодні немає ігор зі знижками.",
//                            cancellationToken: cancellationToken
//                        );
//                        return;
//                    }

//                    var lines = games
//                        .Select(g => $"▪️ {g.Name} (ID: {g.Id}) – {g.Discount}% знижки")
//                        .ToList();

//                    var text = "🔥 <b>ТОП знижок сьогодні:</b>\n\n" + string.Join("\n", lines);

//                    var user = await _userService.GetSettingsAsync(chatId);

//                    var inlineMarkup = new InlineKeyboardMarkup(
//                        new[]
//                        {
//                            new[]
//                            {
//                                InlineKeyboardButton.WithCallbackData(
//                                    user.SubscriptionOnSales
//                                        ? "🔕 Відписатися від знижок"
//                                        : "🔔 Підписатися на знижки",
//                                    user.SubscriptionOnSales
//                                        ? "unsubscribe_sales"
//                                        : "subscribe_sales"
//                                ),
//                            },
//                        }
//                    );

//                    await _botClient.SendMessage(
//                        chatId,
//                        text,
//                        parseMode: ParseMode.Html,
//                        replyMarkup: inlineMarkup,
//                        cancellationToken: cancellationToken
//                    );

//                    await _botClient.SendMessage(
//                        chatId,
//                        "⬅️ Повернутись у меню",
//                        replyMarkup: new ReplyKeyboardMarkup(
//                            new[] { new[] { new KeyboardButton("⬅️ Назад") } }
//                        )
//                        {
//                            ResizeKeyboard = true,
//                        },
//                        cancellationToken: cancellationToken
//                    );

//                    break;
//                }

//                case "Пошук ігор":
//                    await _botClient.SendMessage(
//                        chatId: chatId,
//                        text: "Оберіть тип пошуку:",
//                        replyMarkup: KeyboardManager.GetSearchKeyboard(),
//                        cancellationToken: cancellationToken
//                    );
//                    break;

//                case "Пошук по назві":
//                    await _botClient.SendMessage(
//                        chatId: chatId,
//                        text: "Введіть назву гри:",
//                        replyMarkup: new ReplyKeyboardRemove(),
//                        cancellationToken: cancellationToken
//                    );
//                    _userStates[chatId] = "WaitingForName";
//                    break;

//                case "Пошук по жанру":
//                    await _botClient.SendMessage(
//                        chatId: chatId,
//                        text: "📚 Введіть жанр англійською мовою, наприклад:\n\n▫ RPG\n▫ Action\n▫ Indie\n▫ Strategy\n▫ Simulation\n▫ MMO\n\n🔁 Якщо не знаєш що ввести — спробуй RPG або Action.",
//                        replyMarkup: new ReplyKeyboardRemove(),
//                        cancellationToken: cancellationToken
//                    );
//                    _userStates[chatId] = "WaitingForGenre";
//                    break;

//                case "Пошук по бюджету":
//                    await _botClient.SendMessage(
//                        chatId: chatId,
//                        text: "💰 Введіть бюджет у доларах (наприклад: 1.5, 3.99, 0.49):",
//                        replyMarkup: new ReplyKeyboardRemove(),
//                        cancellationToken: cancellationToken
//                    );
//                    _userStates[chatId] = "WaitingForBudget";
//                    break;

//                case "/help":
//                    await _botClient.SendMessage(
//                        chatId: chatId,
//                        text: "Доступні команди:\n"
//                            + "/start\n"
//                            + "Список бажань\n"
//                            + "Пошук по назві\n"
//                            + "Пошук по жанру\n"
//                            + "Пошук по бюджету",
//                        replyMarkup: KeyboardManager.GetMainKeyboard(),
//                        cancellationToken: cancellationToken
//                    );
//                    break;

//                default:
//                    await _botClient.SendMessage(
//                        chatId: chatId,
//                        text: "Невідома команда. Використайте меню.",
//                        replyMarkup: KeyboardManager.GetMainKeyboard(),
//                        cancellationToken: cancellationToken
//                    );
//                    break;
//            }
//        }

//        public async Task SendGameDetailsAsync(
//            long chatId,
//            int appId,
//            CancellationToken cancellationToken
//        )
//        {
//            var settings = await _userService.GetSettingsAsync(chatId);
//            var wishlist = settings.Wishlist;
//            var data = await _apiClient.GetGameDetailsAsync(appId);

//            if (
//                data == null
//                || !data.TryGetValue("data", out var raw)
//                || !(raw is JsonElement json)
//            )
//            {
//                await _botClient.SendMessage(
//                    chatId,
//                    "Не вдалося завантажити інформацію.",
//                    cancellationToken: cancellationToken
//                );
//                return;
//            }

//            var details = GameDetails.FromJson(json, appId, wishlist);

//            var subscribed = settings.SubscribedGames ?? new List<int>();

//            await _botClient.SendMessage(
//                chatId: chatId,
//                text: details.ToHtmlCaption(),
//                parseMode: ParseMode.Html,
//                replyMarkup: details.ToInlineKeyboard("UA", subscribed),
//                cancellationToken: cancellationToken
//            );

//            await _botClient.SendMessage(
//                chatId: chatId,
//                text: "Що далі?",
//                replyMarkup: KeyboardManager.GetMainKeyboard(),
//                cancellationToken: cancellationToken
//            );
//        }
//    }
//}
