# US-08 — Notas de crédito y débito

**Épica:** Emisión  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RF-07  
**Depende de:** US-05  
**Habilita:** US-09, US-10, US-12, US-13

## Historia

Como usuario emisor, quiero emitir notas de crédito y débito asociadas a una factura autorizada para corregir o ajustar una operación.

## Contexto

Una nota debe referenciar un comprobante autorizado del mismo contribuyente. Conserva el tipo A, B o C de la factura original y usa una secuencia propia por tipo.

## Criterios de aceptación

- [ ] Se aceptan notas de débito A (`2`), crédito A (`3`), débito B (`7`), crédito B (`8`), débito C (`12`) y crédito C (`13`).
- [ ] La nota referencia un comprobante autorizado del mismo contribuyente.
- [ ] No se puede referenciar un comprobante de otra empresa.
- [ ] No se puede referenciar un comprobante anulado o rechazado.
- [ ] El tipo de nota corresponde al tipo de la factura original.
- [ ] La nota tiene su propio número correlativo por punto de venta y tipo.
- [ ] La nota tiene su propio CAE y vencimiento.
- [ ] El importe de la nota no puede superar el saldo ajustable de la factura original.
- [ ] Una nota de crédito disminuye el saldo; una de débito lo aumenta.
- [ ] La respuesta `201` incluye el comprobante asociado y el CAE.
- [ ] El comprobante original conserva su número y CAE.
- [ ] Se registra auditoría de la emisión.
- [ ] Hay tests de tipos, referencia inválida, saldo y aislamiento.

## Escenarios Gherkin

```gherkin
Scenario: Nota de crédito válida
  Given una factura A autorizada por 12100
  When se emite una nota de crédito A por 1210
  Then la nota se crea con CAE propio
  And el saldo ajustable de la factura queda en 10890
```

```gherkin
Scenario: Nota de otra empresa
  Given una factura de la empresa A
  When la empresa B intenta emitir una nota sobre esa factura
  Then la API devuelve 404
  And no se crea la nota
```

```gherkin
Scenario: Tipo incompatible
  Given una factura C
  When se intenta emitir una nota de crédito A
  Then la API devuelve 400 con código "TIPO_NOTA_INCOMPATIBLE"
```

```gherkin
Scenario: Importe superior al saldo
  Given una factura con saldo ajustable de 1000
  When se emite una nota de crédito por 1500
  Then la API devuelve 400 con código "IMPORTE_EXCEDIDO"
  And no se consume un número
```

## Tareas técnicas

- [ ] Extender el modelo de comprobante con la referencia al comprobante original.
- [ ] Implementar el cálculo de saldo ajustable.
- [ ] Validar tipo, tenant y estado del comprobante original.
- [ ] Integrar numeración y CAE de US-05 y US-07.
- [ ] Crear el endpoint de notas o reutilizar `POST /v1/comprobantes` con tipo de nota.
- [ ] Registrar auditoría.
- [ ] Agregar tests de notas A, B, C y validaciones.
- [ ] Documentar tipos, referencia y errores.

## Definition of Done

- [ ] Se emiten notas de crédito y débito A, B y C.
- [ ] Las notas quedan asociadas a la factura original.
- [ ] No se ajusta una factura de otra empresa.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Contrato técnico

### Modelo de datos

**Cambios de modelo requeridos**

La entidad `Comprobante` **no tiene hoy** los dos campos que esta historia necesita. Hay que agregarlos en Domain y actualizar [`docs/modelos.md`](../modelos.md) en la historia de documentación.

| Entidad | Campo | Tipo | Nota |
| --- | --- | --- | --- |
| `Comprobante` | `ComprobanteOriginalId` | `Guid?` | Nuevo. `null` en facturas. En una nota apunta al comprobante original que ajusta. |
| `Comprobante` | `ImporteOriginalAjustado` | `decimal?` | Nuevo. `null` en notas. En una factura es el saldo pendiente de esa factura. |

