FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SmartTripPlanner.API/SmartTripPlanner.API.csproj SmartTripPlanner.API/
COPY SmartTripPlanner.ApplicationServices/SmartTripPlanner.ApplicationServices.csproj SmartTripPlanner.ApplicationServices/
COPY SmartTripPlanner.Domain/SmartTripPlanner.Domain.csproj SmartTripPlanner.Domain/
COPY SmartTripPlanner.Infrastructure/SmartTripPlanner.Infrastructure.csproj SmartTripPlanner.Infrastructure/
RUN dotnet restore SmartTripPlanner.API/SmartTripPlanner.API.csproj
COPY . .
RUN dotnet build SmartTripPlanner.API/SmartTripPlanner.API.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish SmartTripPlanner.API/SmartTripPlanner.API.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartTripPlanner.API.dll"]
