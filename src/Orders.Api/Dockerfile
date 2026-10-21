# Se construye desde la raíz del repositorio (contexto "."), porque necesita el proyecto OrderFlow.Contracts.

# ---- Etapa 1: compilar con el SDK completo ----
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Primero solo los .csproj: mientras no cambien las dependencias, Docker reutiliza la capa del restore.
COPY src/OrderFlow.Contracts/OrderFlow.Contracts.csproj src/OrderFlow.Contracts/
COPY src/Orders.Api/Orders.Api.csproj src/Orders.Api/
RUN dotnet restore src/Orders.Api/Orders.Api.csproj

COPY src/OrderFlow.Contracts/ src/OrderFlow.Contracts/
COPY src/Orders.Api/ src/Orders.Api/
RUN dotnet publish src/Orders.Api/Orders.Api.csproj -c Release -o /app --no-restore

# ---- Etapa 2: imagen final, solo con el runtime de ASP.NET ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /app .

# Usuario sin privilegios que ya trae la imagen oficial.
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Orders.Api.dll"]