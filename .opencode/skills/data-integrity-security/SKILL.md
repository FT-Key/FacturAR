---
name: data-integrity-security
description: Protege integridad y seguridad de datos en FacturAR con aislamiento multiempresa, validación, idempotencia, transacciones y manejo de secretos. Usar al tocar Mongo, autenticación, emisión o configuración.
---

# Integridad y seguridad de datos

## Aislamiento multiempresa

- Toda consulta o escritura de negocio debe filtrar por `ContribuyenteId` obtenido del contexto autenticado, nunca de un ID enviado por el cliente.
- No confíes en IDs de ruta o body para autorizar. Verificá pertenencia antes de leer o escribir.
- Un `404` es preferible a un `403` que confirme la existencia de datos de otra empresa.

## Validación

- Validá en el borde: formato de CUIT, CAE, enums, rangos, longitudes y campos obligatorios.
- Revalidá invariantes críticas en Domain o Application.
- Rechazá payloads con campos inesperados si el contrato es cerrado.
- Normalizá antes de comparar (trim, mayúsculas, cultura invariante) cuando corresponda.

## Integridad

- Usá índices únicos para CUIT, CAE, numeración e idempotencia.
- Operaciones multi-documento (secuencia + comprobante + outbox) deben usar transacción en replica set.
- Asegurá atomicidad con `FindOneAndUpdate` o transacciones; no leas y escribas en dos pasos sin control de concurrencia.
- La emisión debe ser idempotente: misma clave → mismo resultado, sin duplicar comprobantes.
- Guardá dinero como `Decimal128`/`decimal` y fechas en UTC.
- No borres datos de auditoría sin una historia que lo autorice.

## Secretos

- La connection string y las API keys viven en variables de entorno o secretos, nunca en el repo.
- No imprimas `.env`, connection strings, tokens ni hashes en logs o reportes.
- Si una API key se muestra una vez, guardá solo su hash/prefix; nunca el valor completo.
- En tests, usá valores ficticios y bases temporales; no datos reales.

## Autorización

- API keys: comparar hash, revocar por estado, limitar alcance y registrar uso sin exponer la clave.
- Sesiones del portal: cookie segura, `HttpOnly`, `SameSite`, expiración y antiforgery en formularios.
- No implementes autorización “porque el frontend lo oculta”; validala siempre en el backend.

## Pruebas

- Cubrí acceso cruzado entre contribuyentes, idempotencia, duplicados por índice único y validaciones.
- Verificá que las respuestas públicas no expongan campos internos.
