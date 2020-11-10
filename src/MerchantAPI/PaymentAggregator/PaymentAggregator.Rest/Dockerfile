#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["MerchantAPI/PaymentAggregator/PaymentAggregator.Rest/MerchantAPI.PaymentAggregator.Rest.csproj", "MerchantAPI/PaymentAggregator/PaymentAggregator.Rest/"]
COPY ["MerchantAPI/PaymentAggregator/PaymentAggregator.Infrastructure/MerchantAPI.PaymentAggregator.Infrastructure.csproj", "MerchantAPI/PaymentAggregator/PaymentAggregator.Infrastructure/"]
COPY ["MerchantAPI/PaymentAggregator/PaymentAggregator.Domain/MerchantAPI.PaymentAggregator.Domain.csproj", "MerchantAPI/PaymentAggregator/PaymentAggregator.Domain/"]
COPY ["MerchantAPI/PaymentAggregator/PaymentAggregator.Database/PaymentAggregator.Database.csproj", "MerchantAPI/PaymentAggregator/PaymentAggregator.Database/"]
COPY ["MerchantAPI/Common/Common/MerchantAPI.Common.csproj", "MerchantAPI/Common/Common/"]
RUN dotnet restore "MerchantAPI/PaymentAggregator/PaymentAggregator.Rest/MerchantAPI.PaymentAggregator.Rest.csproj"
COPY . .
WORKDIR "/src/MerchantAPI/PaymentAggregator/PaymentAggregator.Rest"
RUN dotnet build "MerchantAPI.PaymentAggregator.Rest.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MerchantAPI.PaymentAggregator.Rest.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

WORKDIR /
COPY ["Deploy/PaymentAggregator/run.sh", "."]

RUN chmod +x run.sh

ENTRYPOINT ["./run.sh"]