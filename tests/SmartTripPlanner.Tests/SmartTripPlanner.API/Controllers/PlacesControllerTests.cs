using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartTripPlanner.API.Controllers;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.Tests.SmartTripPlanner.API.Controllers;

[TestClass]
public sealed class PlacesControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly PlacesController _controller;

    public PlacesControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new PlacesController(_mediatorMock.Object);
    }

    [TestMethod]
    public async Task Search_ValidRequest_Returns200WithPlaces()
    {
        var request = new PlaceSearchRequest("Museo", "madrid-es", 5);
        var results = new List<PlaceModel>
        {
            new("fsq_1", "Museo del Prado", "madrid-es",
                new PlaceLocationModel(40.4168, -3.7038), 120, true, true,
                new List<OpeningHoursWindowModel>())
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SearchPlacesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchPlacesResponse(results.AsReadOnly()));

        var act = await _controller.Search(request);

        var okResult = act as OkObjectResult;
        Assert.IsNotNull(okResult);
        Assert.AreEqual(StatusCodes.Status200OK, okResult.StatusCode);
        var returnedPlaces = okResult.Value as List<PlaceModel>;
        Assert.IsNotNull(returnedPlaces);
        Assert.AreEqual(1, returnedPlaces.Count);
    }

    [TestMethod]
    public async Task Search_ExternalServiceFailure_Returns422WithExternalServiceFailure()
    {
        var request = new PlaceSearchRequest("Museo", "madrid-es", 5);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SearchPlacesRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException());

        var act = await _controller.Search(request);

        var unprocessableResult = act as UnprocessableEntityObjectResult;
        Assert.IsNotNull(unprocessableResult);
        Assert.AreEqual(StatusCodes.Status422UnprocessableEntity, unprocessableResult.StatusCode);

        var errors = unprocessableResult.Value as List<ValidationResult>;
        Assert.IsNotNull(errors);
        Assert.AreEqual(1, errors.Count);
        Assert.AreEqual(ErrorCode.EXTERNAL_SERVICE_FAILURE, errors[0].ErrorCode);
    }
}
