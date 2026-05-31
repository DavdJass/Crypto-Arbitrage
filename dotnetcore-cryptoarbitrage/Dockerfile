# ─── Build Stage ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution & projects
COPY dotnetcore-cryptoarbitrage.sln ./
COPY ArbitrageBot.Domain/ArbitrageBot.Domain.csproj ArbitrageBot.Domain/
COPY ArbitrageBot.Application/ArbitrageBot.Application.csproj ArbitrageBot.Application/
COPY ArbitrageBot.Infrastructure/ArbitrageBot.Infrastructure.csproj ArbitrageBot.Infrastructure/
COPY ArbitrageBot.API/ArbitrageBot.API.csproj ArbitrageBot.API/

# Restore API only (sln includes Tests, not copied into image)
RUN dotnet restore ArbitrageBot.API/ArbitrageBot.API.csproj

# Copy full source
COPY ArbitrageBot.Domain/ ArbitrageBot.Domain/
COPY ArbitrageBot.Application/ ArbitrageBot.Application/
COPY ArbitrageBot.Infrastructure/ ArbitrageBot.Infrastructure/
COPY ArbitrageBot.API/ ArbitrageBot.API/

# Publish
RUN dotnet publish ArbitrageBot.API/ArbitrageBot.API.csproj -c Release -o /app --no-restore

# ─── Runtime Stage ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
ENV SQLITE_PATH=/data/arbitrage.db
EXPOSE 8080

# Persistencia opcional (SQLite) — montar volumen en /data
RUN mkdir -p /data

ENTRYPOINT ["dotnet", "ArbitrageBot.API.dll"]
