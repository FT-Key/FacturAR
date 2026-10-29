# FacturAR — guía para agentes

## Contexto

FacturAR es un simulador de facturación electrónica argentina con onboarding de empresas. El alcance obligatorio está en `docs/historias-usuario/` (US-01 a US-21 y US-27 a US-30) y se implementa en un único sprint; US-19, US-20 y US-31 están completadas y archivadas en `docs/historias-usuario-terminadas/`. Las US-22 a US-25 son stretch y no se implementan sin pedido explícito.

Stack: .NET 10, ASP.NET Core Minimal API, Blazor Web App (Server) + Razor Pages, MongoDB 8+ con `MongoDB.Driver`, xUnit, Docker Compose y GitHub Actions.

## Mapa de la solución

```text
facturAR_backend/src/
  FacturAR.Domain/          Entidades, enums y validaciones. Sin dependencias de infraestructura.
  FacturAR.Application/     Casos de uso e interfaces. Sin MongoDB.Driver.
  FacturAR.Infrastructure/  MongoDB, repositorios, índices y serialización.
  FacturAR.Contracts/       DTOs compartidos entre API y frontend.
  FacturAR.Api/             Minimal API, DI, health, OpenAPI y mapeo HTTP.
facturAR_backend/tests/
  FacturAR.UnitTests/
  FacturAR.IntegrationTests/
facturAR_frontend/src/FacturAR.Web/  Blazor Server + Razor Pages. Nunca accede a MongoDB.
docs/
  historias-usuario/
  historias-usuario-terminadas/
  postman/
  contratos-api.md
  plan-implementacion.md
  despliegue-azure.md
  proceso-alta-empresa.md
  manual-integracion.md
  requisitos-funcionales.md
  requisitos-no-funcionales.md
  modelos.md
```

## Comandos

```powershell
dotnet build FacturAR.slnx -c Release
dotnet test FacturAR.slnx -c Release --no-build --logger "console;verbosity=minimal"
dotnet format FacturAR.slnx --verify-no-changes
docker compose --env-file .env up --build -d
docker compose ps
```

- API local: `http://localhost:5171`
- Portal local: `http://localhost:5116`
- API Docker: `http://localhost:8080`
- Portal Docker: `http://localhost:8081`
- OpenAPI: `/openapi/v1.json` en Development
- Health: `/health` (503 si MongoDB no responde) y `/health/live` (siempre 200, no consulta MongoDB)
- `/` y `/health` informan `version` y `commit` de la build

Los tests con `MongoFact` se omiten si no existe `MONGO_CONNECTION_STRING` o `Mongo__ConnectionString`. Cuando corren, usan una base temporal corta (`facturar_it_*`, máximo 38 bytes) y la eliminan al final.

## Reglas de código

- `docs/contratos-api.md` es el contrato transversal normativo: catálogo de errores, roles, autenticación, idempotencia, paginación, emisión y endpoints. Si una historia y ese documento se contradicen, manda el contrato.
- No agregar comentarios al código salvo que el usuario lo pida.
- Mantener límites de capas: Domain no conoce Infrastructure; Application no conoce el driver de Mongo; Web no conoce Mongo.
- DTOs públicos en `FacturAR.Contracts`. No exponer entidades ni campos internos.
- El onboarding es solicitud → aprobación → invitación → activación. No hay registro libre.
- El CUIT no prueba titularidad; la aprobación manual y la invitación al email son el control del MVP.
- El `ContribuyenteId` se resuelve desde la sesión o la API key, nunca desde el body.
- Tokens de invitación y API keys se guardan hasheados; el valor completo se muestra una vez.
- Un fallo de email no revierte la aprobación ni la creación de la empresa.
- `SuperAdmin` aprueba y rechaza; `Soporte` solo consulta y reenvía invitaciones.
- Usar `decimal` para dinero, UTC para fechas y `CancellationToken` en operaciones asíncronas.
- Preferir `async`/`await`, validación en el borde y excepciones solo para fallas inesperadas.
- No agregar paquetes nuevos sin justificar la necesidad y verificar que no exista una alternativa en el stack.
- Respetar el formato y los patrones existentes. Evitar sobreingeniería y abstracciones sin uso real.
- Nunca imprimir, loguear ni commitear secretos, connection strings, tokens ni datos personales.

## Despliegue

