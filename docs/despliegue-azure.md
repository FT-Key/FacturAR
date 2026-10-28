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
- Salud de API: `https://<api-app>.azurewebsites.net/health`
- Base: MongoDB Atlas M0.
- Imágenes: `<dockerhub-user>/facturar-api:<sha>` y `<dockerhub-user>/facturar-web:<sha>`.
- Push a `main` → CI → CD publica imágenes → CD despliega API → smoke test → CD despliega portal → smoke test.

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
   │  build + tests + MongoDB 8
   ▼
main con CI exitoso
   │
   ▼
GitHub Actions CD (US-31)
   │
   ├── docker build/push ──► Docker Hub
   │                         ├── facturar-api:<sha>
   │                         └── facturar-web:<sha>
   │
   ├── azure/webapps-deploy ──► App Service API
   │
   └── azure/webapps-deploy ──► App Service Web
                                  │
                                  ▼
                            MongoDB Atlas
```

Puntos importantes:

- Azure no detecta cambios en Docker Hub. GitHub Actions debe ejecutar el deploy explícitamente.
- No se usa ACR. ACR es Azure Container Registry, un registro privado de Azure; no es necesario para este alcance.
- No se usa Azure Container Apps ni Kubernetes.
- El portal es Blazor Server: necesita WebSockets y afinidad de sesión.
- La API y el portal son dos Web Apps separadas.

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
| `AZURE_API_URL` | `https://facturar-api-utn-frt-gncabkgzghh2gbf2.brazilsouth-01.azurewebsites.net` | URL pública completa de la API. Se usa en `environment.url` y en el smoke test de `/health`. Sin barra final. |
| `AZURE_WEB_URL` | `https://facturar-web-utn-frt-gjbhdhb3bjgggxfg.brazilsouth-01.azurewebsites.net` | URL pública completa del portal. Se usa en `environment.url` y en el smoke test de `/`. Sin barra final. |
| `AZURE_DEPLOY_ENABLED` | `false` | Habilita los jobs `deploy-api` y `deploy-web`. Usar `true` solo cuando Azure y los secretos estén listos. |

`AZURE_API_APP_NAME` y `AZURE_WEB_APP_NAME` son los nombres cortos del recurso, tal como aparecen en el portal de Azure. No usar el hostname completo en esas variables: `azure/webapps-deploy` espera el nombre de la Web App.

Los hostnames `https://<app>.azurewebsites.net` pueden no existir si Azure asigna un sufijo adicional al crear la Web App. Copiar la URL real desde **Overview → Default domain** y cargarla completa en `AZURE_API_URL` y `AZURE_WEB_URL`. Esas URLs son las que usan los smoke tests.

No guardar connection strings, tokens ni publish profiles en el repositorio.

### 4.5 Runner de la CI

La CI usa `image: mongo:8.2` en el servicio de MongoDB. Con `mongo:8` (8.0.x) el contenedor no arranca en los runners de GitHub Actions: usan kernel `7.0.0` y MongoDB 8.0.x aborta el arranque por el guard TCMalloc/rseq (`SERVER-121912`), un falso positivo porque el guard parsea `uname -r` como "7.0.0" y no reconoce el kernel upstream 7.0.14 (`SERVER-136848`). El job falla en "Initialize containers" con `Failed to initialize container mongo:8`. MongoDB 8.2 no tiene ese guard y arranca correctamente.

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

Abrir `http://localhost:8080/health`.

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

2. **Configuration** → **General settings**:
   - **Health check path:** `/health`.
   - Guardar y reiniciar.

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

1. Abrir `https://facturar-api-utn-frt-gncabkgzghh2gbf2.brazilsouth-01.azurewebsites.net/health`; debe responder `200`.
2. Abrir `https://facturar-web-utn-frt-gjbhdhb3bjgggxfg.brazilsouth-01.azurewebsites.net/`; debe cargar el portal.
3. Abrir `/verificar` del portal con un CAE de 14 dígitos, por ejemplo `https://facturar-web-utn-frt-gjbhdhb3bjgggxfg.brazilsouth-01.azurewebsites.net/verificar?cae=12345678901234`; debe responder `200` y mostrar la validación del comprobante. Si responde 500, revisar `Api__BaseUrl` (ver Troubleshooting).
4. Probar login o una ruta pública según el estado de las historias.
5. Revisar **Log stream** de cada Web App si algo falla.

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

