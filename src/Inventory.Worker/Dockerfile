# Se construye desde la raíz del repositorio (contexto "."), porque necesita el proyecto OrderFlow.Contracts.

# ---- Etapa 1: compilar con el SDK completo ----
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY src/OrderFlow.Contracts/OrderFlow.Contracts.csproj src/OrderFlow.Contracts/
COPY src/Inventory.Worker/Inventory.Worker.csproj src/Inventory.Worker/
RUN dotnet restore src/Inventory.Worker/Inventory.Worker.csproj

COPY src/OrderFlow.Contracts/ src/OrderFlow.Contracts/
COPY src/Inventory.Worker/ src/Inventory.Worker/
RUN dotnet publish src/Inventory.Worker/Inventory.Worker.csproj -c Release -o /app --no-restore

# ---- Etapa 2: imagen final. Sin HTTP, así que basta el runtime base (sin ASP.NET) ----
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine
WORKDIR /app
COPY --from=build /app .

USER app
ENTRYPOINT ["dotnet", "Inventory.Worker.dll"]