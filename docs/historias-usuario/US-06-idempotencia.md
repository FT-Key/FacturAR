# US-06 — Idempotencia de emisión

**Épica:** Emisión  
**Prioridad:** Alta  
**Estimación:** 1 día  
**Requisito:** RF-05  
**Depende de:** US-05  
**Habilita:** US-21

## Historia

Como sistema cliente, quiero reintentar una emisión con la misma clave de idempotencia sin duplicar comprobantes para tolerar timeouts y fallos de red.

## Contexto

La clave se envía en el header `Idempotency-Key`. Es única por contribuyente. Si el request se repite con la misma clave y el mismo payload, se devuelve el comprobante original. Si el payload cambia, se rechaza.

## Criterios de aceptación

- [ ] La emisión acepta el header `Idempotency-Key`.
- [ ] La clave es única por contribuyente.
- [ ] Un request repetido con la misma clave y el mismo payload devuelve el comprobante original.
- [ ] No se vuelve a numerar ni a generar un CAE en un reintento idempotente.
- [ ] Un request con la misma clave y payload distinto devuelve `409`.
- [ ] Dos requests simultáneos con la misma clave generan un solo comprobante.
- [ ] La respuesta del reintento conserva el mismo `id`, `numero` y `cae`.
- [ ] La clave se guarda asociada al comprobante y al contribuyente.
- [ ] El índice único impide duplicados.
- [ ] La clave no se usa para otras operaciones.
- [ ] La respuesta documenta el comportamiento idempotente.
- [ ] Hay tests de reintento, conflicto y concurrencia.

## Escenarios Gherkin

```gherkin
Scenario: Reintento exitoso
  Given una emisión exitosa con la clave "pedido-001"
  When el cliente repite el mismo request con la misma clave
  Then la API devuelve el mismo comprobante
  And no se crea otro comprobante
  And no se consume otro número
```

```gherkin
Scenario: Conflicto de payload
  Given una emisión exitosa con la clave "pedido-001"
  When el cliente repite la clave con otro importe
  Then la API devuelve 409 con código "IDEMPOTENCIA_CONFLICTO"
  And no se modifica el comprobante original
```

```gherkin
Scenario: Reintentos concurrentes
  Given dos requests simultáneos con la misma clave
  When ambos llegan a la API
  Then solo uno crea el comprobante
  And el otro devuelve el mismo comprobante o 409 controlado
```

```gherkin
Scenario: Clave repetida en otra empresa
  Given una empresa A usó la clave "pedido-001"
  When la empresa B usa la misma clave
  Then la emisión de B se procesa como una operación nueva
```

## Tareas técnicas

- [ ] Agregar `IdempotencyKey` al comprobante y su índice único por contribuyente.
- [ ] Crear `IIdempotencyService` o integrar la lógica en `EmitirComprobanteService`.
- [ ] Guardar el hash del payload para detectar conflictos.
- [ ] Implementar la búsqueda previa y el manejo de carrera.
- [ ] Devolver el comprobante existente sin renumerar.
- [ ] Agregar tests de reintento, conflicto y concurrencia.
- [ ] Documentar el header y los errores en el manual.

## Definition of Done

- [ ] Un reintento no duplica comprobantes ni números.
- [ ] Un payload distinto con la misma clave devuelve `409`.
- [ ] La concurrencia está cubierta por tests.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Contrato técnico

### Modelo de datos

**Colecciones**

| Colección | Documento | Uso en esta historia |
| --- | --- | --- |
| `comprobantes` | `Comprobante` | Guarda la clave y el hash del payload. El índice `ux_comprobantes_idempotencia` es el árbitro final de la carrera. |

**No hay colección `idempotencia`.** La idempotencia se apoya en el propio comprobante: US-06 no agrega colecciones, ni documentos de control, ni índices nuevos.

**Campos de `comprobantes` que usa US-06**

| Campo | Tipo | Obligatorio | Nota |
| --- | --- | --- | --- |
| `IdempotencyKey` | `string` | Sí | Clave recibida en el header `Idempotency-Key`, tal cual llega. Ya existe en la entidad. |
| `IdempotencyPayloadHash` | `string` | Sí | SHA-256 en hexadecimal minúsculo del payload canónico. **Nuevo: lo agrega US-06.** |

**Índices**

