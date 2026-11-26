# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY FiveAndOne.sln ./
COPY FiveAndOne/FiveAndOne.API.csproj FiveAndOne/
COPY FiveAndOne.Application/FiveAndOne.Application.csproj FiveAndOne.Application/
COPY FiveAndOne.Domain/FiveAndOne.Domain.csproj FiveAndOne.Domain/
COPY FiveAndOne.Infrastructure/FiveAndOne.Infrastructure.csproj FiveAndOne.Infrastructure/
RUN dotnet restore FiveAndOne/FiveAndOne.API.csproj
COPY . .
RUN dotnet publish FiveAndOne/FiveAndOne.API.csproj -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
# The app stores UTC and converts with Europe/Belgrade (tz data ships with the Debian-based image).
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
# Mock file storage writes here; mount a volume (or switch to object storage).
USER root
RUN mkdir -p /app/wwwroot/uploads && chown -R app:app /app/wwwroot
USER app
EXPOSE 8080
# Point your host's health probe at GET /health.
ENTRYPOINT ["dotnet", "FiveAndOne.API.dll"]
