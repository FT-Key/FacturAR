# US-20 — Integración continua (CI)

**Épica:** Infraestructura  
**Prioridad:** Alta  
**Estimación:** 1 día  
**Requisito:** RF-21 / RNF-13  
**Depende de:** US-19  
**Habilita:** US-21, US-26, US-31  
**Estado:** Completada

## Historia

Como equipo, quiero validar cada cambio con build, tests y nombre de rama antes de mergear, para evitar que código roto llegue a `development` o `main`.

## Contexto

La integración continua vive en `.github/workflows/ci.yml` y no publica ni despliega imágenes. El despliegue continuo es una historia separada: **US-31**.

La CI se ejecuta en pushes y pull requests a `main` y `development`, levanta MongoDB 8 como servicio y ejecuta los tests unitarios y los tests `MongoFact`.

Para que las reglas de GitHub funcionen, la rama default debe ser `development` y los rulesets deben exigir los checks de CI. El paso a paso está en [`docs/despliegue-azure.md`](../../despliegue-azure.md), secciones 4.1 a 4.3.

## Criterios de aceptación

- [x] CI valida el nombre de la rama en pull requests.
- [x] CI omite la validación de nombre en las ramas de promoción `development` y `main`.
- [x] CI restaura, compila en `Release` y ejecuta tests en pushes y pull requests a `main` y `development`.
- [x] CI levanta MongoDB 8 y ejecuta los tests `MongoFact`, no solo los unitarios.
- [x] CI sube los resultados de tests como artefacto.
- [x] CI no publica imágenes ni despliega.
- [x] El repositorio remoto está conectado y CI se ejecuta en GitHub Actions.
- [x] La rama default del repositorio es `development`.
- [x] Existe un ruleset activo para `main` con PR, aprobación, checks, conversaciones resueltas y bloqueo de force push/borrado.
- [x] Existe un ruleset activo para `development` con PR, checks y bloqueo de force push/borrado.
- [x] Los checks `CI / Validar nombre de rama` y `CI / Build y tests` están seleccionados como requeridos.
- [x] Se validó un pull request real de una rama `feature/us-XX-slug` hacia `development`.
- [x] Se validó el bloqueo de merge cuando CI falla.

## Escenarios Gherkin

```gherkin
Scenario: Rama inválida en CI
  Given un pull request desde la rama "mi-rama"
  When se ejecuta CI
  Then el job de validación falla
  And el merge queda bloqueado por el ruleset
```

```gherkin
Scenario: Rama válida en CI
  Given un pull request desde "feature/us-01-solicitud-alta"
  When se ejecuta CI
  Then la validación de rama pasa
  And se ejecutan build y tests
```

```gherkin
Scenario: Promoción de development a main
  Given un pull request desde "development" hacia "main"
  When se ejecuta CI
  Then la validación de nombre de rama no bloquea la promoción
  And se ejecutan build y tests
```

```gherkin
Scenario: CI falla
  Given un cambio que rompe un test
  When CI falla
  Then el merge queda bloqueado
  And US-31 no recibe un commit válido para desplegar
```

## Tareas técnicas

- [x] Crear `.github/workflows/ci.yml`.
- [x] Validar el nombre de rama en pull requests.
- [x] Omitir la validación en `main` y `development`.
- [x] Agregar MongoDB 8 como servicio de CI.
- [x] Compilar en `Release` y ejecutar la suite completa.
- [x] Publicar `TestResults` como artefacto.
- [x] Conectar el repositorio remoto.
- [x] Verificar CI en `main` y `development`.
- [x] Cambiar la rama default a `development`.
- [x] Crear el ruleset de `main`.
- [x] Crear el ruleset de `development`.
- [x] Validar un PR real y el bloqueo por CI fallida.
- [x] Documentar la evidencia de la configuración de GitHub en esta historia.

## Definition of Done

- [x] CI compila y ejecuta tests en GitHub Actions.
- [x] CI no publica imágenes ni despliega.
- [x] El README y esta historia explican CI y el flujo de ramas.
- [x] La rama default es `development`.
- [x] Los rulesets de `main` y `development` están activos.
- [x] Los checks requeridos aparecen y bloquean merges inválidos.
- [x] La evidencia de la configuración de GitHub está registrada.

## Evidencia