| Índice | Claves | Único | Introducido por |
| --- | --- | --- | --- |
| `ux_comprobantes_idempotencia` | `ContribuyenteId`, `IdempotencyKey` | Sí | Base actual. Es el árbitro de la carrera: garantiza un comprobante por clave y contribuyente. |
| `ux_comprobantes_numeracion` | `ContribuyenteId`, `PuntoVenta`, `TipoComprobante`, `Numero` | Sí | Base actual. |
| `ux_secuencias_contribuyente_punto_tipo` | `ContribuyenteId`, `PuntoVenta`, `TipoComprobante` | Sí | Base actual (lo usa US-07). |

No hay índice TTL: la clave de idempotencia **no expira** (es una nota de alcance explícita de US-06).

### Endpoints

US-06 no crea endpoints: extiende el contrato de `POST /v1/comprobantes` de US-05.

| Método | Ruta | Auth | Headers obligatorios | Body | Respuesta | Errores |
| --- | --- | --- | --- | --- | --- | --- |
| `POST` | `/v1/comprobantes` | API key `X-Api-Key` (US-03) o sesión de portal (US-04) | `X-Api-Key`, `Idempotency-Key` | `EmitirComprobanteRequest` | `201` `ComprobanteResponse` en la primera emisión; `200` `ComprobanteResponse` en el replay, con el mismo `id`, `numero` y `cae` | `400 VALIDACION`, `409 IDEMPOTENCIA_CONFLICTO`, más los de US-05 |

- Todas las respuestas, exitosas o no, devuelven el header `Idempotency-Key` con la clave recibida.
- El header `Idempotency-Key` solo tiene efecto en `POST /v1/comprobantes`. El resto de los endpoints lo ignoran: la clave no se usa para otras operaciones.
- El replay devuelve el **mismo** cuerpo que la emisión original, con `200` y no `201`, para que el cliente distinga "repetido" de "nuevo" por el código de estado.

### Casos de uso y servicios

| Tipo | Nombre | Responsabilidad |
| --- | --- | --- |
| Servicio | `FacturAR.Application.Comprobantes.IdempotenciaService` (implementa `IIdempotenciaService`) | Resuelve el comprobante de una clave ya usada y decide entre replay, conflicto o ejecución nueva. |
| Servicio | `FacturAR.Application.Comprobantes.EmitirComprobanteService` (US-05) | Consulta la idempotencia antes de emitir y escribe `IdempotencyKey` e `IdempotencyPayloadHash` al insertar el comprobante. |
| Calculador | `FacturAR.Application.Comprobantes.PayloadHasher` | Función pura: request → payload canónico → hash. Sin dependencias de infraestructura. |
| Repositorio | `FacturAR.Application.Abstractions.IComprobanteRepository` | US-06 agrega `GetByIdempotencyKeyAsync(contribuyenteId, clave, ct)`. |

`IdempotenciaService` no conoce `MongoDB.Driver`: el acceso a datos queda en la implementación de Infrastructure.

### Validaciones

| Regla | Mensaje | Código |
| --- | --- | --- |
| Falta el header `Idempotency-Key` | "Falta el header Idempotency-Key." | `400 VALIDACION` |
| `Idempotency-Key` con más de 128 caracteres | "El header Idempotency-Key admite hasta 128 caracteres." | `400 VALIDACION` |
| `Idempotency-Key` con caracteres de control | "El header Idempotency-Key no admite caracteres de control." | `400 VALIDACION` |
| Misma clave con un payload de hash distinto | "La clave de idempotencia ya se usó con un payload diferente." | `409 IDEMPOTENCIA_CONFLICTO` |

- El código del conflicto es `IDEMPOTENCIA_CONFLICTO`, tal como fija el escenario Gherkin de esta historia y [`docs/contratos-api.md`](../contratos-api.md) §2.1.
- **No hay límite de intentos ni ventana temporal.** US-06 no implementa expiración de claves de idempotencia, ni ventaneo de reintentos, ni contador de intentos: un reintento con la misma clave y el mismo payload devuelve siempre el comprobante original.

### Cálculos

**Payload canónico**: se construye con los campos que terminan en el comprobante, en un orden fijo de claves (independiente del orden que envíe el cliente):

| Campo | Normalización |
| --- | --- |
| `tipoComprobante` | Entero. |
| `puntoVenta` | Entero. |
| `fechaEmision` | `DateTime` en UTC, formato ISO 8601 con precisión de segundos. `null` si no viene. |
| `receptor.documentoTipo` | `Trim`. `null` y cadena vacía equivalen. |
| `receptor.documentoNumero` | `Trim`. |
| `receptor.razonSocial` | `Trim`. |
| `receptor.condicionIva` | Entero. |
| `items[].descripcion` | `Trim`. |
| `items[].cantidad` | `decimal` redondeado a 2 decimales con `MidpointRounding.AwayFromZero`. |
| `items[].precioUnitario` | Ídem. |
| `items[].alicuotaIva` | Ídem. |
| `importeTotal` | Ídem. `null` si no viene. |
| `observaciones` | `Trim`. `null` si no viene. |
| `comprobanteOriginalId` | `Guid?`. `null` en US-05; lo agrega US-08. |