- Plataforma objetivo: Azure App Service con contenedores de Docker Hub. **No se usa Kubernetes**; la decisión está en `docs/plan-implementacion.md` §2.
- La CI valida build, formato y tests, y construye las imágenes sin publicarlas. La CD publica en Docker Hub y despliega en Azure solo si la CI del mismo commit fue exitosa.
- El smoke test del CD compara el `commit` de `/health/live` con el SHA esperado: si no coinciden, el deploy falla.
- El rollback automático está configurado en el job `rollback` del CD. El rollback manual se dispara con `workflow_dispatch` y `modo=rollback` + `image_tag=<sha>`.
- El plan completo de despliegue está en `docs/despliegue-azure.md`.

## Historias de usuario

1. Leer `docs/historias-usuario/README.md` y solo la historia solicitada.
2. Verificar dependencias y bloqueos antes de escribir código. Si falta una historia previa, detenerse y reportar el bloqueo.
3. Implementar exclusivamente el alcance de esa historia. No avanzar a la siguiente ni tocar stretch.
4. Marcar un checkbox solo con evidencia (test, comando, QA o prueba manual registrada). Si no hay evidencia, dejarlo sin marcar y listar lo pendiente.
5. Al completar todos los criterios y el Definition of Done, mover el archivo a `docs/historias-usuario-terminadas/`, actualizar el enlace y el estado en el índice.
6. Estados válidos: `Pendiente`, `Base implementada`, `Parcial`, `Completada`, `Stretch`.

## Git, ramas y commits

- Ramas de historia: `feature/us-XX-slug` o `fix/us-XX-slug`, todo en minúsculas.
- Ramas auxiliares: `chore/slug`, `docs/slug`, `refactor/slug`, `test/slug`, `perf/slug`, `ci/slug` o `build/slug`.
- La misma regla se valida en `.github/workflows/ci.yml`.
- Commits convencionales: `feat(scope): ...`, `fix(scope): ...`, `test(scope): ...`, `docs(scope): ...`, `refactor(scope): ...`, `chore(scope): ...`.
- Hacer varios commits pequeños por intención, no uno gigante.
- Pull request siempre contra `development`; nunca mergear automáticamente.
- Nunca commitear `.env`, secretos, `bin/`, `obj/`, `TestResults/` ni artefactos.
- No usar `git push --force`, `git reset --hard` ni `git clean` sin autorización explícita.
- Si no hay repositorio Git, remoto o MCP de GitHub, no inventar URLs: pushear lo que se pueda y reportar qué falta.

## Tests

- Todo comportamiento nuevo debe tener test en el nivel adecuado.
- Preferir tests unitarios para reglas y tests de integración para endpoints, Mongo y configuración.
- Los tests de integración usan `WebApplicationFactory` y no requieren servicios externos salvo los marcados `MongoFact`.
- No desactivar ni borrar tests para hacer pasar la suite.
- Comando de cierre: `dotnet test FacturAR.slnx -c Release --no-build --logger "console;verbosity=minimal"`.

## Documentación

- Mantener sincronizados README, OpenAPI, Postman (`.http` y `docs/postman/`), `docs/proceso-alta-empresa.md`, `docs/manual-integracion.md` y las historias.
- Documentar códigos de error y decisiones relevantes; el onboarding no se documenta solo en una historia.
- No crear documentación nueva si la existente alcanza.

## Ahorro de tokens

- Usar `glob`/`grep` y lecturas dirigidas con offset; no leer archivos completos si alcanza un fragmento.
- Delegar exploración amplia a `explore` y trabajo de historia a los subagentes.
- Pedir a los subagentes reportes estructurados y breves, no volcados de logs.
- Usar `--no-build` después de compilar y `--logger "console;verbosity=minimal"`.
- Evitar re-leer archivos sin cambios y repetir la misma búsqueda.
- Limitar a dos ciclos de revisión/corrección por historia.
- Nunca pegar secretos ni connection strings en prompts, logs o commits.

## Agentes

El agente primario `facturar-orchestrator` coordina:

1. `facturar-planner`: plan y alcance.
2. `facturar-implementer`: código de producción.
3. `facturar-reviewer`: revisión de calidad.
4. `facturar-test-engineer`: tests.
5. `facturar-qa`: validación con Playwright si está disponible.
6. `facturar-docs`: checkboxes, índice y documentación.
7. `facturar-release`: rama, commits, push y PR.

Las skills están en `.opencode/skills/`. Cargar solo las relevantes para la tarea.
