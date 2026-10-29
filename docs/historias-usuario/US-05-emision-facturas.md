# US-05 — Emisión de facturas A, B y C

**Épica:** Emisión  
**Prioridad:** Alta  
**Estimación:** 3 días  
**Requisito:** RF-04  
**Depende de:** US-03, US-07  
**Habilita:** US-06, US-08, US-09, US-11, US-13, US-17, US-18, US-21, US-24

## Historia

Como usuario emisor o sistema cliente, quiero emitir una factura con sus ítems para obtener un número correlativo y un CAE simulado, respetando la condición frente al IVA del emisor y del receptor.

## Contexto

La emisión usa el `ContribuyenteId` autenticado. El número y el CAE se asignan dentro de una transacción. La factura se persiste con una copia histórica del emisor y del receptor.

## Criterios de aceptación

- [ ] Se aceptan facturas A (`1`), B (`6`) y C (`11`) con los códigos AFIP correspondientes.
- [ ] El emisor se resuelve desde la sesión o la API key; no se acepta en el body.
- [ ] Se valida que el contribuyente y el punto de venta estén activos y pertenezcan al emisor.
- [ ] Se valida el receptor: tipo y número de documento, razón social y condición IVA.
- [ ] Se validan los ítems: descripción, cantidad mayor que cero, precio no negativo y alícuota permitida.
- [ ] Una factura A se emite a responsables inscriptos y discrimina IVA.
- [ ] Una factura B se emite a consumidor final, monotributista o exento; el IVA se incluye y no se discrimina.
- [ ] Una factura C se emite a consumidor final y no discrimina IVA.
- [ ] El total se calcula como neto + IVA y no se acepta un total inconsistente.
- [ ] La emisión asigna número y CAE dentro de una transacción.
- [ ] El CAE tiene 14 dígitos, es único y vence a los 10 días.
- [ ] La respuesta `201` incluye id, número, CAE, vencimiento, importes y estado.
- [ ] Un error de validación devuelve `400` con `ErrorResponse` y detalles por campo.
- [ ] Un punto de venta ajeno o inexistente devuelve `404`.
- [ ] Un contribuyente inactivo devuelve `403`.
- [ ] La emisión registra un evento de auditoría.
- [ ] El comprobante queda disponible para consulta y verificación por CAE.

## Escenarios Gherkin

```gherkin
Scenario: Emitir una factura C
  Given una empresa monotributista autenticada con el punto de venta 1 activo
  When emite una factura C para consumidor final por 10000
  Then la API devuelve 201
  And el número asignado es el siguiente de la secuencia
  And el CAE tiene 14 dígitos
  And el comprobante queda persistido
```

```gherkin
Scenario: Factura A con IVA discriminado
  Given una empresa responsable inscripto y un receptor responsable inscripto
  When emite una factura A con alícuota 21
  Then el importe neto es la suma de los subtotales
  And el IVA se discrimina en la respuesta
  And el total es neto más IVA
```

```gherkin
Scenario: Total inconsistente
  Given un request de factura con un total que no coincide con neto más IVA
  When se procesa la emisión
  Then la API devuelve 400 con código "VALIDACION"
  And no se consume un número de secuencia
```

```gherkin
Scenario: Punto de venta ajeno
  Given una API key de la empresa A
  When intenta emitir con un punto de venta de la empresa B
  Then la API devuelve 404
  And no se crea el comprobante
```

```gherkin
Scenario: CAE único
  Given dos emisiones simultáneas
  When ambas generan CAE
  Then los CAE son distintos
  And el índice único impide duplicados
```

## Tareas técnicas

- [ ] Crear `Comprobante`, `ComprobanteItem` y enums si no existen.
- [ ] Crear `IEmitirComprobanteService` y `IComprobanteRepository`.
- [ ] Implementar validaciones de emisor, receptor, ítems e importes.
- [ ] Implementar el cálculo de neto, IVA y total por tipo.
- [ ] Integrar la asignación atómica de número de US-07.
- [ ] Generar el CAE con `CaeGenerator` y calcular el vencimiento.
- [ ] Persistir el comprobante y sus ítems en una transacción.
- [ ] Crear `POST /v1/comprobantes` con autenticación por API key.
- [ ] Registrar auditoría.
- [ ] Agregar tests de factura A, B, C, validaciones y aislamiento.
- [ ] Documentar el contrato, ejemplos y errores en OpenAPI, Postman y manual.

