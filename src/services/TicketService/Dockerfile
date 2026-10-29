FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Services/TicketService/TicketService.csproj", "Services/TicketService/"]
COPY ["Contracts/Messaging/Messaging.csproj", "Contracts/Messaging/"]
RUN dotnet restore "Services/TicketService/TicketService.csproj"
COPY . .
RUN dotnet publish "Services/TicketService/TicketService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "TicketService.dll"]
