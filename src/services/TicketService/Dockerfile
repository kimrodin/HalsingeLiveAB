FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["[sS]ervices/TicketService/TicketService.csproj", "services/TicketService/"]
COPY ["Contracts/Messaging/Messaging.csproj", "Contracts/Messaging/"]
RUN dotnet restore "services/TicketService/TicketService.csproj"
COPY ["[sS]ervices/TicketService/", "services/TicketService/"]
COPY ["Contracts/Messaging/", "Contracts/Messaging/"]
RUN dotnet publish "services/TicketService/TicketService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "TicketService.dll"]
