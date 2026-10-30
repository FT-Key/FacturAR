# Despliegue continuo en Azure con Docker Hub y App Service

Esta guía explica cómo publicar FacturAR en internet usando recursos gratuitos o de bajo costo:

- GitHub Actions para CI/CD.
- Docker Hub para las imágenes.
- Azure App Service for Containers, plan B1, para la API y el portal.
- MongoDB Atlas M0 para la base de datos.

El objetivo es que un push a `main` con CI exitoso publique las imágenes y actualice ambas aplicaciones sin intervención manual. El procedimiento es el mismo patrón que el proyecto ICS: Docker Hub + App Service + publish profile. FacturAR agrega una segunda Web App para el portal Blazor.

El plan B1 se eligió para la demo porque soporta **Always On** y F1 no. Con F1 las apps se dormían y el primer request tardaba demasiado; B1 mantiene los contenedores despiertos y sigue siendo de bajo costo.

> Esta guía corresponde a **US-31 — Entrega continua y despliegue en Azure**. La CI está en **US-20**. El despliegue manual por SSH de `docker-compose.prod.yml` sigue disponible para otros entornos.

## 1. Resultado esperado

Al terminar:

- API: `https://<api-app>.azurewebsites.net`
- Portal: `https://<web-app>.azurewebsites.net`
- Readiness de API: `https://<api-app>.azurewebsites.net/health`
- Liveness de API: `https://<api-app>.azurewebsites.net/health/live`
- Salud del portal: `https://<web-app>.azurewebsites.net/health` y `https://<web-app>.azurewebsites.net/health/live`
- Base: MongoDB Atlas M0.
- Imágenes: `<dockerhub-user>/facturar-api:<sha>` y `<dockerhub-user>/facturar-web:<sha>`.
- Push a `main` → CI → CD publica imágenes → CD despliega API → smoke test del commit → CD despliega portal → smoke test del commit.

Cada despliegue verifica que la aplicación servida informe el **mismo commit** que el pipeline acaba de publicar. Si el `commit` de `/health/live` no coincide con el SHA esperado, el job falla y no se continúa.

En la demo actual las URLs completas son:

- API: `https://facturar-api-utn-frt-gncabkgzghh2gbf2.brazilsouth-01.azurewebsites.net`
- Portal: `https://facturar-web-utn-frt-gjbhdhb3bjgggxfg.brazilsouth-01.azurewebsites.net`

Los hostnames cortos `https://<app>.azurewebsites.net` pueden no existir: Azure puede asignar un sufijo adicional. Siempre usar la URL real que muestra el portal en **Overview → Default domain**.

## 2. Arquitectura

```text
Push / PR
   │
   ▼
GitHub Actions CI (US-20)
   │  build + tests + lint + MongoDB 8.2 + imágenes Docker
   ▼
main con CI exitoso
   │
   ▼
GitHub Actions CD (US-31)
   │
   ├── resolve-target ──► determina el SHA objetivo (deploy o rollback)
   │
   ├── docker build/push ──► Docker Hub
   │                         ├── facturar-api:<sha>
   │                         └── facturar-web:<sha>
   │
   ├── azure/webapps-deploy ──► App Service API
   │        └── smoke test: /health/live debe informar el <sha>
   │
   ├── azure/webapps-deploy ──► App Service Web
   │        └── smoke test: /health/live debe informar el <sha>
   │
   └── rollback (si falla deploy-api o deploy-web)
            └── vuelve al SHA anterior publicado en Docker Hub

App Service API ──► MongoDB Atlas
App Service Web ──► App Service API
```

Puntos importantes:

- Azure no detecta cambios en Docker Hub. GitHub Actions debe ejecutar el deploy explícitamente.
- No se usa ACR. ACR es Azure Container Registry, un registro privado de Azure; no es necesario para este alcance.
- No se usa Azure Container Apps ni Kubernetes.
- El portal es Blazor Server: necesita WebSockets y afinidad de sesión.
- La API y el portal son dos Web Apps separadas.
- Las imágenes se construyen con los build args `SOURCE_COMMIT=<sha>` y `APP_VERSION=0.1.0`, que quedan como variables de entorno dentro del contenedor. Son la fuente de `commit` y `version` en `/`, `/health` y `/health/live`.
- El health check path de App Service debe apuntar a `/health/live` (ver §10).

## 3. Requisitos previos

- Repositorio GitHub con `main` y `development`.
- Cuenta de Docker Hub.
- Cuenta de Azure con una suscripción activa: Azure Free o Azure for Students.
- Cluster MongoDB Atlas M0.
- Permisos de administrador en GitHub, Docker Hub y Azure.
- Docker local opcional para probar imágenes.
- Azure CLI opcional para rollback; también se puede hacer desde el portal.

## 4. Configurar GitHub

### 4.1 Cambiar la rama default a `development`

En GitHub:

1. **Settings** → **General**.
2. En **Default branch**, cambiar de `main` a `development`.
3. Confirmar con **Update**.

Por qué:

- El flujo de trabajo define PRs contra `development`.
- `workflow_run` necesita que el workflow de CD exista en la rama default.
- `main` queda como rama de producción, actualizada solo por PR desde `development`.

Verificar que `.github/workflows/ci.yml` y `.github/workflows/cd.yml` existan en `development` y en `main`.

### 4.2 Crear rulesets

Los rulesets se configuran en **Settings** → **Rules** → **Rulesets** → **New branch ruleset**.

#### Ruleset `main-protection`

- **Name:** `main-protection`
- **Enforcement status:** Active
- **Target branches:** Include by pattern → `main`
- **Bypass list:** vacía, salvo que se necesite una excepción administrativa documentada.

Activar:

- **Require a pull request before merging**.
  - Required approvals: `1`.
  - Dismiss stale pull request approvals when new commits are pushed: opcional.
- **Require conversation resolution before merging**.
- **Require status checks to pass**.
  - Require branches to be up to date before merging: opcional; si se activa, cada actualización vuelve a ejecutar CI.
  - Checks requeridos:
    - `CI / Validar nombre de rama`
    - `CI / Build y tests`
- **Block force pushes**.
- **Restrict deletions**.

#### Ruleset `development-protection`

- **Name:** `development-protection`
- **Enforcement status:** Active
- **Target branches:** Include by pattern → `development`
- **Bypass list:** vacía.

Activar:

- **Require a pull request before merging**.
  - Required approvals: `1`.
- **Require conversation resolution before merging**.
- **Require status checks to pass**.
  - Checks requeridos:
    - `CI / Validar nombre de rama`
    - `CI / Build y tests`
- **Block force pushes**.
- **Restrict deletions**.

Los checks aparecen en la lista después del primer run de CI. Si no aparecen, ejecutar CI una vez y volver a editar el ruleset.

La CI omite la validación de nombre para `main` y `development`, por lo que un PR de promoción `development` → `main` no se bloquea por ese check.

### 4.3 Permisos de GitHub Actions

En **Settings** → **Actions** → **General**:

- **Actions permissions:** Allow all actions and reusable workflows.
- **Workflow permissions:** Read repository contents and packages permissions.
- **Allow GitHub Actions to create and approve pull requests:** desactivado.

No hace falta `contents: write` porque el workflow no commitea. Si se usa OIDC en lugar de publish profiles, agregar `id-token: write` al job de deploy.

### 4.4 Crear secretos y variables

En **Settings** → **Secrets and variables** → **Actions**.

#### Secrets

