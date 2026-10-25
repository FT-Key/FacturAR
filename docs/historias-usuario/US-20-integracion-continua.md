# US-20 — Integración continua (CI)

**Épica:** Infraestructura  
**Prioridad:** Alta  
**Estimación:** 1 día  
**Requisito:** RF-21 / RNF-13  
**Depende de:** US-19  
**Habilita:** US-21, US-26, US-31  
**Estado:** Base implementada

## Historia

Como equipo, quiero validar cada cambio con build, tests y nombre de rama antes de mergear, para evitar que código roto llegue a `development` o `main`.

## Contexto

La integración continua vive en `.github/workflows/ci.yml` y no publica ni despliega imágenes. El despliegue continuo es una historia separada: **US-31**.

La CI se ejecuta en pushes y pull requests a `main` y `development`, levanta MongoDB 8 como servicio y ejecuta los tests unitarios y los tests `MongoFact`.

Para que las reglas de GitHub funcionen, la rama default debe ser `development` y los rulesets deben exigir los checks de CI. El paso a paso está en [`docs/despliegue-azure.md`](../despliegue-azure.md), secciones 4.1 a 4.3.

## Criterios de aceptación

- [x] CI valida el nombre de la rama en pull requests.
- [x] CI omite la validación de nombre en las ramas de promoción `development` y `main`.
- [x] CI restaura, compila en `Release` y ejecuta tests en pushes y pull requests a `main` y `development`.
- [x] CI levanta MongoDB 8 y ejecuta los tests `MongoFact`, no solo los unitarios.
- [x] CI sube los resultados de tests como artefacto.
- [x] CI no publica imágenes ni despliega.
- [x] El repositorio remoto está conectado y CI se ejecuta en GitHub Actions.
- [ ] La rama default del repositorio es `development`.
- [ ] Existe un ruleset activo para `main` con PR, aprobación, checks, conversaciones resueltas y bloqueo de force push/borrado.
- [ ] Existe un ruleset activo para `development` con PR, checks y bloqueo de force push/borrado.
- [ ] Los checks `CI / Validar nombre de rama` y `CI / Build y tests` están seleccionados como requeridos.
- [ ] Se validó un pull request real de una rama `feature/us-XX-slug` hacia `development`.
- [ ] Se validó el bloqueo de merge cuando CI falla.

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
- [ ] Cambiar la rama default a `development`.
- [ ] Crear el ruleset de `main`.
- [ ] Crear el ruleset de `development`.
- [ ] Validar un PR real y el bloqueo por CI fallida.
- [ ] Documentar la evidencia de la configuración de GitHub en esta historia.

## Definition of Done

- [x] CI compila y ejecuta tests en GitHub Actions.
- [x] CI no publica imágenes ni despliega.
- [x] El README y esta historia explican CI y el flujo de ramas.
- [ ] La rama default es `development`.
- [ ] Los rulesets de `main` y `development` están activos.
- [ ] Los checks requeridos aparecen y bloquean merges inválidos.
- [ ] La evidencia de la configuración de GitHub está registrada.

## Evidencia

- `.github/workflows/ci.yml` contiene validación de rama, build, tests, MongoDB 8 y artefacto.
- CI en `main` con MongoDB 8: https://github.com/FT-Key/FacturAR/actions/runs/40243514062 (success).
- CI en `development` con MongoDB 8: https://github.com/FT-Key/FacturAR/actions/runs/40243508964 (success).
- La validación de rama omite `main` y `development` para permitir la promoción.
- Pendiente: configuración de rama default, rulesets y evidencia de un PR real.

## Notas de alcance

- Esta historia termina en CI. La publicación de imágenes y el despliegue en Azure son US-31.
- No implementa Kubernetes; eso es stretch US-26.
- No crea ambientes de staging.
- Los tags de release quedan fuera de esta historia.