El resto de los campos de `Comprobante` no cambia. Las notas usan la misma colección `comprobantes`, con su propio `TipoComprobante`, su propio `Numero` y su propio `Items`.

**Colecciones**

| Colección | Documento | Uso en esta historia |
| --- | --- | --- |
| `comprobantes` | `Comprobante` | Se inserta la nota y se actualiza el `ImporteOriginalAjustado` del original. |
| `secuencias` | `Secuencia` | La nota consume la secuencia de su propio tipo (US-07). |
| `eventos` | `EventoAuditoria` | Un evento por emisión de nota. |

**Índices**

| Índice | Claves | Único | Estado |
| --- | --- | --- | --- |
| `ux_comprobantes_numeracion` | `ContribuyenteId`, `PuntoVenta`, `TipoComprobante`, `Numero` | Sí | Base actual. Cubre la numeración propia de la nota. |
| `ix_comprobantes_original` | `ContribuyenteId`, `ComprobanteOriginalId` | No | **Nuevo en US-08.** Permite listar las notas de un comprobante original. |
| `ux_comprobantes_cae` | `Cae` | Sí | Base actual. **Único y sin filtro**: cada comprobante, factura o nota, tiene su propio CAE. |
| `ux_comprobantes_idempotencia` | `ContribuyenteId`, `IdempotencyKey` | Sí | Base actual. Aplica también a las notas. |

- `ux_comprobantes_cae` **no se modifica** en US-08: sigue siendo único y sin `partialFilterExpression`. La nota tiene CAE propio, así que no hay CAEs compartidos entre notas y facturas.
- No hay migración de índices que hacer en `MongoIndexInitializer` por este lado.

### Endpoints

US-08 **no crea un endpoint nuevo**: reutiliza `POST /v1/comprobantes` de US-05 con un tipo de nota y el campo `comprobanteOriginalId`.

| Método | Ruta | Auth | Headers obligatorios | Body | Respuesta | Errores |
| --- | --- | --- | --- | --- | --- | --- |
| `POST` | `/v1/comprobantes` | API key `X-Api-Key` (US-03) o sesión de portal (US-04) | `X-Api-Key`, `Idempotency-Key` | `EmitirComprobanteRequest` con `tipoComprobante` de nota y `comprobanteOriginalId` | `201` `ComprobanteResponse` | `400 VALIDACION`, `400 TIPO_NOTA_INCOMPATIBLE`, `400 IMPORTE_EXCEDIDO`, `400 CUIT_INVALIDO`, `401 API_KEY_AUSENTE`, `401 API_KEY_INVALIDA`, `403 CUENTA_INACTIVA`, `403 SIN_PERMISOS`, `404 COMPROBANTE_NO_ENCONTRADO` (original inexistente o de otro contribuyente), `409 COMPROBANTE_ANULADO` (original anulado o rechazado), `409 PUNTO_VENTA_INACTIVO`, `503 MONGO_NO_DISPONIBLE`, `500 ERROR_INTERNO` |

Body de una nota de crédito A:

```json
{
  "tipoComprobante": 3,
  "puntoVenta": 1,
  "comprobanteOriginalId": "6ae24e50-1c3e-4f0a-9f3e-9d2f0b7a1c22",
  "receptor": {
    "documentoTipo": "CUIT",
    "documentoNumero": "20123456786",
    "razonSocial": "Cliente S.A.",
    "condicionIva": 1
  },
  "items": [
    { "descripcion": "Corrección de la factura 0001-00000042", "cantidad": 1, "precioUnitario": 1000, "alicuotaIva": 21 }
  ],
  "observaciones": "Nota de crédito parcial"
}
```

- `comprobanteOriginalId` es obligatorio cuando `tipoComprobante` es un tipo nota, y está prohibido en una factura.
- El `tipoComprobante` admitido es `2`, `3`, `7`, `8`, `12` o `13`. Cualquier otro valor devuelve `400 VALIDACION`.
- La respuesta `201` es el mismo `ComprobanteResponse` de US-05, con `tipoComprobanteDescripcion` de la nota. No expone `ComprobanteOriginalId` ni `ImporteOriginalAjustado`: son campos internos.

