using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace SteamStoreBot.Services
{
    internal class KeyboardManager
    {
        public static ReplyKeyboardMarkup GetMainKeyboard()
        {
            return new ReplyKeyboardMarkup(
                new[]
                {
                    new[]
                    {
                        new KeyboardButton("Список бажань"),
                        new KeyboardButton("Щоденні знижки"),
                    },
                    new[]
                    {
                        new KeyboardButton("Підписка на новини"),
                        new KeyboardButton("Пошук ігор"),
                    },
                }
            );
        }

        public static ReplyKeyboardMarkup GetSearchKeyboard()
        {
            return new ReplyKeyboardMarkup(
                new[]
                {
                    new[]
                    {
                        new KeyboardButton("Пошук по назві"),
                        new KeyboardButton("Пошук по жанру"),
                        new KeyboardButton("Пошук по бюджету"),
                    },
                    new[] { new KeyboardButton("⬅️ Назад") },
                }
            );
        }

        public static ReplyKeyboardMarkup GetWishlistKeyboard()
        {
            return new ReplyKeyboardMarkup(
                new[]
                {
                    new[]
                    {
                        new KeyboardButton("❌ Видалити гру з вішліста"),
                        new KeyboardButton("⬅️ Назад"),
                    },
                }
            )
            {
                ResizeKeyboard = true,
            };
        }
    }
}
