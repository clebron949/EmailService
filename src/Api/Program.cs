using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(options => builder.Configuration.GetSection("EmailConfiguration").Get<EmailConfiguration>() ?? new("",0,"", ""));
builder.Services.AddSingleton(opts => new Token(builder.Configuration.GetValue<string>("Token")));
builder.Services.AddSingleton<EmailService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/api/sendemail", (
    [FromBody] EmailMessage message, 
    [FromServices] ILogger<Program> logger, 
    [FromServices] EmailService emailService
    ) =>
{
    logger.LogInformation($"Email message received: {JsonSerializer.Serialize(message)}");
    logger.LogInformation("Sending email...");
    try
    {
        emailService.SendEmail(message);
    }
    catch (Exception ex)
    {
        var msg = $"Failed to send email. Error: {ex.Message}";
        logger.LogError(msg);
        Results.BadRequest(msg);
    }
    return "Email sent!";
});

app.Run();

public record EmailMessage(string? token, string? Sender,string? Name, string? Email, string? Subject, string? Body);

public record EmailConfiguration(string Server, int Port, string Username, string Password);

public record Token(string? token);

public class EmailService(ILogger<EmailService> logger, EmailConfiguration emailConfig, Token token)
{
    public void SendEmail(EmailMessage message)
    {
        if (message.token != token.token)
        {
            throw new Exception("Invalid token provided.");
        }

        try
        {
            using var smtpClient = new SmtpClient(emailConfig.Server)
            {
                Port = emailConfig.Port,
                Credentials = new NetworkCredential(emailConfig.Username, emailConfig.Password),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(emailConfig.Username),
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = true,
            };
            mailMessage.To.Add(message.Sender);

            smtpClient.Send(mailMessage);
            logger.LogInformation("Email sent successfully.");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to send email. Error: {ex.Message}");
        }
    }
}