### Casos de uso y servicios

| Tipo | Nombre | Responsabilidad |
| --- | --- | --- |
| Servicio | `FacturAR.Application.Comprobantes.EmitirComprobanteService` (US-05) | Un único servicio para facturas y notas: la rama de notas resuelve el original, valida tipo y saldo, consume la secuencia de la nota, persiste la nota y ajusta el saldo del original en la misma transacción. |
| Validador | `FacturAR.Domain.Validation.NotaComprobanteValidator` | Reglas de tipo base, estado del original y saldo. No depende de nada externo. |
| Calculador | `FacturAR.Application.Comprobantes.CalculadoraImportes` (US-05) | La nota calcula sus importes con las mismas fórmulas que una factura. |
| Repositorio | `FacturAR.Application.Abstractions.IComprobanteRepository` | US-08 agrega `GetByIdAsync(contribuyenteId, id, ct)` y `AjustarSaldoAsync(contribuyenteId, originalId, saldoEsperado, delta, ct)`. |
| Repositorio | `FacturAR.Application.Abstractions.ISecuenciaRepository` (US-07) | La nota consume la secuencia de su tipo. |

`AjustarSaldoAsync` es la pieza crítica: aplica el delta con un filtro condicional sobre el saldo leído, de modo que dos notas concurrentes no puedan sobrescribir el mismo saldo.

### Validaciones

| Regla | Mensaje | Código |
| --- | --- | --- |
| `tipoComprobante` no es un tipo nota | "El tipo de comprobante no está habilitado en este endpoint." | `400 VALIDACION` |
| `comprobanteOriginalId` ausente en una nota | "Falta el comprobante original de la nota." | `400 VALIDACION` |
| `comprobanteOriginalId` presente en una factura | "Una factura no admite comprobante original." | `400 VALIDACION` |
| El original no existe o es de otro contribuyente | "El comprobante original no existe o no pertenece al contribuyente autenticado." | `404 COMPROBANTE_NO_ENCONTRADO` |
| El tipo base de la nota no coincide con el del original | "El tipo de nota no es compatible con el comprobante original." | `400 TIPO_NOTA_INCOMPATIBLE` |
| El original es una nota (no una factura) | "Una nota no puede referenciar otra nota." | `400 TIPO_NOTA_INCOMPATIBLE` |
| El original está `Anulado` o `Rechazado` | "El comprobante original está anulado y no admite notas." | `409 COMPROBANTE_ANULADO` |
| Una nota de crédito supera el saldo ajustable del original | "El importe de la nota de crédito supera el saldo ajustable del comprobante original." | `400 IMPORTE_EXCEDIDO` |
| El original ya no tiene saldo (saldo 0) y la nota es de crédito | "El comprobante original no tiene saldo ajustable." | `400 IMPORTE_EXCEDIDO` |
| El receptor de la nota no coincide con el del original *(regla a confirmar)* | "El receptor de la nota debe coincidir con el del comprobante original." | `400 VALIDACION` |

**Reglas del importe y del saldo**

- El límite del saldo ajustable aplica a las **notas de crédito**: una nota de crédito no puede superar el saldo pendiente del original. Si lo supera, `400 IMPORTE_EXCEDIDO` y **no** se consume número ni CAE (la validación corre antes de tocar la secuencia y antes de generar el CAE).
- Una **nota de débito** aumenta el saldo del original y **no está limitada** por él: se puede emitir una nota de débito mayor que el saldo pendiente.
- Interpretación del criterio de aceptación "El importe de la nota no puede superar el saldo ajustable de la factura original": el límite aplica a las notas de crédito, que son las que descuentan del saldo. Las notas de débito lo incrementan.
- La secuencia de la nota se crea de forma implícita (US-07): si no existe para `(contribuyente, punto de venta, tipo de nota)`, se crea y la nota recibe el número `1`. No hay error de secuencia faltante.

