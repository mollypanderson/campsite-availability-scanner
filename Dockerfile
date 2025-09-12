# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o out

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app

# Copy published output
COPY --from=build /app/out .

# Expose port for bot
EXPOSE 8080

# Start the application
ENTRYPOINT ["dotnet", "campsite-availability-scanner.dll"]
