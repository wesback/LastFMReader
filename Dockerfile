FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build-env
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

RUN apk add --no-cache icu-libs

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
COPY CleaningRules.json ./
RUN dotnet publish -c Release -o out LastFM.ReaderCore.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine
WORKDIR /app

COPY --from=build-env /app/out .
COPY --from=build-env /app/CleaningRules.json . 
ENTRYPOINT ["dotnet", "LastFM.ReaderCore.dll"]
