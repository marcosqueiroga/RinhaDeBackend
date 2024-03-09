FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
RUN apt-get update && apt-get install clang zlib1g-dev -y --no-install-recommends
WORKDIR /app

COPY ./src ./

RUN dotnet restore -p:Optimize=true
COPY . .
RUN dotnet publish -p:Optimize=true -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["/app/RinhaDeBackend"]