**Tipos base y compatibilidad**

| Base | Factura | Nota de débito | Nota de crédito |
| --- | --- | --- | --- |
| A | `1` | `2` | `3` |
| B | `6` | `7` | `8` |
| C | `11` | `12` | `13` |

- Una nota de base A solo puede referenciar una `FacturaA`; de base B, una `FacturaB`; de base C, una `FacturaC`.
- `TIPO_NOTA_INCOMPATIBLE` e `IMPORTE_EXCEDIDO` son intocables: están fijados por los escenarios Gherkin de esta historia y por [`docs/contratos-api.md`](../contratos-api.md) §2.1.

### Cálculos

**Importes de la nota**: se calculan exactamente como en US-05.

```text
Subtotal_i   = Redondear2(Cantidad_i × PrecioUnitario_i)
ImporteNeto  = Redondear2(Σ Subtotal_i)
ImporteIva_i = Redondear2(Subtotal_i × AlicuotaIva_i / 100)
ImporteIva   = Redondear2(Σ ImporteIva_i)
ImporteTotal = Redondear2(ImporteNeto + ImporteIva)
```

El tratamiento del IVA sigue la regla de US-05 según la base de la nota: una nota de base A discrimina IVA (`ImporteTotal = ImporteNeto + ImporteIva`); las notas de base B y C no lo discriminan (`ImporteIva = 0`, `ImporteTotal = ImporteNeto`). El `ImporteTotal` de la nota es el que se descuenta o suma al saldo del original.

**Saldo del comprobante original**

```text
ImporteOriginalAjustado (inicial, en la factura) = ImporteTotal de la factura

Saldo' = Saldo - ImporteTotalNota    si la nota es de CRÉDITO
Saldo' = Saldo + ImporteTotalNota    si la nota es de DÉBITO
```

Equivalente: `Saldo = ImporteTotal - Σ(notas de crédito) + Σ(notas de débito)`.

- El `ImporteTotal` del comprobante original **no** cambia: el ajuste vive en `ImporteOriginalAjustado`.
- Ejemplo del Gherkin: factura A por 12100, nota de crédito A por 1210 → el saldo de la factura queda en 10890.
- La nota guarda `ImporteOriginalAjustado = null` y `ComprobanteOriginalId` apuntando al original.
- El límite `ImporteTotalNota > Saldo` produce `400 IMPORTE_EXCEDIDO` y **solo aplica a las notas de crédito**. La comprobación se hace contra el saldo leído y se vuelve a confirmar con la actualización condicional (§ Concurrencia).
- Una nota de débito no tiene límite de importe: siempre aumenta el saldo.

**CAE de la nota**

La nota es un comprobante más: tiene **su propio CAE**, **su propio vencimiento** y **su propia secuencia** por punto de venta y tipo.

| Tipo de nota | `Cae` | `CaeVencimiento` |
| --- | --- | --- |
| Nota de débito (`2`, `7`, `12`) | CAE propio de `CaeGenerator.Generar()` (14 dígitos aleatorios) | `CaeGenerator.CalcularVencimiento(fechaEmision)`: emisión + 10 días |
| Nota de crédito (`3`, `8`, `13`) | CAE propio de `CaeGenerator.Generar()` (14 dígitos aleatorios) | `CaeGenerator.CalcularVencimiento(fechaEmision)`: emisión + 10 días |

- El comprobante original **conserva su número y su CAE**: la nota no lo modifica. Cada uno tiene su propio CAE, así que `ux_comprobantes_cae` sigue siendo único y sin filtro.
- La verificación pública por CAE devuelve cada comprobante por su propio CAE: la factura y su nota son dos consultas distintas.

### Estados

| Entidad | Transición | Detalle |
| --- | --- | --- |
| Nota | (inexistente) → `Emitido` | La nota nace `Emitido`, igual que una factura. |
| Original | `Emitido` → `Emitido` | El `Estado` del original no cambia. Lo que cambia es `ImporteOriginalAjustado`. |
| Original con saldo 0 | `Emitido` → `Emitido` | Un original totalmente ajustado sigue `Emitido`: no hay estado "cancelado" en el MVP. |

