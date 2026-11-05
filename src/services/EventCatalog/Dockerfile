# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["EventCatalog.csproj", "."]

RUN dotnet restore "EventCatalog.csproj"

COPY . .

RUN dotnet publish "EventCatalog.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore


# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "EventCatalog.dll"]
