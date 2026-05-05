FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BabaPortal.Api/BabaPortal.Api.csproj src/BabaPortal.Api/
RUN dotnet restore src/BabaPortal.Api/BabaPortal.Api.csproj
COPY . .
RUN dotnet publish src/BabaPortal.Api/BabaPortal.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    Database__Path=/data/baba.db \
    Ollama__BaseUrl=http://host.docker.internal:11434
VOLUME ["/data"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "BabaPortal.Api.dll"]