| Nombre | Valor | Cómo obtenerlo |
| --- | --- | --- |
| `DOCKERHUB_USERNAME` | Usuario de Docker Hub | Perfil de Docker Hub. Se usa también para armar el nombre de la imagen. |
| `DOCKERHUB_TOKEN` | Token de acceso personal | Docker Hub → Account settings → Personal access tokens → New access token. Permiso **Read & Write**. Copiarlo una sola vez. |
| `AZURE_WEBAPP_PUBLISH_PROFILE_API` | XML del publish profile de la API | App Service API → Overview → **Get publish profile**. Abrir el `.PublishSettings` y copiar todo el XML. |
| `AZURE_WEBAPP_PUBLISH_PROFILE_WEB` | XML del publish profile del portal | App Service Web → Overview → **Get publish profile**. |

Si Azure rechaza el publish profile porque la autenticación básica está deshabilitada:

1. App Service → **Configuration** → **General settings**.
2. **SCM Basic Auth Publishing Credentials** → **On**.
3. Guardar y volver a descargar el publish profile.

#### Variables

| Nombre | Ejemplo | Uso |
| --- | --- | --- |
| `AZURE_API_APP_NAME` | `facturar-api-utn-frt` | Nombre corto del recurso de la Web App de la API. No es el hostname. Se usa en `app-name` del deploy. |
| `AZURE_WEB_APP_NAME` | `facturar-web-utn-frt` | Nombre corto del recurso de la Web App del portal. No es el hostname. Se usa en `app-name` del deploy. |
| `AZURE_API_URL` | `https://facturar-api-utn-frt-gncabkgzghh2gbf2.brazilsouth-01.azurewebsites.net` | URL pública completa de la API. Se usa en `environment.url` y en el smoke test de `/health/live`. Sin barra final. |
| `AZURE_WEB_URL` | `https://facturar-web-utn-frt-gjbhdhb3bjgggxfg.brazilsouth-01.azurewebsites.net` | URL pública completa del portal. Se usa en `environment.url` y en el smoke test de `/health/live`. Sin barra final. |
| `AZURE_DEPLOY_ENABLED` | `true` | Habilita los jobs `deploy-api`, `deploy-web` y `rollback`. Con `false` esos jobs se omiten y el CD solo publica imágenes. El valor del repositorio es `true`: el deploy automático ya está validado (US-31). |

`AZURE_API_APP_NAME` y `AZURE_WEB_APP_NAME` son los nombres cortos del recurso, tal como aparecen en el portal de Azure. No usar el hostname completo en esas variables: `azure/webapps-deploy` espera el nombre de la Web App.

Los hostnames `https://<app>.azurewebsites.net` pueden no existir si Azure asigna un sufijo adicional al crear la Web App. Copiar la URL real desde **Overview → Default domain** y cargarla completa en `AZURE_API_URL` y `AZURE_WEB_URL`. Esas URLs son las que usan los smoke tests.

No guardar connection strings, tokens ni publish profiles en el repositorio.

#### Variables de build

No son variables del repositorio: las inyecta el pipeline como **build args** de Docker, y los Dockerfiles las convierten en variables de entorno del contenedor.

| Build arg | Valor | Uso |
| --- | --- | --- |
| `SOURCE_COMMIT` | SHA de 40 caracteres hexadecimales del commit | Se expone como variable de entorno `SOURCE_COMMIT` en el contenedor. La API y el portal la devuelven en el campo `commit` de `/`, `/health` y `/health/live`. El smoke test del CD compara ese valor con el SHA esperado. |
| `APP_VERSION` | `0.1.0` | Se expone como variable de entorno `APP_VERSION` en el contenedor y se devuelve como `version`. Si no está definida, la aplicación usa `0.1.0` y `commit = "unknown"`. |

La CI pasa `SOURCE_COMMIT=${{ github.sha }}` y `APP_VERSION=0.1.0` al construir las imágenes sin publicar. El CD pasa `SOURCE_COMMIT=<sha validado>` y `APP_VERSION=0.1.0` al publicar.

### 4.5 Runner de la CI

La CI usa `image: mongo:8.2` en el servicio de MongoDB. Con `mongo:8` (8.0.x) el contenedor no arranca en los runners de GitHub Actions: el guard TCMalloc/rseq de MongoDB aborta el arranque en kernels Linux ≥ 6.19. Los runners actuales corren kernel `7.0.0` y el guard parsea `uname -r` como "7.0.0", sin reconocer el kernel upstream 7.0.14 (`SERVER-121912`, `SERVER-136848`). El job falla en "Initialize containers" con `Failed to initialize container mongo:8`. Un workflow de diagnóstico ejecutado en el runner confirmó que `mongo:8` falla con `CONTROL 12257600` y que `mongo:8.2` (8.2.12) arranca correctamente.

Fijar el runner en `ubuntu-24.04` no resuelve el problema: esa imagen también corre kernel `7.0.0`.

El workflow de CD no necesita este ajuste porque no usa MongoDB.

## 5. Configurar Docker Hub

1. Crear una cuenta en Docker Hub.
2. Crear dos repositorios **públicos**:
   - `facturar-api`
   - `facturar-web`
3. Crear un token en **Account settings** → **Personal access tokens**:
   - Nombre: `github-actions-facturar`.
   - Permiso: **Read & Write**.
   - Copiar el token una sola vez y guardarlo como `DOCKERHUB_TOKEN`.
4. Guardar el usuario como `DOCKERHUB_USERNAME`.

Las imágenes públicas se pueden descargar desde App Service sin credenciales de registro. No incluir secretos en las imágenes.

Nombres y tags:

```text
<dockerhub-user>/facturar-api:latest
<dockerhub-user>/facturar-api:<sha>
<dockerhub-user>/facturar-web:latest
<dockerhub-user>/facturar-web:<sha>
```

El CD publica `latest` y el SHA. El deploy usa **siempre el SHA**, no `latest`.

Prueba local opcional:

```powershell
docker login
docker build -t <dockerhub-user>/facturar-api:local -f facturAR_backend/Dockerfile .
docker build -t <dockerhub-user>/facturar-web:local -f facturAR_frontend/Dockerfile .
```

Probar la API local:

```powershell
docker run --rm -p 8080:8080 `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e Mongo__ConnectionString="<connection-string>" `
  -e Mongo__DatabaseName="facturar" `
  -e Mongo__InitializeIndexes="true" `
  <dockerhub-user>/facturar-api:local
```

Abrir `http://localhost:8080/health` (readiness, consulta MongoDB) y `http://localhost:8080/health/live` (liveness, no consulta MongoDB).

## 6. Crear recursos en Azure

Se puede hacer desde el portal. Los nombres de las Web Apps deben ser únicos a nivel global.

### 6.1 Resource group

1. **Resource groups** → **Create**.
2. Suscripción: la disponible.
3. Nombre: `rg-facturar`.
4. Región: la más cercana, por ejemplo `Brazil South`.
5. **Review + create**.

### 6.2 App Service Plan

1. **Create a resource** → **Web App** o **App Service Plan**.
2. Plan:
   - Nombre: `asp-facturar`.
   - OS: **Linux**.
   - Región: la misma del resource group.
   - Pricing plan: **B1 (Basic)**.
3. Crear.

B1 es el plan elegido para la demo porque soporta **Always On**. F1 es gratis pero no tiene Always On: las apps se dormían y el primer request podía tardar demasiado. B1 tiene costo bajo y entra en el crédito de Azure for Students.

