# Сборка проекта
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY ComplimentBot.csproj ./
RUN dotnet restore ComplimentBot.csproj

COPY . ./
RUN dotnet publish ComplimentBot.csproj -c Release -o out

# Финальный образ
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "ComplimentBot.dll"]
