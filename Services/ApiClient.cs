using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SteamStoreBot.Models;

namespace SteamStoreBot.Services
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;

        public ApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<GameSearchResult>> SearchGamesAsync(string name)
        {
            var response = await _httpClient.GetAsync(
                $"api/search/games?name={Uri.EscapeDataString(name)}"
            );
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<GameSearchResult>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }

        public async Task<Dictionary<string, object>> GetGameDetailsAsync(
            int appId,
            string cc = "UA",
            string lang = "ukrainian"
        )
        {
            var response = await _httpClient.GetAsync(
                $"api/search/details?appId={appId}&cc={cc}&l={lang}"
            );
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<Dictionary<string, object>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }

        public async Task<UserSettings> GetUserSettingsAsync(long chatId)
        {
            var resp = await _httpClient.GetAsync($"api/usersettings/{chatId}");
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return new UserSettings { ChatId = chatId };
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<UserSettings>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }

        public async Task UpdateUserSettingsAsync(UserSettings settings)
        {
            var content = new StringContent(
                JsonSerializer.Serialize(settings),
                Encoding.UTF8,
                "application/json"
            );
            var resp = await _httpClient.PutAsync($"api/usersettings/{settings.ChatId}", content);
            resp.EnsureSuccessStatusCode();
        }

        public async Task<List<GameSearchResult>> GetGamesByGenreSpyAsync(string genre)
        {
            var url =
                $"api/search/spy-genre?genre={Uri.EscapeDataString(genre)}&minRating=90&minVotes=2000";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<GameSearchResult>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            return result ?? new List<GameSearchResult>();
        }

        public async Task<List<GameSearchResult>> GetGamesByBudgetSpyAsync(double maxDollars)
        {
            var url =
                $"api/search/spy-budget?max={maxDollars.ToString(System.Globalization.CultureInfo.InvariantCulture)}&minRating=70";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<GameSearchResult>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            return result ?? new List<GameSearchResult>();
        }

        public async Task<List<GameSearchResult>> GetDiscountedGamesAsync()
        {
            var url = $"api/search/spy-discounts";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<GameSearchResult>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new List<GameSearchResult>();
        }

        public async Task<List<Dictionary<string, object>>> GetGameNewsAsync(int appId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/search/news?appId={appId}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    ) ?? new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❗ GetGameNewsAsync error: {ex.Message}");
                return new List<Dictionary<string, object>>();
            }
        }

        public async Task<List<UserSettings>> GetAllUsersAsync()
        {
            var resp = await _httpClient.GetAsync("api/usersettings");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<UserSettings>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new List<UserSettings>();
        }
    }
}
