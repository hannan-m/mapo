using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

public class UserEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; }
}

[Mapper]
public partial class ReverseMapper
{
    public partial UserDto MapToDto(UserEntity entity);
    public partial UserEntity MapToEntity(UserDto dto);

    static void Configure(IMapConfig<UserEntity, UserDto> config)
    {
        config.Map(d => d.DisplayName, s => s.Username);
        config.ReverseMap();
    }
}

public class ReverseMappingTests
{
    [Fact]
    public void ReverseMap_ShouldMapBackToSourceType()
    {
        var mapper = new ReverseMapper();
        
        var dto = new UserDto
        {
            Id = 42,
            DisplayName = "john_doe",
            IsActive = true
        };

        var entity = mapper.MapToEntity(dto);

        entity.Should().NotBeNull();
        entity.Id.Should().Be(42);
        entity.IsActive.Should().BeTrue();
    }
}

[Mapper]
public partial class FullyConfiguredReverseMapper
{
    public partial UserDto MapToDto(UserEntity entity);
    public partial UserEntity MapToEntity(UserDto dto);

    static void Configure(IMapConfig<UserEntity, UserDto> config)
    {
        config.Map(d => d.DisplayName, s => s.Username);
        config.ReverseMap();
    }
    
    // Explicitly configure the DTO -> Entity direction since Mapo doesn't auto-reverse lambdas.
    static void Configure(IMapConfig<UserDto, UserEntity> config)
    {
        config.Map(e => e.Username, d => d.DisplayName)
              .Ignore(e => e.PasswordHash);
    }
}

public class ExplicitReverseMappingTests
{
    [Fact]
    public void ExplicitReverseMap_ShouldMapAllPropertiesCorrectly()
    {
        var mapper = new FullyConfiguredReverseMapper();
        
        var dto = new UserDto
        {
            Id = 99,
            DisplayName = "jane_doe",
            IsActive = false
        };

        var entity = mapper.MapToEntity(dto);

        entity.Should().NotBeNull();
        entity.Id.Should().Be(99);
        entity.Username.Should().Be("jane_doe");
        entity.IsActive.Should().BeFalse();
        
        // Ignored property should be default
        entity.PasswordHash.Should().Be("");
    }
}
