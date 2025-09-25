using System.Xml.Linq;
using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Domain.UnitTests.Entities;

public class ModelCarTests
{
    [Test]
    public void ShouldCreateModelCar_WithValidProperties()
    {
        // Arrange
        var name = "Yaris";
      
        // Act
        var modelCar = new ModelCar
        {
            Name = name,
            Brand = new Brand
            {
                Name = "Toyota",
            }
        };

        // Assert
        modelCar.Name.Should().Be(name);
        modelCar.Brand.Name.Should().Be("Toyota");
      
    }

    [Test]
    public void ToString_ShouldReturnModelNameAndBrandName()
    {
        // Arrange
        var modelCar = new ModelCar
        {
            Name = "Yaris",
            Brand = new Brand
            {
                Name = "Toyota",
            }
        };

        // Act
        var result = modelCar.ToString();

        // Assert
        result.Should().Be("Toyota Yaris");
    }
}
