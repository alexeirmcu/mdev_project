# Design: List Trips

## Technical Approach

New `GET /api/trips` endpoint following existing CQRS pattern: Controller → MediatR Query → Handler → Repository. Handler resolves optional `cityCode` to `long?` via `ICityRepository`, calls `ITripRepository.ListAsync(ownerUserId, cityId, startDate, endDate, ct)`, then maps via AutoMapper with computed aggregate fields.

## Architecture Decisions

| Option | Trade-offs | Decision |
|--------|------------|----------|
| cityCode→cityId in handler vs. controller | Controller stays thin; handler owns resolution | Handler injects `ICityRepository` |
| Computed fields in AutoMapper vs. handler | AutoMapper: declarative, but complex nested iteration | Handler computes after map, sets on response |
| Empty result vs. error on wrong cityCode | cityCode is optional; wrong code = no match | Return `[]` — repository returns empty |

## Data Flow

```
Client ──GET /api/trips?cityCode=madrid-es──→ TripsController
                                                    │
                                          mediator.Send(ListTrips{ cityCode })
                                                    │
                                              ListTripsHandler
                                               ├──ICityRepository.GetByCodeAsync("madrid-es")
                                               │    └──→ cityId = 1
                                               ├──ITripRepository.ListAsync("user-42", 1, null, null, ct)
                                               │    └──→ Trip[]
                                               ├──mapper.Map → TripSummaryResponse[]
                                               │    └── set computed counts after map
                                               └──→ TripSummaryResponse[]
                                                    │
                                              Ok(response)
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `SmartTripPlanner.Domain/ApiModels/TripSummaryResponse.cs` | Modify | `string CityId` → `long CityId`; add `string CityCode` |
| `SmartTripPlanner.ApplicationServices/Commands/ListTrips.cs` | Create | MediatR query with optional `CityCode`, `StartDate`, `EndDate` |
| `SmartTripPlanner.ApplicationServices/Validators/ListTripsValidator.cs` | Create | FluentValidation: date range rule if both provided |
| `SmartTripPlanner.ApplicationServices/Handlers/ListTripsHandler.cs` | Create | Handler: resolve cityCode → cityId, call ListAsync, map, compute aggregates |
| `SmartTripPlanner.API/Configurations/AutoMapperProfile.cs` | Modify | Add `Trip → TripSummaryResponse` mapping |
| `SmartTripPlanner.API/Controllers/TripsController.cs` | Modify | Add `ListTrips` action at `GET /api/trips` |
| `tests/.../Handlers/ListTripsHandlerTests.cs` | Create | Handler tests per AAA + Moq pattern |
| `tests/.../Controllers/TripsControllerTests.cs` | Modify | Add ListTrips controller tests |

## Interfaces / Contracts

```csharp
// SmartTripPlanner.ApplicationServices/Commands/ListTrips.cs
public record ListTrips(
    string? CityCode,
    DateOnly? StartDate,
    DateOnly? EndDate) : IRequest<IReadOnlyList<TripSummaryResponse>>;

// Updated TripSummaryResponse
public record TripSummaryResponse(
    Guid TripId,
    long CityId,                    // was: string CityId
    string CityCode,                // new
    string CityName,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalMustSees,
    int CompletedActivitiesCount,
    int TotalActivitiesCount);
```

### Computed fields (handler sets after AutoMapper map)

```
TotalMustSees           = trip.OriginalMustSees.Count
CompletedActivitiesCount = trip.Days.SumMany(d => d.Morning.Activities
                            + d.Afternoon.Activities + d.Evening.Activities)
                            .Count(a => a.IsCompleted)
TotalActivitiesCount     = trip.Days.SumMany(d => d.Morning.Activities
                            + d.Afternoon.Activities + d.Evening.Activities)
                            .Count()
```

For CREATED trips (no days, no itinerary): both counts = 0.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit (Handler) | CityCode resolution; date filter passthrough; computed counts (CREATED vs GENERATED); null cityCode | Moq ITripRepository + ICityRepository + real AutoMapper |
| Unit (Controller) | Route binding, query params mapped to ListTrips | Moq IMediator, verify Send |
| Unit (Validator) | Date range validation (start > end) | FluentValidation test |
| Integration | Full flow with real DB | Repository-level test |

## Migration / Rollout

No migration required. `TripSummaryResponse` has no existing consumers — this is the first usage.

## Open Questions

None.
