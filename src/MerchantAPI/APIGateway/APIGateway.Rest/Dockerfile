#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["MerchantAPI/APIGateway/APIGateway.Rest/MerchantAPI.APIGateway.Rest.csproj", "MerchantAPI/APIGateway/APIGateway.Rest/"]
COPY ["MerchantAPI/APIGateway/APIGateway.Infrastructure/MerchantAPI.APIGateway.Infrastructure.csproj", "MerchantAPI/APIGateway/APIGateway.Infrastructure/"]
COPY ["MerchantAPI/APIGateway/APIGateway.Domain/MerchantAPI.APIGateway.Domain/MerchantAPI.APIGateway.Domain.csproj", "MerchantAPI/APIGateway/APIGateway.Domain/MerchantAPI.APIGateway.Domain/"]
COPY ["MerchantAPI/APIGateway/APIGateway.Database/APIGateway.Database.csproj", "MerchantAPI/APIGateway/APIGateway.Database/"]
COPY ["MerchantAPI/Common/Common/MerchantAPI.Common.csproj", "MerchantAPI/Common/Common/"]
RUN dotnet restore "MerchantAPI/APIGateway/APIGateway.Rest/MerchantAPI.APIGateway.Rest.csproj"
COPY . .
WORKDIR "/src/MerchantAPI/APIGateway/APIGateway.Rest"
RUN dotnet build "MerchantAPI.APIGateway.Rest.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MerchantAPI.APIGateway.Rest.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

WORKDIR /
COPY ["Deploy/run.sh", "."]

RUN chmod +x run.sh

ENTRYPOINT ["./run.sh"]