## Definition of Done

- [ ] Se emiten facturas A, B y C con numeración y CAE correctos.
- [ ] El comprobante se recupera por CAE.
- [ ] No se consumen números ni se persisten comprobantes ante errores.
- [ ] El aislamiento por empresa está cubierto por tests.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Contrato técnico

### Modelo de datos

**Colecciones**

| Colección | Documento | Uso en esta historia |
| --- | --- | --- |
| `comprobantes` | `Comprobante` | Se inserta un documento por emisión, con `Items` embebido. |
| `secuencias` | `Secuencia` | No la escribe US-05: US-07 la incrementa dentro de la misma transacción. |
| `puntosVenta` | `PuntoVenta` | Se lee para validar existencia, pertenencia y estado del punto de venta. |
| `contribuyentes` | `Contribuyente` | Se lee para validar el estado del emisor y copiar sus datos al comprobante. |
| `eventos` | `EventoAuditoria` | Se inserta un evento por emisión. |

**Campos de `comprobantes` y su origen** (los tipos completos están en [`docs/modelos.md`](../modelos.md)):

| Campo | Tipo | Origen | Nota |
| --- | --- | --- | --- |
| `Id` | `Guid` | aplicación | `Guid.NewGuid()` antes de insertar. |
| `ContribuyenteId` | `Guid` | contexto de autenticación | Nunca se lee del body. |
| `EmisorCuit` | `string` | `Contribuyente.Cuit` | Copia histórica del emisor. |
| `EmisorRazonSocial` | `string` | `Contribuyente.RazonSocial` | Copia histórica del emisor. |
| `PuntoVenta` | `int` | body | Debe existir y estar activo para el contribuyente. |
| `TipoComprobante` | `TipoComprobante` | body | En US-05 solo `1`, `6` y `11`. |
| `Numero` | `long` | US-07 | Sale de `FindOneAndUpdate` con `$inc`. |
| `FechaEmision` | `DateTime` (UTC) | body opcional | Si no viene, `DateTime.UtcNow`. |
| `Cae` | `string` | `CaeGenerator.Generar()` | 14 dígitos aleatorios, sin dígito verificador: el CAE es simulado y no reproduce el algoritmo de AFIP. |
| `CaeVencimiento` | `DateTime` (UTC) | `CaeGenerator.CalcularVencimiento(fechaEmision)` | Fecha de emisión + 10 días. Valor fijo en el MVP: US-18 no lo parametriza. |
| `ReceptorDocumentoTipo` | `string` | body | `99` = consumidor final. |
| `ReceptorDocumentoNumero` | `string` | body | CUIT o documento del receptor. |
| `ReceptorRazonSocial` | `string` | body | Solo puede quedar vacía en tipo C. |
| `ReceptorCondicionIva` | `CondicionIva` | body | Condición frente al IVA del receptor. |
| `ImporteNeto` | `decimal` | cálculo | Persistido como `Decimal128`. |
| `ImporteIva` | `decimal` | cálculo | Persistido como `Decimal128`. |
| `ImporteTotal` | `decimal` | cálculo | Persistido como `Decimal128`. |
| `Moneda` | `string` | configuración | `Facturacion__Moneda`; default `ARS`. |
| `Estado` | `EstadoComprobante` | fijo | `Emitido` (1). |
| `IdempotencyKey` | `string` | header `Idempotency-Key` | US-06 define la semántica del reintento; US-05 solo lo exige y lo persiste. |
| `Observaciones` | `string?` | body opcional | Máximo 500 caracteres. |
| `Items` | `List<ComprobanteItem>` | body | De 1 a 500 ítems. |
| `CreatedAt` | `DateTime` (UTC) | aplicación | |

**Índices** (ya creados en `MongoIndexInitializer`; US-05 no agrega ninguno):

