# US-07 — Numeración atómica

**Épica:** Emisión  
**Prioridad:** Alta  
**Estimación:** 1 día  
**Requisito:** RF-06  
**Depende de:** US-04  
**Habilita:** US-05, US-08, US-18

## Historia

Como usuario emisor, quiero que cada comprobante reciba un número correlativo sin duplicados para cumplir con la numeración fiscal simulada.

## Contexto

La secuencia se lleva por contribuyente, punto de venta y tipo de comprobante. La asignación usa una operación atómica y participa de la misma transacción que la inserción del comprobante.

## Criterios de aceptación

- [ ] La secuencia se identifica por `ContribuyenteId`, `PuntoVenta` y `TipoComprobante`.
- [ ] El número se obtiene con `findOneAndUpdate` y `$inc`.
- [ ] La asignación del número y la inserción del comprobante ocurren en la misma transacción.
- [ ] Dos emisiones simultáneas obtienen números distintos.
- [ ] Si la transacción falla, el número no queda consumido.
- [ ] El primer número emitido es `1`.
- [ ] El número es correlativo y no se reutiliza.
- [ ] La secuencia no se comparte entre contribuyentes, puntos de venta ni tipos.
- [ ] El índice único de numeración impide duplicados.
- [ ] La consulta de último número autorizado refleja el último número persistido.
- [ ] Hay tests de concurrencia y rollback.

## Escenarios Gherkin

```gherkin
Scenario: Primera emisión
  Given una secuencia inexistente para el punto de venta 1 y factura C
  When se emite el primer comprobante
  Then el número asignado es 1
  And la secuencia queda en 1
```

```gherkin
Scenario: Emisiones concurrentes
  Given dos emisiones simultáneas para la misma secuencia
  When ambas solicitan número
  Then obtienen números distintos y consecutivos
  And no se produce un duplicado
```

```gherkin
Scenario: Rollback
  Given una emisión que falla después de asignar el número
  When la transacción se revierte
  Then el número no queda consumido
  And la siguiente emisión puede usar ese número
```

```gherkin
Scenario: Secuencias independientes
  Given dos puntos de venta del mismo contribuyente
  When se emite un comprobante en cada uno
  Then cada punto de venta tiene su propia secuencia
```

## Tareas técnicas

- [ ] Crear `Secuencia` y `ISecuenciaRepository`.
- [ ] Implementar `ObtenerSiguienteNumeroAsync`.
- [ ] Configurar transacciones de MongoDB.
- [ ] Crear el índice único de secuencias.
- [ ] Integrar la asignación en `EmitirComprobanteService`.
- [ ] Agregar tests de concurrencia y rollback.
- [ ] Documentar la regla de numeración.

## Definition of Done

- [ ] No existen números duplicados en `comprobantes`.
- [ ] La prueba de concurrencia pasa de forma repetida.
- [ ] Un fallo no consume números.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el modelo.

## Contrato técnico

### Modelo de datos

**Colección**

| Colección | Documento | Uso en esta historia |
| --- | --- | --- |
| `secuencias` | `Secuencia` | Contador atómico por `(ContribuyenteId, PuntoVenta, TipoComprobante)`. Se crea de forma implícita en la primera emisión de cada combinación. |
| `comprobantes` | `Comprobante` | Se lee para validar que el número no se repita y para exponer el último autorizado (US-09). |

**Campos de `secuencias`**

| Campo | Tipo | Obligatorio | Nota |
| --- | --- | --- | --- |
| `Id` | `Guid` | Sí | Clave primaria. |
| `ContribuyenteId` | `Guid` | Sí | Tenant. |
| `PuntoVenta` | `int` | Sí | Parte de la clave lógica. |
| `TipoComprobante` | `TipoComprobante` | Sí | Parte de la clave lógica. |
| `UltimoNumero` | `long` | Sí | Último número asignado. Empieza en `0`. |

**Índices**

| Índice | Claves | Único | Introducido por |
| --- | --- | --- | --- |
| `ux_secuencias_contribuyente_punto_tipo` | `ContribuyenteId`, `PuntoVenta`, `TipoComprobante` | Sí | Base actual. |
| `ux_comprobantes_numeracion` | `ContribuyenteId`, `PuntoVenta`, `TipoComprobante`, `Numero` | Sí | Base actual. Es la red de seguridad del correlativo. |

El filtro del `FindOneAndUpdate` debe usar exactamente las mismas claves del índice único, en el mismo orden. Si el filtro se escribiera con otro orden, o sin `ContribuyenteId`, se perdería el aislamiento y el índice no se usaría.

