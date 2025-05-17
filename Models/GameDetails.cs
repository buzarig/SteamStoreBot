using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Telegram.Bot.Types.ReplyMarkups;

namespace SteamStoreBot.Models
{
    /// Модель для зберігання деталей гри та генерації відповідного контенту.
    public class GameDetails
    {
        public int AppId { get; set; }
        public string Name { get; set; }
        public string PriceText { get; set; }
        public string ShortDescription { get; set; }
        public string MinRequirements { get; set; }
        public bool HasUaLocalization { get; set; }
        public string MetacriticScore { get; set; }
        public int ReviewsCount { get; set; }
        public List<string> Genres { get; set; }
        public string Hashtags { get; set; }
        public string StoreUrl { get; set; }
        public string TrailerUrl { get; set; }
        public bool IsInWishlist { get; set; }

        public static GameDetails FromJson(
            JsonElement dataJson,
            int appId,
            IEnumerable<int> wishlistGameIds
        )
        {
            var priceOverview = dataJson.GetProperty("price_overview");
            var original = priceOverview.GetProperty("initial_formatted").GetString();
            var final = priceOverview.GetProperty("final_formatted").GetString();
            var discount = priceOverview.GetProperty("discount_percent").GetInt32();
            string priceText = discount > 0 ? $"{original} ➔ {final} (-{discount}% )" : final;

            string shortDesc =
                dataJson.GetProperty("short_description").GetString() ?? string.Empty;
            var minHtml =
                dataJson.GetProperty("pc_requirements").GetProperty("minimum").GetString()
                ?? string.Empty;
            var minReq = Regex.Replace(minHtml, "<.*?>", string.Empty).Trim();

            var langs = dataJson.GetProperty("supported_languages").GetString() ?? string.Empty;
            bool hasUa = langs.IndexOf("ukrainian", StringComparison.OrdinalIgnoreCase) >= 0;

            string metac =
                dataJson.TryGetProperty("metacritic", out var mc)
                && mc.TryGetProperty("score", out var ms)
                    ? ms.GetInt32().ToString()
                    : "-";
            int reviews = dataJson.GetProperty("recommendations").GetProperty("total").GetInt32();

            var genres = dataJson
                .GetProperty("genres")
                .EnumerateArray()
                .Select(x => x.GetProperty("description").GetString())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
            string hashtags = string.Join(
                " ",
                genres.Select(g => "#" + Regex.Replace(g.ToLower(), "[^0-9a-z]", string.Empty))
            );

            string storeUrl = $"https://store.steampowered.com/app/{appId}";
            string trailer = null;
            if (dataJson.TryGetProperty("movies", out var movies) && movies.GetArrayLength() > 0)
                trailer = movies[0].GetProperty("mp4").GetProperty("max").GetString();

            return new GameDetails
            {
                AppId = appId,
                Name = dataJson.GetProperty("name").GetString() ?? string.Empty,
                PriceText = priceText,
                ShortDescription = shortDesc,
                MinRequirements = minReq,
                HasUaLocalization = hasUa,
                MetacriticScore = metac,
                ReviewsCount = reviews,
                Genres = genres,
                Hashtags = hashtags,
                StoreUrl = storeUrl,
                TrailerUrl = trailer,
                IsInWishlist = wishlistGameIds.Contains(appId),
            };
        }

        public string ToHtmlCaption()
        {
            var lines = new List<string>
            {
                $"🎮 <b>Гра:</b> {Escape(Name)}",
                "",
                $"💰 <b>Ціна:</b> {Escape(PriceText)}",
                "",
                $"📝 <b>Опис:</b> {Escape(ShortDescription)}",
                "",
                $"🖥️ <b>Мін. вимоги:</b> {Escape(MinRequirements)}",
                "",
                $"🌐 <b>Локалізація UA:</b> {(HasUaLocalization ? "✅" : "❌")}",
                "",
                $"⭐ <b>Metacritic:</b> {Escape(MetacriticScore)}",
                $"💬 <b>Відгуки:</b> {ReviewsCount} user ratings",
                "",
                $"📂 <b>Жанри:</b> {Escape(string.Join(", ", Genres))}",
                $"🔖 {Hashtags}",
            };
            return string.Join("\n", lines);
        }

        public InlineKeyboardMarkup ToInlineKeyboard()
        {
            var buttons = new List<InlineKeyboardButton[]>
            {
                new[] { InlineKeyboardButton.WithUrl("🔗 Відкрити в Steam", StoreUrl) },
            };
            if (!string.IsNullOrEmpty(TrailerUrl))
                buttons.Add(
                    new[] { InlineKeyboardButton.WithUrl("🎞 Переглянути трейлер", TrailerUrl) }
                );

            var wishlistBtn = IsInWishlist
                ? InlineKeyboardButton.WithCallbackData("✅ У вішліст", "noop")
                : InlineKeyboardButton.WithCallbackData("➕ Вішліст", $"add_to_wishlist_{AppId}");

            buttons.Add(new[] { wishlistBtn });
            return new InlineKeyboardMarkup(buttons);
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
