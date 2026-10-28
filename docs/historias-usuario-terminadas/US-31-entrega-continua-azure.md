# US-31 — Entrega continua y despliegue en Azure

**Épica:** Infraestructura  
**Prioridad:** Alta  
**Estimación:** 3 días  
**Requisito:** RNF-20 / RF-21  
**Depende de:** US-19, US-20  
**Habilita:** US-21, US-26  
**Estado:** Completada

## Historia

Como equipo, quiero que un cambio validado en `main` publique las imágenes en Docker Hub y actualice automáticamente la API y el portal en Azure App Service, para demostrar el sistema en internet sin depender de un despliegue manual por SSH.

## Contexto

US-20 valida el código con CI. Esta historia agrega el CD de despliegue:

- Docker Hub publica `facturar-api` y `facturar-web`.
- Azure App Service ejecuta cada imagen en una Web App Linux separada.
- MongoDB sigue en MongoDB Atlas; no se despliega una base en Azure.
- El plan de App Service para la demo es **B1**, que soporta **Always On**, y la base es **MongoDB Atlas M0**.
- Se usa B1 en lugar de F1 porque F1 no soporta Always On y las aplicaciones se dormían; B1 mantiene los contenedores despiertos y sigue siendo de bajo costo.
- No se usa ACR en esta historia. ACR es un registro privado de Azure y queda como mejora posterior.
- Azure **no detecta** cambios en Docker Hub por sí solo. El workflow de GitHub Actions ejecuta `azure/webapps-deploy@v3` con el tag SHA para actualizar cada Web App.

El paso a paso completo, con variables, secretos, nombres de imágenes, creación de recursos, smoke tests y rollback, está en [`docs/despliegue-azure.md`](../../despliegue-azure.md). Esta historia no duplica ese procedimiento: lo referencia y registra la evidencia.

La guía sigue el mismo patrón que el proyecto ICS: GitHub Actions construye la imagen, la publica en Docker Hub y despliega en Azure App Service. La diferencia es que FacturAR tiene dos contenedores, API y portal, y ambos se despliegan con el mismo SHA.

## Criterios de aceptación

- [x] CD se dispara solo después de un CI exitoso en `main`.
- [x] CD no se ejecuta en pull requests ni cuando CI falla.
- [x] CD publica las imágenes de API y web en Docker Hub con tags `latest` y SHA.
- [x] CD usa secretos de Docker Hub y no los expone.
- [x] CD no ejecuta tests; reutiliza el commit validado por CI.
- [x] Existen los repositorios públicos `facturar-api` y `facturar-web` en Docker Hub.
- [x] Las imágenes publicadas se pueden descargar y ejecutar.
- [x] Existe un resource group y un App Service Plan Linux B1.
- [x] Existen las Web Apps `facturar-api-utn-frt` y `facturar-web-utn-frt` con contenedor Docker Hub.
- [x] La API tiene configurados `WEBSITES_PORT`, `ASPNETCORE_ENVIRONMENT`, `Mongo__ConnectionString`, `Mongo__DatabaseName` y `Mongo__InitializeIndexes`.
- [x] El portal tiene configurados `WEBSITES_PORT`, `ASPNETCORE_ENVIRONMENT` y `Api__BaseUrl`.
- [x] El portal tiene WebSockets y session affinity habilitados para Blazor Server.
- [x] MongoDB Atlas permite las conexiones de Azure.
- [x] GitHub tiene los secretos de Docker Hub y los publish profiles de Azure.
- [x] GitHub tiene corregidas las variables de nombres de las Web Apps, las URLs completas y el flag de despliegue.
- [x] El CD despliega API y portal usando el SHA validado, nunca `latest`.
- [x] El CD ejecuta smoke tests contra `/health` de la API y `/` del portal.
- [x] Un push a `main` con CI exitoso actualiza ambas apps sin intervención manual.
- [x] El rollback a un SHA anterior está documentado y probado.
- [x] README, esta historia y `docs/despliegue-azure.md` están sincronizados.

## Escenarios Gherkin

```gherkin
Scenario: Deploy automático después de CI exitoso
  Given un push a main
  When CI termina con éxito
  Then CD publica facturar-api y facturar-web con el SHA del commit
  And Azure App Service actualiza la API con ese SHA
  And Azure App Service actualiza el portal con ese SHA
  And los smoke tests responden correctamente
```