| Índice | Claves | Único | Uso |
| --- | --- | --- | --- |
| `ux_comprobantes_cae` | `Cae` | Sí | Garantiza CAE único. |
| `ux_comprobantes_numeracion` | `ContribuyenteId`, `PuntoVenta`, `TipoComprobante`, `Numero` | Sí | Garantiza que no haya dos comprobantes con el mismo número. |
| `ux_comprobantes_idempotencia` | `ContribuyenteId`, `IdempotencyKey` | Sí | Cierra la carrera de reintentos (US-06). |
| `ix_comprobantes_contribuyente_fecha` | `ContribuyenteId`, `FechaEmision` | No | Listado y paginación (US-09). |
| `ix_comprobantes_receptor_documento` | `ReceptorDocumentoNumero` | No | Consulta por receptor. |

### Endpoints

| Método | Ruta | Auth | Headers obligatorios | Body | Respuesta | Errores |
| --- | --- | --- | --- | --- | --- | --- |
| `POST` | `/v1/comprobantes` | API key `X-Api-Key` (US-03) o sesión de portal (US-04) | `X-Api-Key`, `Idempotency-Key` | `EmitirComprobanteRequest` | `201` `ComprobanteResponse` | `400 VALIDACION`, `400 EMISION_INVALIDA`, `400 CUIT_INVALIDO`, `401 API_KEY_AUSENTE`, `401 API_KEY_INVALIDA`, `403 CUENTA_INACTIVA`, `403 SIN_PERMISOS`, `404 COMPROBANTE_NO_ENCONTRADO` (punto de venta inexistente o ajeno), `409 PUNTO_VENTA_INACTIVO`, `503 MONGO_NO_DISPONIBLE`, `500 ERROR_INTERNO` |

US-06 agrega a este mismo endpoint el `409 IDEMPOTENCIA_CONFLICTO`.

Body (`FacturAR.Contracts.Comprobantes.EmitirComprobanteRequest`):

```json
{
  "tipoComprobante": 1,
  "puntoVenta": 1,
  "fechaEmision": "2026-10-28T15:30:00Z",
  "receptor": {
    "documentoTipo": "CUIT",
    "documentoNumero": "20123456786",
    "razonSocial": "Cliente S.A.",
    "condicionIva": 1
  },
  "items": [
    { "descripcion": "Servicio de prueba", "cantidad": 2, "precioUnitario": 5000, "alicuotaIva": 21 }
  ],
  "importeTotal": 12100,
  "observaciones": "Texto opcional"
}
```

- `fechaEmision`, `importeTotal` y `observaciones` son opcionales. Si viene `importeTotal`, debe coincidir con el total calculado.
- `comprobanteOriginalId` no existe todavía en el body: lo agrega US-08 para los tipos nota.

Respuesta `201` (`FacturAR.Contracts.Comprobantes.ComprobanteResponse`):

```json
{
  "id": "6ae24e50-1c3e-4f0a-9f3e-9d2f0b7a1c22",
  "tipoComprobante": 1,
  "tipoComprobanteDescripcion": "Factura A",
  "puntoVenta": 1,
  "numero": 42,
  "fechaEmision": "2026-10-28T15:30:00Z",
  "cae": "12345678901234",
  "caeVencimiento": "2026-11-07T00:00:00Z",
  "receptor": {
    "documentoTipo": "CUIT",
    "documentoNumero": "20123456786",
    "razonSocial": "Cliente S.A.",
    "condicionIva": 1
  },
  "importeNeto": 10000.00,
  "importeIva": 2100.00,
  "importeTotal": 12100.00,
  "moneda": "ARS",
  "estado": "Emitido"
}
```

Los DTOs públicos viven en `FacturAR.Contracts.Comprobantes`. La API no expone entidades de Domain ni campos internos (por ejemplo `IdempotencyKey`, `Items` en crudo o `ContribuyenteId`).

El `ErrorResponse` del contrato transversal tiene solo `Codigo` y `Mensaje` ([`docs/contratos-api.md`](../contratos-api.md) §2). El detalle por campo de los errores de validación es una **extensión opcional futura**: si se agrega, se hace con campos opcionales del DTO en una historia posterior, sin alterar `Codigo` ni `Mensaje`. En esta historia los `400` de validación no enumeran los campos que fallaron.

### Casos de uso y servicios

