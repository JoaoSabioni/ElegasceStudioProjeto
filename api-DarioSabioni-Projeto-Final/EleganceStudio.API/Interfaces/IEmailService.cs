namespace EleganceStudio.API.Interfaces;

public interface IEmailService
{
    Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string textContent,
        string htmlContent,
        string? replyToEmail = null,
        string? replyToName = null);
}
