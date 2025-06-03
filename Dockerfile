#############################################
#   1) BUILD STAGE: використовуємо образ із  #
#      .NET Framework SDK 4.8 для збірки     #
#############################################
FROM mcr.microsoft.com/dotnet/framework/sdk:4.8 AS build
SHELL ["powershell", "-Command"] 
WORKDIR C:\src

# 1.1) Копіюємо файл .csproj (або solution), щоб відновити NuGet-пакети
#      Якщо у вас є Solution (*.sln), замініть на нього. 
COPY ["SteamStoreBot.csproj", "./"]

# 1.2) Відновлюємо пакети. 
#      Якщо ви використовуєте packages.config → nuget restore, 
#      або якщо PackageReference у .csproj → msbuild /t:Restore
RUN nuget restore "SteamStoreBot.csproj"

# 1.3) Копіюємо весь вихідний код у контейнер
COPY . .

# 1.4) Збираємо й публікуємо Release‐версію у C:\app\publish
#      Тут використовуємо MSBuild для .NET Framework 4.8
RUN msbuild "SteamStoreBot.csproj" /p:Configuration=Release /p:OutputPath="C:\app\publish"

#################################################
#   2) RUNTIME STAGE: легший runtime‐образ      #
#      із .NET Framework 4.8 лише для запуску   #
#################################################
FROM mcr.microsoft.com/dotnet/framework/runtime:4.8
SHELL ["powershell", "-Command"]
WORKDIR C:\app

# 2.1) Копіюємо зібрані файли з попередньої стадії
COPY --from=build C:\app\publish\* .\

# 2.2) Якщо ваш бот читає botConfig.json, переконайтеся, що 
#      ви його скопіювали разом із вихідними файлами у C:\app\publish. 
#      Якщо config-JSON ви будете передавати через ENV, можете не копіювати його.
#      (У BUILD‐стадії ми вже копіювали весь каталок, отож botConfig.json там є.)

# 2.3) Вказуємо команду запуску (консольний .exe)
ENTRYPOINT ["SteamStoreBot.exe"]
