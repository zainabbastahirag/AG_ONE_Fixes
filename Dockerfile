FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AI-BABA-G/AI.Baba.Web/AI.Baba.Web.csproj AI-BABA-G/AI.Baba.Web/
RUN dotnet restore AI-BABA-G/AI.Baba.Web/AI.Baba.Web.csproj
COPY . .
RUN dotnet publish AI-BABA-G/AI.Baba.Web/AI.Baba.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    Database__Path=/data/baba.db \
    Ollama__BaseUrl=http://host.docker.internal:11434
VOLUME ["/data"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "AI.Baba.Web.dll"]