- `.github/workflows/ci.yml` contiene validación de rama, build, tests, MongoDB 8 y artefacto.
- CI en `main` con MongoDB 8 como servicio: https://github.com/FT-Key/FacturAR/actions/runs/40639636114 (success).
- CI en `development`: https://github.com/FT-Key/FacturAR/actions/runs/40637265953 (success).
- CI del PR #2 `development` → `main`: https://github.com/FT-Key/FacturAR/actions/runs/40637531339 (attempt 1 falló por infraestructura: `Failed to initialize container mongo:8`; attempt 2 success).
- Rama default verificada por API: `GET /repos/FT-Key/FacturAR` devuelve `default_branch=development`.
- Rulesets activos (API):
  - `main-protection` (id 26004120): `enforcement=active`, `ref_name include refs/heads/main`, creado 2026-10-28T01:18:11Z; reglas `deletion`, `non_fast_forward`, `pull_request` (required_approving_review_count=1, required_review_thread_resolution=true, dismiss_stale_reviews_on_push=true) y `required_status_checks` (strict_required_status_checks_policy=true).
  - `development-protection` (id 26004163): `enforcement=active`, `ref_name include refs/heads/development`, creado 2026-10-28T01:19:59Z; mismas reglas con `strict_required_status_checks_policy=false`.
- Pull request real validado: PR #1 `feature/us-31-entrega-continua` → `development`, CI https://github.com/FT-Key/FacturAR/actions/runs/40634705545 con los jobs "Validar nombre de rama" (success) y "Build y tests" (success); mergeado 2026-10-28T05:18:52Z (merge commit `4ae84a2`).
- La validación de rama omite `main` y `development` para permitir la promoción.
- La CI no publica imágenes ni despliega: eso es US-31.

### Rulesets, checks requeridos y bloqueo de merge

- Rulesets con los checks requeridos seleccionados (API `GET /repos/FT-Key/FacturAR/rulesets/{id}`):
  - `main-protection` (id 26004120): `enforcement=active`, `ref_name include refs/heads/main`, actualizado 2026-10-28T19:01:14Z; `required_status_checks` con los contexts "Validar nombre de rama" y "Build y tests" (integration_id 15368) y `strict_required_status_checks_policy: true`.
  - `development-protection` (id 26004163): `enforcement=active`, `ref_name include refs/heads/development`, actualizado 2026-10-28T19:00:51Z; mismos checks requeridos con `strict_required_status_checks_policy: false`.
- Bloqueo de merge validado: el PR #3 (evidencia de US-20/US-31) quedó con el check requerido "Build y tests" en rojo (run https://github.com/FT-Key/FacturAR/actions/runs/40748717363, attempt 4) y el merge bloqueado por el ruleset.
- Incidente del servicio de MongoDB en la CI y fix: los runners de GitHub Actions corren kernel `7.0.0` (`ubuntu-latest` con Ubuntu 26.04 y también `ubuntu-24.04`, imagen `20261026.354.1`) y MongoDB 8.0.x aborta el arranque con `Failed to initialize container mongo:8` por el guard TCMalloc/rseq (`SERVER-121912`), un falso positivo porque el guard parsea `uname -r` como "7.0.0" y no reconoce el kernel upstream 7.0.14 (`SERVER-136848`). Un workflow de diagnóstico ejecutado en el runner confirmó que `mongo:8` falla con `CONTROL 12257600` y que `mongo:8.2` (8.2.12) arranca correctamente. Fix: `image: mongo:8.2` en el servicio `mongo` de `.github/workflows/ci.yml` (PR https://github.com/FT-Key/FacturAR/pull/4). Runs afectados: `40637531339` (attempt 1) y `40748717363` (attempts 1 a 4). CI en verde con el fix: https://github.com/FT-Key/FacturAR/actions/runs/40752233101.
- `cd.yml` no se modifica porque no usa MongoDB; el CD funcionó sobre Ubuntu 26.04 (run https://github.com/FT-Key/FacturAR/actions/runs/40639732298).

## Notas de alcance

- Esta historia termina en CI. La publicación de imágenes y el despliegue en Azure son US-31.
- No implementa Kubernetes; eso es stretch US-26.
- No crea ambientes de staging.
- Los tags de release quedan fuera de esta historia.
