FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Services/Booking/Booking.csproj", "Services/Booking/"]
COPY ["Contracts/Messaging/Messaging.csproj", "Contracts/Messaging/"]
RUN dotnet restore "Services/Booking/Booking.csproj"
COPY . .
RUN dotnet publish "Services/Booking/Booking.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Booking.dll"]
