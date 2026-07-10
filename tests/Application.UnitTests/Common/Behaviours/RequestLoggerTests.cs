using RemSolution.Application.Common.Behaviours;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using RemSolution.Application.Features.Brand.Commands.CreateBrandCommand;

namespace RemSolution.Application.UnitTests.Common.Behaviours;

public class RequestLoggerTests
{
    private Mock<ILogger<CreateBrandCommand>> _logger = null!;

    [SetUp]
    public void Setup()
    {
        _logger = new Mock<ILogger<CreateBrandCommand>>();
    }

    [Test]
    public async Task ShouldLogOneInformationEventPerRequest()
    {
        // UserId / AgencyId / CorrelationId are enriched onto the log context by
        // the web middleware, so the behaviour no longer resolves the user; it
        // just emits the request event.
        var requestLogger = new LoggingBehaviour<CreateBrandCommand>(_logger.Object);

        await requestLogger.Process(new CreateBrandCommand { Name = "BMW" }, new CancellationToken());

        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
