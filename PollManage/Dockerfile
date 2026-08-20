# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Render's free tier limits how many inotify watchers a container can create, and
# ASP.NET Core uses one to watch appsettings.json for changes. When the limit is hit
# the app crashes on the first line of Program.cs. Polling avoids inotify completely.
ENV DOTNET_USE_POLLING_FILE_WATCHER=true

EXPOSE 8080

ENTRYPOINT ["sh", "-c", "dotnet PollManage.dll --urls http://0.0.0.0:${PORT:-8080}"]
