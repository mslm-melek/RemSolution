using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.UnitTests.Entities;

public class CarTests
{
    [Test]
    public void ShouldCreateCar_WithValidProperties()
    {
        // Arrange
        var matricule = "1452TU555";
        var modelId = 1;
        var color = "Red";
        var firstCirculation = new DateTime(2020, 1, 1);

        // Act
        var car = new Car
        {
            Matricule = matricule,
            ModelId = modelId,
            Model = new ModelCar
            {
                Id = modelId,
                Name = "Model3",
                Brand = new Brand { Name = "Tesla" }
            },
            Color = color,
            BranchId = 7,
            Status = CarStatus.Maintenance,
            DailyRate = Money.Of(49.90m, "TND"),
            FirstCirculationDate = firstCirculation,
            PhotoFileId = 42,
            Power = 120,
            FuelType = FuelType.Gasoline
        };

        // Assert
        car.Matricule.Should().Be(matricule);
        car.ModelId.Should().Be(modelId);
        car.Model!.Name.Should().Be("Model3");
        car.Model.Brand!.Name.Should().Be("Tesla");
        car.Color.Should().Be(color);
        car.BranchId.Should().Be(7);
        car.Status.Should().Be(CarStatus.Maintenance);
        car.DailyRate.Should().Be(Money.Of(49.90m, "TND"));
        car.FirstCirculationDate.Should().Be(firstCirculation);
        car.PhotoFileId.Should().Be(42);
        car.Power.Should().Be(120);
        car.FuelType.Should().Be(FuelType.Gasoline);
    }

    [Test]
    public void ShouldDefaultToActiveStatus()
    {
        var car = new Car
        {
            Matricule = "XYZ-789",
            FirstCirculationDate = DateTime.UtcNow
        };

        car.Status.Should().Be(CarStatus.Active);
        car.BranchId.Should().BeNull();
        car.DailyRate.Should().BeNull();
    }

    [Test]
    public void ShouldAllowNullOptionalProperties()
    {
        // Act
        var car = new Car
        {
            Matricule = "XYZ-789",
            FirstCirculationDate = DateTime.UtcNow
            // No Color, PhotoFile, ModelId, etc.
        };

        // Assert
        car.Color.Should().BeNull();
        car.PhotoFileId.Should().BeNull();
        car.PhotoFile.Should().BeNull();
        car.Model.Should().BeNull();
        car.Expenses.Should().BeNull();
        car.Rentings.Should().BeNull();
    }

    // The odometer rule the renting write paths lean on: readings taken off the
    // car move it, and it never runs backwards.
    [Test]
    public void RecordOdometer_ShouldOnlyEverMoveForward()
    {
        var car = new Car { Matricule = "ODO-1", FirstCirculationDate = DateTime.UtcNow };

        // A car nobody has read yet takes the first reading it is given.
        car.RecordOdometer(42_000);
        car.Mileage.Should().Be(42_000);

        car.RecordOdometer(42_850);
        car.Mileage.Should().Be(42_850);

        // A correction to an older hire, or a return typed before its pickup, is
        // not evidence the car has driven less.
        car.RecordOdometer(40_000);
        car.Mileage.Should().Be(42_850);

        // Nothing recorded means nothing to move it with.
        car.RecordOdometer(null);
        car.Mileage.Should().Be(42_850);
    }

    [Test]
    public void RecordOdometer_ShouldAcceptZeroOnAnUnreadCar()
    {
        // A car delivered new reads zero, which is a reading and not "unknown".
        var car = new Car { Matricule = "ODO-2", FirstCirculationDate = DateTime.UtcNow };

        car.RecordOdometer(0);

        car.Mileage.Should().Be(0);
    }

    [Test]
    public void ToString_ShouldReturnMatricule()
    {
        // Arrange
        var car = new Car
        {
            Matricule = "1452TU555",
            Model = new ModelCar
            {
                Name = "Model3",
                Brand = new Brand { Name = "Tesla" }
            },
            FirstCirculationDate = DateTime.UtcNow
        };

        // Act
        var result = car.ToString();

        // Assert
        result.Should().Be("Tesla Model3 - 1452TU555");
    }
}
