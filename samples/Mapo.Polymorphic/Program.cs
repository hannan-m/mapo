using Mapo.Attributes;

namespace Mapo.Polymorphic;

// =============================================================================
// DOMAIN MODELS - Notification/Event System
// =============================================================================

public abstract class Notification
{
    public Guid Id { get; set; }
    public string RecipientId { get; set; } = "";
    public DateTime SentAt { get; set; }
}

public class EmailNotification : Notification
{
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string AttachmentUrl { get; set; } = "";
}

public class SmsNotification : Notification
{
    public string PhoneNumber { get; set; } = "";
    public string Message { get; set; } = "";
}

public class PushNotification : Notification
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string DeepLink { get; set; } = "";
}

public class UserPreferences
{
    public Guid UserId { get; set; }
    public Dictionary<string, bool> ChannelStatus { get; set; } = [];
}

// =============================================================================
// DTOs
// =============================================================================

public abstract record NotificationDto
{
    public Guid Id { get; set; }
    public string RecipientId { get; set; } = "";
    public DateTime SentAt { get; set; }
}

public record EmailDto : NotificationDto
{
    public string Subject { get; set; } = "";
    public string BodySummary { get; set; } = "";
}

public record SmsDto : NotificationDto
{
    public string MaskedPhone { get; set; } = "";
    public string Message { get; set; } = "";
}

public record PushDto : NotificationDto
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
}

public record PreferenceDto
{
    public Guid UserId { get; init; }
    public List<string> EnabledChannels { get; init; } = [];
}

// =============================================================================
// MAPPER
// =============================================================================

[Mapper]
public partial class NotificationMapper
{
    [MapDerived(typeof(EmailNotification), typeof(EmailDto))]
    [MapDerived(typeof(SmsNotification), typeof(SmsDto))]
    [MapDerived(typeof(PushNotification), typeof(PushDto))]
    public partial NotificationDto MapNotification(Notification notification);

    public partial List<NotificationDto> MapNotifications(List<Notification> notifications);

    public partial PreferenceDto MapPreferences(UserPreferences preferences);

    static void Configure(IMapConfig<EmailNotification, EmailDto> config)
    {
        config.Map(d => d.BodySummary, s => s.Body.Length > 50 ? s.Body.Substring(0, 47) + "..." : s.Body);
    }

    static void Configure(IMapConfig<SmsNotification, SmsDto> config)
    {
        config.Map(
            d => d.MaskedPhone,
            s => s.PhoneNumber.Length > 5 ? s.PhoneNumber.Substring(0, 3) + "***" : s.PhoneNumber
        );
    }

    static void Configure(IMapConfig<UserPreferences, PreferenceDto> config)
    {
        config.Map(d => d.EnabledChannels, s => s.ChannelStatus.Where(kv => kv.Value).Select(kv => kv.Key).ToList());
    }
}

// =============================================================================
// PROGRAM
// =============================================================================

public class Program
{
    public static void Main()
    {
        Console.WriteLine("=== Mapo Polymorphic Notification Sample ===\n");

        var notifications = new List<Notification>
        {
            new EmailNotification
            {
                Id = Guid.NewGuid(),
                RecipientId = "user_1",
                Subject = "Welcome!",
                Body = "Welcome to our platform. We are happy to have you here.",
                SentAt = DateTime.UtcNow.AddHours(-1),
            },
            new SmsNotification
            {
                Id = Guid.NewGuid(),
                RecipientId = "user_2",
                PhoneNumber = "+1234567890",
                Message = "Your OTP is 123456",
                SentAt = DateTime.UtcNow.AddMinutes(-30),
            },
            new PushNotification
            {
                Id = Guid.NewGuid(),
                RecipientId = "user_1",
                Title = "New Message",
                Text = "You have a new message from Alice",
                SentAt = DateTime.UtcNow,
            },
        };

        var preferences = new UserPreferences
        {
            UserId = Guid.NewGuid(),
            ChannelStatus = new Dictionary<string, bool>
            {
                { "Email", true },
                { "SMS", false },
                { "Push", true },
            },
        };

        var mapper = new NotificationMapper();

        var dtos = mapper.MapNotifications(notifications);
        foreach (var dto in dtos)
        {
            var type = dto switch
            {
                EmailDto => "Email",
                SmsDto => "SMS",
                PushDto => "Push",
                _ => "Unknown",
            };
            Console.WriteLine($"[{dto.SentAt:t}] {type} to {dto.RecipientId}");

            if (dto is EmailDto email)
                Console.WriteLine($"  Subject: {email.Subject}, Body: {email.BodySummary}");
            if (dto is SmsDto sms)
                Console.WriteLine($"  Phone: {sms.MaskedPhone}, Message: {sms.Message}");
            if (dto is PushDto push)
                Console.WriteLine($"  Title: {push.Title}");
        }

        var prefDto = mapper.MapPreferences(preferences);
        Console.WriteLine(
            $"\nUser {prefDto.UserId} preferences: Enabled = {string.Join(", ", prefDto.EnabledChannels)}"
        );
    }
}
