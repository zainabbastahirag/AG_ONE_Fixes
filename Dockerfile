FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AI-BABA-G/AI.Baba.Web/AI.Baba.Web.csproj AI-BABA-G/AI.Baba.Web/
RUN dotnet restore AI-BABA-G/AI.Baba.Web/AI.Baba.Web.csproj
COPY . .
RUN dotnet publish AI-BABA-G/AI.Baba.Web/AI.Baba.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /app/data && chmod 777 /app/data
# Defaults that make the image deploy-ready out of the box (no SQL Server
# required — falls back to a SQLite file under /app/data which can be
# mounted as a persistent volume).
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    Database__Provider=sqlite \
    ConnectionStrings__BabaDb="Data Source=/app/data/baba.db" \
    Ollama__BaseUrl=http://host.docker.internal:11434
VOLUME ["/app/data"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "AI.Baba.Web.dll"]
