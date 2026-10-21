FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/BookStore.Api/BookStore.Api.csproj", "src/BookStore.Api/"]
COPY ["src/BookStore.Infrastructure/BookStore.Infrastructure.csproj", "src/BookStore.Infrastructure/"]
COPY ["src/BookStore.Application/BookStore.Application.csproj", "src/BookStore.Application/"]
COPY ["src/BookStore.Domain/BookStore.Domain.csproj", "src/BookStore.Domain/"]
RUN dotnet restore "src/BookStore.Api/BookStore.Api.csproj"

COPY src/ src/
WORKDIR "/src/src/BookStore.Api"

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BookStore.Api.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BookStore.Api.dll"]