### Endpoints

US-07 no agrega endpoints: es una historia de servicio.

| Método | Ruta | Auth | Body | Respuesta | Errores |
| --- | --- | --- | --- | --- | --- |
| — | — | — | — | — | — |

El número se expone a través de `POST /v1/comprobantes` (US-05) y, para consulta, de `GET /v1/comprobantes/ultimo-autorizado?tipo=1` (US-09).

### Casos de uso y servicios

| Tipo | Nombre | Responsabilidad |
| --- | --- | --- |
| Servicio | `FacturAR.Application.Comprobantes.SecuenciaService` (implementa `ISecuenciaService`) | Valida que el punto de venta esté activo y pide el siguiente número. |
| Repositorio | `FacturAR.Application.Abstractions.ISecuenciaRepository` | `ObtenerSiguienteNumeroAsync(contribuyenteId, puntoVenta, tipo, ct)` devuelve `long`: el número asignado. Crea la secuencia si no existe. |
| Repositorio | `FacturAR.Application.Comprobantes.SecuenciaRepository` (Infrastructure) | Ejecuta el `FindOneAndUpdate` con `$inc` y `IsUpsert`. Es el único lugar donde se toca el contador. |
| Servicio | `FacturAR.Application.Comprobantes.EmitirComprobanteService` (US-05) | Consume el número devuelto y lo persiste. No recalcula ni ajusta. |

El filtro y la actualización del repositorio:

```text
filter : { ContribuyenteId: <tenant>, PuntoVenta: <pv>, TipoComprobante: <tipo> }
update : { $inc: { UltimoNumero: 1 } }
options: { ReturnDocument: After, IsUpsert: true }
```

`ReturnDocument.After` es obligatorio: con `Before` se devolvería el valor previo y el primer número sería `0`.

`IsUpsert: true` es lo que hace la secuencia **implícita**: si el documento no existe, se crea con el contador en `0` y la operación entrega el número `1`. No hay un error de "secuencia no configurada".

### Validaciones

| Regla | Mensaje | Código |
| --- | --- | --- |
| El punto de venta existe pero está inactivo | "El punto de venta está inactivo." | `409 PUNTO_VENTA_INACTIVO` |
| El punto de venta no existe o es de otro contribuyente | "El punto de venta no existe o no pertenece al contribuyente autenticado." | `404 COMPROBANTE_NO_ENCONTRADO` |

- La comprobación del punto de venta ocurre **antes** de tocar la secuencia: si el punto de venta está inactivo, no se consume número.
- **No hay error de secuencia faltante.** La secuencia se crea de forma implícita: si no existe documento en `secuencias` para `(contribuyente, punto de venta, tipo)`, la primera emisión lo crea y recibe el número `1`. No hace falta habilitarla antes por onboarding ni por seed.
- La numeración solo falla por un punto de venta inexistente (`404`) o inactivo (`409 PUNTO_VENTA_INACTIVO`), o por una transacción que no se puede confirmar.

### Cálculos

```text
Numero asignado = Secuencia.UltimoNumero después del $inc
```

- El número se incrementa siempre de a uno: `n → n+1`.
- Con `UltimoNumero = 0`, el primer número es `1`.
- Si la secuencia no existe, el `upsert` la crea en `0` y el número asignado es `1`.
- El número **nunca** se calcula en memoria de la aplicación. No hay `leer + 1`: el valor lo devuelve el documento devuelto por el `FindOneAndUpdate`.
- `UltimoNumero` es `long`: la numeración no tiene tope en el MVP.
- El número completo expuesto es el par `PuntoVenta` + `Numero`, por ejemplo punto de venta 1, número 42 → `0001-00000042` en el nombre de archivo del PDF (US-10).

### Estados

`Secuencia.UltimoNumero` es un contador, no una máquina de estados:

| Estado | Descripción | Transición |
| --- | --- | --- |
| `0` | Secuencia creada y sin números emitidos. | → `1` en la primera emisión. |
| `n` | Último número emitido es `n`. | → `n+1` en la siguiente emisión. |

- La secuencia se crea **de forma implícita**, en la primera emisión de la combinación `(contribuyente, punto de venta, tipo)`: el documento nace con `UltimoNumero = 0` y esa emisión recibe el número `1`. El onboarding o el seed pueden crearla antes, pero no es un prerrequisito.
- No hay reinicio anual, ni yearly closing, ni rangos reservados. Queda fuera del alcance.
- `PuntoVenta.Activo` es la condición habilitante del punto de venta; un punto de venta inactivo bloquea la emisión con `409 PUNTO_VENTA_INACTIVO` y no altera el contador.

