using APIHandler.Models.Chat;
using System.Threading.Tasks;

namespace APIHandler.Services.Chat;

public interface IChatService
{
    Task<ChatResponse> SendAsync(ChatRequest request, string guestSessionToken);
}
