FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Hosts/FeedMind.API/. .

RUN dotnet restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FeedMind.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FeedMind.API.dll"]
