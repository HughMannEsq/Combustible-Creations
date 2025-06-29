# Use the official .NET runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Use the SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AutumnRidgeUSA.csproj", "."]
RUN dotnet restore "AutumnRidgeUSA.csproj"
COPY . .
RUN dotnet build "AutumnRidgeUSA.csproj" -c Release -o /app/build

# Publish the app
FROM build AS publish
RUN dotnet publish "AutumnRidgeUSA.csproj" -c Release -o /app/publish

# Final stage/image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AutumnRidgeUSA.dll"]