using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// =============================================================================
// Update mapping: void partial methods with 2 params modify existing target
// =============================================================================

public class ProfileUpdate
{
    public string DisplayName { get; set; } = "";
    public string Bio { get; set; } = "";
    public int Age { get; set; }
}

public class UserProfile
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string Bio { get; set; } = "";
    public int Age { get; set; }
    public DateTime CreatedAt { get; set; }
    public string InternalNote { get; set; } = "";
}

[Mapper]
public partial class ProfileMapper
{
    public partial void ApplyUpdate(ProfileUpdate source, UserProfile target);

    static void Configure(IMapConfig<ProfileUpdate, UserProfile> config)
    {
        config.Ignore(d => d.Id)
              .Ignore(d => d.CreatedAt)
              .Ignore(d => d.InternalNote);
    }
}

public class UpdateMappingTests
{
    [Fact]
    public void UpdateMapping_ShouldModifyTargetInPlace()
    {
        var mapper = new ProfileMapper();
        var originalId = Guid.NewGuid();
        var originalDate = new DateTime(2020, 1, 1);

        var target = new UserProfile
        {
            Id = originalId,
            DisplayName = "OldName",
            Bio = "OldBio",
            Age = 20,
            CreatedAt = originalDate,
            InternalNote = "Admin note"
        };

        var update = new ProfileUpdate
        {
            DisplayName = "NewName",
            Bio = "Updated bio",
            Age = 25
        };

        mapper.ApplyUpdate(update, target);

        // Updated properties
        target.DisplayName.Should().Be("NewName");
        target.Bio.Should().Be("Updated bio");
        target.Age.Should().Be(25);

        // Ignored properties should be preserved
        target.Id.Should().Be(originalId);
        target.CreatedAt.Should().Be(originalDate);
        target.InternalNote.Should().Be("Admin note");
    }

    [Fact]
    public void UpdateMapping_NullSource_ShouldNotModifyTarget()
    {
        var mapper = new ProfileMapper();
        var target = new UserProfile
        {
            DisplayName = "Original",
            Bio = "Original bio",
            Age = 30
        };

        // Update mappings return early on null source instead of throwing
        mapper.ApplyUpdate(null!, target);

        target.DisplayName.Should().Be("Original");
        target.Bio.Should().Be("Original bio");
        target.Age.Should().Be(30);
    }
}
