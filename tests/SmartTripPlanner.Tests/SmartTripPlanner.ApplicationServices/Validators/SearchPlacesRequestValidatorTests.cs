using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using Moq;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Configurations;
using SmartTripPlanner.ApplicationServices.Validators;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.Tests.SmartTripPlanner.ApplicationServices.Validators;

[TestClass]
public sealed class SearchPlacesRequestValidatorTests
{
    private readonly SearchPlacesRequestValidator _sut;
    private readonly Mock<ICityRepository> _cityRepoMock = new();
    private readonly PlaceSearchOptions _options = new()
    {
        MaxResults = 10
    };

    public SearchPlacesRequestValidatorTests()
    {
        _sut = new SearchPlacesRequestValidator(_cityRepoMock.Object, Options.Create(_options));
    }

    [TestMethod]
    public async Task ValidRequest_PassesValidation()
    {
        var request = new SearchPlacesRequest(
            new PlaceSearchRequest("Museo", "madrid-es", 5));

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es"))
            .ReturnsAsync(new City("madrid-es", "Madrid", true));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestMethod]
    public async Task Query_WhenEmpty_Fails_WithRequiredField()
    {
        var request = new SearchPlacesRequest(
            new PlaceSearchRequest("", "madrid-es", null));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.SearchRequest.Query)
            .WithErrorCode(nameof(ErrorCode.REQUIRED_FIELD));
    }

    [TestMethod]
    public async Task Query_WhenLessThan3Chars_Fails_WithMinLengthViolation()
    {
        var request = new SearchPlacesRequest(
            new PlaceSearchRequest("Mu", "madrid-es", null));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.SearchRequest.Query)
            .WithErrorCode(nameof(ErrorCode.MIN_LENGTH_VIOLATION));
    }

    [TestMethod]
    public async Task CityCode_WhenEmpty_Fails_WithRequiredField()
    {
        var request = new SearchPlacesRequest(
            new PlaceSearchRequest("Museo", "", null));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.SearchRequest.CityCode)
            .WithErrorCode(nameof(ErrorCode.REQUIRED_FIELD));
    }

    [TestMethod]
    public async Task CityCode_WhenNotFound_Fails_WithInvalidCity()
    {
        var request = new SearchPlacesRequest(
            new PlaceSearchRequest("Museo", "london-gb", null));

        _cityRepoMock.Setup(r => r.GetByCodeAsync("london-gb"))
            .ReturnsAsync((City?)null);

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.SearchRequest.CityCode)
            .WithErrorCode(nameof(ErrorCode.INVALID_CITY));
    }

    [TestMethod]
    public async Task CityCode_WhenNotAllowed_Fails_WithInvalidCity()
    {
        var request = new SearchPlacesRequest(
            new PlaceSearchRequest("Museo", "disabled-city", null));

        _cityRepoMock.Setup(r => r.GetByCodeAsync("disabled-city"))
            .ReturnsAsync(new City("disabled-city", "Disabled", false));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.SearchRequest.CityCode)
            .WithErrorCode(nameof(ErrorCode.INVALID_CITY));
    }

    [TestMethod]
    public async Task MaxResults_WhenExceedsLimit_Fails_WithMaxResultsExceeded()
    {
        var request = new SearchPlacesRequest(
            new PlaceSearchRequest("Museo", "madrid-es", 20));

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es"))
            .ReturnsAsync(new City("madrid-es", "Madrid", true));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldHaveValidationErrorFor(x => x.SearchRequest.MaxResults)
            .WithErrorCode(nameof(ErrorCode.MAX_RESULTS_EXCEEDED));
    }

    [TestMethod]
    public async Task MaxResults_WhenProvided_AndWithinLimit_Passes()
    {
        var request = new SearchPlacesRequest(
            new PlaceSearchRequest("Museo", "madrid-es", 5));

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es"))
            .ReturnsAsync(new City("madrid-es", "Madrid", true));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldNotHaveValidationErrorFor(x => x.SearchRequest.MaxResults);
    }

    [TestMethod]
    public async Task MaxResults_WhenNull_Passes()
    {
        var request = new SearchPlacesRequest(
            new PlaceSearchRequest("Museo", "madrid-es", null));

        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es"))
            .ReturnsAsync(new City("madrid-es", "Madrid", true));

        var result = await _sut.TestValidateAsync(request);

        result.ShouldNotHaveValidationErrorFor(x => x.SearchRequest.MaxResults);
    }
}