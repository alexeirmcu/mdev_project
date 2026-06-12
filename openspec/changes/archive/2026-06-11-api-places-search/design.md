# Design: API Places Search

## Architecture Overview

Thin `ApiController` layer over existing MediatR pipeline. No changes to Domain, Infrastructure, or ApplicationServices. New code lives entirely in `SmartTripPlanner.API`.

```
Client → PlacesController (IMediator, IOptions<PlaceSearchOptions>)
              │
              ├─ ValidateInput() → 422 if invalid
              │
              └─ IMediator.Send(SearchPlacesRequest)
                      │
                      └─ SearchPlacesHandler.Handle()
                            │
                            └─ IPlaceRepository.SearchAsync()
                                  ├─ (local DB → results) → return
                                  └─ (no results → IPlaceExternalService)
                                        ├─ Foursquare → success → return
                                        └─ Foursquare → fail → empty
```

---

## Key Decisions

### Decision: Manual validation at controller level (no FluentValidation)
- **Status**: Accepted
- **Context**: Validation rules are simple (min length, list membership, range check). Adding FluentValidation adds a dependency for minimal gain.
- **Consequence**: Private helper method `ValidateRequest()` returns `List<ValidationResult>?` (null if valid).

### Decision: Static ValidationResult helper
- **Status**: Accepted
- **Context**: All 422 responses share the same format. A private static method `ToValidationResult(string errorCode, string description)` ensures consistency.

### Decision: IOptions<PlaceSearchOptions> for configuration
- **Status**: Accepted
- **Context**: User explicitly requested city list and max results to be configurable.
- **Consequence**: `PlaceSearchOptions` in `SmartTripPlanner.API.Configurations`, registered via `builder.Services.Configure<PlaceSearchOptions>()`.

---

## Component Design

### PlacesController

```csharp
[ApiController]
[Route("trips/places")]
public class PlacesController(
    IMediator mediator,
    IOptions<PlaceSearchOptions> options) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        [FromQuery] string cityId,
        [FromQuery] int? maxResults)
    {
        var errors = ValidateRequest(query, cityId, maxResults);
        if (errors is not null)
            return UnprocessableEntity(errors);

        var request = new SearchPlacesRequest(
            query, cityId, maxResults ?? options.Value.MaxResults);

        try
        {
            var response = await mediator.Send(request);
            if (response.Results.Count == 0 && /* external failure indicator? */)
                return UnprocessableEntity(new List<ValidationResult>
                {
                    new("EXTERNAL_SERVICE_FAILURE",
                        "Unable to search places at this time. Please try again later.")
                });

            return Ok(response.Results);
        }
        catch (HttpRequestException)
        {
            return UnprocessableEntity(new List<ValidationResult>
            {
                new("EXTERNAL_SERVICE_FAILURE",
                    "Unable to search places at this time. Please try again later.")
            });
        }
    }
}
```

**Note on external failure detection**: Since `PlaceRepository` already handles `HttpRequestException` inside the cascade (returns empty list), the controller won't see the exception. The design must differentiate between "no results found" (valid 200 with empty array) and "external API failed" (422). 

Two options:
1. Modify `SearchPlacesResponse` to include a success flag — rejected (no changes to ApplicationServices).
2. **Keep a separate fallback check**: If local DB had results but external call was skipped, or if external call returned results, we're fine. Since we can't easily differentiate at controller level, the simplest approach is: **the 422 for external failure is for cases where the handler explicitly propagates the error**. If the handler gracefully degrades (returns empty), the controller returns 200 with empty list.

**Final decision**: The controller returns 200 with empty results list when cascade gracefully degrades. The 422 `EXTERNAL_SERVICE_FAILURE` will be used for unhandled exceptions only (catch-all in controller).

### PlaceSearchOptions

```csharp
namespace SmartTripPlanner.API.Configurations;

public class PlaceSearchOptions
{
    public const string SectionName = "PlaceSearch";

    public string[] AllowedCities { get; set; } = ["madrid-es"];
    public int MaxResults { get; set; } = 10;
}
```

### Validation Logic

```
ValidateRequest(query, cityId, maxResults):
  errors = []
  if query is null or query.Length < 3:
      errors.Add("MIN_LENGTH_VIOLATION", "The search query must be at least 3 characters long.")
  if cityId is null or cityId not in options.AllowedCities:
      errors.Add("INVALID_CITY", $"City '{cityId}' is not supported.")
  if maxResults.HasValue and (maxResults < 1 or maxResults > options.MaxResults):
      errors.Add("MAX_RESULTS_EXCEEDED", $"Max results must be between 1 and {options.MaxResults}.")
  return errors.Count > 0 ? errors : null
```

### DI Registration (Program.cs)

```csharp
builder.Services.Configure<PlaceSearchOptions>(
    builder.Configuration.GetSection(PlaceSearchOptions.SectionName));
```

### appsettings.json

```json
{
  "PlaceSearch": {
    "AllowedCities": ["madrid-es"],
    "MaxResults": 10
  }
}
```

---

## Affected Files

| File | Action |
|------|--------|
| `SmartTripPlanner.API/Controllers/PlacesController.cs` | **Create** |
| `SmartTripPlanner.API/Configurations/PlaceSearchOptions.cs` | **Create** |
| `SmartTripPlanner.API/appsettings.json` | **Modify** — add PlaceSearch section |
| `SmartTripPlanner.API/Program.cs` | **Modify** — add `Configure<PlaceSearchOptions>` |
| `tests/SmartTripPlanner.Tests/SmartTripPlanner.API/Controllers/PlacesControllerTests.cs` | **Create** |

---

## Rollback

1. Delete `PlacesController.cs`
2. Delete `PlaceSearchOptions.cs`
3. Revert `appsettings.json`
4. Revert `Program.cs`
5. Delete test files
