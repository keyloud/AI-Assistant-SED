FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["AssistantApi/AssistantApi.csproj", "AssistantApi/"]
RUN dotnet restore "AssistantApi/AssistantApi.csproj"

COPY . .
WORKDIR "/src/AssistantApi"
RUN dotnet build "AssistantApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AssistantApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AssistantApi.dll"]
