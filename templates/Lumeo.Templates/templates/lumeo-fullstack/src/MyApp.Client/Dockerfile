# syntax=docker/dockerfile:1
# Publish the Blazor WASM client, then serve the static output with nginx (which also
# reverse-proxies /api to the API container, so the browser is same-origin — no CORS).
# Context is the SOLUTION ROOT (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/MyApp.Client/MyApp.Client.csproj src/MyApp.Client/
RUN dotnet restore src/MyApp.Client/MyApp.Client.csproj
COPY src/MyApp.Client/ src/MyApp.Client/
RUN dotnet publish src/MyApp.Client/MyApp.Client.csproj -c Release -o /app/publish

FROM nginx:alpine AS final
COPY src/MyApp.Client/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
