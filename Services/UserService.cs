using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using SteamStoreBot.Models;

namespace SteamStoreBot.Services
{
    public class UserService : IUserService
    {
        private readonly ApiClient _api;
        private readonly ConcurrentDictionary<long, UserSettings> _cache =
            new ConcurrentDictionary<long, UserSettings>();

        public UserService(ApiClient api) => _api = api;

        public async Task<UserSettings> GetSettingsAsync(long chatId)
        {
            if (_cache.TryGetValue(chatId, out var s))
                return s;

            var settings = await _api.GetUserSettingsAsync(chatId);
            _cache[chatId] = settings;
            return settings;
        }

        public async Task AddToWishlistAsync(long chatId, int appId)
        {
            var s = await GetSettingsAsync(chatId);
            if (!s.Wishlist.Contains(appId))
            {
                Console.WriteLine(chatId.ToString(), appId);
                s.Wishlist.Add(appId);
                await _api.UpdateUserSettingsAsync(s);
            }
        }

        public async Task ToggleNewsSubscriptionAsync(long chatId, bool enable)
        {
            var s = await GetSettingsAsync(chatId);
            s.SubscriptionOnNews = enable;
            await _api.UpdateUserSettingsAsync(s);
        }

        public async Task ToggleSalesSubscriptionAsync(long chatId, bool enable)
        {
            var s = await GetSettingsAsync(chatId);
            s.SubscriptionOnSales = enable;
            await _api.UpdateUserSettingsAsync(s);
        }
    }
}
