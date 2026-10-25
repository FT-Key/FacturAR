---
name: token-efficient-workflow
description: Reduce consumo de tokens en sesiones de opencode con lecturas dirigidas, subagentes, límites de salida, logs mínimos y compactación. Usar en tareas largas o cuando el contexto crezca.
---

# Ahorro de tokens

## Lecturas

- Usá `grep`/`glob` para ubicar primero; después `read` con offset y límite.
- No leas archivos completos si alcanza un fragmento.
- No re-leas archivos sin cambios.
- Preferí `git diff --stat` y `git diff -- archivo` antes que volcar todo el diff.

## Subagentes

- Delegá exploración amplia a `explore` y trabajo de historia a los subagentes.
- Pedí reportes estructurados y breves: archivos, hallazgos, comandos, resultado.
- No pidas logs completos; pedí el resumen y el comando para reproducir.
- Cargá solo las skills relevantes.

## Comandos

- Compilá una vez y después usá `--no-build`.
- Usá `--logger "console;verbosity=minimal"`.
- Filtrá tests por proyecto o nombre cuando sea posible.
- Evitá `docker compose logs` completo; usá `--tail` y el servicio específico.
- Redirigí salidas enormes a archivo y buscá dentro con `grep`.

## Contexto

- Cerrá ciclos de revisión en máximo dos iteraciones.
- Si una hipótesis no se confirma con una búsqueda, cambiá de estrategia; no repitas.
- Resumí decisiones y pendientes antes de una compactación.
- No pegues secretos, connection strings, tokens ni datos personales en prompts o logs.

## Configuración

`opencode.json` limita `tool_output` y activa compactación automática. Si el contexto crece demasiado, pedí al usuario reiniciar la sesión con un resumen.
