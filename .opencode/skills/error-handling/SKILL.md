---
name: error-handling
description: Define errores, excepciones, códigos HTTP, logs, timeouts y cancelación en FacturAR. Usar al crear endpoints, servicios o integraciones que puedan fallar.
---

# Manejo de errores y resiliencia

## Clasificación

- **Error esperado**: dato inválido, recurso inexistente, conflicto, falta de autorización. Devolvé un resultado explícito, no una excepción.
- **Falla inesperada**: Mongo caído, bug, timeout de red. Dejá que suba al `UseExceptionHandler` y registrala sin filtrar detalles al cliente.

## API

- Validación de entrada → `400` con `ErrorResponse(codigo, mensaje)` y código estable en mayúsculas.
- Recurso inexistente → `404` con código estable.
- Conflicto de estado o idempotencia → `409`.
- No autorizado → `401`; prohibido → `403`.
- Error inesperado → `500` sin stack trace, connection string ni datos internos.
- Mantené los códigos existentes: `CAE_INVALIDO`, `COMPROBANTE_NO_ENCONTRADO`.
- Documentá cada código nuevo en README y OpenAPI.

## Servicios

- No atrapes `Exception` para devolver `null` o un mensaje genérico.
- Si reintentás, hacelo solo con operaciones idempotentes y con límite.
- Propagá `CancellationToken`; no lo reemplaces por `CancellationToken.None`.
- Usá timeouts explícitos para llamadas externas y Mongo cuando corresponda.
- Logueá con contexto útil (operación, IDs no sensibles) y sin PII ni secretos.

## Logs

- `LogInformation` para hitos, `LogWarning` para degradación recuperable, `LogError` para fallas inesperadas.
- Nunca loguear connection strings, tokens, API keys, passwords, cuerpos completos con datos personales.
- No loguear el mismo error en cada capa; una vez con contexto suficiente.

## Health y degradación

- `/health` devuelve `503` cuando Mongo no responde, sin tumbar el proceso.
- Un fallo de índice al iniciar no debe impedir el arranque, pero debe quedar como warning.
- Si una funcionalidad no puede operar, devolvé un error claro antes que datos parciales.

## Pruebas

- Testeá el camino feliz, cada error esperado y al menos una falla de dependencia cuando sea viable.
- Verificá que un error no filtre campos internos ni secretos.