### Concurrencia y atomicidad

**Por qué dos emisiones concurrentes no repiten número**

1. MongoDB garantiza que una actualización de un **único documento** es atómica. El `FindOneAndUpdate` con `$inc` sobre `secuencias` toma un lock de escritura a nivel de documento durante la operación: las dos emisiones concurrentes sobre la misma secuencia se serializan y cada una ve el valor ya incrementado por la anterior. No hay ventana de *read-modify-write* en la aplicación, porque la aplicación nunca lee el contador para calcular el número.
2. Cada emisión corre dentro de su propia transacción (`session.WithTransactionAsync`). Si dos transacciones tocan el mismo documento de `secuencias`, MongoDB no puede aplicar ambas a la vez: una recibe un `WriteConflict` (`TransientTransactionError`) y el driver reintenta la transacción completa. Al reintentar, esa transacción obtiene el siguiente número. Por eso los números concurrentes son **distintos y consecutivos**, nunca repetidos.
3. El índice único `ux_comprobantes_numeracion` sobre `(ContribuyenteId, PuntoVenta, TipoComprobante, Numero)` es la red de seguridad: si dos transacciones compartieran número, la segunda inserción fallaría por clave duplicada y su transacción se abortaría. No se persisten dos comprobantes con el mismo número.
4. La creación implícita de la secuencia también es segura: si dos emisiones concurrentes intentan crear la secuencia inexistente, una de ellas recibe una clave duplicada del índice `ux_secuencias_contribuyente_punto_tipo`. En ese caso se reintenta el `FindOneAndUpdate` sin `IsUpsert` y se obtiene el número ya asignado: el número nunca se duplica.

**Rollback**

- La asignación del número y la inserción del comprobante están en la **misma transacción**. Si la inserción falla (por ejemplo, por un error de validación tardía o por una clave duplicada), el `$inc` se revierte y el número **no queda consumido**: la siguiente emisión puede usarlo.
- Por eso la validación de todo el body debe ocurrir **antes** de abrir la transacción y tocar la secuencia: un `400` de validación nunca debe dejar un número consumido.

**Requisitos de infraestructura**

- Las transacciones de MongoDB exigen un deployment con **replica set** (single-node sirve). Atlas M0 ya es replica set; el MongoDB local que usa `docker-compose.yml` debe correr con `--replSet` e inicializarse con `rs.initiate()`. Sin replica set no hay transacciones y el rollback del `$inc` no está garantizado, así que US-07 no se puede dar por cerrada.
- El `Session` de la transacción debe propagarse a **todas** las operaciones de la emisión (secuencia, comprobante y auditoría). Una operación fuera de la sesión no participa del rollback.

### Integración con otras historias

