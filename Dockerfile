# Starfold (the homepage and Ezra) on Linux. Build with --pull so the .NET
# bases are the latest patched ones:
#   docker build --pull -t renaiparagonic/renai-repo:<tag> .
# The git-ignored star GIF (wwwroot/images/star-bg.gif) must be present in the
# build context; .dockerignore keeps out the database, keys and dev settings.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY starfold-mobile.csproj ./
RUN dotnet restore starfold-mobile.csproj
COPY . .
RUN dotnet publish starfold-mobile.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# Debian-based, so tzdata is there for Ezra's America/Chicago date folders.
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
# The shared SQLite database, its WAL files and the Data Protection keys live
# in /data (a host folder mounted there, owned by the app user, UID 1654).
RUN mkdir /data && chown $APP_UID /data
ENV ConnectionStrings__Starfold="Data Source=/data/starfold.db"
USER $APP_UID
# The image's ASPNETCORE_HTTP_PORTS is 8080.
EXPOSE 8080
ENTRYPOINT ["dotnet", "starfold-mobile.dll"]