Una sola instancia B1 puede alojar las dos Web Apps, aunque comparten CPU y memoria. Para la demo alcanza; si se necesita más aislamiento, usar un plan por app.

### 6.3 Web App de la API

1. **Create** → **Web App**.
2. Basics:
   - Resource group: `rg-facturar`.
   - Name: `facturar-api-<sufijo>`.
   - Publish: **Docker Container**.
   - Operating System: **Linux**.
   - Region: la misma.
   - App Service Plan: `asp-facturar` (B1).
3. Docker:
   - Image Source: **Docker Hub**.
   - Image: `<dockerhub-user>/facturar-api:latest`.
   - No configurar credenciales si el repositorio es público.
4. **Review + create**.

Después de crear:

1. **Configuration** → **Application settings** → **New application setting**:

| Nombre | Valor |
| --- | --- |
| `WEBSITES_PORT` | `8080` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Mongo__ConnectionString` | Connection string de Atlas |
| `Mongo__DatabaseName` | `facturar` |
| `Mongo__InitializeIndexes` | `true` |
| `Email__Provider` | `Smtp` |
| `Email__Host` | Host SMTP del proveedor |
| `Email__Port` | `587` |
| `Email__User` | Usuario SMTP |
| `Email__Password` | Clave SMTP (secreto) |
| `Email__UseSsl` | `true` |
| `Email__From` | Remitente verificado por el proveedor |
| `Email__FromName` | `FacturAR` |
| `Email__TimeoutSegundos` | `15` |
| `Portal__BaseUrl` | URL pública del portal, sin barra final |
| `Admin__NotificationEmail` | Email para avisos de administración (opcional) |

Estas últimas claves son obligatorias desde US-29 y van **solo en la Web App de la API**, no en la del portal. En `Production` el provider por defecto es `Smtp` y el arranque falla si `Email__Host` está vacío: `Email:Host es obligatorio cuando Email:Provider es Smtp`. `Email__Provider=File` solo está permitido en `Development`. `Portal__BaseUrl` es la URL que se usa en los emails de invitación.

2. **Configuration** → **General settings**:
   - **Health check path:** `/health/live`.
   - Guardar y reiniciar.

   `/health` consulta MongoDB: si la base no responde devuelve `503`, y App Service interpretaría eso como contenedor unhealthy. Con `/health/live` el chequeo solo confirma que el proceso está vivo. Ver §10 para el detalle y el paso opcional por CLI.

3. **Overview** → **Get publish profile** y guardar el XML como `AZURE_WEBAPP_PUBLISH_PROFILE_API`.

No hace falta configurar CORS: el portal llama a la API desde el servidor.

### 6.4 Web App del portal

1. **Create** → **Web App**.
2. Basics:
   - Resource group: `rg-facturar`.
   - Name: `facturar-web-<sufijo>`.
   - Publish: **Docker Container**.
   - Operating System: **Linux**.
   - Region: la misma.
   - App Service Plan: `asp-facturar` (B1).
3. Docker:
   - Image Source: **Docker Hub**.
   - Image: `<dockerhub-user>/facturar-web:latest`.
4. **Review + create**.

Después de crear:

1. **Configuration** → **Application settings**:

| Nombre | Valor |
| --- | --- |
| `WEBSITES_PORT` | `8080` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Api__BaseUrl` | `https://facturar-api-utn-frt-gncabkgzghh2gbf2.brazilsouth-01.azurewebsites.net` |

2. **Configuration** → **General settings**:
   - **Web sockets:** On.
   - **Session affinity:** On.
   - **ARR affinity:** On si aparece como opción separada.
   - Guardar y reiniciar.

3. **Overview** → **Get publish profile** y guardar el XML como `AZURE_WEBAPP_PUBLISH_PROFILE_WEB`.

`Api__BaseUrl` debe ser la URL pública de la API, sin barra final. El valor `http://api:8080` de `docker-compose.prod.yml` no funciona en Azure.

### 6.5 Activar Always On en API y portal

Always On mantiene despierto el contenedor y evita el cold start. Requiere un plan Basic o superior; F1 no lo soporta. Activarlo en las dos Web Apps.

Desde el portal:

1. Web App de la API → **Configuration** → **General settings**.
2. **Always On** → **On**.
3. **Save** y reiniciar si Azure lo pide.
4. Repetir en la Web App del portal.

Con Azure CLI:

```powershell
az webapp config set --name facturar-api-utn-frt --resource-group rg-facturar --always-on true
az webapp config set --name facturar-web-utn-frt --resource-group rg-facturar --always-on true
az webapp restart --name facturar-api-utn-frt --resource-group rg-facturar
az webapp restart --name facturar-web-utn-frt --resource-group rg-facturar
```

Verificar que el plan sea B1; en F1 el comando falla porque Always On no está disponible.

### 6.6 MongoDB Atlas

1. Crear un cluster **M0 Free** en Atlas.
2. Crear un usuario de base de datos.
3. En **Network Access**, permitir las salidas de Azure:
   - Para una demo simple: `0.0.0.0/0`.
   - Para algo más cerrado: obtener las outbound IPs de cada Web App y agregarlas.
4. Copiar el connection string `mongodb+srv://...`.
5. Reemplazar `<password>` y usar `facturar` como base.

No commitear la connection string. Cargarla como `Mongo__ConnectionString` en App Settings.

## 7. Verificar el deploy

1. Abrir `https://facturar-api-utn-frt-gncabkgzghh2gbf2.brazilsouth-01.azurewebsites.net/health`; debe responder `200` con `status: "ok"` y `database: "ok"` si MongoDB responde.
2. Abrir `https://facturar-api-utn-frt-gncabkgzghh2gbf2.brazilsouth-01.azurewebsites.net/health/live`; debe responder `200` con `status`, `version` y `commit`.
3. Abrir `https://facturar-web-utn-frt-gjbhdhb3bjgggxfg.brazilsouth-01.azurewebsites.net/`; debe cargar el portal.
4. Abrir `/health` y `/health/live` del portal; ambos deben responder `200`.
5. Comparar el campo `commit` de `/health/live` con el SHA del deploy. Debe coincidir con el tag publicado en Docker Hub.
6. Abrir `/verificar` del portal con un CAE de 14 dígitos, por ejemplo `https://facturar-web-utn-frt-gjbhdhb3bjgggxfg.brazilsouth-01.azurewebsites.net/verificar?cae=12345678901234`; debe responder `200` y mostrar la validación del comprobante. Si responde 500, revisar `Api__BaseUrl` (ver Troubleshooting).
7. Probar login o una ruta pública según el estado de las historias.
8. Revisar **Log stream** de cada Web App si algo falla.

El `commit` que devuelve `/health/live` es el `SOURCE_COMMIT` con el que se construyó la imagen desplegada. Si no coincide con el SHA del deploy, la Web App está sirviendo otra imagen.

Esas son las URLs reales de la demo. No asumir que `https://<app>.azurewebsites.net` existe: usar siempre el **Default domain** que muestra el portal de Azure.

Si la API no arranca:

- Verificar `WEBSITES_PORT=8080`.
- Verificar que la imagen exista en Docker Hub y sea pública.
- Revisar `Mongo__ConnectionString` y la red de Atlas.
- Revisar los logs del contenedor.

Si el portal carga pero la API falla:

- Revisar `Api__BaseUrl`.
- Abrir `/health` de la API desde el navegador.
- Verificar que la API esté en la misma región o que no haya restricciones de red.

Si Blazor pierde la conexión:

