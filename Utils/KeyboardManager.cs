// Utils/KeyboardManager.cs
using Telegram.Bot.Types.ReplyMarkups;

namespace SteamStoreBot.Utils
{
    internal static class KeyboardManager
    {
        public static ReplyKeyboardMarkup GetMainKeyboard()
        {
            return new ReplyKeyboardMarkup(
                new[]
                {
                    new[] { new KeyboardButton("📜 Список бажань") },
                    new[] { new KeyboardButton("🔎 Пошук ігор") },
                    new[] { new KeyboardButton("📰 Підписка на новини") },
                    new[] { new KeyboardButton("🔥 Щоденні знижки") },
                }
            )
            {
                ResizeKeyboard = true,
            };
        }

        public static ReplyKeyboardMarkup GetSearchKeyboard()
        {
            return new ReplyKeyboardMarkup(
                new[]
                {
                    new[] { new KeyboardButton("🖊️ Пошук по назві") },
                    new[] { new KeyboardButton("📚 Пошук по жанру") },
                    new[] { new KeyboardButton("💰 Пошук по бюджету") },
                    new[] { new KeyboardButton("⬅️ Назад") },
                }
            )
            {
                ResizeKeyboard = true,
            };
        }

        public static ReplyKeyboardMarkup GetWishlistKeyboard()
        {
            return new ReplyKeyboardMarkup(
                new[]
                {
                    new[] { new KeyboardButton("❌ Видалити з вішліста") },
                    new[] { new KeyboardButton("⬅️ Назад") },
                }
            )
            {
                ResizeKeyboard = true,
            };
        }

        public static ReplyKeyboardMarkup GetSubscriptionKeyboard()
        {
            return new ReplyKeyboardMarkup(
                new[]
                {
                    new[] { new KeyboardButton("❌ Відписатися від новин") },
                    new[] { new KeyboardButton("⬅️ Назад") },
                }
            )
            {
                ResizeKeyboard = true,
            };
        }
    }
}
