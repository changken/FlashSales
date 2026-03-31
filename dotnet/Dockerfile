FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/FlashSales.Api/FlashSales.Api.csproj src/FlashSales.Api/
RUN dotnet restore src/FlashSales.Api/FlashSales.Api.csproj
COPY src/ src/
RUN dotnet publish src/FlashSales.Api/FlashSales.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "FlashSales.Api.dll"]