- Verificar **Web sockets: On**.
- Verificar **Session affinity: On**.
- Verificar que **Always On** esté activo. B1 lo soporta; F1 no, y por eso las apps se dormían.

## 8. Deploy en el workflow de CD

El `cd.yml` publica las imágenes y despliega en Azure. Tiene cinco jobs: `resolve-target`, `publish-images`, `deploy-api`, `deploy-web` y `rollback`.

Disparadores:

- `workflow_run` sobre el workflow `CI`, cuando termina en `main` y la conclusión es `success` y el evento que lo disparó fue un `push`.
- `workflow_dispatch` manual sobre `main`, con los inputs `modo` (`deploy` o `rollback`) e `image_tag`.

El workflow declara `permissions: contents: read` y `actions: read` a nivel global, y `concurrency` con el grupo `cd-main` y `cancel-in-progress: true`: un run en curso cancela al anterior.

### 8.1 Estructura real del workflow

Extracto real de `.github/workflows/cd.yml`:

```yaml
name: CD

on:
  workflow_run:
    workflows: ["CI"]
    types: [completed]
    branches: [main]
  workflow_dispatch:
    inputs:
      modo:
        description: "deploy = publica y despliega el commit validado; rollback = despliega un tag ya publicado en Docker Hub"
        required: false
        default: "deploy"
        type: choice
        options:
          - deploy
          - rollback
      image_tag:
        description: "Tag SHA a desplegar. Solo se usa con modo=rollback."
        required: false
        default: ""
        type: string

permissions:
  contents: read
  actions: read

concurrency:
  group: cd-main
  cancel-in-progress: true

jobs:
  resolve-target:
    name: Resolver imagen objetivo
    if: |
      (github.event_name == 'workflow_dispatch' && github.ref == 'refs/heads/main') ||
      (github.event_name == 'workflow_run' && github.event.workflow_run.conclusion == 'success' && github.event.workflow_run.event == 'push')
    runs-on: ubuntu-latest
    timeout-minutes: 5
    outputs:
      sha: ${{ steps.resolve.outputs.sha }}
      mode: ${{ steps.resolve.outputs.mode }}
    steps:
      - name: Determinar el SHA objetivo
        id: resolve
        env:
          MODO: ${{ inputs.modo }}
          IMAGE_TAG: ${{ inputs.image_tag }}
          RUN_SHA: ${{ github.event.workflow_run.head_sha || github.sha }}
        run: |
          if [ "$MODO" = "rollback" ]; then
            if [ -z "$IMAGE_TAG" ]; then
              echo "::error::El modo rollback requiere el input image_tag con el SHA a desplegar."
              exit 1
            fi
            if ! echo "$IMAGE_TAG" | grep -qE '^[0-9a-f]{40}$'; then
              echo "::error::El input image_tag debe ser un SHA de 40 caracteres hexadecimales."
              exit 1
            fi
            echo "mode=rollback" >> "$GITHUB_OUTPUT"
            echo "sha=$IMAGE_TAG" >> "$GITHUB_OUTPUT"
            echo "Se despliega el tag ya publicado $IMAGE_TAG."
          else
            echo "mode=deploy" >> "$GITHUB_OUTPUT"
            echo "sha=$RUN_SHA" >> "$GITHUB_OUTPUT"
            echo "Se despliega el commit validado $RUN_SHA."
          fi

  publish-images:
    name: Publicar imágenes en Docker Hub
    needs: resolve-target
    if: needs.resolve-target.outputs.mode == 'deploy'
    runs-on: ubuntu-latest
    timeout-minutes: 20
    permissions:
      contents: read
      actions: write
    outputs:
      sha: ${{ steps.sha.outputs.value }}
    steps:
      - name: Resolver SHA validado
        id: sha
        run: echo "value=${{ github.event.workflow_run.head_sha || github.sha }}" >> "$GITHUB_OUTPUT"

      - name: Checkout del commit validado
        uses: actions/checkout@v4
        with:
          ref: ${{ github.event.workflow_run.head_sha || github.sha }}

      - name: Verificar secretos de Docker Hub
        env:
          DOCKERHUB_USERNAME: ${{ secrets.DOCKERHUB_USERNAME }}
          DOCKERHUB_TOKEN: ${{ secrets.DOCKERHUB_TOKEN }}
        run: |
          if [ -z "$DOCKERHUB_USERNAME" ] || [ -z "$DOCKERHUB_TOKEN" ]; then
            echo "::error::Configurá DOCKERHUB_USERNAME y DOCKERHUB_TOKEN en los secretos del repositorio."
            exit 1
          fi

      - name: Configurar Buildx
        uses: docker/setup-buildx-action@v3

      - name: Login en Docker Hub
        uses: docker/login-action@v3
        with:
          username: ${{ secrets.DOCKERHUB_USERNAME }}
          password: ${{ secrets.DOCKERHUB_TOKEN }}

      - name: Build y push API
        uses: docker/build-push-action@v6
        with:
          context: .
          file: facturAR_backend/Dockerfile
          platforms: linux/amd64
          push: true
          build-args: |
            SOURCE_COMMIT=${{ steps.sha.outputs.value }}
            APP_VERSION=0.1.0
          tags: |
            ${{ secrets.DOCKERHUB_USERNAME }}/facturar-api:latest
            ${{ secrets.DOCKERHUB_USERNAME }}/facturar-api:${{ steps.sha.outputs.value }}
          cache-from: type=gha,scope=api
          cache-to: type=gha,mode=max,scope=api

      - name: Build y push Web
        uses: docker/build-push-action@v6
        with:
          context: .
          file: facturAR_frontend/Dockerfile
          platforms: linux/amd64
          push: true
          build-args: |
            SOURCE_COMMIT=${{ steps.sha.outputs.value }}
            APP_VERSION=0.1.0
          tags: |
            ${{ secrets.DOCKERHUB_USERNAME }}/facturar-web:latest
            ${{ secrets.DOCKERHUB_USERNAME }}/facturar-web:${{ steps.sha.outputs.value }}
          cache-from: type=gha,scope=web
          cache-to: type=gha,mode=max,scope=web

  deploy-api:
    name: Desplegar API en Azure
    needs: [resolve-target, publish-images]
    if: >
      always() &&
      needs.resolve-target.result == 'success' &&
      vars.AZURE_DEPLOY_ENABLED == 'true' &&
      (needs.resolve-target.outputs.mode == 'rollback' || needs.publish-images.result == 'success')
    runs-on: ubuntu-latest
    timeout-minutes: 15
    environment:
      name: production
      url: ${{ vars.AZURE_API_URL }}

    steps:
      - name: Verificar secretos y variables de Azure
        env:
          PUBLISH_PROFILE: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE_API }}
          APP_NAME: ${{ vars.AZURE_API_APP_NAME }}
          API_URL: ${{ vars.AZURE_API_URL }}
        run: |
          if [ -z "$PUBLISH_PROFILE" ]; then
            echo "::error::Falta el secreto AZURE_WEBAPP_PUBLISH_PROFILE_API. Descargá el publish profile de la Web App de la API y guardalo como secreto."
            exit 1
          fi
          if [ -z "$APP_NAME" ]; then
            echo "::error::Falta la variable AZURE_API_APP_NAME con el nombre corto de la Web App de la API."
            exit 1
          fi
          if [ -z "$API_URL" ]; then
            echo "::error::Falta la variable AZURE_API_URL con la URL pública completa de la API."
            exit 1
          fi

      - name: Desplegar API
        uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ vars.AZURE_API_APP_NAME }}
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE_API }}
          images: ${{ secrets.DOCKERHUB_USERNAME }}/facturar-api:${{ needs.resolve-target.outputs.sha }}

      - name: Smoke test de la API
        env:
          API_URL: ${{ vars.AZURE_API_URL }}
          EXPECTED_SHA: ${{ needs.resolve-target.outputs.sha }}
        shell: bash
        run: |
          for intento in $(seq 1 18); do
            respuesta=$(curl -fsS --max-time 10 "$API_URL/health/live" 2>/dev/null || true)
            commit=$(printf '%s' "$respuesta" | sed -n 's/.*"commit":"\([^"]*\)".*/\1/p')
            if [ "$commit" = "$EXPECTED_SHA" ]; then
              echo "API desplegada con el commit $EXPECTED_SHA en el intento $intento."
              exit 0
            fi
            echo "Intento $intento/18: commit recibido '${commit:-sin commit}', esperado '$EXPECTED_SHA'; reintentando en 10s."
            sleep 10
          done
          echo "::error::La API no quedó sirviendo el commit $EXPECTED_SHA después de 18 intentos. Revisá el Log stream de la Web App."
          exit 1

  deploy-web:
    name: Desplegar portal en Azure
    needs: [resolve-target, publish-images, deploy-api]
    if: >
      always() &&
      needs.resolve-target.result == 'success' &&
      vars.AZURE_DEPLOY_ENABLED == 'true' &&
      (needs.resolve-target.outputs.mode == 'rollback' || needs.publish-images.result == 'success') &&
      needs.deploy-api.result == 'success'
    runs-on: ubuntu-latest
    timeout-minutes: 15
    environment:
      name: production
      url: ${{ vars.AZURE_WEB_URL }}

    steps:
      - name: Verificar secretos y variables de Azure
        env:
          PUBLISH_PROFILE: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE_WEB }}
          APP_NAME: ${{ vars.AZURE_WEB_APP_NAME }}
          WEB_URL: ${{ vars.AZURE_WEB_URL }}
        run: |
          if [ -z "$PUBLISH_PROFILE" ]; then
            echo "::error::Falta el secreto AZURE_WEBAPP_PUBLISH_PROFILE_WEB. Descargá el publish profile de la Web App del portal y guardalo como secreto."
            exit 1
          fi
          if [ -z "$APP_NAME" ]; then
            echo "::error::Falta la variable AZURE_WEB_APP_NAME con el nombre corto de la Web App del portal."
            exit 1
          fi
          if [ -z "$WEB_URL" ]; then
            echo "::error::Falta la variable AZURE_WEB_URL con la URL pública completa del portal."
            exit 1
          fi

      - name: Desplegar portal
        uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ vars.AZURE_WEB_APP_NAME }}
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE_WEB }}
          images: ${{ secrets.DOCKERHUB_USERNAME }}/facturar-web:${{ needs.resolve-target.outputs.sha }}

      - name: Smoke test del portal
        env:
          WEB_URL: ${{ vars.AZURE_WEB_URL }}
          EXPECTED_SHA: ${{ needs.resolve-target.outputs.sha }}
        shell: bash
        run: |
          for intento in $(seq 1 18); do
            respuesta=$(curl -fsS --max-time 10 "$WEB_URL/health/live" 2>/dev/null || true)
            commit=$(printf '%s' "$respuesta" | sed -n 's/.*"commit":"\([^"]*\)".*/\1/p')
            root_code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 10 "$WEB_URL/" 2>/dev/null || true)
            verificar_code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 10 "$WEB_URL/verificar" 2>/dev/null || true)
            login_code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 10 "$WEB_URL/login" 2>/dev/null || true)
            admin_code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 10 "$WEB_URL/admin/login" 2>/dev/null || true)
            if [ "$commit" = "$EXPECTED_SHA" ] && [ "$root_code" = "200" ] && [ "$verificar_code" = "200" ] && [ "$login_code" = "200" ] && [ "$admin_code" = "200" ]; then
              echo "Portal desplegado con el commit $EXPECTED_SHA; / /verificar /login /admin/login responden 200."
              exit 0
            fi
            echo "Intento $intento/18: commit '${commit:-sin commit}', /=$root_code, /verificar=$verificar_code, /login=$login_code, /admin/login=$admin_code; esperado '$EXPECTED_SHA' y 200; reintentando en 10s."
            sleep 10
          done
          echo "::error::El portal no quedó sirviendo el commit $EXPECTED_SHA con las rutas públicas en 200 después de 18 intentos. Revisá el Log stream de la Web App."
          exit 1

  rollback:
    name: Revertir al SHA anterior
    needs: [resolve-target, deploy-api, deploy-web]
    if: >
      always() &&
      needs.resolve-target.result == 'success' &&
      vars.AZURE_DEPLOY_ENABLED == 'true' &&
      (needs.deploy-api.result == 'failure' || needs.deploy-web.result == 'failure')
    runs-on: ubuntu-latest
    timeout-minutes: 15
    environment:
      name: production
      url: ${{ vars.AZURE_API_URL }}

    steps:
      - name: Verificar secretos y variables de Azure
        env:
          PUBLISH_PROFILE_API: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE_API }}
          PUBLISH_PROFILE_WEB: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE_WEB }}
          API_APP_NAME: ${{ vars.AZURE_API_APP_NAME }}
          WEB_APP_NAME: ${{ vars.AZURE_WEB_APP_NAME }}
          DOCKERHUB_USERNAME: ${{ secrets.DOCKERHUB_USERNAME }}
        run: |
          if [ -z "$PUBLISH_PROFILE_API" ] || [ -z "$PUBLISH_PROFILE_WEB" ]; then
            echo "::error::Faltan los publish profiles de Azure; no se puede revertir."
            exit 1
          fi
          if [ -z "$API_APP_NAME" ] || [ -z "$WEB_APP_NAME" ]; then
            echo "::error::Faltan AZURE_API_APP_NAME o AZURE_WEB_APP_NAME; no se puede revertir."
            exit 1
          fi
          if [ -z "$DOCKERHUB_USERNAME" ]; then
            echo "::error::Falta DOCKERHUB_USERNAME; no se puede resolver el SHA anterior."
            exit 1
          fi

      - name: Resolver el SHA anterior en Docker Hub
        id: previous
        env:
          DOCKERHUB_USERNAME: ${{ secrets.DOCKERHUB_USERNAME }}
          CURRENT_SHA: ${{ needs.resolve-target.outputs.sha }}
        shell: bash
        run: |
          tags_api=$(curl -fsS --max-time 20 "https://hub.docker.com/v2/repositories/${DOCKERHUB_USERNAME}/facturar-api/tags?page_size=100&ordering=last_updated" \
            | grep -oE '"name":"[0-9a-f]{40}"' \
            | grep -oE '[0-9a-f]{40}' \
            | grep -vxF "$CURRENT_SHA" \
            | head -n 1)
          if [ -z "$tags_api" ]; then
            echo "::error::No se encontró un SHA anterior publicado en Docker Hub. Revisá el historial de tags de ${DOCKERHUB_USERNAME}/facturar-api."
            exit 1
          fi
          tags_web=$(curl -fsS --max-time 20 "https://hub.docker.com/v2/repositories/${DOCKERHUB_USERNAME}/facturar-web/tags?page_size=100&ordering=last_updated" \
            | grep -oE '"name":"[0-9a-f]{40}"' \
            | grep -oE '[0-9a-f]{40}' || true)
          if ! printf '%s\n' "$tags_web" | grep -qxF "$tags_api"; then
            echo "::error::El SHA $tags_api no está publicado en ${DOCKERHUB_USERNAME}/facturar-web. No se puede revertir de forma consistente."
            exit 1
          fi
          echo "sha=$tags_api" >> "$GITHUB_OUTPUT"
          echo "Se revierte al commit $tags_api."

      - name: Revertir la API
        uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ vars.AZURE_API_APP_NAME }}
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE_API }}
          images: ${{ secrets.DOCKERHUB_USERNAME }}/facturar-api:${{ steps.previous.outputs.sha }}

      - name: Revertir el portal
        uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ vars.AZURE_WEB_APP_NAME }}
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE_WEB }}
          images: ${{ secrets.DOCKERHUB_USERNAME }}/facturar-web:${{ steps.previous.outputs.sha }}

      - name: Verificar la reversión
        env:
          API_URL: ${{ vars.AZURE_API_URL }}
          WEB_URL: ${{ vars.AZURE_WEB_URL }}
          EXPECTED_SHA: ${{ steps.previous.outputs.sha }}
        shell: bash
        run: |
          for intento in $(seq 1 18); do
            api_commit=$(curl -fsS --max-time 10 "$API_URL/health/live" 2>/dev/null | sed -n 's/.*"commit":"\([^"]*\)".*/\1/p' || true)
            web_commit=$(curl -fsS --max-time 10 "$WEB_URL/health/live" 2>/dev/null | sed -n 's/.*"commit":"\([^"]*\)".*/\1/p' || true)
            if [ "$api_commit" = "$EXPECTED_SHA" ] && [ "$web_commit" = "$EXPECTED_SHA" ]; then
              echo "API y portal revertidos al commit $EXPECTED_SHA en el intento $intento."
              exit 0
            fi
            echo "Intento $intento/18: API '${api_commit:-sin commit}', portal '${web_commit:-sin commit}', esperado '$EXPECTED_SHA'; reintentando en 10s."
            sleep 10
          done
          echo "::error::No se pudo verificar la reversión al commit $EXPECTED_SHA. Revisá el Log stream de ambas Web Apps."
          exit 1
```

