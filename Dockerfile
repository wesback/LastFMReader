FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
COPY CleaningRules.json ./
RUN dotnet publish -c Release -o out LastFM.ReaderCore.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

COPY --from=build-env /app/out .
COPY --from=build-env /app/CleaningRules.json . 
ENTRYPOINT ["dotnet", "LastFM.ReaderCore.dll"]
