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

        public ApiClient()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("https://localhost:7272/") };
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
    }
}
