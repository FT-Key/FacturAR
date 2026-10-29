FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY facturAR_backend/src/FacturAR.Domain/FacturAR.Domain.csproj facturAR_backend/src/FacturAR.Domain/
COPY facturAR_backend/src/FacturAR.Contracts/FacturAR.Contracts.csproj facturAR_backend/src/FacturAR.Contracts/
COPY facturAR_backend/src/FacturAR.Application/FacturAR.Application.csproj facturAR_backend/src/FacturAR.Application/
COPY facturAR_backend/src/FacturAR.Infrastructure/FacturAR.Infrastructure.csproj facturAR_backend/src/FacturAR.Infrastructure/
COPY facturAR_backend/src/FacturAR.Api/FacturAR.Api.csproj facturAR_backend/src/FacturAR.Api/

RUN dotnet restore facturAR_backend/src/FacturAR.Api/FacturAR.Api.csproj

COPY facturAR_backend/src/ facturAR_backend/src/

RUN dotnet publish facturAR_backend/src/FacturAR.Api/FacturAR.Api.csproj \
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
RUN mkdir -p /app/emails && chown app:app /app/emails
USER app

ENTRYPOINT ["dotnet", "FacturAR.Api.dll"]