### 8.2 Qué hace cada job

| Job | `needs` | Condición `if` | `environment` | `timeout-minutes` | Resultado |
| --- | --- | --- | --- | --- | --- |
| `resolve-target` | — | `workflow_dispatch` sobre `main`, o `workflow_run` de `CI` con conclusión `success` y evento `push` | — | 5 | Expone `sha` y `mode` (`deploy` o `rollback`). |
| `publish-images` | `resolve-target` | `needs.resolve-target.outputs.mode == 'deploy'` | — | 20 | Publica `facturar-api` y `facturar-web` con tags `latest` y SHA. `permissions: contents: read` + `actions: write`. |
| `deploy-api` | `resolve-target`, `publish-images` | `always()` + `resolve-target` exitoso + `AZURE_DEPLOY_ENABLED == 'true'` + (`rollback` o `publish-images` exitoso) | `production` (`AZURE_API_URL`) | 15 | Despliega la API y verifica el commit con el smoke test. |
| `deploy-web` | `resolve-target`, `publish-images`, `deploy-api` | las condiciones de `deploy-api` + `deploy-api` exitoso | `production` (`AZURE_WEB_URL`) | 15 | Despliega el portal y verifica el commit con el smoke test. |
| `rollback` | `resolve-target`, `deploy-api`, `deploy-web` | `always()` + `resolve-target` exitoso + `AZURE_DEPLOY_ENABLED == 'true'` + (`deploy-api` o `deploy-web` en `failure`) | `production` (`AZURE_API_URL`) | 15 | Vuelve al SHA anterior publicado. |

