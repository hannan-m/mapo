using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// Types for enum↔string conversion
public enum TaskPriority { Low, Medium, High, Critical }

public class TaskEntity
{
    public string Title { get; set; } = "";
    public TaskPriority Priority { get; set; }
}

public class TaskDto
{
    public string Title { get; set; } = "";
    public string Priority { get; set; } = "";
}

public class TaskInput
{
    public string Title { get; set; } = "";
    public string Priority { get; set; } = "";
}

public class TaskOutput
{
    public string Title { get; set; } = "";
    public TaskPriority Priority { get; set; }
}

[Mapper]
public static partial class EnumStringMapper
{
    public static partial TaskDto MapToDto(TaskEntity entity);
    public static partial TaskOutput MapToOutput(TaskInput input);
}

public class EnumStringConversionTests
{
    [Fact]
    public void EnumToString_MapsCorrectly()
    {
        var entity = new TaskEntity { Title = "Fix bug", Priority = TaskPriority.High };
        var dto = EnumStringMapper.MapToDto(entity);

        dto.Title.Should().Be("Fix bug");
        dto.Priority.Should().Be("High");
    }

    [Fact]
    public void EnumToString_AllValues()
    {
        foreach (var priority in Enum.GetValues<TaskPriority>())
        {
            var entity = new TaskEntity { Priority = priority };
            var dto = EnumStringMapper.MapToDto(entity);
            dto.Priority.Should().Be(priority.ToString());
        }
    }

    [Fact]
    public void StringToEnum_MapsCorrectly()
    {
        var input = new TaskInput { Title = "Deploy", Priority = "Critical" };
        var output = EnumStringMapper.MapToOutput(input);

        output.Title.Should().Be("Deploy");
        output.Priority.Should().Be(TaskPriority.Critical);
    }

    [Fact]
    public void StringToEnum_InvalidValue_ThrowsException()
    {
        var input = new TaskInput { Title = "Test", Priority = "InvalidValue" };
        var act = () => EnumStringMapper.MapToOutput(input);
        act.Should().Throw<ArgumentException>();
    }
}
