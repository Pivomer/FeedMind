FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY *.sln ./
COPY src/. .

RUN dotnet restore "Hosts/FeedMind.API/FeedMind.API.csproj"
WORKDIR /src/Hosts/FeedMind.API
RUN dotnet build "FeedMind.API.csproj" -c Release -o /app/build

FROM build AS unit-tests
WORKDIR /src/UnitTests/UnitTests
RUN dotnet restore "./UnitTests.csproj"
RUN dotnet test "./UnitTests.csproj" -c Release --logger "trx;LogFileName=testresults.trx"

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FeedMind.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FeedMind.API.dll"]