Notas:

- `resolve-target` valida el input `image_tag`: exige un SHA de 40 caracteres hexadecimales (`^[0-9a-f]{40}$`). Con un valor inválido el run falla antes de tocar Azure.
- En modo `rollback`, `publish-images` se omite: no se recompila nada, se despliega un tag ya publicado.
- `needs` garantiza que el portal no se despliegue si la API falla.
- Si `AZURE_DEPLOY_ENABLED` es `false` o no existe, los jobs de deploy y de rollback se omiten y solo se publican imágenes.
- `environment: production` permite ver la URL en GitHub. No agregar required reviewers si se quiere deploy automático.
- `azure/webapps-deploy@v3` no necesita `actions/checkout`.
- Los smoke tests reintentan 18 veces cada 10 segundos (3 minutos). App Service puede tardar en levantar el contenedor.
- El deploy automático real quedó verificado: con `AZURE_DEPLOY_ENABLED=true` y la CI exitosa en `main`, el CD publica las imágenes y despliega API y portal con el SHA validado.

### 8.3 Verificación de versión

Las imágenes se construyen con dos build args que quedan como variables de entorno del contenedor:

| Build arg | Valor | Dónde se usa |
| --- | --- | --- |
| `SOURCE_COMMIT` | SHA de 40 hex del commit desplegado | `ENV SOURCE_COMMIT` en los dos Dockerfiles. La aplicación lo lee con la clave de configuración `SOURCE_COMMIT`. |
| `APP_VERSION` | `0.1.0` | `ENV APP_VERSION` en los dos Dockerfiles. La aplicación lo lee con la clave de configuración `APP_VERSION`. |

La API y el portal devuelven esos valores como `version` y `commit` en `/`, `/health` y `/health/live`.

El smoke test del CD **no se queda con un 200**: consulta `$API_URL/health/live` y `$WEB_URL/health/live`, extrae el campo `commit` de la respuesta y lo compara con el SHA esperado (`needs.resolve-target.outputs.sha`). Además, el portal debe responder `200` en `/`, `/verificar`, `/login` y `/admin/login`, para que un error de render no pase solo porque `/health/live` sigue vivo. Si algo no coincide, reintenta hasta 18 veces y después falla el job. Ese es el mecanismo que garantiza que la Web App esté sirviendo exactamente la imagen recién publicada y que las rutas públicas funcionen.

Esto reemplaza el smoke test anterior, que solo verificaba que la URL respondiera `200` y no comprobaba qué versión estaba servida.

## 9. Rollback

Cada deploy conserva un tag SHA en Docker Hub. El rollback usa un tag SHA ya publicado: no hay que recompilar la imagen.

Hay dos caminos, y conviene no confundirlos:

| Camino | Disparador | Estado |
| --- | --- | --- |
| Manual (`workflow_dispatch`) | Ejecución manual del workflow CD con `modo=rollback` | **Probado.** Validado en US-31. |
| Automático (job `rollback`) | Falla `deploy-api` o `deploy-web` | **Configurado, pero no probado.** No hay evidencia de una ejecución real. |

### 9.1 Rollback automático (configurado, no probado)

El job `rollback` del `cd.yml` se dispara cuando `deploy-api` o `deploy-web` terminan en `failure` y `AZURE_DEPLOY_ENABLED` vale `true`. Qué hace:

1. Verifica que existan los dos publish profiles, `AZURE_API_APP_NAME`, `AZURE_WEB_APP_NAME` y `DOCKERHUB_USERNAME`.
2. Busca el SHA anterior en Docker Hub: consulta la API de tags de `facturar-api` con `ordering=last_updated`, se queda con los tags de 40 hex, excluye el SHA que falló y toma el primero.
3. Verifica que ese mismo SHA esté publicado también en `facturar-web`. Si no está, falla: no se revierte de forma inconsistente.
4. Redespliega API y portal con `azure/webapps-deploy@v3` usando ese tag SHA.
5. Verifica que `/health/live` de API y portal devuelvan el `commit` esperado, con los mismos 18 reintentos de 10 segundos.

