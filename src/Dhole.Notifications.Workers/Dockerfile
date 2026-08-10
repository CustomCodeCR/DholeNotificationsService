FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "src/Dhole.Notifications.Workers/Dhole.Notifications.Workers.csproj"
RUN dotnet publish "src/Dhole.Notifications.Workers/Dhole.Notifications.Workers.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Dhole.Notifications.Workers.dll"]
