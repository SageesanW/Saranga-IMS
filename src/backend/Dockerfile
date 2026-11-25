# Multi-stage Dockerfile for Saranga ASP.NET Core .NET 10 API
# Target Environment: Render Web Service (Docker)
# Base Directory / Build Context: src/backend

# ==========================================
# Stage 1: Build & Publish
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for optimized layer caching during restore
COPY Saranga.Domain/Saranga.Domain.csproj ./Saranga.Domain/
COPY Saranga.Application/Saranga.Application.csproj ./Saranga.Application/
COPY Saranga.Infrastructure/Saranga.Infrastructure.csproj ./Saranga.Infrastructure/
COPY Saranga.Api/Saranga.Api.csproj ./Saranga.Api/

# Restore NuGet dependencies
RUN dotnet restore ./Saranga.Api/Saranga.Api.csproj

# Copy remaining source code
COPY Saranga.Domain/ ./Saranga.Domain/
COPY Saranga.Application/ ./Saranga.Application/
COPY Saranga.Infrastructure/ ./Saranga.Infrastructure/
COPY Saranga.Api/ ./Saranga.Api/

# Build and publish in Release mode
WORKDIR /src/Saranga.Api
RUN dotnet publish Saranga.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# Stage 2: Runtime
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Default port configuration for Render
ENV PORT=10000
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose Render default port
EXPOSE 10000

# Copy published application
COPY --from=build /app/publish .

# Run the API application
ENTRYPOINT ["dotnet", "Saranga.Api.dll"]