> **El rollback automático está configurado pero no fue probado.** No hay registro de una ejecución real del job `rollback`: la evidencia de rollback del proyecto es la del procedimiento manual de US-31 (§9), que se describe abajo.

### 9.2 Rollback manual con `workflow_dispatch`

1. Elegir el SHA sano en el historial de GitHub Actions o en los tags de Docker Hub.
2. En **Actions → CD → Run workflow**, sobre la rama `main`:
   - `modo`: `rollback`.
   - `image_tag`: el SHA de 40 caracteres hexadecimales a desplegar.
3. El job `resolve-target` valida que `image_tag` sea un SHA de 40 hex y falla con un error explícito si no lo es.
4. `publish-images` se omite: no se construye ni se publica nada.
5. `deploy-api` y `deploy-web` despliegan ese tag y verifican el `commit` con el smoke test.

El rollback usa un tag SHA ya publicado en Docker Hub: con el pipeline actual el tag a usar es el SHA anterior publicado, por ejemplo `8a3541507a5eabb566571ad843f19e9c7baa0f3f`.

> Procedimiento documentado y validado con los tags SHA publicados (`8a3541507a5eabb566571ad843f19e9c7baa0f3f` y `077a247695125defb70ae325f5bad052023fb398`).

Opción con Azure CLI:

```powershell
az login
az webapp config container set `
  --name <api-app> `
  --resource-group rg-facturar `
  --container-image-name <dockerhub-user>/facturar-api:<sha> `
  --container-registry-url https://index.docker.io

az webapp restart --name <api-app> --resource-group rg-facturar
```

Repetir para `facturar-web`. Verificar `/health/live` de API y portal y comparar el campo `commit` con el SHA esperado.

## 10. Endpoints de salud

La API y el portal exponen los mismos dos endpoints de salud.

| Endpoint | Semántica | Respuesta |
| --- | --- | --- |
| `GET /health` | Readiness. Consulta MongoDB. | `200` con `{ status, database, timestamp, version, commit }`. Si MongoDB no responde: `503` con `{ status: "degraded", database: "unavailable", ... }`. |
| `GET /health/live` | Liveness. **No** consulta MongoDB. | `200` con `{ status, version, commit }`. |

En el portal, `/health` no consulta la API ni MongoDB: devuelve el estado del propio proceso web con `database: "ok"`.

`version` y `commit` provienen de las variables de entorno `APP_VERSION` y `SOURCE_COMMIT`, inyectadas por el pipeline de build (ver §4.4). Si no están definidas, la aplicación responde `version: "0.1.0"` y `commit: "unknown"`.

La API también expone `GET /`, que informa el nombre del servicio, `version`, `commit` y la ruta del documento OpenAPI.

### 10.1 Configuración recomendada en Azure

El **health check path** de App Service debe apuntar a `/health/live`:

- `/health` depende de MongoDB. Si la base no responde devuelve `503`, App Service lo interpreta como contenedor unhealthy y reinicia el contenedor: una caída transitoria de la base produce un loop de reinicios.
- `/health/live` solo confirma que el proceso está vivo y que la versión desplegada es la esperada.

Cambio opcional y manual, del lado de Azure (no del repositorio). Desde el portal: **Configuration → General settings → Health check path → `/health/live`**, guardar y reiniciar.

Con Azure CLI:

```powershell
az webapp config set --name facturar-api-utn-frt --resource-group rg-facturar --health-check-path /health/live
az webapp config set --name facturar-web-utn-frt --resource-group rg-facturar --health-check-path /health/live
az webapp restart --name facturar-api-utn-frt --resource-group rg-facturar
az webapp restart --name facturar-web-utn-frt --resource-group rg-facturar
```

`/health` sigue siendo el chequeo de readiness para uso humano, para `docker compose` y para diagnóstico.

## 11. Cambios recientes en CI

La CI (`.github/workflows/ci.yml`) tiene tres jobs.

| Job | Qué hace |
| --- | --- |
| `Validar nombre de rama` | Solo en pull requests. Acepta `feature/us-XX-slug`, `fix/us-XX-slug` o una rama auxiliar `chore/`, `docs/`, `refactor/`, `test/`, `perf/`, `ci/` o `build/`. Omite `main` y `development` para permitir la promoción. |
| `Build y tests` | Restaura, compila en `Release`, **verifica formato y analizadores**, levanta MongoDB 8.2 como servicio, ejecuta la suite completa y sube `TestResults` como artefacto. |
| `Construir imágenes Docker` | Construye la imagen de la API y la del portal **sin publicarlas**, para validar ambos Dockerfiles en cada push y PR. |

El gate de lint es el paso **Verificar formato y analizadores**:

```bash
dotnet format FacturAR.slnx --verify-no-changes --verbosity minimal
```

Falla el job si hay diferencias de formato o si los analizadores reportan problemas. Es la puerta de entrada antes de los tests.

El job **Construir imágenes Docker** usa `docker/setup-buildx-action@v3` y `docker/build-push-action@v6` con `push: false`, `platforms: linux/amd64` y los mismos build args que usa el CD:

```yaml
build-args: |
  SOURCE_COMMIT=${{ github.sha }}
  APP_VERSION=0.1.0
```

Usa la caché de GitHub Actions con un scope por imagen (`api` y `web`) para no reconstruir desde cero.

El job también aparece como check disponible en GitHub y se puede agregar a los rulesets de `main` y `development` como check requerido, junto a `CI / Validar nombre de rama` y `CI / Build y tests`.

## 12. Costos y límites

- **GitHub Actions:** gratis para repositorios públicos. En privados, 2.000 minutos/mes en el plan Free.
- **Docker Hub:** repositorios públicos gratis. Hay límites de pulls anónimos; el deploy no hace pulls frecuentes.
- **Azure App Service B1:** bajo costo, con Always On. Es el plan elegido para la demo.
- **Azure App Service F1:** gratis, pero sin Always On; no se usa porque las apps se dormían.
- **Azure for Students:** incluye crédito que cubre B1 durante la demo.
- **MongoDB Atlas M0:** gratis, 512 MB.
- No crear recursos pagos innecesarios. Eliminar el resource group al terminar la demo si no se usa más.

## 13. Troubleshooting

