using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace SteamStoreBot.Services
{
    public interface ICommandHandler
    {
        Task HandleAsync(Update update, CancellationToken cancellationToken);
    }
}