| Tipo | Nombre | Responsabilidad |
| --- | --- | --- |
| Servicio | `FacturAR.Application.Comprobantes.EmitirComprobanteService` (implementa `IEmitirComprobanteService`) | Orquesta: valida emisor, punto de venta, receptor, ítems e importes; calcula importes; pide el número (US-07); genera el CAE; inserta el comprobante y registra auditoría. |
| Servicio | `FacturAR.Application.Comprobantes.CalculadoraImportes` | Función pura: ítems → subtotal, neto, IVA y total. Sin dependencias de infraestructura. |
| Validador | `FacturAR.Domain.Validation.ComprobanteValidator` | Reglas de receptor, ítems e importes por tipo de comprobante. Vive en Domain porque no depende de nada externo. |
| Repositorio | `FacturAR.Application.Abstractions.IComprobanteRepository` | Ya existe: `GetByCaeAsync`, `AddAsync`. US-05 agrega `GetByIdAsync`, `GetByNumeroAsync` y `AjustarSaldoAsync` (US-08). |
| Repositorio | `FacturAR.Application.Abstractions.ISecuenciaRepository` | La introduce US-07: `ObtenerSiguienteNumeroAsync(contribuyenteId, puntoVenta, tipo, ct)`. |
| Repositorio | `FacturAR.Application.Abstractions.IPuntoVentaRepository` | US-05: `GetByNumeroAsync(contribuyenteId, numero, ct)`. |
| Repositorio | `FacturAR.Application.Abstractions.IContribuyenteRepository` | US-05: `GetByIdAsync(contribuyenteId, ct)`. |
| Repositorio | `FacturAR.Application.Abstractions.IEventoAuditoriaRepository` | US-05: `AddAsync(evento, ct)`. La entity `EventoAuditoria` ya existe; US-17 la amplía con `AdministradorId`, `ActorTipo` y outbox. |
| Generador | `FacturAR.Application.Comprobantes.CaeGenerator` | Ya existe: `Generar()` y `CalcularVencimiento(fechaEmision)`. |

Ninguno de estos tipos conoce `MongoDB.Driver`. La única implementación que lo conoce es la de Infrastructure.

### Validaciones

| Regla | Mensaje | Código |
| --- | --- | --- |
| Falta el header `Idempotency-Key` | "Falta el header Idempotency-Key." | `400 VALIDACION` |
| `Idempotency-Key` con más de 128 caracteres | "El header Idempotency-Key admite hasta 128 caracteres." | `400 VALIDACION` |
| Falta el header `X-Api-Key` | "Falta el header X-Api-Key." | `401 API_KEY_AUSENTE` |
| API key inexistente, revocada o con hash distinto | "La API key no es válida." | `401 API_KEY_INVALIDA` |
| Rol `Consulta` (o cualquier rol que no sea `Emisor` ni `Administrador`) | "El rol del principal no permite emitir comprobantes." | `403 SIN_PERMISOS` |
| Contribuyente inactivo | "El contribuyente está inactivo." | `403 CUENTA_INACTIVA` |
| `tipoComprobante` ausente o distinto de `1`, `6`, `11` | "El tipo de comprobante no está habilitado en este endpoint." | `400 VALIDACION` |
| `puntoVenta` ausente o menor o igual a 0 | "El punto de venta es obligatorio." | `400 VALIDACION` |
| Punto de venta inexistente o de otro contribuyente | "El punto de venta no existe o no pertenece al contribuyente autenticado." | `404 COMPROBANTE_NO_ENCONTRADO` |
| Punto de venta existente pero inactivo | "El punto de venta está inactivo." | `409 PUNTO_VENTA_INACTIVO` |
| `items` vacío o con más de 500 elementos | "El comprobante debe tener entre 1 y 500 ítems." | `400 VALIDACION` |
| `items[].descripcion` vacía o con más de 500 caracteres | "Cada ítem debe tener descripción de 1 a 500 caracteres." | `400 VALIDACION` |
| `items[].cantidad` menor o igual a 0 | "La cantidad de cada ítem debe ser mayor que cero." | `400 VALIDACION` |
| `items[].precioUnitario` negativo | "El precio unitario no puede ser negativo." | `400 VALIDACION` |
| `items[].alicuotaIva` fuera de la lista permitida | "La alícuota de IVA no está permitida." | `400 VALIDACION` |
| Emisor con `CondicionIva` `ConsumidorFinal` que emite una factura A | "Un emisor consumidor final no puede emitir una factura A." | `400 EMISION_INVALIDA` |
| Tipo A sin CUIT válido en el receptor | "El receptor de una factura A debe tener un CUIT válido." | `400 CUIT_INVALIDO` |
| Tipo B sin `99` y sin CUIT válido en el receptor | "El documento del receptor no es válido." | `400 CUIT_INVALIDO` |
| Tipo C con `documentoTipo` distinto de `99` | "La factura C se emite a consumidor final." | `400 VALIDACION` |
| `condicionIva` del receptor no admisible para el tipo | "La condición frente al IVA del receptor no es admisible para este tipo de comprobante." | `400 VALIDACION` |
| `razonSocial` del receptor vacía en tipo A o B | "La razón social del receptor es obligatoria." | `400 VALIDACION` |
| `observaciones` con más de 500 caracteres | "Las observaciones admiten hasta 500 caracteres." | `400 VALIDACION` |
| `importeTotal` enviado distinto del total calculado | "El importe total no coincide con el cálculo de los ítems." | `400 VALIDACION` |

