# Use the official .NET SDK image as the base image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory inside the container
WORKDIR /app

# Copy the .NET project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the entire project and build the app
COPY . ./
RUN dotnet publish -c Release -o /app/out

# Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Specify the target runtime using a placeholder (modify if needed)
ARG TARGET_RUNTIME=linux-x64

COPY --from=build /app/out ./

# Set the entry point for the container
ENTRYPOINT ["dotnet", "/app/Rinha2024Q1-Marcelo-Lucas-Dotnet.dll"]
