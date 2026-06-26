# Tasks: Flexible Query & External Fallback for Place Search

## Review Workload Forecast

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: Medium

| Field | Value |
|-------|-------|
| Estimated changed lines | ~350-450 |
| 400-line budget risk | Medium |
| Chained PRs recommended | Yes |
| Delivery strategy | ask-on-risk |
| Suggested split | PR 1 (Domain+Infra) → PR 2 (App+API+Tests) |

### Suggested Work Units

| Unit | Goal | PR | Notes |
|------|------|----|-------|
| 1 | Domain+Infra foundation | PR 1 | ProviderId, repo, config, client, migration |
| 2 | Handler+Validator+Tests+API | PR 2 | Depends on PR 1 |

## Phase 1: Domain Layer

- [x] 1.1 **PlaceAttribute.ProviderId** (S) — Add nullable `ProviderId` (init), update ctors. **TDD**: Test with/without providerId, null-safety.
- [x] 1.2 **IPlaceRepository** (S) — `SearchAsync` query to `string?`, add `GetProviderIdForCategoryAsync`. **TDD**: Compile check.
- [x] 1.3 **Request+Model fields** (XS) — Add `FetchFromExternalIfInsufficient` to `PlaceSearchRequest`. Add `ProviderId` to `PlaceAttributeModel`.

## Phase 2: Infrastructure Layer

- [x] 2.1 **EF config + migration** (XS) — Add ProviderId column (nullable, max 100) to PlaceAttribute config. Create migration.
- [x] 2.2 **PlaceRepository.SearchAsync** (M) — Null-safe query handling. Category filter on attribute. Remove cascade. Add `GetProviderIdForCategoryAsync`. **TDD**: Test null query, category filter, resolution.
- [x] 2.3 **PlaceRepository.UpsertRangeAsync dedup** (M) — Match by ProviderReferenceId. Update basic fields (Name, Location, Hours, Attributes, IsFriendly/Indoor/Duration). Preserve enrichment (FamilyFriendlyScore, Popularity, IsEnriched). **TDD**: Test enrichment kept, new insert, idempotent.
- [x] 2.4 **ResolveAttributesAsync ProviderId** (S) — Preserve DB ProviderId. Accept incoming ProviderId on new attrs. **TDD**: Test populate, retain.
- [x] 2.5 **FoursquareApiClient + IFoursquareApiClient** (S) — Add optional `List<string>? fsqCategoryIds` param, build URL with `categories`. **TDD**: Test URL includes/excludes param.
- [x] 2.6 **FoursquarePlaceService** (M) — Remove chain mapping. Populate `ProviderId` from `FsqCategoryId`. Pass `fsqCategoryIds`. **TDD**: Test no chains, ProviderId set.

## Phase 3: Application Layer

- [x] 3.1 **SearchPlacesRequest** (XS) — Add `FetchFromExternalIfInsufficient` field.
- [x] 3.2 **SearchPlacesRequestValidator** (M) — Remove query required+minlength. Add at-least-one guard (query/category/filters). Query min 3 only when present. **TDD**: Test all-null fails, category-only passes, short query fails.
- [x] 3.3 **SearchPlacesHandler rewrite** (L) — Local search → if count < maxResults AND FetchFromExternal=true → resolve category → external → dedup → upsert → return. Cold start: skip external. **TDD**: Test sufficient local, insufficient local, fetch=false, cold start, dedup merge, null query.

## Phase 4: API & Fixes

- [x] 4.1 **AutoMapper** (XS) — Map `ProviderId` to `PlaceAttributeModel`. (Convention-based, no explicit change needed.)
- [x] 4.2 **Fix existing tests** (M) — Update handler tests for new flow. FoursquarePlaceServiceTests: remove chain test, add ProviderId. PlaceRepositoryTests: fix chain test. Validator tests: swap query-required for at-least-one.