- **No hay error de secuencia faltante.** La secuencia se crea de forma implícita (US-07): si no existe para `(contribuyente, punto de venta, tipo)`, el primer comprobante la crea y recibe el número `1`.
- **Alícuota en tipos B y C (nota, no error).** En B y C el IVA no se discrimina, así que la `alicuotaIva` de los ítems **no se aplica** al cálculo: `ImporteIva = 0` y `ImporteTotal = ImporteNeto`. Rechazar una `alicuotaIva` distinta de 0 en tipo C sería una validación opcional y futura; en esta historia no es un error bloqueante.
- El error de validación no enumera los campos que fallaron: el `ErrorResponse` solo lleva `Codigo` y `Mensaje`. El detalle por campo es una extensión opcional futura.

**Reglas de receptor por tipo**

| Tipo | `documentoTipo` | `documentoNumero` | `condicionIva` | `razonSocial` |
| --- | --- | --- | --- | --- |
| A (`1`) | Se persiste literal. Solo `99` tiene significado en el MVP. | CUIT válido (11 dígitos, dígito verificador). | `ResponsableInscripto` | Obligatoria. |
| B (`6`) | `99` o un valor persistente cualquiera. | Obligatorio: si `documentoTipo` es `99` puede ser DNI o CUIT; si no, debe ser un CUIT válido. | `ConsumidorFinal`, `Monotributo` o `Exento` | Obligatoria. |
| C (`11`) | `99` | Opcional. | `ConsumidorFinal` | Opcional. |

### Cálculos

```text
Redondear2(x)      = Math.Round(x, 2, MidpointRounding.AwayFromZero)
Subtotal_i         = Redondear2(Cantidad_i × PrecioUnitario_i)
ImporteNeto        = Redondear2(Σ Subtotal_i)
ImporteIva_i       = Redondear2(Subtotal_i × AlicuotaIva_i / 100)   ← solo tipo A
ImporteIva         = Redondear2(Σ ImporteIva_i)                     ← solo tipo A
ImporteTotal       = Redondear2(ImporteNeto + ImporteIva)
```

- Todos los importes son `decimal`. En MongoDB se persisten como `Decimal128`. Nunca `double` ni `float`.
- El redondeo a 2 decimales es `MidpointRounding.AwayFromZero`: `10,005` se redondea a `10,01`, no a `10,00`.
- Alícuotas admitidas: `0`, `5`, `10,5`, `21`, `27` (`Facturacion__AlicuotasPermitidas`).
- El cálculo de `ImporteIva` por ítem solo aplica a la factura A. En B y C el IVA no se discrimina: `ImporteIva = 0` y `ImporteTotal = ImporteNeto`.

**Reglas por tipo de comprobante**

| Tipo | ImporteNeto | ImporteIva | ImporteTotal |
| --- | --- | --- | --- |
| A | `Σ Subtotal` | `Σ ImporteIva_i` (IVA discriminado) | `ImporteNeto + ImporteIva` |
| B | `Σ Subtotal` | `0` (el IVA está incluido en el precio unitario y no se discrimina) | `ImporteNeto` |
| C | `Σ Subtotal` | `0` (no discrimina IVA; la `alicuotaIva` de los ítems no se aplica) | `ImporteNeto` |

