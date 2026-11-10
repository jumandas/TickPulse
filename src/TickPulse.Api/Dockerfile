# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY TickPulse.sln ./
COPY src/TickPulse.Core/TickPulse.Core.csproj src/TickPulse.Core/
COPY src/TickPulse.Analyst/TickPulse.Analyst.csproj src/TickPulse.Analyst/
COPY src/TickPulse.Api/TickPulse.Api.csproj src/TickPulse.Api/
RUN dotnet restore src/TickPulse.Api/TickPulse.Api.csproj
COPY . .
RUN dotnet publish src/TickPulse.Api/TickPulse.Api.csproj -c Release -o /app/publish --no-restore

# Run stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "TickPulse.Api.dll"]