| Síntoma | Causa probable | Solución |
| --- | --- | --- |
| App Service muestra 503 | El contenedor no escucha en el puerto esperado | Configurar `WEBSITES_PORT=8080`. |
| App Service muestra 503 y en el log aparece `linuxFxVersion=sitecontainers` con `targetPort=80` | Azure sondea el puerto 80 del sitecontainer e ignora `WEBSITES_PORT` | Pasar a Docker clásico con `az webapp config container set --container-image-name ... --container-registry-url https://index.docker.io` o cambiar el `targetPort` del sitecontainer a `8080`; reiniciar. |
| `ImagePullBackOff` o error de imagen | Imagen inexistente o privada | Verificar el tag en Docker Hub y usar repositorio público. |
| API responde 503 en `/health` | Mongo no conecta | Revisar connection string, usuario y Network Access de Atlas. `/health/live` sigue respondiendo `200` mientras el proceso esté vivo. |
| Portal carga pero no llama a la API | `Api__BaseUrl` incorrecto | Usar la URL real de `AZURE_API_URL`, sin barra final. |
| Portal `/verificar` devuelve 500 | `Api__BaseUrl` mal formado (sin `https://`) genera `System.UriFormatException` al construir el `HttpClient` | Usar la URL completa de `AZURE_API_URL` con `https://` y sin barra final. |
| Blazor pierde conexión | WebSockets o afinidad deshabilitados | Activar WebSockets y Session affinity. |
| Deploy falla con 401/403 | Publish profile vencido o app name incorrecto | Descargar de nuevo el publish profile y revisar `AZURE_*_APP_NAME`. |
| Deploy falla por basic auth | SCM Basic Auth deshabilitado | Activarlo en Configuration → General settings. |
| Docker Hub deniega el push | Token sin permisos | Crear token con Read & Write y actualizar el secreto. |
| CD no se dispara | `cd.yml` no está en la rama default o CI no se llama `CI` | Verificar default branch y el nombre del workflow de CI. |
| Los jobs de deploy y de rollback se omiten | `AZURE_DEPLOY_ENABLED` no vale `true` | Crear o corregir la variable del repositorio. |
| CI falla en `Initialize containers` con `Failed to initialize container mongo:8` | El guard TCMalloc/rseq de MongoDB 8.0.x aborta el arranque en kernels Linux ≥ 6.19 (`SERVER-121912`) | Usar `image: mongo:8.2` en el servicio `mongo` de `.github/workflows/ci.yml`. |
| CI falla en un PR de promoción | Validación de rama sobre `development` | La CI ya omite `main` y `development`; verificar que el cambio esté en la rama. |
| Primer request lento | Always On desactivado | Activar Always On en la Web App. F1 no lo soporta; usar B1. |

Si el log de la Web App muestra `linuxFxVersion=sitecontainers` y `targetPort=80`, `WEBSITES_PORT` se ignora y Azure sondea el puerto 80. Opciones:

```powershell
az webapp config container set `
  --name <api-app> `
  --resource-group rg-facturar `
  --container-image-name <dockerhub-user>/facturar-api:<sha> `
  --container-registry-url https://index.docker.io

az webapp restart --name <api-app> --resource-group rg-facturar
```

O actualizar el `targetPort` del sitecontainer a `8080` y reiniciar. Verificar después `/health/live`.

## 14. Diagnóstico rápido

Errores frecuentes del pipeline y qué revisar.

| Síntoma | Causa | Qué revisar |
| --- | --- | --- |
| El smoke test falla con `La API no quedó sirviendo el commit <sha>` o `El portal no quedó sirviendo el commit <sha>` | La Web App sirve una imagen distinta de la publicada en ese run | Comparar el `commit` de `/health/live` con el SHA esperado. Verificar que el tag SHA exista en Docker Hub, que la Web App tenga la imagen correcta configurada y revisar el **Log stream**. Un despliegue sobre el mismo tag SHA puede servir la imagen cacheada: reiniciar la Web App. |
| La API no arranca y el **Log stream** muestra `Email:Host es obligatorio cuando Email:Provider es Smtp` | Faltan las App Settings de email en la Web App de la API, o se cargaron en la del portal | Cargar `Email__*`, `Portal__BaseUrl` y `Admin__NotificationEmail` en `facturar-api-...`, no en `facturar-web-...` (ver §6.3) y reiniciar. |
| El job `deploy-api` o `deploy-web` aparece como *skipped* | `vars.AZURE_DEPLOY_ENABLED` no vale `true` | Crear o corregir la variable del repositorio en **Settings → Secrets and variables → Actions → Variables**. Con `false` el CD solo publica imágenes. |
| `Falta el secreto AZURE_WEBAPP_PUBLISH_PROFILE_API` o `..._WEB` | El publish profile no está cargado o está vencido | Descargarlo de nuevo desde **Overview → Get publish profile** y actualizar el secreto. Verificar que SCM Basic Auth Publishing Credentials esté en `On`. |
| `Configurá DOCKERHUB_USERNAME y DOCKERHUB_TOKEN en los secretos del repositorio` | Faltan credenciales de Docker Hub | Crear el secreto con un token de Docker Hub de permiso **Read & Write**. |
| `Faltan AZURE_API_APP_NAME o AZURE_WEB_APP_NAME` | Faltan variables de repositorio | Cargar los nombres cortos del recurso, no el hostname. |
| El job `rollback` falla con `No se encontró un SHA anterior publicado en Docker Hub` | No hay ningún tag de 40 hex anterior en `facturar-api` | Revisar el historial de tags del repositorio en Docker Hub. El job solo reconoce tags de 40 hex, ordenados por `last_updated`, y excluye el SHA que falló. |
| El job `rollback` falla con `El SHA <sha> no está publicado en <usuario>/facturar-web` | El tag existe solo en una de las dos imágenes | Publicar el tag en la imagen que falta, o hacer el rollback manual con otro SHA. |
| `El input image_tag debe ser un SHA de 40 caracteres hexadecimales` | El input del rollback manual no es un SHA válido | Usar el SHA completo de 40 caracteres, en minúsculas. |
| El `commit` de `/health/live` dice `unknown` | La imagen se construyó sin el build arg `SOURCE_COMMIT` | Reconstruir y republicar con `SOURCE_COMMIT=<sha>`. Las imágenes construidas a mano sin build args no reportan versión. |

Caché de Docker Hub y de la Web App:

- El CD publica `latest` y el tag SHA. Los deploys usan **siempre el tag SHA**: un `latest` desactualizado no afecta el despliegue.
- La Web App cachea la imagen descargada. Si se redespliega el mismo tag SHA, Azure puede no volver a bajarla. Reiniciar la Web App fuerza el pull.
- El job `rollback` lista los tags por la API de Docker Hub (`ordering=last_updated`) y se queda con los de 40 hex. Los tags con otro formato se ignoran.
- Docker Hub impone límites de pulls anónimos. El deploy no hace pulls frecuentes.

## 15. Checklist de cierre

- [x] Rama default: `development`.
- [x] Ruleset de `main` activo.
- [x] Ruleset de `development` activo.
- [x] Checks requeridos seleccionados.
- [x] Repositorios de Docker Hub creados.
- [x] Token de Docker Hub configurado.
- [x] Resource group y App Service Plan B1 creados.
- [x] Web App de API creada.
- [x] Web App del portal creada.
- [x] App Settings de API configurados.
- [x] App Settings y WebSockets del portal configurados.
- [x] Always On activo en la API y en el portal.
- [x] Atlas M0 accesible desde Azure.
- [x] Publish profiles guardados como secretos.
- [x] Variables de Azure configuradas: `AZURE_API_APP_NAME`, `AZURE_WEB_APP_NAME`, `AZURE_API_URL`, `AZURE_WEB_URL` y `AZURE_DEPLOY_ENABLED`.
- [x] URLs reales de API y portal verificadas y cargadas sin barra final.
- [x] Jobs `deploy-api` y `deploy-web` agregados.
- [x] Deploy automático desde `main` probado.
- [x] Smoke tests pasan.
- [x] Rollback manual probado (US-31, §9).
- [x] Evidencia registrada en US-31.
- [x] Endpoints `/health` y `/health/live` publicados en API y portal.
- [x] Smoke test verifica el `commit` servido contra el SHA esperado.
- [ ] Health check path de App Service cambiado a `/health/live`: es un paso manual del lado de Azure, opcional.
- [ ] Rollback automático (job `rollback`) ejecutado en un entorno real: está configurado en `cd.yml` pero no hay evidencia de ejecución.
