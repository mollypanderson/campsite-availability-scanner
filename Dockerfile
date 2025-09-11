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

# Copy scripts folder
COPY ./scripts ./scripts

# Make all scripts executable
RUN chmod +x ./scripts/*.sh

# Install jq and curl for ngrok
RUN apt-get update && apt-get install -y curl jq && rm -rf /var/lib/apt/lists/*

# Copy entrypoint script
COPY scripts/entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

# Expose port for bot
EXPOSE 5167

# Entrypoint
ENTRYPOINT ["/app/entrypoint.sh"]
