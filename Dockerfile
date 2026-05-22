FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["api-DarioSabioni-Projeto-Final/EleganceStudio.API/EleganceStudio.API.csproj", "api-DarioSabioni-Projeto-Final/EleganceStudio.API/"]
RUN dotnet restore "api-DarioSabioni-Projeto-Final/EleganceStudio.API/EleganceStudio.API.csproj"
COPY . .
WORKDIR "/src/api-DarioSabioni-Projeto-Final/EleganceStudio.API"
RUN dotnet publish "EleganceStudio.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EleganceStudio.API.dll"]
