using System;
using System.Collections.Generic;
using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

public class Employee
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Department Department { get; set; } = null!;
}

public class Department
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<Employee> Employees { get; set; } = [];
}

public class EmployeeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DepartmentDto Department { get; set; } = null!;
}

public class DepartmentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<EmployeeDto> Employees { get; set; } = [];
}

[Mapper(UseReferenceTracking = true)]
public partial class OrganizationMapper
{
    public partial EmployeeDto Map(Employee emp);

    public partial DepartmentDto Map(Department dept);
}

public class ReferenceTrackingTests
{
    [Fact]
    public void UseReferenceTracking_ShouldPreventInfiniteRecursion()
    {
        var mapper = new OrganizationMapper();
        var deptId = Guid.NewGuid();
        var empId = Guid.NewGuid();

        var dept = new Department { Id = deptId, Name = "Engineering" };
        var emp = new Employee
        {
            Id = empId,
            Name = "Alice",
            Department = dept,
        };
        dept.Employees.Add(emp);

        // Map the employee
        var empDto = mapper.Map(emp);

        empDto.Should().NotBeNull();
        empDto.Name.Should().Be("Alice");
        empDto.Department.Should().NotBeNull();
        empDto.Department.Name.Should().Be("Engineering");

        // The department's employee list should contain an employee
        // that is the EXACT SAME reference as empDto.
        empDto.Department.Employees.Should().HaveCount(1);
        var innerEmpDto = empDto.Department.Employees[0];

        ReferenceEquals(empDto, innerEmpDto)
            .Should()
            .BeTrue("Reference tracking should return the cached instance for circular references.");
    }
}