| Historia | Relación |
| --- | --- |
| US-05 | Consume el número y lo persiste. Es la historia que abre la transacción. |
| US-04 | Define el aislamiento por contribuyente, del que depende la clave de la secuencia. |
| US-08 | Las notas usan su propia secuencia por tipo (`2`, `3`, `7`, `8`, `12`, `13`): cada combinación se crea de forma implícita en su primera emisión. |
| US-09 | `GET /v1/comprobantes/ultimo-autorizado?tipo=1` lee el último número persistido; no recalcula nada. |
| US-18 | El seed puede crear las secuencias `FacturaA`, `FacturaB` y `FacturaC` del contribuyente demo, en `0`. No es obligatorio: la creación es implícita. |
| US-27 / US-28 | El onboarding puede crear la secuencia del punto de venta por defecto del contribuyente. No es obligatorio: la creación es implícita. |
| US-13 | La emisión manual desde el portal usa el mismo camino de numeración. |

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-07-01 | No existe `Secuencia` para (contribuyente, punto de venta 1, `FacturaC`). | Emitir una factura C. | Número asignado `1`. La secuencia se crea de forma implícita y queda en `1`. |
| CP-07-02 | Secuencia en `41`. | Emitir otra factura C. | Número asignado `42`. La secuencia queda en `42`. |
| CP-07-03 | Dos emisiones simultáneas de facturas C en el mismo punto de venta. | Emitir las dos al mismo tiempo. | Los números son `n` y `n+1`: distintos y consecutivos. No hay duplicado en `comprobantes`. |
| CP-07-04 | Seis emisiones simultáneas sobre la misma secuencia. | Emitir las seis al mismo tiempo. | Los seis números son correlativos y sin repeticiones; la prueba se repite varias veces para descartar dependencia del orden del scheduler. |
| CP-07-05 | Emisión que falla después de asignar el número (por ejemplo, una clave de idempotencia duplicada). | Procesar la emisión. | La transacción se revierte: la secuencia queda en el valor anterior y la siguiente emisión usa ese número. |
| CP-07-06 | Body inválido (por ejemplo, `importeTotal` inconsistente). | Emitir la factura. | `400 VALIDACION` y la secuencia queda sin cambios: el número no se consumió. |
| CP-07-07 | El contribuyente A tiene secuencia para `FacturaC` en `7`. | El contribuyente B emite una factura C en su punto de venta 1. | El contribuyente B arranca en `1`: las secuencias no se comparten entre contribuyentes. |
| CP-07-08 | Dos puntos de venta del mismo contribuyente. | Emitir una factura C en cada punto de venta. | Cada punto de venta tiene su propio correlativo: el segundo no continúa la numeración del primero. |
| CP-07-09 | El mismo contribuyente emite `FacturaC` y `FacturaB` en el mismo punto de venta. | Emitir una de cada tipo. | Cada tipo tiene su propia secuencia: no comparten correlativo. |
| CP-07-10 | No existe `Secuencia` para (contribuyente, punto de venta 3, `FacturaA`). | Emitir una factura A en el punto de venta 3. | `201` con `numero` 1: la secuencia se crea de forma implícita. |
| CP-07-11 | El punto de venta 2 está inactivo. | Emitir una factura en el punto de venta 2. | `409 PUNTO_VENTA_INACTIVO`. La secuencia queda sin cambios. |
| CP-07-12 | Punto de venta 1 habilitado y secuencia en `5`. | Emitir una factura y forzar el fallo de la inserción del comprobante. | La secuencia vuelve a `5`: el número no queda consumido. |
| CP-07-13 | Secuencia en `9`. | Consultar el último autorizado de `FacturaC`. | Devuelve el número `9`, que es el último persistido. |

## Dependencias técnicas

**Colecciones que deben existir**

| Colección | Introducida por |
| --- | --- |
| `secuencias` | Base actual (`MongoDbContext` y `MongoIndexInitializer`). |
| `comprobantes` | Base actual. |
| `puntosVenta` | Base actual. |
| `eventos` | Base actual, para el evento de auditoría que escribe US-05 dentro de la misma transacción. |

**Índices que deben existir antes de empezar**

| Índice | Claves | Único | Introducido por |
| --- | --- | --- | --- |
| `ux_secuencias_contribuyente_punto_tipo` | `ContribuyenteId`, `PuntoVenta`, `TipoComprobante` | Sí | Base actual. |
| `ux_comprobantes_numeracion` | `ContribuyenteId`, `PuntoVenta`, `TipoComprobante`, `Numero` | Sí | Base actual. |
| `ux_puntos_venta_contribuyente_numero` | `ContribuyenteId`, `Numero` | Sí | Base actual. |

**Entidades**: `Secuencia` (base actual), `Comprobante` (base actual), `PuntoVenta` (base actual).

**Clases de Application**

| Clase | Estado |
| --- | --- |
| `ISecuenciaRepository`, `SecuenciaRepository`, `ISecuenciaService`, `SecuenciaService` | Las introduce US-07. |
| `EmitirComprobanteService` | La introduce US-05 y abre la transacción que US-07 reutiliza. |
| `IComprobanteRepository` | Ya existe; US-05 la extiende. |
| `IEventoAuditoriaRepository` | La introduce US-05. |

**Otros prerrequisitos**

| Prerrequisito | Origen |
| --- | --- |
| Aislamiento por contribuyente resuelto en el contexto | US-04 |
| API key que resuelva `ContribuyenteId` | US-03 |
| MongoDB desplegado como replica set (single-node) para transacciones y rollback | Infraestructura. Atlas M0 ya es replica set; el Mongo local de `docker-compose.yml` debe correr con `--replSet` e inicializarse con `rs.initiate()`. |
| Documentos de `secuencias` previos | No hace falta: la secuencia se crea de forma implícita en la primera emisión de cada combinación. El onboarding (US-27/US-28) o el seed (US-18) pueden crearlos antes, pero es opcional. |

## Notas de alcance

- No implementa reinicio anual de secuencias.
- No implementa numeración por sucursal fuera del punto de venta.
- No implementa autorización de números por AFIP.