- Una nota no puede ser el original de otra nota: el original debe ser una factura de la misma base. Por eso el `ImporteOriginalAjustado` solo existe en facturas.
- El original debe estar en `Emitido`. `Anulado` (2) y `Rechazado` (3) devuelven `409 COMPROBANTE_ANULADO`.
- La anulación administrativa de un comprobante es US-22 (stretch) y está fuera de alcance.

### Concurrencia y atomicidad

- La emisión de la nota (número, inserción de la nota, ajuste del saldo del original y auditoría) ocurre en **una transacción** de MongoDB, igual que en US-05.
- El ajuste del saldo es una **actualización condicional atómica**:

```text
filter : { _id: originalId, ContribuyenteId: <tenant>, ComprobanteOriginalId: null, ImporteOriginalAjustado: saldoLeido }
update : { $inc: { ImporteOriginalAjustado: delta } }
```

- Si `ModifiedCount == 1`, el ajuste se aplicó. Si `ModifiedCount == 0`, el saldo cambió entre la lectura y la escritura (otra nota concurrente lo consumió) y la operación se rechaza con `400 IMPORTE_EXCEDIDO`. No hay *lost update*: el saldo nunca se sobreescribe con un valor absoluto calculado en memoria.
- El filtro condicional protege el saldo en las **notas de crédito**, que son las que pueden pasarse del saldo disponible. Una **nota de débito** solo aumenta el saldo: su `$inc` es aditivo y no puede violar ningún límite, así que no se rechaza por importe.
- El filtro incluye `ContribuyenteId`: un original de otra empresa nunca se toca.
- Dos notas concurrentes sobre el mismo original se serializan sobre el mismo documento: la primera aplica el delta, la segunda falla el filtro condicional y se rechaza, en lugar de dejar el saldo en negativo.
- El rechazo por `400 IMPORTE_EXCEDIDO` se produce **antes** de consumir número y CAE: la validación del importe se hace contra el saldo leído y, si falla, no se genera CAE ni se incrementa la secuencia.
- El `Session` de la transacción debe propagarse a las tres operaciones (secuencia, inserción de la nota, ajuste del saldo). Una operación fuera de la sesión no participa del rollback.
- **Prerrequisito**: MongoDB como replica set (single-node) para poder usar transacciones. Atlas M0 ya es replica set; el MongoDB local que usa `docker-compose.yml` debe correr con `--replSet` e inicializarse con `rs.initiate()`.

### Integración con otras historias

