---
name: rest-api-contracts
description: Define contratos REST de FacturAR con rutas /v1, DTOs, códigos HTTP, ErrorResponse, OpenAPI, paginación e idempotencia. Usar al crear endpoints o actualizar Postman/OpenAPI. GraphQL queda fuera del MVP.
---

# Contratos REST

## Alcance

- FacturAR usa REST en el MVP. GraphQL/gRPC es stretch (US-25) y no se implementa sin pedido explícito.
- Versioná rutas con `/v1/...`.
- Usá sustantivos plurales y verbos HTTP con su semántica: `GET` seguro, `POST` creación, `PUT`/`PATCH` actualización, `DELETE` baja.

## DTOs

- Públicos en `FacturAR.Contracts`, como `record` inmutables.
- No devuelvas entidades de Domain ni tipos de Mongo.
- No incluyas campos internos: `IdempotencyKey`, `ContribuyenteId`, documentos de receptor, observaciones privadas, secretos.
- Si un campo es opcional, modelalo explícitamente; no uses `null` para significar estados distintos sin documentarlo.

## Respuestas

- `200` lectura/actualización exitosa; `201` creación con `Location`; `204` sin contenido.
- `400` validación con código estable; `401`/`403` autenticación/autorización; `404` inexistente; `409` conflicto o idempotencia.
- Errores de negocio con `ErrorResponse(codigo, mensaje)` y documentación en README.
- Errores inesperados con ProblemDetails/500, sin detalles internos.
- Fechas ISO 8601 UTC; dinero como número decimal o string consistente con el contrato existente.

## Validación

- Validá parámetros de ruta, query y body en el borde.
- No confíes en IDs del cliente para autorización.
- Rechazá CAE/CUIT con formato inválido antes de consultar Mongo.
- Devolvé mensajes útiles pero sin filtrar datos de otras empresas.

## Idempotencia y paginación

- Emisión usa `Idempotency-Key` y devuelve el mismo resultado ante reintentos.
- Listados: `page`/`pageSize` o cursor, con límite máximo y orden estable.
- Documentá el contrato de paginación en OpenAPI.

## OpenAPI y Postman

- Anotá `Produces`, códigos de error y nombres de endpoints.
- Mantené `docs/postman/FacturAR.postman_collection.json` y `FacturAR.Api.http` sincronizados.
- Si cambia un contrato, actualizá tests, frontend y colección en el mismo cambio.

## Tests

- Cubrí éxito, validación, no encontrado, conflicto y ausencia de campos internos.
- Verificá que OpenAPI incluya la ruta en Development.
