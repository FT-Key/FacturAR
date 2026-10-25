# US-20 — CI, CD y despliegue

**Épica:** Infraestructura  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RF-21  
**Depende de:** US-19  
**Habilita:** US-21, US-26  
**Estado:** Base implementada

## Historia

Como equipo, quiero validar cada cambio con integración continua y publicar imágenes solo después de una validación exitosa, para entregar versiones reproducibles sin mezclar tests con despliegue.

## Contexto

La integración continua (CI) y la entrega continua (CD) son workflows separados:

- `.github/workflows/ci.yml`: valida rama, restaura, compila y ejecuta tests.
- `.github/workflows/cd.yml`: publica imágenes en Docker Hub después de que CI termine con éxito en `main`.

El CD no despliega al servidor. El despliegue sigue siendo manual por SSH. Para que `workflow_run` funcione, el workflow de CD debe existir en la rama default del repositorio.

## Criterios de aceptación

- [x] CI valida el nombre de la rama en pull requests.
- [x] CI restaura, compila y ejecuta tests en pushes y pull requests a `main` y `development`.
- [x] CI sube los resultados de tests como artefacto.
- [x] CI no publica imágenes.
- [x] CD se dispara solo después de un CI exitoso en `main`.
- [x] CD no se ejecuta en pull requests ni cuando CI falla.
- [x] CD publica las imágenes de API y web en Docker Hub con tags `latest` y SHA.
- [x] CD usa secretos de Docker Hub y no los expone.
- [x] El README documenta CI, CD, secretos, ramas y despliegue manual.
- [x] El README incluye badges de CI y CD.
- [x] El repositorio remoto está conectado y los workflows se ejecutan en GitHub Actions.
- [ ] Los secretos `DOCKERHUB_USERNAME` y `DOCKERHUB_TOKEN` están configurados.
- [ ] Las imágenes publicadas se pueden descargar y ejecutar.
- [ ] El despliegue manual por SSH está probado.
- [ ] El rollback por `IMAGE_TAG` está probado.

## Escenarios Gherkin

```gherkin
Scenario: Rama inválida en CI
  Given un pull request desde la rama "mi-rama"
  When se ejecuta CI
  Then el job de validación falla
  And el merge queda bloqueado por la regla de rama
```

```gherkin
Scenario: Rama válida en CI
  Given un pull request desde "feature/us-01-solicitud-alta"
  When se ejecuta CI
  Then la validación de rama pasa
  And se ejecutan build y tests
```

```gherkin
Scenario: Push a main con CI exitoso
  Given un push a main
  When CI termina con éxito
  Then CD se ejecuta
  And publica las imágenes de API y web con tag latest y SHA
```

```gherkin
Scenario: CI falla
  Given un cambio que rompe un test
  When CI falla en main
  Then CD no se ejecuta
  And no se publican imágenes nuevas
```

```gherkin
Scenario: Pull request
  Given un pull request hacia development
  When CI termina con éxito
  Then CD no se ejecuta
  And no se publican imágenes
```

```gherkin
Scenario: Publicación manual
  Given un operador autorizado
  When ejecuta CD con workflow_dispatch sobre main
  Then se publican las imágenes del commit de main
```

## Tareas técnicas

- [x] Separar CI y CD en dos workflows.
- [x] Crear validación de rama y build/test en `ci.yml`.
- [x] Crear publicación de imágenes en `cd.yml`.
- [x] Encadenar CD a CI con `workflow_run` y conclusión exitosa.
- [x] Documentar secretos, despliegue manual y rollback.
- [x] Agregar badges de CI y CD al README.
- [x] Conectar el repositorio remoto.
- [ ] Configurar secretos de Docker Hub.
- [ ] Ejecutar CI en un pull request real.
- [x] Verificar que CD se dispare solo después de CI en `main`.
- [ ] Verificar las imágenes publicadas.
- [ ] Probar el despliegue y el rollback.

## Definition of Done

- [x] CI pasa en GitHub Actions.
- [x] CD se dispara solo después de CI exitoso en `main`; la publicación queda pendiente por secretos.
- [ ] Las imágenes se publican y se pueden ejecutar.
- [ ] El despliegue y el rollback están probados.
- [x] El README muestra el estado de CI y CD.
- [x] La historia está enlazada desde el índice y el README.

## Evidencia

- `.github/workflows/ci.yml` contiene validación de rama, build, tests y artefacto.
- `.github/workflows/cd.yml` contiene publicación de imágenes condicionada a CI exitoso en `main`.
- `README.md` documenta CI, CD, secretos, flujo de ramas, protección y despliegue manual.
- CI en `main`: https://github.com/FT-Key/FacturAR/actions/runs/40242885161 (success).
- CI en `development`: https://github.com/FT-Key/FacturAR/actions/runs/40242890151 (success).
- CD disparado por CI de `main`: https://github.com/FT-Key/FacturAR/actions/runs/40242936550 (failure esperada: faltan secretos de Docker Hub).
- Pendiente: configurar secretos, verificar imágenes, desplegar y probar rollback.

## Notas de alcance

- CD publica imágenes; no despliega automáticamente al servidor.
- El despliegue y el rollback son manuales por SSH.
- No implementa Kubernetes; eso es stretch US-26.
- No implementa múltiples ambientes simultáneos.
- Los tags de release se pueden agregar más adelante con un workflow específico.
