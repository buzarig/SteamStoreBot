using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamStoreBot.Models;

namespace SteamStoreBot.Services
{
    public interface IUserService
    {
        Task<UserSettings> GetSettingsAsync(long chatId);
        Task AddToWishlistAsync(long chatId, int appId);
        Task ToggleNewsSubscriptionAsync(long chatId, bool enable);
        Task ToggleSalesSubscriptionAsync(long chatId, bool enable);
    }
}