```gherkin
Scenario: CI falla
  Given un cambio que rompe un test
  When CI falla en main
  Then CD no se ejecuta
  And Azure conserva la versión anterior
```

```gherkin
Scenario: Pull request
  Given un pull request hacia development
  When CI termina con éxito
  Then CD no se ejecuta
  And no se publican ni despliegan imágenes
```

```gherkin
Scenario: Deploy con tag inmutable
  Given una imagen publicada como facturar-api:<sha>
  When el workflow despliega en Azure
  Then App Service usa facturar-api:<sha>
  And no se despliega el tag latest
```

```gherkin
Scenario: Falta un secreto de Azure
  Given que falta AZURE_WEBAPP_PUBLISH_PROFILE_API
  When CD intenta desplegar
  Then el job falla con un mensaje claro
  And no se marca el deploy como exitoso
```

```gherkin
Scenario: Rollback
  Given un SHA anterior disponible en Docker Hub
  When se ejecuta el procedimiento de rollback
  Then la API y el portal vuelven a ese SHA
  And los smoke tests responden correctamente
```

## Tareas técnicas

- [x] Separar la publicación de imágenes de la CI.
- [x] Encadenar CD a CI con `workflow_run` y conclusión exitosa.
- [x] Publicar API y web en Docker Hub con `latest` y SHA.
- [x] Documentar el flujo de ramas y la dependencia de CI.
- [x] Crear la cuenta y los repositorios públicos de Docker Hub.
- [x] Crear el token de Docker Hub con permisos de lectura y escritura.
- [x] Crear el resource group y el App Service Plan Linux B1.
- [x] Crear las dos Web Apps para contenedores.
- [x] Configurar App Settings de API y portal.
- [x] Configurar WebSockets y session affinity del portal.
- [x] Configurar MongoDB Atlas y su acceso de red.
- [x] Descargar los publish profiles de ambas Web Apps.
- [x] Configurar los secretos de Docker Hub y los publish profiles en GitHub.
- [x] Configurar y corregir las variables de Azure en GitHub.
- [x] Agregar `outputs.sha` al job de publicación.
- [x] Agregar el job `deploy-api` con `azure/webapps-deploy@v3`.
- [x] Agregar el job `deploy-web` con `azure/webapps-deploy@v3`.
- [x] Agregar smoke tests con reintentos.
- [x] Probar el deploy automático desde `main`.
- [x] Probar el rollback con un SHA anterior.
- [x] Registrar la evidencia y actualizar checkboxes.

## Definition of Done

- [x] Docker Hub tiene ambas imágenes descargables.
- [x] Azure tiene API y portal ejecutándose con el SHA desplegado por CD.
- [x] Un push a `main` con CI exitoso actualiza ambas apps automáticamente.
- [x] Los smoke tests pasan después del deploy.
- [x] El rollback está probado.
- [x] Los secretos están en GitHub/Azure, no en el repositorio.
- [x] La guía `docs/despliegue-azure.md` permite repetir el procedimiento desde cero.
- [x] La historia está enlazada desde el índice y el README.

## Evidencia

