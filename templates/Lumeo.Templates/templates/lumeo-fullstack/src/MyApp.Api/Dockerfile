# syntax=docker/dockerfile:1
# Build the API. Context is the SOLUTION ROOT (see docker-compose.yml), so paths
# below are relative to it.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/MyApp.Api/MyApp.Api.csproj src/MyApp.Api/
RUN dotnet restore src/MyApp.Api/MyApp.Api.csproj
COPY src/MyApp.Api/ src/MyApp.Api/
RUN dotnet publish src/MyApp.Api/MyApp.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyApp.Api.dll"]
