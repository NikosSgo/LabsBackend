FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
ARG APP_UID=0
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Consumer/Consumer.csproj", "Consumer/"]
COPY ["Common/Common.csproj", "Common/"]
COPY ["Messages/Messages.csproj", "Messages/"]
COPY ["Models/Models.csproj", "Models/"]
RUN dotnet restore "Consumer/Consumer.csproj"
COPY . .
WORKDIR "/src/Consumer"
RUN dotnet build "./Consumer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Consumer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Consumer.dll"]
