---
name: mongodb-modeling
description: Diseña consultas, índices y pipelines de MongoDB para FacturAR con embebido, ESR, Decimal128, paginación y transacciones. Usar al tocar repositorios, índices, agregaciones o rendimiento de Mongo.
---

# Modelado y consultas MongoDB

## Esquema

- Embebé datos que siempre se leen juntos y tienen cardinalidad baja; referenciá colecciones independientes o de alto crecimiento.
- No embebas arrays sin límite. Si puede crecer, usá colección aparte.
- Mantené `ContribuyenteId` en documentos de negocio para aislamiento y filtros.
- Dinero: `decimal` → `Decimal128`. Fechas: UTC.
- GUID: la app registra `GuidRepresentation.Standard` en `MongoSerialization.Configure()`.

## Índices

- Diseñá con ESR: campos de igualdad, luego orden, luego rango.
- Índices únicos para CUIT, CAE, numeración e idempotencia.
- Índices parciales/TTL solo con una razón clara y documentada.
- Evitá índices que dupliquen el prefijo de otro.
- Verificá el plan con `explain` antes de afirmar que una consulta es eficiente.
- En tests, la base debe tener un nombre corto (`facturar_it_*`, máximo 38 bytes).

## Consultas

- Proyectá solo los campos necesarios; nunca devuelvas la entidad completa a un DTO público.
- Paginá con límite y cursor/orden estable; no uses `skip` grande sin evaluar.
- Ordená por campos indexados.
- Usá `Builders<T>` o filtros tipados; no concatenes JSON controlado por el usuario.
- Evitá N+1 y `$lookup` sin límite; considerá `$facet` para conteo + página.
- En agregaciones, poné `$match` y `$project` lo antes posible; limitá antes de `$lookup` cuando sea posible.
- No uses `$where` ni evaluación de JavaScript.

## Escrituras y concurrencia

- Operaciones multi-documento: transacción con sesión en replica set.
- Para secuencias, `FindOneAndUpdate` atómico con filtro por contribuyente, punto de venta y tipo.
- Idempotencia: índice único por contribuyente + clave; capturá el duplicado y devolvé el resultado existente.
- No hagas “leer, decidir, escribir” sin control de concurrencia.

## Operación

- Reutilizá el `IMongoClient`; no crees uno por request.
- Timeouts de selección de servidor razonables; no bloquees el arranque si Atlas no responde.
- En tests, eliminá la base temporal al finalizar.
- Si el MCP de MongoDB está disponible, usalo para inspeccionar índices y planes; no expongas la connection string.

## Verificación

- Testeá el índice único, el filtro multiempresa y la consulta principal.
- Medí con `explain` si hay riesgo de colección grande.