- El hash se calcula sobre el JSON canónico en UTF-8, con `System.Text.Json` en `camelCase`, sin espacios y con `InvariantCulture`.
- `IdempotencyPayloadHash = SHA-256(canonicalJson)` en hexadecimal minúsculo.
- El orden de los ítems importa: dos requests con los mismos ítems en distinto orden son payloads distintos y producen `409 IDEMPOTENCIA_CONFLICTO`.
- Redondear los importes a 2 decimales antes del hash hace que `100`, `100,0` y `100,00` produzcan el mismo hash.

### Estados

La idempotencia no tiene una máquina de estados propia: la clave vive en el comprobante.

| Estado | Descripción | Transiciones |
| --- | --- | --- |
| `Ausente` | No existe comprobante con `(ContribuyenteId, IdempotencyKey)`: la clave está libre. | → `Resuelta` cuando la emisión se persiste. |
| `Resuelta` | Ya existe un comprobante con esa clave, con su `IdempotencyPayloadHash`. | Estable: los reintentos devuelven ese comprobante. |

- La clave **no expira**: no hay estado "expirada" ni ventana de 24 h. Expirar claves de idempotencia es explícitamente fuera de alcance de esta historia.
- Mientras una emisión está en vuelo, la clave todavía no está persistida: el índice único es el que arbitra esa carrera (ver "Concurrencia y atomicidad").

### Concurrencia y atomicidad

Dos requests simultáneos con la misma clave y el mismo payload se resuelven así:

1. **Camino rápido**: se busca el comprobante por `(ContribuyenteId, IdempotencyKey)`. Si existe, se compara su `IdempotencyPayloadHash`: si difiere, `409 IDEMPOTENCIA_CONFLICTO`; si coincide, se responde `200` con el comprobante original: no se renumera ni se regenera el CAE.
2. **Camino lento**: si no existe comprobante con esa clave, se ejecuta la emisión de US-05 dentro de la transacción, escribiendo `IdempotencyKey` e `IdempotencyPayloadHash`.
3. **Árbitro**: el índice `ux_comprobantes_idempotencia` es único sobre `(ContribuyenteId, IdempotencyKey)`. Si el segundo request llega a insertar, MongoDB devuelve una clave duplicada y la transacción se aborta. El número de secuencia consumido por esa transacción **se revierte** (rollback de US-07).
4. **Resolución**: el que perdió la carrera relee el comprobante por `(ContribuyenteId, IdempotencyKey)`. Si existe y el hash coincide, responde `200` con el mismo `id`, `numero` y `cae`. Si el hash difiere, responde `409 IDEMPOTENCIA_CONFLICTO`.

El alcance de la idempotencia es `(ContribuyenteId, IdempotencyKey)`: la misma clave usada por otro contribuyente es una operación nueva, con su propio comprobante y su propio número.

### Integración con otras historias

