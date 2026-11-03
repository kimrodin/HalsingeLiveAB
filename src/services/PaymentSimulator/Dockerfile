FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Normalize the services directory casing across Git and existing Windows checkouts.
COPY ["[sS]ervices/PaymentSimulator/PaymentSimulator.csproj", "services/PaymentSimulator/"]
COPY ["Contracts/Messaging/Messaging.csproj", "Contracts/Messaging/"]
RUN dotnet restore "services/PaymentSimulator/PaymentSimulator.csproj"

COPY ["[sS]ervices/PaymentSimulator/", "services/PaymentSimulator/"]
COPY ["Contracts/Messaging/", "Contracts/Messaging/"]
RUN dotnet publish "services/PaymentSimulator/PaymentSimulator.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PaymentSimulator.dll"]
