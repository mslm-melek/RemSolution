using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

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
            FirstCirculationDate = firstCirculation,
            ImageUrl = "http://example.com/car.jpg",
            Power = 120,
            FuelType = FuelType.Gasoline
        };

        // Assert
        car.Matricule.Should().Be(matricule);
        car.ModelId.Should().Be(modelId);
        car.Model!.Name.Should().Be("Model3");
        car.Model.Brand!.Name.Should().Be("Tesla");
        car.Color.Should().Be(color);
        car.FirstCirculationDate.Should().Be(firstCirculation);
        car.ImageUrl.Should().Be("http://example.com/car.jpg");
        car.Power.Should().Be(120);
        car.FuelType.Should().Be(FuelType.Gasoline);
    }

    [Test]
    public void ShouldAllowNullOptionalProperties()
    {
        // Act
        var car = new Car
        {
            Matricule = "XYZ-789",
            FirstCirculationDate = DateTime.UtcNow
            // No Color, ImageUrl, ModelId, etc.
        };

        // Assert
        car.Color.Should().BeNull();
        car.ImageUrl.Should().BeNull();
        car.Model.Should().BeNull();
        car.Expenses.Should().BeNull();
        car.Rentings.Should().BeNull();
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