El `cd.yml` publica las imágenes y despliega en Azure. La configuración es:

1. `publish-images` expone el SHA validado como output (`outputs.sha`).
2. `deploy-api` corre después de `publish-images`.
3. `deploy-web` corre después de `deploy-api`.
4. Ambos jobs se condicionan a `vars.AZURE_DEPLOY_ENABLED == 'true'`.
5. Cada job valida el publish profile y las variables antes de desplegar.
6. El deploy usa el SHA validado, nunca `latest`.
7. `environment.url` y los smoke tests usan `AZURE_API_URL` y `AZURE_WEB_URL`; `app-name` usa `AZURE_API_APP_NAME` y `AZURE_WEB_APP_NAME`.

Bloque de referencia:

```yaml
jobs:
  publish-images:
    outputs:
      sha: ${{ steps.sha.outputs.value }}
    steps:
      - id: sha
        run: echo "value=${{ github.event.workflow_run.head_sha || github.sha }}" >> "$GITHUB_OUTPUT"
      # ... build/push existentes ...

  deploy-api:
    needs: publish-images
    if: vars.AZURE_DEPLOY_ENABLED == 'true'
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
          [ -n "$PUBLISH_PROFILE" ] && [ -n "$APP_NAME" ] && [ -n "$API_URL" ] || {
            echo "::error::Faltan el publish profile o las variables de Azure."
            exit 1
          }
      - uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ vars.AZURE_API_APP_NAME }}
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE_API }}
          images: ${{ secrets.DOCKERHUB_USERNAME }}/facturar-api:${{ needs.publish-images.outputs.sha }}
      - name: Smoke test API
        shell: bash
        run: |
          for i in $(seq 1 18); do
            if curl -fsS "${{ vars.AZURE_API_URL }}/health"; then
              exit 0
            fi
            sleep 10
          done
          exit 1

  deploy-web:
    needs: [publish-images, deploy-api]
    if: vars.AZURE_DEPLOY_ENABLED == 'true'
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
          [ -n "$PUBLISH_PROFILE" ] && [ -n "$APP_NAME" ] && [ -n "$WEB_URL" ] || {
            echo "::error::Faltan el publish profile o las variables de Azure."
            exit 1
          }
      - uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ vars.AZURE_WEB_APP_NAME }}
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE_WEB }}
          images: ${{ secrets.DOCKERHUB_USERNAME }}/facturar-web:${{ needs.publish-images.outputs.sha }}
      - name: Smoke test portal
        shell: bash
        run: |
          for i in $(seq 1 18); do
            if curl -fsS "${{ vars.AZURE_WEB_URL }}/"; then
              exit 0
            fi
            sleep 10
          done
          exit 1
```

Notas:

- `needs` garantiza que el portal no se despliegue si la API falla.
- Si `AZURE_DEPLOY_ENABLED` es `false` o no existe, los jobs de deploy se omiten y solo se publican imágenes.
- `environment: production` permite ver la URL en GitHub. No agregar required reviewers si se quiere deploy automático.
- `azure/webapps-deploy@v3` no necesita `actions/checkout`.
- Los smoke tests reintentan 18 veces cada 10 segundos (3 minutos). App Service puede tardar en levantar el contenedor.
- El deploy automático real quedó verificado: con `AZURE_DEPLOY_ENABLED=true` y la CI exitosa en `main`, el CD publica las imágenes y despliega API y portal con el SHA validado.

## 9. Rollback

Cada deploy conserva un tag SHA en Docker Hub. Para volver a una versión anterior:

