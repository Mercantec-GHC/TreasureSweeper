FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Blazor/Blazor.csproj src/Blazor/
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Shared/Shared.csproj src/Shared/
RUN dotnet restore src/Blazor/Blazor.csproj

COPY src/ src/
RUN dotnet publish src/Blazor/Blazor.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false \
    ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
EXPOSE 8080

USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID

ENTRYPOINT ["dotnet", "Blazor.dll"]
