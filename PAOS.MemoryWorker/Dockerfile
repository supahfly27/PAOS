FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PAOS.MemoryWorker/PAOS.MemoryWorker.csproj", "PAOS.MemoryWorker/"]
COPY ["PAOS.Data/PAOS.Data.csproj", "PAOS.Data/"]
RUN dotnet restore "PAOS.MemoryWorker/PAOS.MemoryWorker.csproj"
COPY . .
RUN dotnet publish "PAOS.MemoryWorker/PAOS.MemoryWorker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PAOS.MemoryWorker.dll"]