Ejemplo (tipo A): 2 × 5000 = 10000 de subtotal; IVA 21 % = 2100; total 12100.

Ejemplo (tipo B): 1 × 12100 con el IVA incluido en el precio unitario; `ImporteNeto` 12100, `ImporteIva` 0, `ImporteTotal` 12100.

Ejemplo (tipo C): 1 × 10000; `ImporteNeto` 10000, `ImporteIva` 0, `ImporteTotal` 10000.

**Consistencia del total**: si el body trae `importeTotal`, se redondea a 2 decimales y se compara con el total calculado. Si difieren, la operación se rechaza **antes** de tocar la secuencia.

### Estados

`Comprobante.Estado` es un `EstadoComprobante` persistido como entero.

| Transición | Estado | Quién la dispara | Alcance |
| --- | --- | --- | --- |
| (inexistente) → `Emitido` | `Emitido` (1) | `EmitirComprobanteService` al insertar | Implementada en US-05. |
| `Emitido` → `Anulado` | `Anulado` (2) | Anulación administrativa | US-22, stretch. Fuera de alcance. |
| `Emitido` → `Rechazado` | `Rechazado` (3) | — | No hay endpoint en el MVP. |

- `Borrador` (0) no se usa en US-05: no existe un endpoint que cree borradores.
- El comprobante **nace** en `Emitido`: el `Estado` por defecto de la entidad ya es `Emitido`, pero la emisión lo escribe explícitamente para no depender del valor inicial de la clase.
- Un comprobante `Anulado` o `Rechazado` no admite nuevas operaciones (lo usa US-08 al validar el comprobante original de una nota).

### Concurrencia y atomicidad

- La emisión completa (incremento de la secuencia + inserción del comprobante + evento de auditoría) ocurre dentro de **una transacción** de MongoDB, abierta con `session.WithTransactionAsync` para que el driver reintente los `TransientTransactionError` (por ejemplo, un `WriteConflict`).
- **Requisito de infraestructura**: las transacciones de MongoDB exigen un deployment con **replica set** (single-node sirve). Atlas M0 ya es replica set; el MongoDB local que usa `docker-compose.yml` debe correr con `--replSet` e inicializarse con `rs.initiate()`. Sin replica set, la numeración atómica de US-07 no es garantizable.
- El número nunca se calcula en memoria: lo devuelve `FindOneAndUpdate` con `$inc` sobre `secuencias` (US-07).
- El CAE se genera en memoria (`CaeGenerator.Generar()`, 14 dígitos aleatorios sin dígito verificador), pero se persiste dentro de la misma transacción. El índice `ux_comprobantes_cae` impide que dos comprobantes compartan CAE.
- Si algo falla después del `$inc`, la transacción se revierte: no queda comprobante persistido ni número consumido.
- Dos emisiones concurrentes del mismo contribuyente y punto de venta obtienen números distintos, garantizado por la atomicidad del `$inc` y por `ux_comprobantes_numeracion`.
- La carrera de reintentos con la misma `Idempotency-Key` la cierra `ux_comprobantes_idempotencia` (US-06).

### Integración con otras historias