| Historia | Relación |
| --- | --- |
| US-05 | Provee el endpoint, el cálculo de importes y el CAE. US-08 extiende el modelo de `Comprobante`. |
| US-07 | La nota usa la secuencia de su propio tipo (`2`, `3`, `7`, `8`, `12`, `13`). Cada combinación se crea de forma implícita en su primera emisión. |
| US-06 | Las notas también son idempotentes: el `comprobanteOriginalId` entra en el payload canónico. |
| US-09 | El listado incluye notas; el filtro por tipo distingue facturas de notas. |
| US-11 | La verificación por CAE resuelve cada comprobante por su propio CAE: la nota tiene CAE propio y el original conserva el suyo. |
| US-12 / US-13 | El portal lista notas y permite emitirlas. |
| US-17 | Auditoría y outbox de la emisión de notas. |
| US-18 | El seed puede crear las secuencias de los seis tipos de nota, además de las tres de factura. No es obligatorio: la creación es implícita. |
| US-22 | Anulación administrativa de un comprobante ya emitido (stretch, fuera de alcance). |

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-08-01 | Factura A `Emitido` de 12100, con `ImporteOriginalAjustado` 12100. | Emitir una nota de crédito A por 1210 con un ítem de 1000 más IVA 21. | `201`. La nota tiene `ComprobanteOriginalId` apuntando a la factura y **su propio** `Cae` de 14 dígitos, distinto del de la factura. El saldo de la factura queda en 10890. La factura conserva su número y su CAE. |
| CP-08-02 | Factura B `Emitido` de 12100. | Emitir una nota de crédito B por 1210. | `201`. El tipo base B es válido: la nota se crea y el saldo queda en 10890. |
| CP-08-03 | Factura C `Emitido` de 10000. | Emitir una nota de crédito C por 1000. | `201`. La nota se crea y el saldo de la factura C queda en 9000. |
| CP-08-04 | Factura C `Emitido`. | Emitir una nota de crédito A referenciándola. | `400 TIPO_NOTA_INCOMPATIBLE`. No se crea la nota ni se consume número. |
| CP-08-05 | Factura A de la empresa A. | La empresa B emite una nota de crédito A sobre esa factura. | `404 COMPROBANTE_NO_ENCONTRADO`. No se crea la nota ni se toca el saldo de la factura ajena. |
| CP-08-06 | Factura A con `Estado` `Anulado`. | Emitir una nota de crédito A sobre ella. | `409 COMPROBANTE_ANULADO`. No se crea la nota. |
| CP-08-07 | Factura A con `Estado` `Rechazado`. | Emitir una nota de crédito A sobre ella. | `409 COMPROBANTE_ANULADO`. No se crea la nota. |
| CP-08-08 | Factura A con saldo ajustable 1000. | Emitir una nota de crédito A por 1500. | `400 IMPORTE_EXCEDIDO`. No se crea la nota, no se consume número y no se genera CAE: la secuencia de notas queda sin cambios. |
| CP-08-09 | Factura A con saldo 1000. | Emitir una nota de crédito A por 1000. | `201`. El saldo de la factura queda en 0. Una segunda nota sobre la misma factura devuelve `400 IMPORTE_EXCEDIDO`. |
| CP-08-10 | Factura A con saldo 12100. | Emitir una nota de débito A por 500. | `201`. La nota tiene CAE propio de 14 dígitos y el saldo de la factura sube a 12600. |
| CP-08-11 | Factura A `Emitido`. | Emitir una nota de crédito A sin `comprobanteOriginalId`. | `400 VALIDACION`. No se crea la nota. |
| CP-08-12 | Factura A `Emitido`. | Emitir una factura A con `comprobanteOriginalId`. | `400 VALIDACION`. No se crea el comprobante. |
| CP-08-13 | Factura A `Emitido`. | Emitir una nota de crédito A sobre una nota de crédito A ya emitida. | `400 TIPO_NOTA_INCOMPATIBLE`. No se crea la nota. |
| CP-08-14 | Factura A `Emitido` de 12100 con saldo 12100. | Emitir dos notas de crédito A de 8000 en paralelo. | Una devuelve `201` y la otra `400 IMPORTE_EXCEDIDO`. El saldo nunca queda negativo. |
| CP-08-15 | Factura A `Emitido`. | Emitir una nota de crédito A con un ítem de 1000 y alícuota 21. | `201`. `importeNeto` 1000, `importeIva` 210, `importeTotal` 1210. |
| CP-08-16 | Factura A `Emitido` con saldo 12100, sin `Secuencia` para `NotaCreditoA`. | Emitir una nota de crédito A. | `201` con `numero` 1: la secuencia de notas se crea de forma implícita. |
| CP-08-17 | Factura A `Emitido` de 12100 con una nota de crédito emitida sobre ella. | Consultar por CAE la factura y la nota. | Cada CAE devuelve su propio comprobante: la factura por su CAE y la nota por su CAE propio. |
| CP-08-18 | Factura A `Emitido` con saldo 1000. | Emitir una nota de débito A por 50000. | `201`: la nota de débito no está limitada por el saldo. El saldo de la factura sube a 51000. |
| CP-08-19 | Factura A `Emitido` con saldo 1000. | Emitir una nota de crédito A por 1500. | `400 IMPORTE_EXCEDIDO`. No se crea la nota, no se consume número de la secuencia de notas y no se genera CAE. |

