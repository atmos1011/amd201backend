# ---------------------------------------------------------------------------
# Multi-stage build for VotingService.
# Build context is the REPOSITORY ROOT, because every service also compiles the
# shared PollBuilder.Contracts project:
#   docker build -f src/VotingService/Dockerfile -t pollbuilder-votingservice .
# ---------------------------------------------------------------------------

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from project files only, so the (slow) restore layer is reused
# whenever only source files change.
COPY Directory.Build.props ./
COPY src/PollBuilder.Contracts/PollBuilder.Contracts.csproj src/PollBuilder.Contracts/
COPY src/VotingService/VotingService.csproj src/VotingService/
RUN dotnet restore src/VotingService/VotingService.csproj

COPY src/PollBuilder.Contracts/ src/PollBuilder.Contracts/
COPY src/VotingService/ src/VotingService/
RUN dotnet publish src/VotingService/VotingService.csproj -c Release -o /app/publish --no-restore

# ---- runtime stage ----
# The runtime image carries no SDK, no source and no NuGet cache.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Run as the non-root user that ships with the ASP.NET image.
USER $APP_UID

EXPOSE 8080

# Render injects PORT at runtime; 8080 is the default everywhere else.
ENTRYPOINT ["sh", "-c", "dotnet VotingService.dll --urls http://0.0.0.0:${PORT:-8080}"]