| Historia | Relación |
| --- | --- |
| US-03 | Provee `X-Api-Key` y la resolución de `ContribuyenteId`. |
| US-04 | Provee la sesión del portal, los roles (`Emisor`, `Administrador`, `Consulta`) y `CUENTA_INACTIVA`. |
| US-07 | Provee el número. US-05 lo consume y no lo calcula. |
| US-06 | Define la semántica de `Idempotency-Key`. US-05 solo exige el header y persiste el valor. |
| US-08 | Agrega `comprobanteOriginalId` al body y los tipos nota al mismo endpoint. |
| US-09 | Lee `comprobantes` para listar y consultar: US-05 persiste `FechaEmision`, `Estado` y `Cae` con los nombres actuales. |
| US-11 | Verifica por CAE: US-05 garantiza que `Cae` sea único y que `Estado` sea `Emitido`. |
| US-13 | Emisión manual desde el portal: reutiliza el mismo caso de uso con la sesión en lugar de la API key. |
| US-17 | Formaliza auditoría y outbox. US-05 escribe solo el evento de auditoría, sin outbox. |
| US-18 | Aporta `Facturacion__AlicuotasPermitidas` y `Facturacion__Moneda`. El vencimiento del CAE es fijo (10 días) y US-18 no lo parametriza. |

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-05-01 | Contribuyente responsable inscripto activo, punto de venta 1 activo, `Secuencia` de `FacturaA` en 0, API key con rol `Emisor`. | Emitir factura A con un ítem `2 × 5000`, alícuota 21 y `importeTotal` 12100. | `201`. `importeNeto` 10000, `importeIva` 2100, `importeTotal` 12100, `numero` 1, `cae` de 14 dígitos, `estado` "Emitido". |
| CP-05-02 | Misma precondición. | Emitir factura C a consumidor final (`documentoTipo` "99") con un ítem `1 × 10000`, alícuota 0. | `201`. `importeNeto` 10000, `importeIva` 0, `importeTotal` 10000. |
| CP-05-03 | Misma precondición. | Emitir factura B con un ítem `1 × 12100` (IVA incluido en el precio unitario), alícuota 21. | `201`. `importeNeto` 12100, `importeIva` 0, `importeTotal` 12100: la factura B incluye el IVA y no lo discrimina. |
| CP-05-04 | Receptor con documento `123456789` (no es un CUIT válido). | Emitir factura A para ese receptor. | `400 CUIT_INVALIDO`. No se inserta comprobante ni se consume número. |
| CP-05-05 | Receptor con `condicionIva` `ConsumidorFinal`. | Emitir factura A para ese receptor. | `400 VALIDACION`. No se inserta comprobante ni se consume número. |
| CP-05-06 | Solicitud con `importeTotal` 9999 y ítems que suman 12100. | Emitir la factura. | `400 VALIDACION`. La secuencia sigue en el mismo valor: no se consumió número. |
| CP-05-07 | API key del contribuyente A; el punto de venta 2 pertenece al contribuyente B. | Emitir una factura con `puntoVenta` 2. | `404 COMPROBANTE_NO_ENCONTRADO`. No se crea el comprobante. |
| CP-05-08 | El punto de venta 1 existe con `Activo = false`. | Emitir una factura con `puntoVenta` 1. | `409 PUNTO_VENTA_INACTIVO`. No se crea el comprobante. |
| CP-05-09 | No existe `Secuencia` para (contribuyente, punto de venta 1, `FacturaC`). | Emitir una factura C en el punto de venta 1. | `201`. La secuencia se crea de forma implícita: `numero` 1 y el documento de `secuencias` queda en `UltimoNumero = 1`. |
| CP-05-10 | Principal con rol `Consulta`. | Emitir una factura. | `403 SIN_PERMISOS`. No se crea el comprobante. |
| CP-05-11 | Contribuyente con `Activo = false`. | Emitir una factura. | `403 CUENTA_INACTIVA`. No se crea el comprobante. |
| CP-05-12 | API key válida. | Emitir una factura sin el header `Idempotency-Key`. | `400 VALIDACION`. No se crea el comprobante. |
| CP-05-13 | Sin header `X-Api-Key`. | Emitir una factura. | `401 API_KEY_AUSENTE`. |
| CP-05-14 | API key con prefijo inexistente. | Emitir una factura. | `401 API_KEY_INVALIDA`. |
| CP-05-15 | Ítem con `cantidad` 0 y otro con `precioUnitario` -1. | Emitir una factura con esos ítems. | `400 VALIDACION`. No se crea el comprobante. El `ErrorResponse` lleva `Codigo` y `Mensaje`; el detalle por campo es una extensión opcional futura. |
| CP-05-16 | Ítem con `alicuotaIva` 12. | Emitir una factura. | `400 VALIDACION`. No se crea el comprobante. |
| CP-05-17 | Factura C con un ítem de alícuota 21. | Emitir la factura C. | `201`: la factura C no discrimina IVA, así que `importeIva` es 0 y `importeTotal` es `importeNeto`. La `alicuotaIva` del ítem no se aplica. |
| CP-05-18 | Dos emisiones simultáneas del mismo contribuyente y punto de venta. | Emitir dos facturas al mismo tiempo. | Ambas devuelven `201` con `numero` distintos y consecutivos, y `cae` distintos. |
| CP-05-19 | Contribuyente con `CondicionIva` `ConsumidorFinal`. | Emitir una factura A. | `400 EMISION_INVALIDA`. No se inserta comprobante ni se consume número. |
| CP-05-20 | Contribuyente con `CondicionIva` `ResponsableInscripto`. | Emitir una factura A para un receptor responsable inscripto. | `201`. `importeNeto` 10000, `importeIva` 2100, `importeTotal` 12100. |

