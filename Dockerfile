# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY Server/Server.csproj Server/
COPY ProcedureCore/ProcedureCore.csproj ProcedureCore/

# Restore dependencies
RUN dotnet restore Server/Server.csproj

# Copy source code
COPY Server/ Server/
COPY ProcedureCore/ ProcedureCore/

# Build and publish
RUN dotnet publish Server/Server.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "Server.dll"]
