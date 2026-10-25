# US-31 — Entrega continua y despliegue en Azure

**Épica:** Infraestructura  
**Prioridad:** Alta  
**Estimación:** 3 días  
**Requisito:** RNF-20 / RF-21  
**Depende de:** US-19, US-20  
**Habilita:** US-21, US-26  
**Estado:** Base implementada

## Historia

Como equipo, quiero que un cambio validado en `main` publique las imágenes en Docker Hub y actualice automáticamente la API y el portal en Azure App Service, para demostrar el sistema en internet sin depender de un despliegue manual por SSH.

## Contexto

US-20 valida el código con CI. Esta historia agrega el CD de despliegue:

- Docker Hub publica `facturar-api` y `facturar-web`.
- Azure App Service ejecuta cada imagen en una Web App Linux separada.
- MongoDB sigue en MongoDB Atlas; no se despliega una base en Azure.
- El plan gratuito objetivo es **App Service F1** y **MongoDB Atlas M0**.
- No se usa ACR en esta historia. ACR es un registro privado de Azure y queda como mejora posterior.
- Azure **no detecta** cambios en Docker Hub por sí solo. El workflow de GitHub Actions ejecuta `azure/webapps-deploy@v3` con el tag SHA para actualizar cada Web App.

El paso a paso completo, con variables, secretos, nombres de imágenes, creación de recursos, smoke tests y rollback, está en [`docs/despliegue-azure.md`](../despliegue-azure.md). Esta historia no duplica ese procedimiento: lo referencia y registra la evidencia.

La guía sigue el mismo patrón que el proyecto ICS: GitHub Actions construye la imagen, la publica en Docker Hub y despliega en Azure App Service. La diferencia es que FacturAR tiene dos contenedores, API y portal, y ambos se despliegan con el mismo SHA.

## Criterios de aceptación

- [x] CD se dispara solo después de un CI exitoso en `main`.
- [x] CD no se ejecuta en pull requests ni cuando CI falla.
- [x] CD publica las imágenes de API y web en Docker Hub con tags `latest` y SHA.
- [x] CD usa secretos de Docker Hub y no los expone.
- [x] CD no ejecuta tests; reutiliza el commit validado por CI.
- [ ] Existen los repositorios públicos `facturar-api` y `facturar-web` en Docker Hub.
- [ ] Las imágenes publicadas se pueden descargar y ejecutar.
- [ ] Existe un resource group y un App Service Plan Linux F1.
- [ ] Existen las Web Apps `facturar-api-*` y `facturar-web-*` con contenedor Docker Hub.
- [ ] La API tiene configurados `WEBSITES_PORT`, `ASPNETCORE_ENVIRONMENT`, `Mongo__ConnectionString`, `Mongo__DatabaseName` y `Mongo__InitializeIndexes`.
- [ ] El portal tiene configurados `WEBSITES_PORT`, `ASPNETCORE_ENVIRONMENT` y `Api__BaseUrl`.
- [ ] El portal tiene WebSockets y session affinity habilitados para Blazor Server.
- [ ] MongoDB Atlas permite las conexiones de Azure.
- [ ] GitHub tiene los secretos de Docker Hub y los publish profiles de Azure.
- [ ] GitHub tiene las variables de nombres de las Web Apps y el flag de despliegue.
- [ ] El CD despliega API y portal usando el SHA validado, nunca `latest`.
- [ ] El CD ejecuta smoke tests contra `/health` de la API y `/` del portal.
- [ ] Un push a `main` con CI exitoso actualiza ambas apps sin intervención manual.
- [ ] El rollback a un SHA anterior está documentado y probado.
- [ ] README, esta historia y `docs/despliegue-azure.md` están sincronizados.

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
- [ ] Crear la cuenta y los repositorios públicos de Docker Hub.
- [ ] Crear el token de Docker Hub con permisos de lectura y escritura.
- [ ] Crear el resource group y el App Service Plan Linux F1.
- [ ] Crear las dos Web Apps para contenedores.
- [ ] Configurar App Settings de API y portal.
- [ ] Configurar WebSockets y session affinity del portal.
- [ ] Configurar MongoDB Atlas y su acceso de red.
- [ ] Descargar los publish profiles de ambas Web Apps.
- [ ] Configurar secretos y variables en GitHub.
- [ ] Agregar `outputs.sha` al job de publicación.
- [ ] Agregar el job `deploy-api` con `azure/webapps-deploy@v3`.
- [ ] Agregar el job `deploy-web` con `azure/webapps-deploy@v3`.
- [ ] Agregar smoke tests con reintentos.
- [ ] Probar el deploy automático desde `main`.
- [ ] Probar el rollback con un SHA anterior.
- [ ] Registrar la evidencia y actualizar checkboxes.

## Definition of Done

- [ ] Docker Hub tiene ambas imágenes descargables.
- [ ] Azure tiene API y portal ejecutándose con el SHA desplegado por CD.
- [ ] Un push a `main` con CI exitoso actualiza ambas apps automáticamente.
- [ ] Los smoke tests pasan después del deploy.
- [ ] El rollback está probado.
- [ ] Los secretos están en GitHub/Azure, no en el repositorio.
- [ ] La guía `docs/despliegue-azure.md` permite repetir el procedimiento desde cero.
- [ ] La historia está enlazada desde el índice y el README.

## Evidencia

- `.github/workflows/cd.yml` publica las imágenes de API y web después de CI exitoso en `main`.
- CD en `main` disparado por CI: https://github.com/FT-Key/FacturAR/actions/runs/40243592916 (failure esperada: faltan secretos de Docker Hub).
- La guía operativa está en [`docs/despliegue-azure.md`](../despliegue-azure.md).
- Pendiente: crear recursos de Azure, configurar secretos, agregar los jobs de deploy, validar URLs y probar rollback.

## Notas de alcance

- No se usa ACR; se usa Docker Hub público.
- No se usa Azure Container Apps ni Kubernetes en esta historia.
- No se crea una base de datos en Azure; se usa MongoDB Atlas.
- El plan F1 no tiene Always On ni deployment slots. Para la demo se acepta el cold start.
- Si F1 no alcanza, se puede usar el crédito de Azure for Students con un plan B1, sin cambiar el workflow.
- No se implementan múltiples ambientes simultáneos.
- No se implementa aprobación manual del deploy; el objetivo es que `main` despliegue automáticamente después de CI.
- Los tags `latest` se publican por comodidad, pero el deploy usa el SHA.
