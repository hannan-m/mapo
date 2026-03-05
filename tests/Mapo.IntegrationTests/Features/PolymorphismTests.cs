using System.Collections.Generic;
using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

public abstract class Vehicle { public string Name { get; set; } = ""; }
public class Car : Vehicle { public int NumberOfDoors { get; set; } }
public class Boat : Vehicle { public bool HasMotor { get; set; } }

public abstract class VehicleDto { public string Name { get; set; } = ""; }
public class CarDto : VehicleDto { public int NumberOfDoors { get; set; } }
public class BoatDto : VehicleDto { public bool HasMotor { get; set; } }

[Mapper]
public partial class VehicleMapper
{
    [MapDerived(typeof(Car), typeof(CarDto))]
    [MapDerived(typeof(Boat), typeof(BoatDto))]
    public partial VehicleDto Map(Vehicle vehicle);

    public partial CarDto MapCar(Car car);
    public partial BoatDto MapBoat(Boat boat);

    public partial List<VehicleDto> MapList(List<Vehicle> vehicles);
}

public class PolymorphismTests
{
    [Fact]
    public void MapDerived_ShouldMapToCorrectSubclass()
    {
        var mapper = new VehicleMapper();

        Vehicle car = new Car { Name = "Sedan", NumberOfDoors = 4 };
        Vehicle boat = new Boat { Name = "Sailboat", HasMotor = false };

        var carDto = mapper.Map(car);
        var boatDto = mapper.Map(boat);

        carDto.Should().BeOfType<CarDto>();
        ((CarDto)carDto).Name.Should().Be("Sedan");
        ((CarDto)carDto).NumberOfDoors.Should().Be(4);

        boatDto.Should().BeOfType<BoatDto>();
        ((BoatDto)boatDto).Name.Should().Be("Sailboat");
        ((BoatDto)boatDto).HasMotor.Should().BeFalse();
    }

    [Fact]
    public void MapDerived_InList_ShouldMapAllItemsCorrectly()
    {
        var mapper = new VehicleMapper();
        var vehicles = new List<Vehicle>
        {
            new Car { Name = "SUV", NumberOfDoors = 4 },
            new Boat { Name = "Speedboat", HasMotor = true }
        };

        var dtos = mapper.MapList(vehicles);

        dtos.Should().HaveCount(2);
        dtos[0].Should().BeOfType<CarDto>();
        dtos[1].Should().BeOfType<BoatDto>();
    }
}
