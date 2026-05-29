namespace WebApplication2.Services
{
    public interface IWhatsAppService
    {
        Task<bool> SendMessageAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
    }
}
