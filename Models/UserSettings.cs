// SteamStoreBot/Models/UserSettings.cs  (Bot)
using System.Collections.Generic;

namespace SteamStoreBot.Models
{
    public class UserSettings
    {
        public long ChatId { get; set; }
        public List<int> Wishlist { get; set; } = new List<int>();
        public bool SubscriptionOnNews { get; set; }
        public bool SubscriptionOnSales { get; set; }
    }
}