- `.github/workflows/cd.yml` publica las imágenes de API y web después de CI exitoso en `main`, usa `latest` y el SHA, e incluye los jobs `deploy-api` y `deploy-web` con `azure/webapps-deploy@v3`, preflight de secretos/variables y smoke tests con reintentos.
- CD automático verificado: run https://github.com/FT-Key/FacturAR/actions/runs/40639732298, disparado por `workflow_run` de la CI https://github.com/FT-Key/FacturAR/actions/runs/40639636114 en `main` (merge commit `077a247695125defb70ae325f5bad052023fb398`), con los tres jobs en `success`.
- Los jobs de deploy usan `needs.publish-images.outputs.sha`, nunca `latest`.
- Los pasos "Smoke test de la API" y "Smoke test del portal" del mismo run terminaron en `success`.
- CI en `main`: https://github.com/FT-Key/FacturAR/actions/runs/40639636114 (success).
- Docker Hub: repositorios `fntkey/facturar-api` y `fntkey/facturar-web` con los tags `077a247695125defb70ae325f5bad052023fb398` y `latest`, publicados 2026-10-28 05:45/05:46 UTC. SHA anterior disponible para rollback: `8a3541507a5eabb566571ad843f19e9c7baa0f3f`.
- Variables del repositorio corregidas: `AZURE_API_APP_NAME=facturar-api-utn-frt`, `AZURE_WEB_APP_NAME=facturar-web-utn-frt`, `AZURE_API_URL=https://facturar-api-utn-frt-gncabkgzghh2gbf2.brazilsouth-01.azurewebsites.net`, `AZURE_WEB_URL=https://facturar-web-utn-frt-gjbhdhb3bjgggxfg.brazilsouth-01.azurewebsites.net` y `AZURE_DEPLOY_ENABLED=true`.
- Azure funciona con deploy manual y con CD sobre el plan B1, resource group `rg-facturar` y las apps `facturar-api-utn-frt` y `facturar-web-utn-frt`.
- URLs reales verificadas:
  - API: `https://facturar-api-utn-frt-gncabkgzghh2gbf2.brazilsouth-01.azurewebsites.net`
  - Portal: `https://facturar-web-utn-frt-gjbhdhb3bjgggxfg.brazilsouth-01.azurewebsites.net`
- API desplegada: `/health` responde `200` con `{"status":"ok","database":"ok"}` y `/v1/comprobantes/verificar/12345678901234` responde `404` con `COMPROBANTE_NO_ENCONTRADO` (API sana y con MongoDB accesible).
- Portal desplegado: `/verificar` responde 200 y el portal ya consulta la API desplegada.
- `Api__BaseUrl` estaba mal formado (valor sin `https://`) y provocaba `System.UriFormatException` en `Program.cs:13` al crear el `HttpClient`, con 500 en `/verificar`; corregido y verificado: `/verificar` → 200 con el formulario, `/verificar?cae=123` → 200 con la validación de 14 dígitos y `/verificar?cae=12345678901234` → 200 con "No se encontró un comprobante emitido con el CAE indicado".
- WebSockets y session affinity habilitados en el portal: `az webapp config set --web-sockets-enabled true` (webSocketsEnabled=true, alwaysOn=true) y `az webapp update --client-affinity-enabled true` (clientAffinityEnabled=true) en `facturar-web-utn-frt`.
- Se eligió B1 en lugar de F1 porque F1 no soporta Always On y las apps se dormían. Always On está activo en la API y en el portal.
- Environment `production` en GitHub sin required reviewers, wait timer 0 y sin restricción de ramas, para no frenar el deploy automático.
- Los hostnames cortos `https://<app>.azurewebsites.net` no existen para estas apps; por eso el workflow usa `AZURE_API_URL` y `AZURE_WEB_URL` con las URLs completas, mientras que `AZURE_API_APP_NAME` y `AZURE_WEB_APP_NAME` guardan solo el nombre corto del recurso.
- La guía operativa está en [`docs/despliegue-azure.md`](../../despliegue-azure.md).

### Rollback

Rollback documentado en [`docs/despliegue-azure.md`](../../despliegue-azure.md) §9 y validado: el procedimiento despliega un tag SHA ya publicado en Docker Hub con `az webapp config container set --container-image-name <usuario>/facturar-<api|web>:<sha> --container-registry-url https://index.docker.io`, el mismo mecanismo que aplica el CD, y cada versión conserva su tag inmutable (`8a3541507a5eabb566571ad843f19e9c7baa0f3f` y `077a247695125defb70ae325f5bad052023fb398`), de modo que se puede volver a cualquiera de las dos.

## Notas de alcance

- No se usa ACR; se usa Docker Hub público.
- No se usa Azure Container Apps ni Kubernetes en esta historia.
- No se crea una base de datos en Azure; se usa MongoDB Atlas.
- El plan B1 se usa para la demo porque soporta Always On. F1 es gratis pero no tiene Always On y las apps se dormían.
- Always On debe quedar activo en API y portal para evitar el cold start.
- No se implementan múltiples ambientes simultáneos.
- No se implementa aprobación manual del deploy; el objetivo es que `main` despliegue automáticamente después de CI.
- Los tags `latest` se publican por comodidad, pero el deploy usa el SHA.
