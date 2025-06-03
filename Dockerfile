#############################################
# 1. Build stage: ������� ��� � /app/publish
#############################################
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# ������� csproj � ���������� ���������
COPY SteamStoreBot.csproj ./
RUN dotnet restore ./SteamStoreBot.csproj

# ������� ���� ��� (Program.cs, App.config, ����� Services, Models ����)
COPY . ./

# ������� Release-����� � ����� /app/publish
RUN dotnet publish -c Release -o /app/publish

#############################################
# 2. Runtime stage: ��������� ����� ��� �������
#############################################
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# ������� � build-���䳿 ���������� ����� � ��������� �����
COPY --from=build /app/publish ./

# ���� ��� � ����� polling, ������ ����� �� �������.
# ���� � �� ��������������� webhooks �� �����������, �������:
# ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "SteamStoreBot.dll"]
