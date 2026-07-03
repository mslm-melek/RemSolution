using System.Reflection;
using System.Runtime.CompilerServices;
using Mapster;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Entities;
using NUnit.Framework;
using RemSolution.Application.Features.Car.DTOs;
using RemSolution.Application.Features.ModelCar.DTOs;
using RemSolution.Application.Features.Country.DTOs;
using RemSolution.Application.Features.Brand.DTOs;

namespace RemSolution.Application.UnitTests.Common.Mappings;

public class MappingTests
{
    private readonly TypeAdapterConfig _configuration;

    public MappingTests()
    {
        _configuration = new TypeAdapterConfig();
        _configuration.Scan(Assembly.GetAssembly(typeof(IApplicationDbContext))!);
    }

    [Test]
    public void ShouldHaveValidConfiguration()
    {
        _configuration.Compile();
    }

    [Test]
    [TestCase(typeof(Car), typeof(CarDto))]
    [TestCase(typeof(ModelCar), typeof(ModelCarDto))]
    [TestCase(typeof(Country), typeof(CountryDto))]
    [TestCase(typeof(Brand), typeof(BrandDto))]
    public void ShouldSupportMappingFromSourceToDestination(Type source, Type destination)
    {
        var instance = GetInstanceOf(source);

        instance.Adapt(source, destination, _configuration);
    }

    private object GetInstanceOf(Type type)
    {
        if (type.GetConstructor(Type.EmptyTypes) != null)
            return Activator.CreateInstance(type)!;

        // Type without parameterless constructor
        return RuntimeHelpers.GetUninitializedObject(type);
    }
}
