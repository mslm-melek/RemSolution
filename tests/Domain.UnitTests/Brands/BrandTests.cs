using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Domain.UnitTests.Entities;

public class BrandTests
{
    [Test]
    public void ShouldCreateBrand_WithValidProperties()
    {
        // Arrange
        var name = "Toyota";
       

        // Act
        var car = new Brand
        {
            Name = name
        };

        // Assert
        car.Name.Should().Be(name);
    }


    [Test]
    public void ToString_ShouldReturnName()
    {
        // Arrange
        var brand = new Brand
        {
            Name = "Toyota"
        };

        // Act
        var result = brand.ToString();

        // Assert
        result.Should().Be("Toyota");
    }
}
