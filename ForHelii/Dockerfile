# Сборка проекта
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Копируем .csproj отдельно и восстанавливаем зависимости
COPY ComplimentBot.csproj ./
RUN dotnet restore ComplimentBot.csproj

# Копируем остальные файлы и публикуем сборку
COPY . ./
RUN dotnet publish -c Release -o out

# Финальный образ
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "ComplimentBot.dll"]
