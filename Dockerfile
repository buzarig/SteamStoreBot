#############################################
# 1. Build stage: збираємо бот у /app/publish
#############################################
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копіюємо csproj і відновлюємо залежності
COPY SteamStoreBot.csproj ./
RUN dotnet restore ./SteamStoreBot.csproj

# Копіюємо весь код (Program.cs, App.config, папку Services, Models тощо)
COPY . ./

# Збираємо Release-версію у папку /app/publish
RUN dotnet publish -c Release -o /app/publish

#############################################
# 2. Runtime stage: мінімальний образ для запуску
#############################################
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Копіюємо з build-стадії згенеровані файли у фінальний образ
COPY --from=build /app/publish ./

# Якщо бот у режимі polling, явного порту не потрібно.
# Якщо б ви використовували webhooks із вебсервером, додайте:
# ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "SteamStoreBot.dll"]
