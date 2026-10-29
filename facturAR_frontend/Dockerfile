FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY facturAR_backend/src/FacturAR.Contracts/FacturAR.Contracts.csproj facturAR_backend/src/FacturAR.Contracts/
COPY facturAR_frontend/src/FacturAR.Web/FacturAR.Web.csproj facturAR_frontend/src/FacturAR.Web/

RUN dotnet restore facturAR_frontend/src/FacturAR.Web/FacturAR.Web.csproj

COPY facturAR_backend/src/FacturAR.Contracts/ facturAR_backend/src/FacturAR.Contracts/
COPY facturAR_frontend/src/FacturAR.Web/ facturAR_frontend/src/FacturAR.Web/

RUN dotnet publish facturAR_frontend/src/FacturAR.Web/FacturAR.Web.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ARG SOURCE_COMMIT=unknown
ARG APP_VERSION=0.1.0

ENV ASPNETCORE_URLS=http://+:8080
ENV SOURCE_COMMIT=$SOURCE_COMMIT
ENV APP_VERSION=$APP_VERSION
EXPOSE 8080

COPY --from=build /app/publish .
USER app

ENTRYPOINT ["dotnet", "FacturAR.Web.dll"]
