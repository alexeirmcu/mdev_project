# Proposal: INF-2 — CI/CD Pipeline

## Intent

Add GitHub Actions CI/CD pipeline that builds the .NET project and runs tests on every push and PR to main.

## Scope

### In Scope
- `.github/workflows/ci.yml` — build + test workflow
- Dockerfile for the API (optional, for docker build step)

### Out of Scope
- Deployment to production (INF-3)
- Release automation

## Capabilities

### New Capabilities
- `ci-cd-pipeline`: GitHub Actions workflow for build and test

### Modified Capabilities
- None

## Approach

1. Create `.github/workflows/ci.yml`
2. Use `actions/setup-dotnet@v4` with .NET 8
3. Run: `dotnet restore`, `dotnet build --no-restore`, `dotnet test --no-build`
4. Trigger on push/PR to `main`

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `.github/workflows/ci.yml` | New | CI pipeline definition |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| No solution file — tests reference API project directly | Low | Run `dotnet test` on test csproj, which transitively builds all |

## Rollback Plan

Delete `.github/workflows/ci.yml`

## Dependencies

- GitHub repository (already on GitHub)

## Success Criteria

- [ ] CI runs on push to main
- [ ] CI runs on PR to main
- [ ] All 598 tests pass in CI
- [ ] Build succeeds without warnings treated as errors
