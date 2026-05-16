FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PAOS.MemoryAPI/PAOS.MemoryAPI.csproj", "PAOS.MemoryAPI/"]
COPY ["PAOS.Data/PAOS.Data.csproj", "PAOS.Data/"]
RUN dotnet restore "PAOS.MemoryAPI/PAOS.MemoryAPI.csproj"
COPY . .
RUN dotnet publish "PAOS.MemoryAPI/PAOS.MemoryAPI.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PAOS.MemoryAPI.dll"]
