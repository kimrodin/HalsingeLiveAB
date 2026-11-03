FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["[sS]ervices/Booking/Booking.csproj", "services/Booking/"]
COPY ["Contracts/Messaging/Messaging.csproj", "Contracts/Messaging/"]
RUN dotnet restore "services/Booking/Booking.csproj"
COPY ["[sS]ervices/Booking/", "services/Booking/"]
COPY ["Contracts/Messaging/", "Contracts/Messaging/"]
RUN dotnet publish "services/Booking/Booking.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Booking.dll"]
