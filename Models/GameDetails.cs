using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Telegram.Bot.Types.ReplyMarkups;

namespace SteamStoreBot.Models
{
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
        public List<string> Genres { get; set; } = new List<string>();
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
            string priceText = "Недоступна";

            if (
                dataJson.TryGetProperty("price_overview", out var priceOverview)
                && priceOverview.ValueKind == JsonValueKind.Object
            )
            {
                if (priceOverview.TryGetProperty("final_formatted", out var finalProp))
                {
                    var final = finalProp.GetString();
                    var original = priceOverview.TryGetProperty("initial_formatted", out var o)
                        ? o.GetString()
                        : null;
                    var discount = priceOverview.TryGetProperty("discount_percent", out var d)
                        ? d.GetInt32()
                        : 0;

                    if (!string.IsNullOrEmpty(final))
                    {
                        priceText =
                            discount > 0 && !string.IsNullOrEmpty(original)
                                ? $"{original} ➔ {final} (-{discount}%)"
                                : final;
                    }
                }
            }
            else if (
                dataJson.TryGetProperty("is_free", out var isFreeProp) && isFreeProp.GetBoolean()
            )
            {
                priceText = "Безкоштовно";
            }

            string shortDesc = dataJson.TryGetProperty("short_description", out var descProp)
                ? descProp.GetString() ?? string.Empty
                : string.Empty;

            string minReq = string.Empty;
            if (
                dataJson.TryGetProperty("pc_requirements", out var reqs)
                && reqs.TryGetProperty("minimum", out var min)
            )
            {
                var minHtml = min.GetString() ?? string.Empty;
                minReq = Regex.Replace(minHtml, "<.*?>", string.Empty).Trim();
            }

            var langs = dataJson.TryGetProperty("supported_languages", out var langProp)
                ? langProp.GetString() ?? string.Empty
                : string.Empty;
            langs = Regex.Replace(langs, "<.*?>", string.Empty);
            bool hasUa = langs.IndexOf("українська", StringComparison.OrdinalIgnoreCase) >= 0;

            string metac = "-";
            if (
                dataJson.TryGetProperty("metacritic", out var meta)
                && meta.TryGetProperty("score", out var score)
            )
                metac = score.GetInt32().ToString();

            int reviews = 0;
            if (
                dataJson.TryGetProperty("recommendations", out var rec)
                && rec.TryGetProperty("total", out var total)
            )
                reviews = total.GetInt32();

            var genres = new List<string>();
            if (dataJson.TryGetProperty("genres", out var genreArray))
            {
                genres = genreArray
                    .EnumerateArray()
                    .Select(x => x.TryGetProperty("description", out var g) ? g.GetString() : null)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
            }

            var categoryDescriptions = new List<string>();
            if (dataJson.TryGetProperty("categories", out var categoriesJson))
            {
                categoryDescriptions = categoriesJson
                    .EnumerateArray()
                    .Select(x =>
                        x.TryGetProperty("description", out var d)
                            ? d.GetString() ?? string.Empty
                            : string.Empty
                    )
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
            }

            var genreTags = genres
                .Select(g => "#" + Regex.Replace(g.ToLower(), "[^a-z0-9]", string.Empty))
                .Where(tag => tag.Length > 1);

            var categoryTags = categoryDescriptions
                .Select(c => "#" + Regex.Replace(c.ToLower(), "[^a-z0-9]", string.Empty))
                .Where(tag => tag.Length > 1);

            string hashtags = string.Join(" ", genreTags.Concat(categoryTags).Distinct());

            string storeUrl = $"https://store.steampowered.com/app/{appId}";

            string trailer = null;
            if (dataJson.TryGetProperty("movies", out var movies) && movies.GetArrayLength() > 0)
            {
                if (
                    movies[0].TryGetProperty("mp4", out var mp4)
                    && mp4.TryGetProperty("max", out var url)
                )
                    trailer = url.GetString();
            }

            return new GameDetails
            {
                AppId = appId,
                Name = dataJson.TryGetProperty("name", out var name)
                    ? name.GetString() ?? string.Empty
                    : string.Empty,
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

        public InlineKeyboardMarkup ToInlineKeyboard(
            string currency = "UA",
            IEnumerable<int> subscribedGameIds = null
        )
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
                ? InlineKeyboardButton.WithCallbackData("✅ У вішлісті", "noop")
                : InlineKeyboardButton.WithCallbackData(
                    "➕ Вішліст",
                    $"addwishlist:{AppId}:{currency.ToLower()}"
                );

            var isSubscribed = subscribedGameIds?.Contains(AppId) == true;

            var subscribeBtn = isSubscribed
                ? InlineKeyboardButton.WithCallbackData(
                    "🔕 Скасувати підписку",
                    $"unsubscribe_news:{AppId}"
                )
                : InlineKeyboardButton.WithCallbackData(
                    "🔔 Підписатись на новини",
                    $"subscribe_news:{AppId}"
                );

            buttons.Add(new[] { wishlistBtn });

            buttons.Add(new[] { subscribeBtn });

            if (
                PriceText.IndexOf("Недоступна", StringComparison.OrdinalIgnoreCase) < 0
                && PriceText.IndexOf("Free", StringComparison.OrdinalIgnoreCase) < 0
                && PriceText.IndexOf("безкоштовно", StringComparison.OrdinalIgnoreCase) < 0
            )
            {
                if (currency == "UA")
                {
                    buttons.Add(
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                "💲 Показати ціну в $",
                                $"convert_to_usd_{AppId}"
                            ),
                        }
                    );
                }
                else
                {
                    buttons.Add(
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                "💴 Показати в грн",
                                $"convert_to_uah_{AppId}"
                            ),
                        }
                    );
                }
            }

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