1. Elegir el SHA sano en el historial de GitHub Actions o Docker Hub.
2. Ejecutar el workflow de rollback o desplegar manualmente con ese tag.

El rollback usa un tag SHA ya publicado en Docker Hub: no hay que recompilar la imagen. Con el pipeline actual el tag a usar es el SHA anterior publicado, por ejemplo `8a3541507a5eabb566571ad843f19e9c7baa0f3f`.

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

Repetir para `facturar-web`. Verificar `/health` y `/`.

Recomendación: crear un `rollback.yml` con `workflow_dispatch` e inputs `image_tag` y `target` (`api`, `web`, `both`). No es obligatorio para cerrar US-31, pero deja el rollback reproducible.

## 10. Costos y límites

- **GitHub Actions:** gratis para repositorios públicos. En privados, 2.000 minutos/mes en el plan Free.
- **Docker Hub:** repositorios públicos gratis. Hay límites de pulls anónimos; el deploy no hace pulls frecuentes.
- **Azure App Service B1:** bajo costo, con Always On. Es el plan elegido para la demo.
- **Azure App Service F1:** gratis, pero sin Always On; no se usa porque las apps se dormían.
- **Azure for Students:** incluye crédito que cubre B1 durante la demo.
- **MongoDB Atlas M0:** gratis, 512 MB.
- No crear recursos pagos innecesarios. Eliminar el resource group al terminar la demo si no se usa más.

## 11. Troubleshooting

| Síntoma | Causa probable | Solución |
| --- | --- | --- |
| App Service muestra 503 | El contenedor no escucha en el puerto esperado | Configurar `WEBSITES_PORT=8080`. |
| App Service muestra 503 y en el log aparece `linuxFxVersion=sitecontainers` con `targetPort=80` | Azure sondea el puerto 80 del sitecontainer e ignora `WEBSITES_PORT` | Pasar a Docker clásico con `az webapp config container set --container-image-name ... --container-registry-url https://index.docker.io` o cambiar el `targetPort` del sitecontainer a `8080`; reiniciar. |
| `ImagePullBackOff` o error de imagen | Imagen inexistente o privada | Verificar el tag en Docker Hub y usar repositorio público. |
| API responde 500 en `/health` | Mongo no conecta | Revisar connection string, usuario y Network Access de Atlas. |
| Portal carga pero no llama a la API | `Api__BaseUrl` incorrecto | Usar la URL real de `AZURE_API_URL`, sin barra final. |
| Portal `/verificar` devuelve 500 | `Api__BaseUrl` mal formado (sin `https://`) genera `System.UriFormatException` al construir el `HttpClient` | Usar la URL completa de `AZURE_API_URL` con `https://` y sin barra final. |
| Blazor pierde conexión | WebSockets o afinidad deshabilitados | Activar WebSockets y Session affinity. |
| Deploy falla con 401/403 | Publish profile vencido o app name incorrecto | Descargar de nuevo el publish profile y revisar `AZURE_*_APP_NAME`. |
| Deploy falla por basic auth | SCM Basic Auth deshabilitado | Activarlo en Configuration → General settings. |
| Docker Hub deniega el push | Token sin permisos | Crear token con Read & Write y actualizar el secreto. |
| CD no se dispara | `cd.yml` no está en la rama default o CI no se llama `CI` | Verificar default branch y el nombre del workflow de CI. |
| Los jobs de deploy se omiten | `AZURE_DEPLOY_ENABLED` no vale `true` | Crear o corregir la variable del repositorio. |
| CI falla en `Initialize containers` con `Failed to initialize container mongo:8` | MongoDB 8.0.x aborta en kernels ≥ 6.19 por el guard TCMalloc/rseq (`SERVER-121912`) | Usar `image: mongo:8.2` en el servicio `mongo` de `.github/workflows/ci.yml`. |
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

O actualizar el `targetPort` del sitecontainer a `8080` y reiniciar. Verificar después `/health`.

## 12. Checklist de cierre

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
- [x] Rollback probado.
- [x] Evidencia registrada en US-31.
