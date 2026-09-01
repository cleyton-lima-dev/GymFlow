FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/GymFlow.Domain/GymFlow.Domain.csproj src/GymFlow.Domain/
COPY src/GymFlow.Application/GymFlow.Application.csproj src/GymFlow.Application/
COPY src/GymFlow.Infrastructure/GymFlow.Infrastructure.csproj src/GymFlow.Infrastructure/
COPY src/GymFlow.Api/GymFlow.Api.csproj src/GymFlow.Api/

RUN dotnet restore src/GymFlow.Api/GymFlow.Api.csproj

COPY src/ src/

RUN dotnet publish src/GymFlow.Api/GymFlow.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "GymFlow.Api.dll"]