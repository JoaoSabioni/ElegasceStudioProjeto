using EleganceStudio.API.Data;
using EleganceStudio.API.Interfaces;
using EleganceStudio.API.Models;
using System.Net.Http.Headers;

namespace EleganceStudio.API.Services;

public class BrevoEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<BrevoEmailService> _logger;

    public BrevoEmailService(
        HttpClient http,
        AppDbContext db,
        IConfiguration config,
        ILogger<BrevoEmailService> logger)
    {
        _http = http;
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string textContent,
        string htmlContent,
        string? replyToEmail = null,
        string? replyToName = null)
    {
        var apiKey = _config["Email:BrevoApiKey"];
        var senderEmail = _config["Email:SenderEmail"];
        var senderName = _config["Email:SenderName"] ?? "Elegance Studio";

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(senderEmail))
        {
            _logger.LogWarning("Email nao enviado: Email:BrevoApiKey ou Email:SenderEmail em falta.");
            await LogNotificationAsync(toEmail, subject, "Skipped", "Configuracao de email em falta.");
            return;
        }

        string? error = null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("api-key", apiKey);
            request.Content = JsonContent.Create(new
            {
                sender = new { email = senderEmail, name = senderName },
                to = new[] { new { email = toEmail, name = toName } },
                replyTo = string.IsNullOrWhiteSpace(replyToEmail)
                    ? null
                    : new { email = replyToEmail, name = replyToName ?? replyToEmail },
                subject,
                textContent,
                htmlContent
            });

            using var response = await _http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                await LogNotificationAsync(toEmail, subject, "Sent", null);
                return;
            }

            var body = await response.Content.ReadAsStringAsync();
            error = $"Brevo HTTP {(int)response.StatusCode}: {body}";
            _logger.LogError(
                "Falha ao enviar email Brevo para {Email}. Status {Status}. Body: {Body}",
                toEmail,
                (int)response.StatusCode,
                body);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _logger.LogError(
                ex,
                "Erro inesperado ao enviar email Brevo para {Email}.",
                toEmail);
        }

        await LogNotificationAsync(toEmail, subject, "Failed", error);
    }

    private async Task LogNotificationAsync(
        string recipient,
        string subject,
        string status,
        string? error)
    {
        try
        {
            _db.NotificationLogs.Add(new NotificationLog
            {
                Channel = "Email",
                Provider = "Brevo",
                Recipient = recipient,
                Subject = subject,
                Status = status,
                Error = error,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nao foi possivel registar NotificationLog para {Recipient}.", recipient);
        }
    }
}
