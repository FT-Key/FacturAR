# FacturAR

[![CI](https://github.com/FT-Key/FacturAR/actions/workflows/ci.yml/badge.svg?branch=development)](https://github.com/FT-Key/FacturAR/actions/workflows/ci.yml)
[![CD](https://github.com/FT-Key/FacturAR/actions/workflows/cd.yml/badge.svg?branch=main)](https://github.com/FT-Key/FacturAR/actions/workflows/cd.yml)

Simulador de facturación electrónica de AFIP/ARCA para el Trabajo Final Integrador de la carrera. Incluye onboarding de empresas con solicitud pública, aprobación por administrador global e invitación por email, además de emisión, consultas, API keys, portal y verificación pública de un CAE.

> Estado actual: base funcional con API, portal Blazor/Razor, MongoDB, endpoint público de verificación, Docker y CI/CD. El onboarding, la autenticación, la emisión, el PDF y el SOAP están documentados y preparados como próximos incrementos. US-19, US-20 y US-31 están completadas; US-11 tiene base implementada; US-21 está en estado parcial.
>
> Planificación: las historias pendientes están refinadas con **contrato técnico** (modelo de datos, endpoints, validaciones y casos de prueba) contra el [contrato transversal de la API](docs/contratos-api.md), y el [plan de implementación](docs/plan-implementacion.md) fija el orden de ejecución.

## Alcance

- Solicitud pública de alta de empresa.
- Aprobación o rechazo por administrador global.
- Invitación por email, activación de cuenta y login multiempresa.
- Roles de empresa y roles globales.
- API keys y autenticación de integraciones.
- Emisión de facturas A, B y C con CAE simulado.
- Notas de crédito y débito.
- Numeración atómica por contribuyente, punto de venta y tipo de comprobante.
- Idempotencia de emisión.
- Consulta de comprobantes y último número autorizado.
- PDF con código QR.
- Verificación pública por CAE.
- API REST y endpoints SOAP compatibles con el flujo de AFIP.
- Auditoría, outbox, emails transaccionales y datos semilla.
- Manual online de integración.
- Despliegue con Docker en desarrollo y producción.

## Stack

| Capa | Tecnología |
| --- | --- |
| Backend | .NET 10, ASP.NET Core Minimal API |
| SOAP | CoreWCF o SoapCore |
| Frontend | Blazor Web App (Server) + Razor Pages |
| Base de datos | MongoDB 8+ (Atlas o replica set local) |
| PDF / QR | QuestPDF, QRCoder |
| Contenedores | Docker, Docker Compose |
| CI y CD | GitHub Actions + Docker Hub |
| Tests | xUnit |

No se utilizan EF Core, Dapper, SQL Server, SQLite, Web Forms ni gRPC en el alcance obligatorio.

Kubernetes tampoco se usa, por decisión de plataforma: el despliegue corre sobre Azure App Service for Containers, que ya se encarga de ejecutar los contenedores, aplicar los despliegues y sostener las dos Web Apps sin necesidad de un clúster propio. La historia US-26 (Kubernetes) queda como stretch y no se implementa en el MVP.

## Arquitectura

```text
Navegador
   |
   v
FacturAR.Web (Blazor Server + Razor Pages)
   |
   | HTTP/JSON
   v
FacturAR.Api (Minimal API + SOAP)
   |
   | MongoDB.Driver
   v
MongoDB (Atlas staging/producción o replica set local)
```

El frontend nunca accede directamente a MongoDB. La connection string queda exclusivamente en el backend.

### Proyectos

```text
FacturAR.slnx
facturAR_backend/
  src/
    FacturAR.Domain/          Entidades, enums y validaciones
    FacturAR.Application/     Casos de uso e interfaces
    FacturAR.Infrastructure/  MongoDB, repositorios e índices
    FacturAR.Contracts/       DTOs compartidos
    FacturAR.Api/             API REST, health y OpenAPI
  tests/
    FacturAR.UnitTests/
    FacturAR.IntegrationTests/
facturAR_frontend/
  src/
    FacturAR.Web/             Blazor Server + Razor Pages
docs/
  historias-usuario/
  historias-usuario-terminadas/
  postman/
  proceso-alta-empresa.md
  manual-integracion.md
  requisitos-funcionales.md
  requisitos-no-funcionales.md
  modelos.md
```

## Requisitos previos

- .NET SDK 10.0 o superior.
- Docker Desktop (para desarrollo con contenedores).
- MongoDB Atlas o una instancia local con replica set.
- Git.

Verificación rápida:

```powershell
dotnet --version
docker version
```

## Configuración

La API lee estas variables de entorno:

| Variable | Descripción | Ejemplo |
| --- | --- | --- |
| `Mongo__ConnectionString` | Connection string de MongoDB | `mongodb+srv://...` |
| `Mongo__DatabaseName` | Nombre de la base | `facturar_dev` |
| `Mongo__InitializeIndexes` | Crea/verifica índices al iniciar | `true` |
| `ASPNETCORE_ENVIRONMENT` | Entorno de ejecución | `Development` |

El frontend lee:

| Variable | Descripción | Ejemplo |
| --- | --- | --- |
| `Api__BaseUrl` | URL interna de la API | `http://localhost:5171` |
| `ASPNETCORE_ENVIRONMENT` | Entorno de ejecución | `Development` |

Variables previstas para el onboarding (US-04, US-27 y US-29):

| Variable | Descripción | Ejemplo |
| --- | --- | --- |
| `Admin__Email` | Email del administrador global inicial | `admin@facturar.local` |
| `Admin__Password` | Contraseña inicial; solo para bootstrap | `CambiarEstaClave123` |
| `Admin__Nombre` | Nombre visible del administrador | `Administrador` |
| `Admin__NotificationEmail` | Destino de avisos de nuevas solicitudes | `admin@facturar.local` |
| `Email__Provider` | `File` en desarrollo o `Smtp` en producción | `File` |
| `Email__From` | Remitente | `no-reply@facturar.local` |
| `Email__Host` | Host SMTP | `smtp.example.com` |
| `Email__Port` | Puerto SMTP | `587` |
| `Email__User` | Usuario SMTP | `no-reply@example.com` |
| `Email__Password` | Contraseña SMTP | `***` |
| `Email__UseSsl` | Usar TLS | `true` |
| `Portal__BaseUrl` | URL pública del portal para links | `http://localhost:5116` |

No se deben commitear archivos `.env` reales. Usar `.env.example` y `.env.production.example` como plantillas. Las variables de onboarding se incorporarán a esas plantillas al implementar las historias correspondientes.

## Ejecución local

```powershell
dotnet restore FacturAR.slnx
dotnet build FacturAR.slnx
```

Terminal 1, API:

```powershell
$env:Mongo__ConnectionString = "mongodb+srv://USUARIO:PASSWORD@cluster.mongodb.net/?retryWrites=true&w=majority"
$env:Mongo__DatabaseName = "facturar_dev"
dotnet run --project facturAR_backend/src/FacturAR.Api
```

Terminal 2, portal:

```powershell
$env:Api__BaseUrl = "http://localhost:5171"
dotnet run --project facturAR_frontend/src/FacturAR.Web
```

URLs locales:

- API: `http://localhost:5171`
- OpenAPI: `http://localhost:5171/openapi/v1.json`
- Salud (readiness): `http://localhost:5171/health`
- Salud (liveness): `http://localhost:5171/health/live`
- Portal: `http://localhost:5116`
- Salud del portal: `http://localhost:5116/health` y `http://localhost:5116/health/live`
- Verificación: `http://localhost:5116/verificar`
- Solicitud de alta (prevista): `http://localhost:5116/solicitud`
- Activación (prevista): `http://localhost:5116/activar`
- Manual (previsto): `http://localhost:5116/manual`
- Administración global (prevista): `http://localhost:5116/admin`

### MongoDB

Para desarrollo se recomienda un cluster gratuito de MongoDB Atlas. Crear un usuario de base de datos, habilitar el acceso desde la IP de desarrollo y copiar la connection string.

Si se usa MongoDB local, debe ser un replica set para soportar transacciones:

```powershell
docker run -d --name facturar-mongo -p 27017:27017 mongo:8 --replSet rs0 --bind_ip_all
docker exec -it facturar-mongo mongosh --eval "rs.initiate()"
```

Connection string resultante:

```text
mongodb://localhost:27017/?replicaSet=rs0
```

## Docker

### Desarrollo

```powershell
Copy-Item .env.example .env
# Editar .env con la connection string de staging
docker compose --env-file .env up --build
```

- API: `http://localhost:8080`
- Portal: `http://localhost:8081`

Si MongoDB corre en el host, la configuración por defecto usa `host.docker.internal:27017`.

### Producción

```powershell
Copy-Item .env.production.example .env.production
# Editar credenciales, connection string de producción e imagen
docker compose --env-file .env.production -f docker-compose.prod.yml pull
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
```

El servidor de producción debe tener Docker y Docker Compose instalados. La API escucha en `8080` y el portal en `80`; se recomienda colocar un reverse proxy con HTTPS delante.

Para volver a una versión anterior, cambiar `IMAGE_TAG` en `.env.production` por el SHA de un build previo y repetir `pull` + `up -d`.

## API

| Método | Ruta | Descripción |
| --- | --- | --- |
| `GET` | `/` | Información básica del servicio, con `version` y `commit` |
| `GET` | `/health` | Readiness: estado de la API y de MongoDB, con `version` y `commit` |
| `GET` | `/health/live` | Liveness: el proceso está vivo, con `version` y `commit`. No consulta MongoDB |
| `GET` | `/openapi/v1.json` | Documento OpenAPI (solo Development) |
| `GET` | `/v1/comprobantes/verificar/{cae}` | Verificación pública de un comprobante |

Endpoints de onboarding planificados (todavía no implementados):

| Método | Ruta | Historia |
| --- | --- | --- |
| `POST` | `/v1/solicitudes-alta` | US-01 |
| `POST` | `/v1/admin/auth/login` | US-04 |
| `POST` | `/v1/admin/auth/logout` | US-04 |
| `GET` | `/v1/admin/solicitudes` | US-27 |
| `GET` | `/v1/admin/solicitudes/{id}` | US-27 |
| `PUT` | `/v1/admin/solicitudes/{id}` | US-27 |
| `POST` | `/v1/admin/solicitudes/{id}/aprobar` | US-27 |
| `POST` | `/v1/admin/solicitudes/{id}/rechazar` | US-27 |
| `POST` | `/v1/admin/solicitudes/{id}/reenviar-invitacion` | US-27 |
| `GET` | `/v1/invitaciones/{token}` | US-28 |
| `POST` | `/v1/invitaciones/{token}/aceptar` | US-28 |
| `POST` | `/v1/auth/login` | US-04 |
| `POST` | `/v1/auth/logout` | US-04 |
| `GET` | `/v1/auth/me` | US-04 |
| `GET` | `/v1/api-keys` | US-02 |
| `POST` | `/v1/api-keys` | US-02 |
| `DELETE` | `/v1/api-keys/{id}` | US-02 |
| `POST` | `/v1/api-keys/{id}/regenerar` | US-02 |

Ejemplo:

```powershell
Invoke-RestMethod http://localhost:5171/v1/comprobantes/verificar/12345678901234
```

Respuesta esperada si existe:

```json
{
  "cae": "12345678901234",
  "cuitEmisor": "20123456786",
  "razonSocialEmisor": "Empresa de Prueba S.A.",
  "tipoComprobante": 1,
  "tipoComprobanteDescripcion": "Factura A",
  "puntoVenta": 1,
  "numero": 42,
  "fechaEmision": "2026-10-25T00:00:00Z",
  "importeTotal": 121000,
  "moneda": "ARS",
  "estado": "Emitido",
  "caeVencimiento": "2026-11-04T00:00:00Z"
}
```

### Códigos de error

| Código | HTTP | Cuándo |
| --- | --- | --- |
| `CAE_INVALIDO` | `400` | El CAE no tiene exactamente 14 dígitos. |
| `COMPROBANTE_NO_ENCONTRADO` | `404` | El CAE tiene formato válido pero no existe un comprobante emitido. |

Códigos previstos para onboarding y autenticación:

| Código | HTTP | Cuándo |
| --- | --- | --- |
| `VALIDACION` | `400` | Datos inválidos, incluido el CUIT. |
| `SOLICITUD_PENDIENTE` | `409` | Ya existe una solicitud pendiente para el CUIT o email. |
| `SOLICITUD_YA_PROCESADA` | `409` | La solicitud ya fue aprobada o rechazada. |
| `CUIT_YA_REGISTRADO` | `409` | Al aprobar, el CUIT ya pertenece a un contribuyente. |
| `CREDENCIALES_INVALIDAS` | `401` | Email o contraseña incorrectos. |
| `SIN_PERMISOS` | `403` | El rol no permite la acción. |
| `INVITACION_VENCIDA` | `410` | El token expiró. |
| `INVITACION_USADA` | `409` | El token ya fue utilizado. |
| `PASSWORD_DEBIL` | `400` | La contraseña no cumple la política. |
| `API_KEY_INVALIDA` | `401` | La API key no existe, está revocada o es inválida. |
| `RATE_LIMIT` | `429` | Se superó el límite de intentos. |

El endpoint `/health` devuelve `503` con estado `degraded` cuando MongoDB no responde. `/health/live` no consulta MongoDB y sigue devolviendo `200` mientras el proceso esté vivo. El portal expone los mismos dos endpoints.

`version` y `commit` provienen de las variables de entorno `APP_VERSION` y `SOURCE_COMMIT` que inyectan los Dockerfiles en la imagen. Si no están definidas, la API responde `version: "0.1.0"` y `commit: "unknown"`.

### Postman

Importar en Postman:

- `docs/postman/FacturAR.postman_collection.json`
- `docs/postman/FacturAR.local.postman_environment.json`

El environment usa `http://localhost:8080` para Docker. Para ejecución local, cambiar `baseUrl` a `http://localhost:5171`.

## Tests

```powershell
dotnet test FacturAR.slnx
```

La suite local tiene 18 tests unitarios y 9 de integración. Los unitarios cubren validación de CUIT, CAE y el caso de uso de verificación. Los de integración levantan la API en memoria y validan el endpoint raíz, OpenAPI y el rechazo de CAE inválidos sin requerir MongoDB.

Los tests marcados con `MongoFact` validan la API contra MongoDB real: inserción de un comprobante, verificación pública, `404`, ausencia de datos internos y creación del índice único de CAE. Se omiten automáticamente si no existe `MONGO_CONNECTION_STRING` o `Mongo__ConnectionString`. Cuando se ejecutan, usan una base temporal `facturar_it_*` y la eliminan al finalizar.

```powershell
$env:MONGO_CONNECTION_STRING = "mongodb+srv://..."
dotnet test FacturAR.slnx -c Release --filter "FullyQualifiedName~MongoApiTests"
```

## CI y CD

La integración continua y la entrega continua son dos workflows separados:

- US-20, `.github/workflows/ci.yml`: validación de rama, verificación de formato y analizadores, build, tests y construcción de las imágenes Docker sin publicar. No publica imágenes.
- US-31, `.github/workflows/cd.yml`: publicación de imágenes en Docker Hub y despliegue en Azure App Service. No ejecuta tests.

La guía operativa completa está en [`docs/despliegue-azure.md`](docs/despliegue-azure.md).

### CI

Se ejecuta en:

- Push a `main` y `development`.
- Pull requests hacia `main` y `development`.

Jobs:

1. `Validar nombre de rama`: solo en pull requests. Exige `feature/us-XX-slug`, `fix/us-XX-slug` o una rama auxiliar `chore/`, `docs/`, `refactor/`, `test/`, `perf/`, `ci/` o `build/`. Omite `main` y `development` para permitir la promoción entre ramas.
2. `Build y tests`: restaura, compila en `Release`, **verifica formato y analizadores** con `dotnet format FacturAR.slnx --verify-no-changes --verbosity minimal`, levanta MongoDB 8.2 como servicio, ejecuta la suite completa (incluidos los tests `MongoFact`) y sube `TestResults` como artefacto.
3. `Construir imágenes Docker`: construye la imagen de la API y la del portal **sin publicarlas**, para validar ambos Dockerfiles en cada push y PR. Usa los build args `SOURCE_COMMIT=${{ github.sha }}` y `APP_VERSION=0.1.0`, con caché de GitHub Actions separada por imagen.

La CI debe pasar antes de mergear. No publica ni despliega nada.

### CD

Se ejecuta cuando termina el workflow `CI` en `main` y la conclusión fue exitosa. También permite `workflow_dispatch` manual, únicamente sobre `main`, con los inputs `modo` (`deploy` o `rollback`) e `image_tag`.

Jobs:

1. `resolve-target`: determina el SHA objetivo. En modo `deploy` es el commit validado por la CI; en modo `rollback` es el `image_tag` con el SHA a desplegar, que debe tener 40 caracteres hexadecimales.
2. `publish-images`: publica en Docker Hub las imágenes `facturar-api` y `facturar-web` con dos tags: `latest` y el SHA del commit validado. Se omite en modo `rollback`.
3. `deploy-api`: despliega la API en Azure App Service con el tag SHA validado.
4. `deploy-web`: despliega el portal con el tag SHA validado. Corre después de `deploy-api`.
5. `rollback`: vuelve al SHA anterior si falla `deploy-api` o `deploy-web`.

El **smoke test verifica la versión desplegada**, no solo que la URL responda: consulta `/health/live` de la API y del portal, extrae el campo `commit` y lo compara con el SHA esperado. Si no coinciden, reintenta 18 veces y después falla el job. Es lo que garantiza que la Web App sirva exactamente la imagen recién publicada.

El **rollback automático** está configurado en el job `rollback`: busca el SHA anterior en Docker Hub (tags de 40 hexadecimales ordenados por `last_updated`, excluyendo el SHA que falló), verifica que exista en ambas imágenes y redespliega API y portal. Está configurado pero **no fue probado**: la evidencia de rollback del proyecto es la del rollback manual.

El **rollback manual** se dispara con `workflow_dispatch` sobre `main`, con `modo=rollback` e `image_tag=<sha de 40 hex>`. No recompila nada: despliega un tag ya publicado en Docker Hub.

Azure usa un plan B1 para poder activar Always On, porque F1 no lo soporta y las apps se dormían. El despliegue manual por SSH sigue disponible.

Secretos requeridos:

| Secreto | Uso |
| --- | --- |
| `DOCKERHUB_USERNAME` | Usuario de Docker Hub |
| `DOCKERHUB_TOKEN` | Access token de Docker Hub |
| `AZURE_WEBAPP_PUBLISH_PROFILE_API` | Publish profile de la Web App de la API |
| `AZURE_WEBAPP_PUBLISH_PROFILE_WEB` | Publish profile de la Web App del portal |

Variables requeridas:

| Variable | Uso |
| --- | --- |
| `AZURE_API_APP_NAME` | Nombre corto del recurso de la API, por ejemplo `facturar-api-utn-frt` |
| `AZURE_WEB_APP_NAME` | Nombre corto del recurso del portal, por ejemplo `facturar-web-utn-frt` |
| `AZURE_API_URL` | URL pública completa de la API, sin barra final |
| `AZURE_WEB_URL` | URL pública completa del portal, sin barra final |
| `AZURE_DEPLOY_ENABLED` | `true` habilita los jobs de deploy y de rollback; `false` solo publica imágenes |

Los hostnames cortos `<app>.azurewebsites.net` pueden no existir; `AZURE_*_URL` debe llevar la URL real que muestra Azure. El deploy automático ya está validado (US-31), así que `AZURE_DEPLOY_ENABLED` vale `true`; con `false` los jobs de deploy y de rollback se omiten y el CD solo publica imágenes.

Para que `workflow_run` funcione, `.github/workflows/cd.yml` debe existir en la rama default del repositorio.

### Flujo de ramas

```text
feature/us-XX-slug ──PR──> development ──PR──> main
```

- Las historias se desarrollan en `feature/us-XX-slug` o `fix/us-XX-slug`.
- Las ramas auxiliares usan `chore/`, `docs/`, `refactor/`, `test/`, `perf/`, `ci/` o `build/`.
- Los pull requests se abren contra `development`.
- `main` recibe solo cambios ya validados en `development`.
- Solo un push a `main` con CI exitosa dispara el CD: publicación de imágenes y despliegue en Azure.

### Protección de ramas

Configurar en GitHub:

1. `Settings > General > Default branch`: cambiar la rama default a `development`.
2. `Settings > Rules > Rulesets > New branch ruleset`.
3. Proteger `main`:
   - Requerir pull request antes de mergear.
   - Requerir al menos 1 aprobación.
   - Requerir los checks `CI / Validar nombre de rama` y `CI / Build y tests`.
   - Requerir que la rama esté actualizada antes de mergear.
   - Bloquear force pushes y borrado de la rama.
   - Resolver conversaciones antes de mergear.
4. Proteger `development`:
   - Requerir pull request y 1 aprobación.
   - Requerir los checks `CI / Validar nombre de rama` y `CI / Build y tests`.
   - Resolver conversaciones.
   - Bloquear force pushes y borrado.
5. Opcional: crear un ruleset para tags `v*` y bloquear su borrado o movimiento.

Los checks aparecen en la lista después de la primera ejecución de CI. Si todavía no corrieron, guardar la regla y volver a editarla después del primer push.

### Despliegue manual

```bash
cd /opt/facturar
docker compose --env-file .env.production -f docker-compose.prod.yml pull
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
```

Para rollback, cambiar `IMAGE_TAG` en `.env.production` por el SHA de un build anterior y repetir `pull` + `up -d`.

## Documentación

- [Contratos transversales de la API](docs/contratos-api.md)
- [Plan de implementación](docs/plan-implementacion.md)
- [Proceso de alta de empresa](docs/proceso-alta-empresa.md)
- [Manual de integración](docs/manual-integracion.md)
- [Requisitos funcionales](docs/requisitos-funcionales.md)
- [Requisitos no funcionales](docs/requisitos-no-funcionales.md)
- [Modelo de datos](docs/modelos.md)
- [Despliegue continuo en Azure](docs/despliegue-azure.md)
- [Historias de usuario](docs/historias-usuario/README.md)
- [Historias terminadas](docs/historias-usuario-terminadas/README.md)
- [Colección Postman](docs/postman/FacturAR.postman_collection.json)
- [Guía para agentes](AGENTS.md)

## Alcance y supuestos

- El CAE es simulado, con 14 dígitos y vencimiento a 10 días.
- No se realizan llamadas reales a AFIP/ARCA en el alcance obligatorio.
- No hay registro libre: el alta es por solicitud y aprobación manual.
- El CUIT es público y no prueba la titularidad de la empresa.
- La invitación vence a las 48 horas, es de un solo uso y se envía al email declarado.
- Los emails son transaccionales; en desarrollo se escriben en un archivo local y el log solo guarda metadatos.
- Los montos se modelan con `decimal` (MongoDB `Decimal128`).
- Las fechas se almacenan en UTC.
- El equipo es de 4-5 personas con una ventana de 3 semanas.
- El alcance obligatorio tiene 26 historias y 47 días ideales; US-22 a US-26 son stretch.
