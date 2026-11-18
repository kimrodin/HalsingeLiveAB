FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /work
COPY src/Shared/ClientAddressing/ src/Shared/ClientAddressing/
COPY tests/proxy-chain/probe/ tests/proxy-chain/probe/
RUN dotnet publish tests/proxy-chain/probe/Probe.csproj -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "Probe.dll"]