## Dependencias técnicas

**Colecciones que deben existir**

| Colección | Introducida por |
| --- | --- |
| `comprobantes` | Base actual (`MongoDbContext` y `MongoIndexInitializer`). |
| `secuencias` | Base actual; su contenido es opcional porque la secuencia se crea de forma implícita en la primera emisión (US-07). El onboarding (US-27/US-28) o el seed (US-18) pueden crearlas con `UltimoNumero = 0`. |
| `puntosVenta` | Base actual; el contenido por contribuyente lo crea el onboarding (US-27/US-28) o el seed (US-18). |
| `contribuyentes` | Base actual; el contenido lo crea el onboarding (US-27/US-28). |
| `eventos` | Base actual (la entity `EventoAuditoria` ya existe). |

**Índices que deben existir antes de empezar**

| Índice | Claves | Único | Introducido por |
| --- | --- | --- | --- |
| `ux_comprobantes_cae` | `Cae` | Sí | Base actual. |
| `ux_comprobantes_numeracion` | `ContribuyenteId`, `PuntoVenta`, `TipoComprobante`, `Numero` | Sí | Base actual. |
| `ux_comprobantes_idempotencia` | `ContribuyenteId`, `IdempotencyKey` | Sí | Base actual (lo usa US-06). |
| `ix_comprobantes_contribuyente_fecha` | `ContribuyenteId`, `FechaEmision` | No | Base actual. |
| `ux_secuencias_contribuyente_punto_tipo` | `ContribuyenteId`, `PuntoVenta`, `TipoComprobante` | Sí | Base actual (lo usa US-07). |
| `ux_puntos_venta_contribuyente_numero` | `ContribuyenteId`, `Numero` | Sí | Base actual. |

**Entidades y enums**: `Comprobante`, `ComprobanteItem`, `Secuencia`, `PuntoVenta`, `Contribuyente`, `EventoAuditoria` (base actual); `TipoComprobante`, `EstadoComprobante`, `CondicionIva`, `RolUsuario` (base actual).

**Clases de Application**

| Clase | Estado |
| --- | --- |
| `CaeGenerator` | Ya existe. US-05 la usa. `Generar()` devuelve 14 dígitos aleatorios sin dígito verificador y `CalcularVencimiento(fechaEmision)` fija el vencimiento en 10 días. US-18 no parametriza el plazo. |
| `VerificarComprobanteService` | Ya existe. No se modifica en US-05. |
| `IComprobanteRepository` | Ya existe con `GetByCaeAsync` y `AddAsync`. US-05 la extiende. |
| `ISecuenciaRepository` | La introduce US-07. US-05 no puede arrancar sin ella. |
| `IPuntoVentaRepository`, `IContribuyenteRepository`, `IEventoAuditoriaRepository` | Las introduce US-05. |
| `IEmitirComprobanteService`, `EmitirComprobanteService`, `CalculadoraImportes` | Las introduce US-05. |
| `ComprobanteValidator` (Domain) | La introduce US-05. |

**Otros prerrequisitos**

| Prerrequisito | Origen |
| --- | --- |
| Autenticación por API key que resuelva `ContribuyenteId` | US-03 |
| Sesión de portal y resolución de roles | US-04 |
| Numeración atómica y repositorio de secuencias | US-07 |
| MongoDB desplegado como replica set (single-node) para transacciones | Infraestructura. Atlas M0 ya es replica set; el Mongo local de `docker-compose.yml` debe correr con `--replSet` e inicializarse con `rs.initiate()`. |

## Notas de alcance

- No implementa notas de crédito o débito; eso corresponde a US-08.
- No implementa idempotencia; eso corresponde a US-06.
- No implementa PDF ni QR; eso corresponde a US-10.
- No llama a AFIP/ARCA.
