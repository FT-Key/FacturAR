---
name: dotnet-testing
description: Guía los tests de FacturAR con xUnit, WebApplicationFactory y MongoFact, incluyendo integración real, omisión sin connection string y limpieza. Usar al agregar o revisar tests.
---

# Estrategia de tests

## Pirámide

- **Unitarios**: validaciones, cálculos, reglas de Domain/Application. Rápidos y sin infraestructura.
- **Integración**: endpoints, DI, configuración, Mongo y serialización. Usan `WebApplicationFactory`.
- **QA**: flujos de portal con Playwright cuando está disponible.

## Convenciones

- Nombre: `Metodo_Escenario_Resultado`.
- Un comportamiento por test; Arrange/Act/Assert claro.
- Sin dependencia de orden entre tests ni `Thread.Sleep`.
- Fechas y montos deterministas; UTC explícito.
- No uses datos reales, secretos ni connection strings en el código.

## Integración sin Mongo

- `CustomWebApplicationFactory` levanta la API en memoria.
- Desactivá índices con `Mongo:InitializeIndexes=false` cuando no se usa Mongo.
- Probá raíz, OpenAPI, validaciones y errores de entrada.

## Integración con Mongo

- Usá `[MongoFact]`, que se omite si falta `MONGO_CONNECTION_STRING` o `Mongo__ConnectionString`.
- Base única y corta: `facturar_it_<guid corto>`, máximo 38 bytes.
- Configurá `Mongo:ConnectionString`, `Mongo:DatabaseName` y `Mongo:InitializeIndexes` con `UseSetting`.
- Inicializá índices con timeout suficiente.
- Insertá datos de prueba, llamá al endpoint y verificá contrato y ausencia de campos internos.
- Eliminá la base en `DisposeAsync` aunque el test falle.
- No imprimas el connection string.

## Comandos

```powershell
dotnet build FacturAR.slnx -c Release
dotnet test FacturAR.slnx -c Release --no-build --logger "console;verbosity=minimal"
dotnet test FacturAR.slnx -c Release --no-build --filter "FullyQualifiedName~MongoApiTests"
```

## Reglas

- No borres ni desactives tests para hacer pasar la suite.
- Si un test revela un bug, no lo maquilles: reportalo.
- Todo bug corregido debe tener un test de regresión.
- La cobertura es una guía, no un objetivo; priorizá comportamiento y riesgos.