## Dependencias técnicas

**Colecciones que deben existir**

| Colección | Introducida por |
| --- | --- |
| `comprobantes` | Base actual. |
| `secuencias` | Base actual. Su contenido es opcional: la secuencia de cada tipo de nota se crea de forma implícita en la primera emisión. El onboarding (US-27/US-28) o el seed (US-18) pueden crearlas antes. |
| `puntosVenta` | Base actual. |
| `eventos` | Base actual. |

**Índices que deben existir antes de empezar**

| Índice | Claves | Único | Estado |
| --- | --- | --- | --- |
| `ux_comprobantes_numeracion` | `ContribuyenteId`, `PuntoVenta`, `TipoComprobante`, `Numero` | Sí | Base actual. |
| `ux_comprobantes_cae` | `Cae` | Sí | Base actual. **Único y sin filtro**: la nota tiene CAE propio. |
| `ux_comprobantes_idempotencia` | `ContribuyenteId`, `IdempotencyKey` | Sí | Base actual. |
| `ix_comprobantes_original` | `ContribuyenteId`, `ComprobanteOriginalId` | No | **Nuevo en US-08.** |
| `ux_secuencias_contribuyente_punto_tipo` | `ContribuyenteId`, `PuntoVenta`, `TipoComprobante` | Sí | Base actual (lo usa US-07). |

**Entidades**

| Entidad | Estado |
| --- | --- |
| `Comprobante` | Base actual. **US-08 le agrega `ComprobanteOriginalId` y `ImporteOriginalAjustado`.** |
| `ComprobanteItem` | Base actual. Sin cambios. |
| `Secuencia`, `PuntoVenta`, `Contribuyente`, `EventoAuditoria` | Base actual. Sin cambios. |

**Clases de Application**

| Clase | Estado |
| --- | --- |
| `EmitirComprobanteService`, `CalculadoraImportes` | Las introduce US-05. US-08 las extiende con la rama de notas. |
| `NotaComprobanteValidator` (Domain) | La introduce US-08. |
| `IComprobanteRepository` | Ya existe. US-08 agrega `GetByIdAsync` y `AjustarSaldoAsync`. |
| `ISecuenciaRepository` | La introduce US-07. |
| `CaeGenerator` | Ya existe. |

**Contratos**

| Contrato | Estado |
| --- | --- |
| `EmitirComprobanteRequest` | La introduce US-05. US-08 le agrega `ComprobanteOriginalId` (opcional en el tipo, obligatorio en la práctica cuando el tipo es nota). |
| `ComprobanteResponse` | La introduce US-05. US-08 **no** le agrega `ComprobanteOriginalId` ni `ImporteOriginalAjustado`: no son campos públicos. |
| `VerificacionResponse` | La introduce US-11. US-08 **no** lo modifica: no se le agrega `saldoAjustable` ni ningún otro campo. El saldo es interno del comprobante y la verificación pública expone solo los campos del contrato de US-11. |

**Otros prerrequisitos**

| Prerrequisito | Origen |
| --- | --- |
| Endpoint de emisión y cálculo de importes | US-05 |
| Numeración atómica y secuencia por tipo de nota | US-07 |
| Idempotencia sobre el mismo endpoint | US-06 |
| MongoDB desplegado como replica set (single-node) para transacciones | Infraestructura. Atlas M0 ya es replica set; el Mongo local de `docker-compose.yml` debe correr con `--replSet` e inicializarse con `rs.initiate()`. |
| Secuencias de los seis tipos de nota | No hace falta: cada secuencia se crea de forma implícita en la primera emisión de su tipo. El onboarding (US-27/US-28) o el seed (US-18) pueden crearlas antes, pero es opcional. |

## Notas de alcance

- No implementa anulación administrativa; eso corresponde a US-22.
- No implementa notas en moneda extranjera.
- No implementa devoluciones ni pagos.