| Historia | Relación |
| --- | --- |
| US-05 | Provee `POST /v1/comprobantes` y el `EmitirComprobanteService` al que US-06 envuelve. |
| US-07 | La reversión de la transacción en la carrera es lo que garantiza que un reintento no consuma un número. |
| US-08 | Las notas pasan por el mismo endpoint: también son idempotentes. El `comprobanteOriginalId` entra en el payload canónico. |
| US-03 | Resuelve el `ContribuyenteId` de la API key, que es parte del alcance de la clave. |
| US-13 | La emisión manual del portal también envía `Idempotency-Key`. |

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-06-01 | Emisión exitosa con la clave "pedido-001". | Repetir el mismo request con la misma clave y el mismo body. | `200` con el mismo `id`, `numero` y `cae`. No se crea otro comprobante ni se consume otro número. |
| CP-06-02 | Emisión exitosa con la clave "pedido-001". | Repetir la clave con un importe distinto. | `409 IDEMPOTENCIA_CONFLICTO`. El comprobante original queda intacto. |
| CP-06-03 | Emisión exitosa con la clave "pedido-001". | Repetir la clave con los mismos datos pero `cantidad` expresada como `2` y luego `2,00`. | `200` con el comprobante original: el hash se calcula sobre valores normalizados. |
| CP-06-04 | Emisión exitosa con la clave "pedido-001". | Repetir la clave con los mismos ítems en distinto orden. | `409 IDEMPOTENCIA_CONFLICTO`: el orden de los ítems es parte del payload canónico. |
| CP-06-05 | Sin clave previa. | Emitir sin el header `Idempotency-Key`. | `400 VALIDACION`. No se crea el comprobante. |
| CP-06-06 | Sin clave previa. | Emitir con `Idempotency-Key` de 129 caracteres. | `400 VALIDACION`. No se crea el comprobante. |
| CP-06-07 | Sin clave previa. | Emitir con `Idempotency-Key` de 128 caracteres. | `201`. La clave se acepta en el límite. |
| CP-06-08 | Dos requests simultáneos con la misma clave y el mismo body. | Emitir los dos al mismo tiempo. | Solo uno crea el comprobante. El otro devuelve `200` con el mismo `id`, `numero` y `cae`. El número de secuencia consumido es uno solo. |
| CP-06-09 | Dos requests simultáneos con la misma clave y bodies distintos. | Emitir los dos al mismo tiempo. | Uno crea el comprobante. El otro devuelve `409 IDEMPOTENCIA_CONFLICTO`. |
| CP-06-10 | La empresa A ya usó la clave "pedido-001". | La empresa B emite con la misma clave. | `201`: es una operación nueva, con su propio comprobante y su propio número. |
| CP-06-11 | Emisión exitosa con la clave "pedido-001". | Repetir la misma clave con el mismo body muchas veces seguidas. | Todos los reintentos devuelven `200` con el mismo `id`, `numero` y `cae`. No hay límite de intentos ni `429`. |
| CP-06-12 | Emisión exitosa con la clave "pedido-001" registrada tiempo atrás. | Repetir la clave con el mismo body. | `200` con el comprobante original: la clave de idempotencia no expira. |
| CP-06-13 | Emisión exitosa con la clave "pedido-001". | Repetir el request y observar la respuesta. | El header `Idempotency-Key` de la respuesta trae la clave recibida. |
| CP-06-14 | Emisión exitosa con la clave "pedido-001". | Repetir la clave con el mismo body desde otro contribuyente. | `201` con un comprobante distinto: el alcance de la clave incluye el contribuyente. |

## Dependencias técnicas

**Colecciones que deben existir**

| Colección | Introducida por |
| --- | --- |
| `comprobantes` | Base actual. |
| `secuencias` | Base actual; el número lo asigna US-07. |

No se agrega ninguna colección nueva: la idempotencia vive en `comprobantes`.

**Índices que deben existir antes de empezar**

| Índice | Claves | Único | Introducido por |
| --- | --- | --- | --- |
| `ux_comprobantes_idempotencia` | `ContribuyenteId`, `IdempotencyKey` | Sí | Base actual. Es el árbitro de la carrera. |
| `ux_comprobantes_numeracion` | `ContribuyenteId`, `PuntoVenta`, `TipoComprobante`, `Numero` | Sí | Base actual. |
| `ux_secuencias_contribuyente_punto_tipo` | `ContribuyenteId`, `PuntoVenta`, `TipoComprobante` | Sí | Base actual (lo usa US-07). |

**Entidades**: `Comprobante` (base actual; US-06 le agrega `IdempotencyPayloadHash`) y `Secuencia` (base actual).

**Clases de Application**

| Clase | Estado |
| --- | --- |
| `EmitirComprobanteService` | La introduce US-05. US-06 la extiende para consultar la idempotencia. |
| `IComprobanteRepository` | Ya existe. US-06 agrega `GetByIdempotencyKeyAsync`. |
| `ISecuenciaRepository` | La introduce US-07. La reversión de la transacción depende de ella. |
| `IIdempotenciaService`, `IdempotenciaService`, `PayloadHasher` | Las introduce US-06. |

**Otros prerrequisitos**

| Prerrequisito | Origen |
| --- | --- |
| Autenticación por API key que resuelva `ContribuyenteId` | US-03 |
| Emisión con transacción (numeración + inserción) | US-05 + US-07 |
| MongoDB desplegado como replica set (single-node) | Infraestructura; hoy no está en `docker-compose.yml` |

## Notas de alcance

- No implementa expiración de claves de idempotencia.
- No implementa idempotencia para notas, pagos ni otras operaciones.
- No reemplaza la numeración atómica de US-07.
