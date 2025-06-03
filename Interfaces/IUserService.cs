using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamStoreBot.Models;

namespace SteamStoreBot.Utils
{
    public interface IUserService
    {
        Task<UserSettings> GetSettingsAsync(long chatId);
        Task AddToWishlistAsync(long chatId, int appId);
        Task RemoveFromWishlistAsync(long chatId, int appId);
        Task SubscribeToGameNewsAsync(long chatId, int appId);
        Task ToggleSalesSubscriptionAsync(long chatId, bool enable);
        Task UpdateUserSettingsAsync(UserSettings settings);
        Task<List<UserSettings>> GetAllUsersAsync();
    }
}
