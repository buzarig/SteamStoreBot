using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
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

            if (data.StartsWith("addwishlist:"))
            {
                var parts = data.Split(':');
                if (parts.Length != 3)
                    return;

                if (!int.TryParse(parts[1], out var appId))
                    return;

                var currency = parts[2].ToUpper();

                await _userService.AddToWishlistAsync(chatId, appId);

                var settings = await _userService.GetSettingsAsync(chatId);
                var details = await _apiClient.GetGameDetailsAsync(appId, currency, "ukrainian");

                if (
                    details != null
                    && details.TryGetValue("data", out var raw)
                    && raw is JsonElement json
                )
                {
                    var gameDetails = GameDetails.FromJson(json, appId, settings.Wishlist);
                    var updatedMarkup = gameDetails.ToInlineKeyboard(currency);

                    await _botClient.EditMessageText(
                        chatId: chatId,
                        messageId: messageId,
                        text: gameDetails.ToHtmlCaption(),
                        parseMode: ParseMode.Html,
                        replyMarkup: updatedMarkup,
                        cancellationToken: token
                    );
                }
            }
            else if (data.StartsWith("subscribe_news:"))
            {
                var appIdStr = data.Substring("subscribe_news:".Length);
                if (int.TryParse(appIdStr, out int appId))
                {
                    await _userService.SubscribeToGameNewsAsync(chatId, appId);

                    var settings = await _userService.GetSettingsAsync(chatId);
                    var details = await _apiClient.GetGameDetailsAsync(appId);

                    if (
                        details != null
                        && details.TryGetValue("data", out var raw)
                        && raw is JsonElement json
                    )
                    {
                        var game = GameDetails.FromJson(json, appId, settings.Wishlist);

                        var markup = game.ToInlineKeyboard("UA", settings.SubscribedGames);
                        await _botClient.EditMessageReplyMarkup(
                            chatId,
                            cb.Message.MessageId,
                            replyMarkup: markup,
                            cancellationToken: token
                        );

                        await _botClient.AnswerCallbackQuery(
                            cb.Id,
                            "✅ Підписка активна!",
                            cancellationToken: token
                        );
                    }
                }
            }
            else if (data.StartsWith("unsubscribe_news:"))
            {
                var appIdStr = data.Substring("unsubscribe_news:".Length);
                if (int.TryParse(appIdStr, out int appId))
                {
                    var user = await _userService.GetSettingsAsync(chatId);
                    if (user.SubscribedGames.Remove(appId))
                    {
                        await _apiClient.UpdateUserSettingsAsync(user);

                        var details = await _apiClient.GetGameDetailsAsync(appId);
                        if (
                            details != null
                            && details.TryGetValue("data", out var raw)
                            && raw is JsonElement json
                        )
                        {
                            var game = GameDetails.FromJson(json, appId, user.Wishlist);
                            var markup = game.ToInlineKeyboard("UA", user.SubscribedGames);

                            await _botClient.EditMessageReplyMarkup(
                                chatId,
                                cb.Message.MessageId,
                                replyMarkup: markup,
                                cancellationToken: token
                            );

                            await _botClient.AnswerCallbackQuery(
                                cb.Id,
                                "🔕 Підписку скасовано",
                                cancellationToken: token
                            );
                        }
                    }
                    else
                    {
                        await _botClient.AnswerCallbackQuery(
                            cb.Id,
                            "Ви не були підписані",
                            cancellationToken: token
                        );
                    }
                }
            }
            else if (data.StartsWith("convert_to_usd_"))
            {
                var appId = int.Parse(data.Substring("convert_to_usd_".Length));
                var settings = await _userService.GetSettingsAsync(chatId);

                var details = await _apiClient.GetGameDetailsAsync(appId, "US", "ukrainian");

                if (
                    details != null
                    && details.TryGetValue("data", out var raw)
                    && raw is JsonElement json
                )
                {
                    var gameDetails = GameDetails.FromJson(json, appId, settings.Wishlist);

                    var markup = gameDetails.ToInlineKeyboard("US").InlineKeyboard.ToList();
                    markup.RemoveAt(markup.Count - 1);
                    markup.Add(
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                "💴 Показати в грн",
                                $"convert_to_uah_{appId}"
                            ),
                        }
                    );

                    await _botClient.EditMessageText(
                        chatId: chatId,
                        messageId: messageId,
                        text: gameDetails.ToHtmlCaption(),
                        parseMode: ParseMode.Html,
                        replyMarkup: new InlineKeyboardMarkup(markup),
                        cancellationToken: token
                    );
                }
            }
            else if (data.StartsWith("convert_to_uah_"))
            {
                var appId = int.Parse(data.Substring("convert_to_uah_".Length));

                var settings = await _userService.GetSettingsAsync(chatId);

                var details = await _apiClient.GetGameDetailsAsync(appId, "UA", "ukrainian");

                if (
                    details != null
                    && details.TryGetValue("data", out var raw)
                    && raw is JsonElement json
                )
                {
                    var gameDetails = GameDetails.FromJson(json, appId, settings.Wishlist);

                    var markup = gameDetails.ToInlineKeyboard("UA").InlineKeyboard.ToList();
                    markup.RemoveAt(markup.Count - 1);
                    markup.Add(
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                "💲 Показати ціну в $",
                                $"convert_to_usd_{appId}"
                            ),
                        }
                    );

                    await _botClient.EditMessageText(
                        chatId: chatId,
                        messageId: messageId,
                        text: gameDetails.ToHtmlCaption(),
                        parseMode: ParseMode.Html,
                        replyMarkup: new InlineKeyboardMarkup(markup),
                        cancellationToken: token
                    );
                }
            }
            else if (data == "subscribe_sales")
            {
                await _userService.ToggleSalesSubscriptionAsync(chatId, true);
                await _botClient.AnswerCallbackQuery(cb.Id, "✅ Ви підписались на знижки");
            }
            else if (data == "unsubscribe_sales")
            {
                await _userService.ToggleSalesSubscriptionAsync(chatId, false);
                await _botClient.AnswerCallbackQuery(cb.Id, "🔕 Ви відписались від знижок");
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
                    List<GameSearchResult> games;
                    try
                    {
                        games = await _apiClient.SearchGamesAsync(message);
                    }
                    catch (HttpRequestException)
                    {
                        await _botClient.SendMessage(
                            chatId,
                            "❗ Сталася помилка при пошуку гри. Спробуйте іншу назву або перевірте правильність введення.",
                            cancellationToken: cancellationToken
                        );
                        _userStates[chatId] = "WaitingForName";
                        return;
                    }

                    if (games == null || !games.Any())
                    {
                        await _botClient.SendMessage(
                            chatId,
                            "😕 Гру з такою назвою не знайдено. Спробуйте ще раз.",
                            cancellationToken: cancellationToken
                        );
                        _userStates[chatId] = "WaitingForName";
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
                {
                    var genreSearch = message.Trim();

                    if (string.IsNullOrWhiteSpace(genreSearch) || genreSearch.Length < 2)
                    {
                        await _botClient.SendMessage(
                            chatId,
                            "❗️ Введіть жанр гри англійською. Наприклад: RPG, Action, Indie, MMO",
                            cancellationToken: cancellationToken
                        );
                        _userStates[chatId] = "WaitingForGenre";
                        return;
                    }

                    var genreGames = await _apiClient.GetGamesByGenreSpyAsync(genreSearch);

                    if (genreGames == null || !genreGames.Any())
                    {
                        await _botClient.SendMessage(
                            chatId,
                            $"😕 Ігор з жанром \"{genreSearch}\" не знайдено. Спробуйте інший жанр. Наприклад: Strategy, Racing.",
                            cancellationToken: cancellationToken
                        );
                        _userStates[chatId] = "WaitingForGenre";
                        return;
                    }

                    var genreGameButtons = genreGames
                        .Take(10)
                        .Select(g => new KeyboardButton($"{g.Name} (ID: {g.Id})"))
                        .Select(b => new[] { b })
                        .ToList();

                    await _botClient.SendMessage(
                        chatId,
                        $"🎮 Ось ігри у жанрі {genreSearch}:",
                        replyMarkup: new ReplyKeyboardMarkup(genreGameButtons)
                        {
                            ResizeKeyboard = true,
                        },
                        cancellationToken: cancellationToken
                    );

                    _userStates[chatId] = "WaitingForGameSelection";
                    break;
                }

                case "WaitingForRemoveId":
                    _userStates.Remove(chatId);

                    if (int.TryParse(message, out int removeId))
                    {
                        try
                        {
                            await _userService.RemoveFromWishlistAsync(chatId, removeId);

                            await _botClient.SendMessage(
                                chatId,
                                $"✅ Гру з ID {removeId} успішно видалено з вішліста.",
                                replyMarkup: KeyboardManager.GetMainKeyboard(),
                                cancellationToken: cancellationToken
                            );
                        }
                        catch (InvalidOperationException ex)
                        {
                            await _botClient.SendMessage(
                                chatId,
                                $"❌ {ex.Message}",
                                replyMarkup: KeyboardManager.GetMainKeyboard(),
                                cancellationToken: cancellationToken
                            );
                        }
                    }
                    else
                    {
                        await _botClient.SendMessage(
                            chatId,
                            "❌ Некоректний ID. Спробуйте ще раз або натисніть ⬅️ Назад.",
                            replyMarkup: KeyboardManager.GetMainKeyboard(),
                            cancellationToken: cancellationToken
                        );
                    }
                    break;

                case "WaitingForBudget":
                {
                    _userStates.Remove(chatId);

                    if (
                        !double.TryParse(
                            message.Trim().Replace(',', '.'),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var maxDollars
                        )
                    )
                    {
                        await _botClient.SendMessage(
                            chatId,
                            "❗ Введіть коректну суму в доларах (наприклад: 1.99 або 0.5).",
                            cancellationToken: cancellationToken
                        );
                        _userStates[chatId] = "WaitingForBudget";
                        return;
                    }

                    var budgetGames = await _apiClient.GetGamesByBudgetSpyAsync(maxDollars);

                    if (!budgetGames.Any())
                    {
                        await _botClient.SendMessage(
                            chatId,
                            "😕 Ігор у цьому бюджеті не знайдено. Спробуйте вказати більший ліміт.",
                            cancellationToken: cancellationToken
                        );
                        _userStates[chatId] = "WaitingForBudget";
                        return;
                    }

                    var gameButtons = budgetGames
                        .Take(10)
                        .Select(g => new KeyboardButton($"{g.Name} (ID: {g.Id})"))
                        .Select(b => new[] { b })
                        .ToList();

                    await _botClient.SendMessage(
                        chatId,
                        $"🎮 Ось ігри до ${maxDollars:F2}:",
                        replyMarkup: new ReplyKeyboardMarkup(gameButtons) { ResizeKeyboard = true },
                        cancellationToken: cancellationToken
                    );

                    _userStates[chatId] = "WaitingForGameSelection";
                    break;
                }

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

                case "WaitingForUnsubscribeId":
                {
                    if (int.TryParse(message.Trim(), out var unsubId))
                    {
                        var user = await _userService.GetSettingsAsync(chatId);

                        if (user.SubscribedGames.Remove(unsubId))
                        {
                            await _apiClient.UpdateUserSettingsAsync(user);

                            await _botClient.SendMessage(
                                chatId,
                                $"🔕 Ви відписались від новин гри з ID {unsubId}.",
                                replyMarkup: KeyboardManager.GetMainKeyboard(),
                                cancellationToken: cancellationToken
                            );
                        }
                        else
                        {
                            await _botClient.SendMessage(
                                chatId,
                                "❌ Ви не підписані на новини цієї гри.",
                                replyMarkup: KeyboardManager.GetMainKeyboard(),
                                cancellationToken: cancellationToken
                            );
                        }
                    }
                    else
                    {
                        await _botClient.SendMessage(
                            chatId,
                            "❗ Некоректний ID. Спробуйте ще раз або натисніть ⬅️ Назад.",
                            replyMarkup: KeyboardManager.GetMainKeyboard(),
                            cancellationToken: cancellationToken
                        );
                    }
                    break;
                }
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
                        text: "Привіт! Я бот для допомоги зі Steam - магазином. Оберіть дію:",
                        replyMarkup: KeyboardManager.GetMainKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;

                case "Список бажань":
                    var settings = await _userService.GetSettingsAsync(chatId);

                    if (!settings.Wishlist.Any())
                    {
                        await _botClient.SendMessage(
                            chatId: chatId,
                            text: "Список бажань порожній.",
                            cancellationToken: cancellationToken
                        );
                        return;
                    }

                    var tasks = settings.Wishlist.Select(appId =>
                        _apiClient.GetGameDetailsAsync(appId)
                    );
                    var gameDetailsList = await Task.WhenAll(tasks);

                    var wishlistText = "Ваш список бажань:\n";
                    foreach (var details in gameDetailsList)
                    {
                        if (details.TryGetValue("data", out var raw) && raw is JsonElement json)
                        {
                            var name = json.GetProperty("name").GetString();
                            var id = json.GetProperty("steam_appid").GetInt32();
                            wishlistText += $"🎮 {name} (ID: {id})\n";
                        }
                    }
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: wishlistText,
                        replyMarkup: KeyboardManager.GetWishlistKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;

                case "❌ Видалити гру з вішліста":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Введіть <b>ID гри</b>, яку хочете видалити:",
                        parseMode: ParseMode.Html,
                        replyMarkup: new ReplyKeyboardRemove(),
                        cancellationToken: cancellationToken
                    );
                    _userStates[chatId] = "WaitingForRemoveId";
                    break;

                case "⬅️ Назад":
                    await _botClient.SendMessage(
                        chatId,
                        "Головне меню:",
                        replyMarkup: KeyboardManager.GetMainKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;

                case "Підписка на новини":
                {
                    var user = await _userService.GetSettingsAsync(chatId);

                    if (!user.SubscribedGames.Any())
                    {
                        await _botClient.SendMessage(
                            chatId,
                            "❗ Ви ще не підписані на новини жодної гри.",
                            replyMarkup: KeyboardManager.GetMainKeyboard(),
                            cancellationToken: cancellationToken
                        );
                        return;
                    }

                    var sb = new StringBuilder("📬 Ви підписані на новини цих ігор:\n\n");

                    foreach (var appId in user.SubscribedGames)
                    {
                        var details = await _apiClient.GetGameDetailsAsync(appId);
                        if (details.TryGetValue("data", out var raw) && raw is JsonElement json)
                        {
                            var name = json.GetProperty("name").GetString();
                            sb.AppendLine($"▪️ {name} (ID: {appId})");
                        }
                        else
                        {
                            sb.AppendLine($"▪️ Гра з ID {appId}");
                        }
                    }

                    await _botClient.SendMessage(
                        chatId,
                        sb.ToString(),
                        replyMarkup: KeyboardManager.GetSubscriptionKeyboard(),
                        cancellationToken: cancellationToken
                    );
                    break;
                }

                case "❌ Відписатись від гри":
                    await _botClient.SendMessage(
                        chatId,
                        "Введіть <b>ID гри</b>, від новин якої хочете відписатись:",
                        parseMode: ParseMode.Html,
                        replyMarkup: new ReplyKeyboardRemove(),
                        cancellationToken: cancellationToken
                    );
                    _userStates[chatId] = "WaitingForUnsubscribeId";
                    break;

                case "Щоденні знижки":
                {
                    var games = await _apiClient.GetDiscountedGamesAsync();

                    if (!games.Any())
                    {
                        await _botClient.SendMessage(
                            chatId,
                            "😕 Сьогодні немає ігор зі знижками.",
                            cancellationToken: cancellationToken
                        );
                        return;
                    }

                    var lines = games
                        .Select(g => $"▪️ {g.Name} (ID: {g.Id}) – {g.Discount}% знижки")
                        .ToList();

                    var text = "🔥 <b>ТОП знижок сьогодні:</b>\n\n" + string.Join("\n", lines);

                    var user = await _userService.GetSettingsAsync(chatId);

                    var markup = new InlineKeyboardMarkup(
                        new[]
                        {
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData(
                                    user.SubscriptionOnSales
                                        ? "🔕 Відписатися від знижок"
                                        : "🔔 Підписатися на знижки",
                                    user.SubscriptionOnSales
                                        ? "unsubscribe_sales"
                                        : "subscribe_sales"
                                ),
                            },
                        }
                    );

                    await _botClient.SendMessage(
                        chatId,
                        text,
                        parseMode: ParseMode.Html,
                        replyMarkup: markup,
                        cancellationToken: cancellationToken
                    );

                    await _botClient.SendMessage(
                        chatId,
                        "⬅️ Повернутись в меню:",
                        replyMarkup: KeyboardManager.GetDiscountsKeyboard(),
                        cancellationToken: cancellationToken
                    );

                    break;
                }

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
                        text: "📚 Введіть жанр англійською мовою, наприклад:\n\n▫ RPG\n▫ Action\n▫ Indie\n▫ Strategy\n▫ Simulation\n▫ MMO\n\n🔁 Якщо не знаєш що ввести — спробуй RPG або Action.",
                        replyMarkup: new ReplyKeyboardRemove(),
                        cancellationToken: cancellationToken
                    );
                    _userStates[chatId] = "WaitingForGenre";
                    break;

                case "Пошук по бюджету":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "💰 Введіть бюджет у доларах (наприклад: 1.5, 3.99, 0.49):",
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

            var subscribed = settings.SubscribedGames ?? new List<int>();

            await _botClient.SendMessage(
                chatId: chatId,
                text: details.ToHtmlCaption(),
                parseMode: ParseMode.Html,
                replyMarkup: details.ToInlineKeyboard("UA", subscribed),
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
