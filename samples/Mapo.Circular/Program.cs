using Mapo.Attributes;

namespace Mapo.Circular;

// =============================================================================
// DOMAIN MODELS - Realistic Social Network
// =============================================================================

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string Bio { get; set; } = "";
    public List<User> Followers { get; set; } = [];
    public List<User> Following { get; set; } = [];
    public List<Community> Communities { get; set; } = [];
    public List<Message> SentMessages { get; set; } = [];
    public List<Message> ReceivedMessages { get; set; } = [];
}

public class Community
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public User Admin { get; set; } = null!;
    public List<User> Members { get; set; } = [];
}

public class Message
{
    public Guid Id { get; set; }
    public string Content { get; set; } = "";
    public User Sender { get; set; } = null!;
    public User Receiver { get; set; } = null!;
    public DateTime SentAt { get; set; }
}

// =============================================================================
// DTOs
// =============================================================================

public record UserDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = "";
    public int FollowerCount { get; init; }
    public List<string> CommunityNames { get; init; } = [];
}

public record CommunityDto
{
    public string Name { get; init; } = "";
    public string AdminUsername { get; init; } = "";
    public int MemberCount { get; init; }
}

public record MessageDto
{
    public string Content { get; init; } = "";
    public string SenderName { get; init; } = "";
    public string ReceiverName { get; init; } = "";
    public DateTime SentAt { get; init; }
}

// =============================================================================
// MAPPER
// =============================================================================

[Mapper(UseReferenceTracking = true)]
public partial class SocialNetworkMapper
{
    public partial UserDto MapUser(User user);

    public partial CommunityDto MapCommunity(Community community);

    public partial MessageDto MapMessage(Message message);

    static void Configure(IMapConfig<User, UserDto> config)
    {
        config
            .Map(d => d.FollowerCount, s => s.Followers.Count)
            .Map(d => d.CommunityNames, s => s.Communities.Select(c => c.Name).ToList());
    }

    static void Configure(IMapConfig<Community, CommunityDto> config)
    {
        config.Map(d => d.AdminUsername, s => s.Admin.Username).Map(d => d.MemberCount, s => s.Members.Count);
    }

    static void Configure(IMapConfig<Message, MessageDto> config)
    {
        config.Map(d => d.SenderName, s => s.Sender.Username).Map(d => d.ReceiverName, s => s.Receiver.Username);
    }
}

// =============================================================================
// PROGRAM
// =============================================================================

public class Program
{
    public static void Main()
    {
        Console.WriteLine("=== Mapo Social Network (Circular) Sample ===\n");

        var alice = new User
        {
            Id = Guid.NewGuid(),
            Username = "alice",
            Bio = "Love coding!",
        };
        var bob = new User
        {
            Id = Guid.NewGuid(),
            Username = "bob",
            Bio = "Coffee enthusiast.",
        };

        // Circular: Followers/Following
        alice.Following.Add(bob);
        bob.Followers.Add(alice);

        // Circular: Community <-> Members/Admin
        var dotnetCommunity = new Community
        {
            Id = Guid.NewGuid(),
            Name = ".NET Developers",
            Admin = alice,
            Members = [alice, bob],
        };
        alice.Communities.Add(dotnetCommunity);
        bob.Communities.Add(dotnetCommunity);

        // Circular: Message <-> Sender/Receiver
        var msg = new Message
        {
            Id = Guid.NewGuid(),
            Content = "Hey Bob, check out Mapo!",
            Sender = alice,
            Receiver = bob,
            SentAt = DateTime.UtcNow,
        };
        alice.SentMessages.Add(msg);
        bob.ReceivedMessages.Add(msg);

        var mapper = new SocialNetworkMapper();

        var aliceDto = mapper.MapUser(alice);
        Console.WriteLine(
            $"User: {aliceDto.Username}, Followers: {aliceDto.FollowerCount}, Communities: {string.Join(", ", aliceDto.CommunityNames)}"
        );

        var communityDto = mapper.MapCommunity(dotnetCommunity);
        Console.WriteLine(
            $"Community: {communityDto.Name}, Admin: {communityDto.AdminUsername}, Members: {communityDto.MemberCount}"
        );

        var messageDto = mapper.MapMessage(msg);
        Console.WriteLine($"Message: '{messageDto.Content}' from {messageDto.SenderName} to {messageDto.ReceiverName}");

        Console.WriteLine("\nReference tracking prevented infinite recursion in circular graph.");
    }
}
