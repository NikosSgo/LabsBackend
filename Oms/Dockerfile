FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Oms/Oms.csproj", "Oms/"]
COPY ["Migrations/Migrations.csproj", "Migrations/"]
COPY ["Models/Models.csproj", "Models/"]
COPY ["Messages/Messages.csproj", "Messages/"]
COPY ["Common/Common.csproj", "Common/"]
RUN dotnet restore "Oms/Oms.csproj"
COPY . .
WORKDIR "/src/Oms"
RUN dotnet build "./Oms.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Oms.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Oms.dll"]
