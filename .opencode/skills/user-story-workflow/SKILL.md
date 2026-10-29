---
name: user-story-workflow
description: Gestiona historias US-XX de FacturAR con alcance estricto, dependencias, checkboxes con evidencia, estado y mudanza a historias-usuario-terminadas. Usar al iniciar, cerrar o revisar una historia.
---

# Flujo de historias US-XX

## Inicio

1. Leé `docs/historias-usuario/README.md` y solo la historia solicitada.
2. Verificá dependencias y bloqueos. Si falta una historia previa, detenete y reportá el ID exacto.
3. Confirmá el alcance: criterios de aceptación, tareas técnicas y Definition of Done.
4. No toques las historias fuera de alcance (US-22 a US-25, en `docs/historias-usuario-fuera-de-alcance/`) ni historias fuera de la solicitada.

## Implementación

- Planificá antes de codificar.
- Mantené el cambio mínimo que satisface la historia.
- Agregá tests en el nivel adecuado.
- No marques nada por intención; solo por evidencia.

## Evidencia y checkboxes

Evidencia válida:

- Test que pasa, con comando y resultado.
- QA con Playwright aprobada.
- Prueba manual registrada con fecha y pasos.
- Comando verificable (build, health, OpenAPI).

Si no hay evidencia, dejá el checkbox sin marcar y listá el pendiente. No infieras que “debería funcionar”.

## Estados

- `Pendiente`: no implementada.
- `Base implementada`: esqueleto funcional, falta completar.
- `Parcial`: parte de los criterios está cubierta.
- `Completada`: todos los criterios y el DoD están verificados.
- `Fuera de alcance`: archivada en `docs/historias-usuario-fuera-de-alcance/`; no se implementa sin pedido explícito.

## Cierre

1. Verificá que todos los criterios y el DoD tengan evidencia.
2. Agregá o actualizá `## Evidencia` con comandos y resultados, sin secretos.
3. Mové el archivo a `docs/historias-usuario-terminadas/`.
4. Actualizá el enlace del índice a `../historias-usuario-terminadas/...`.
5. Cambiá el estado a `Completada`.
6. Si no está completa, no la muevas; actualizá a `Base implementada` o `Parcial`.

## Bloqueos

Reportá en este formato:

```text
Bloqueo: falta US-XX
Motivo: <dependencia concreta>
Impacto: <qué no se puede hacer>
Siguiente paso: <implementar US-XX o pedir decisión>
```

## Cierre de sesión

Devolvé un resumen breve: historia, estado, archivos, tests, checkboxes pendientes, QA y bloqueos.
