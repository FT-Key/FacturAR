# US-09 — Consultas y último autorizado

**Épica:** Consultas  
**Prioridad:** Alta  
**Estimación:** 1 día  
**Requisito:** RF-08  
**Depende de:** US-03, US-05  
**Habilita:** US-10, US-12, US-16, US-21

## Historia

Como usuario o sistema cliente, quiero consultar comprobantes, ver su detalle y conocer el último número autorizado para operar y conciliar mi facturación.

## Contexto

Todas las consultas se filtran por el `ContribuyenteId` autenticado. La verificación pública por CAE es un endpoint separado y anónimo.

## Criterios de aceptación

- [ ] `GET /v1/comprobantes` lista solo comprobantes del contribuyente autenticado.
- [ ] Se puede filtrar por fecha desde/hasta, punto de venta, tipo de comprobante, estado y receptor.
- [ ] La lista usa paginación con `page` y `pageSize`, con límite máximo.
- [ ] La lista se ordena por fecha de emisión descendente y número descendente.
- [ ] `GET /v1/comprobantes/{id}` devuelve el detalle con ítems.
- [ ] Un id de otra empresa devuelve `404`.
- [ ] `GET /v1/comprobantes/ultimo-autorizado` devuelve el último número por punto de venta y tipo.
- [ ] Si no hay comprobantes, el último autorizado es `0`.
- [ ] Las fechas se interpretan en UTC y se documentan.
- [ ] La respuesta no expone datos internos, hashes ni API keys.
- [ ] La consulta usa índices por contribuyente y fecha.
- [ ] Hay tests de filtros, paginación, aislamiento y último autorizado.

## Escenarios Gherkin

```gherkin
Scenario: Listar comprobantes
  Given una empresa con tres comprobantes emitidos
  When consulta la lista sin filtros
  Then recibe los tres comprobantes de su empresa
  And no recibe comprobantes de otras empresas
```

```gherkin
Scenario: Filtrar por fecha
  Given comprobantes emitidos en octubre y noviembre
  When consulta con desde 2026-11-01 y hasta 2026-11-30
  Then recibe solo los de noviembre
```

```gherkin
Scenario: Detalle de otra empresa
  Given un comprobante de la empresa B
  When un usuario de la empresa A solicita su detalle
  Then la API devuelve 404
```

```gherkin
Scenario: Último autorizado
  Given el último comprobante de factura C tiene número 42
  When consulta el último autorizado para factura C
  Then la API devuelve 42
```

```gherkin
Scenario: Sin comprobantes
  Given una empresa sin comprobantes de nota de débito A
  When consulta el último autorizado para ese tipo
  Then la API devuelve 0
```

## Tareas técnicas

- [ ] Crear DTOs de listado, detalle y último autorizado.
- [ ] Implementar `ListarComprobantesService` y `ConsultarComprobanteService`.
- [ ] Implementar `ObtenerUltimoAutorizadoService`.
- [ ] Crear `GET /v1/comprobantes`, `GET /v1/comprobantes/{id}` y `GET /v1/comprobantes/ultimo-autorizado`.
- [ ] Aplicar filtro de tenant y paginación.
- [ ] Agregar índices de consulta.
- [ ] Agregar tests de filtros, aislamiento y paginación.
- [ ] Documentar parámetros y respuestas en OpenAPI y Postman.

## Definition of Done

- [ ] Un cliente solo ve sus comprobantes.
- [ ] La paginación y los filtros funcionan.
- [ ] El último autorizado refleja la secuencia persistida.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Contrato técnico

Fuente normativa: `docs/contratos-api.md` (§2 errores, §3 identidad y aislamiento, §6 paginación, §14 convenciones de API) y `docs/modelos.md` (colección `comprobantes`). Esta sección no se aparta de esos documentos, salvo el desvío de autenticación declarado en «Autenticación de los endpoints».

### Modelo de datos

US-09 **no agrega colecciones ni entidades** y no escribe en MongoDB: es una historia de lectura.

| Colección | Documento | Qué usa US-09 |
| --- | --- | --- |
| `comprobantes` | `Comprobante` + `ComprobanteItem` | Listado, detalle y último autorizado. |
| `secuencias` | `Secuencia` | Último número autorizado por (contribuyente, punto de venta, tipo). La introduce US-07. |

Índices:

| Índice | Claves | Uso en US-09 | Estado |
| --- | --- | --- | --- |
| `ix_comprobantes_contribuyente_fecha` | `ContribuyenteId: 1, FechaEmision: -1` | Filtro por tenant y rango de fechas. | Ya existe (US-05). |
| `ix_comprobantes_receptor_documento` | `ReceptorDocumentoNumero: 1` | Filtro por receptor. | Ya existe (US-05). |
| `ix_comprobantes_contribuyente_fecha_numero_id` | `ContribuyenteId: 1, FechaEmision: -1, Numero: -1, Id: 1` | Cubre el `sort` completo del listado. | **Nuevo.** Lo agrega US-09 en `MongoIndexInitializer.InitializeAsync`. |

Por qué el índice nuevo: el orden estable del contrato (§6) es `FechaEmision` descendente con desempate, y el criterio de aceptación pide además número descendente. El `sort` efectivo queda en `{ FechaEmision: -1, Numero: -1, Id: 1 }`. Sin el sufijo `Id`, dos comprobantes de distinto punto de venta o tipo pueden compartir fecha y número, el desempate se resuelve en memoria y la paginación puede repetir o saltar ítems entre páginas. `Id` es un `Guid` serializado como binario BSON (subtipo UUID estándar) por `MongoSerialization.Configure()`, de modo que su orden es determinista.

El índice se crea junto con los demás cuando `Mongo__InitializeIndexes=true`.

### Autenticación de los endpoints

Los tres endpoints aceptan **cualquiera** de los dos esquemas de empresa, con la misma política:

| Esquema | Quién lo usa | Requisito |
| --- | --- | --- |
| API key `X-Api-Key` (US-03) | Integraciones | Key activa de un contribuyente activo. |
| Cookie `cookies.contribuyente` (US-04) | Portal (US-12, US-13) | Sesión de empresa con rol `Administrador`, `Emisor` o `Consulta`. |

- La política se declara con `AddAuthenticationSchemes("ApiKey", "cookies.contribuyente")`. La API key no tiene roles: habilita el grupo completo. La cookie sí: un usuario de empresa sin rol reconocido recibe `403 SIN_PERMISOS`.
- El `ContribuyenteId` sale **siempre** de `ITenantContext`, que lee el claim `contribuyenteId` que escriben los dos esquemas por igual. Nunca del body, del query string ni de un header del cliente.
- Un recurso de otro contribuyente responde `404`, nunca `403`: un `403` confirmaría que el recurso existe.

> **Desvío justificado.** `docs/contratos-api.md` §4.2 y US-03 dicen que la cookie de sesión no habilita `/v1/comprobantes/**`. US-05 ya rompe esa regla en `POST /v1/comprobantes` («API key o sesión de portal») y US-13 necesita esa vía para emitir desde el portal. US-09 extiende el mismo criterio a las consultas, porque el portal (US-12) debe leer los comprobantes **sin** duplicar reglas y **sin** tocar MongoDB. Consecuencia a documentar en el mismo cambio: `docs/contratos-api.md` §4.2 y §14 y US-03 deben pasar a decir que API key y cookie de empresa habilitan el mismo grupo de endpoints de comprobantes, y que la cookie sigue sin habilitar `/v1/soap/**` (integraciones), `/v1/admin/**` ni los endpoints anónimos.

### Endpoints

| Método | Ruta | Autenticación | Query | Respuesta | Errores |
| --- | --- | --- | --- | --- | --- |
| `GET` | `/v1/comprobantes` | API key (US-03) o `cookies.contribuyente` + `EmpresaConsulta` (US-04) | `page`, `pageSize`, `desde`, `hasta`, `tipo`, `estado`, `puntoVenta`, `receptor` | `200` `PaginaResponse<ComprobanteResumenResponse>` | `400 VALIDACION`, `401 API_KEY_AUSENTE`, `401 API_KEY_INVALIDA`, `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS`, `503 MONGO_NO_DISPONIBLE`, `500 ERROR_INTERNO` |
| `GET` | `/v1/comprobantes/{id:guid}` | Ídem | — | `200` `ComprobanteDetalleResponse` | Ídem + `404 COMPROBANTE_NO_ENCONTRADO` |
| `GET` | `/v1/comprobantes/ultimo-autorizado` | Ídem | `tipo` (obligatorio), `puntoVenta` (opcional) | `200` `UltimoAutorizadoResponse` | Ídem (sin `404`) |

- La ruta con id declara la restricción `:guid`. Un id que no es `Guid` no llega al handler: responde `400 VALIDACION`.
- `GET /v1/comprobantes/verificar/{cae}` **no cambia**: sigue siendo anónimo y es de US-11.

Respuesta del listado (misma forma que §6 del contrato):

```json
{
  "items": [
    {
      "id": "6ae24e50-1c3e-4f0a-9f3e-9d2f0b7a1c22",
      "tipoComprobante": 11,
      "tipoComprobanteDescripcion": "Factura C",
      "puntoVenta": 1,
      "numero": 42,
      "fechaEmision": "2026-10-25T14:30:00Z",
      "cae": "12345678901234",
      "receptorRazonSocial": "Cliente Final",
      "receptorDocumentoNumero": "20345678901",
      "importeTotal": 121000.00,
      "moneda": "ARS",
      "estado": "Emitido"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 1,
  "totalPages": 1
}
```

Detalle (`ComprobanteDetalleResponse`): `id`, `tipoComprobante`, `tipoComprobanteDescripcion`, `puntoVenta`, `numero`, `fechaEmision`, `cae`, `caeVencimiento`, `emisorCuit`, `emisorRazonSocial`, `receptor` (`documentoTipo`, `documentoNumero`, `razonSocial`, `condicionIva`), `importeNeto`, `importeIva`, `importeTotal`, `moneda`, `estado`, `observaciones` e `items` (`descripcion`, `cantidad`, `precioUnitario`, `alicuotaIva`, `subtotal`).

Último autorizado:

```json
{ "tipoComprobante": 11, "puntoVenta": 1, "numero": 42 }
```

- `puntoVenta` viene `null` cuando la consulta es agregada por tipo (sin `puntoVenta` en el query).
- `numero` es `0` cuando el contribuyente no tiene comprobantes de ese tipo.

Ninguno de los tres DTOs expone `ContribuyenteId`, `IdempotencyKey`, `Hash`, ni datos de otras empresas. `id` sí se expone: es el identificador del recurso y lo usa el portal para abrir el detalle y descargar el PDF.

### Casos de uso y servicios

**Application** (`FacturAR.Application`)

| Tipo | Responsabilidad |
| --- | --- |
| `Comprobantes.ListarComprobantesService` | Aplica el filtro de tenant, arma el `FilterDefinition` y el `sort`, pagina y devuelve el `PaginaResponse`. |
| `Comprobantes.ConsultarComprobanteService` | Busca por `(Id, ContribuyenteId)` y devuelve el detalle o `null`. |
| `Comprobantes.ObtenerUltimoAutorizadoService` | Resuelve el último número autorizado por (contribuyente, punto de venta, tipo). |
| `Comprobantes.FiltroListadoComprobantes` | Value object con los filtros ya normalizados (`Desde`, `Hasta`, `Tipo`, `Estado`, `PuntoVenta`, `Receptor`). Lo consume el repositorio. |
| `Abstractions.IComprobanteRepository` | US-09 agrega `ListarAsync(contribuyenteId, filtro, page, pageSize, ct)` y `GetByIdAsync(contribuyenteId, id, ct)`. Los dos métodos filtran por `ContribuyenteId` **en la consulta**. |
| `Abstractions.ISecuenciaRepository` | La introduce US-07: `ObtenerUltimoNumeroAsync(contribuyenteId, puntoVenta, tipo, ct)` y `ObtenerMaximoUltimoNumeroAsync(contribuyenteId, tipo, ct)`. |

**Infrastructure** (`FacturAR.Infrastructure`)

- `Repositories/ComprobanteRepository`: implementa `ListarAsync` con `CountDocumentsAsync` + `Find(...).Sort(...).Skip(...).Limit(...)`. El `skip` es `(page - 1) * pageSize`.
- `Repositories/SecuenciaRepository`: lo introduce US-07; US-09 solo lo consume.
- `MongoIndexInitializer`: alta de `ix_comprobantes_contribuyente_fecha_numero_id`.

**Api** (`FacturAR.Api`)

- `Comprobantes/ComprobanteConsultaEndpoints.cs`: los tres handlers. Resuelven el tenant con `ITenantContext` y devuelven `ErrorResponse` con los códigos de la tabla.

**Contracts** (`FacturAR.Contracts`)

- `Comunes/PaginaResponse<T>` con `Items`, `Page`, `PageSize`, `Total`, `TotalPages`. Es genérico para que US-12 pueda reutilizarlo.
- `Comprobantes/ComprobanteResumenResponse`, `Comprobantes/ComprobanteDetalleResponse`, `Comprobantes/ComprobanteItemResponse`, `Comprobantes/UltimoAutorizadoResponse`.

### Validaciones

- `page`: entero `>= 1`. Default `1`.
- `pageSize`: entero entre `1` y `100`. Default `20`. Un valor mayor a `100` o menor a `1` responde `400 VALIDACION`; no se recorta en silencio.
- `desde` y `hasta`: fecha `YYYY-MM-DD` o `DateTime` ISO 8601. Se interpretan en **UTC** (`Kind = Utc`).
- `desde` es inclusivo: `FechaEmision >= desde 00:00:00Z`.
- `hasta` es inclusivo del día completo: se compara contra `FechaEmision < (hasta + 1 día) 00:00:00Z`. Así, `hasta=2026-11-30` incluye lo emitido el 30 de noviembre.
- `desde > hasta` responde `400 VALIDACION`.
- `tipo`: entero válido de `TipoComprobante` (1, 2, 3, 6, 7, 8, 11, 12, 13). Cualquier otro valor responde `400 VALIDACION`.
- `estado`: entero válido de `EstadoComprobante` (0, 1, 2, 3). Cualquier otro valor responde `400 VALIDACION`.
- `puntoVenta`: entero `>= 1`.
- `receptor`: se normaliza quitando guiones y espacios y se compara **exacto** contra `ReceptorDocumentoNumero`.
- `id` de ruta: `Guid`. Un valor no parseable responde `400 VALIDACION`, no `404`.
- `tipo` de `ultimo-autorizado` es **obligatorio**: si falta o no es válido responde `400 VALIDACION`.
- Los filtros se combinan con AND. Todos son opcionales salvo `tipo` en `ultimo-autorizado`.

### Paginación y filtros

- Parámetros: `page` (1-based, default `1`) y `pageSize` (default `20`, máximo `100`), tal como fija §6 del contrato.
- `total` es el total de documentos que cumplen el filtro, no el de la página.
- `totalPages` es `ceil(total / pageSize)`; con `total = 0` es `0`.
- Una `page` mayor que `totalPages` devuelve `items: []` con los metadatos correctos. No es un error.
- Orden: `{ FechaEmision: -1, Numero: -1, Id: 1 }`. Es un orden total: no hay dos comprobantes con la misma terna, de modo que la concatenación de las páginas no repite ni saltea ítems.
- Filtros: `desde`, `hasta`, `tipo`, `estado`, `puntoVenta`, `receptor`. Todos se aplican sobre la colección ya filtrada por `ContribuyenteId`.
- El filtro por `receptor` no usa el índice `ix_comprobantes_receptor_documento` de forma directa porque el filtro por tenant tiene prioridad; el índice sirve para acotar candidatos y el filtro por `ContribuyenteId` se aplica siempre.

### Integración con otras historias

| Historia | Punto de integración |
| --- | --- |
| US-03 | Esquema de autenticación `ApiKey` y claim `contribuyenteId`. |
| US-04 | `ITenantContext`, cookie `cookies.contribuyente` y política `EmpresaConsulta`. |
| US-05 | Colección `comprobantes`, entidad `Comprobante`/`ComprobanteItem` e índices existentes. |
| US-07 | `secuencias` e `ISecuenciaRepository` para el último autorizado. |
| US-10 | El PDF se descarga a partir del detalle que devuelve `ConsultarComprobanteService`. |
| US-11 | La verificación pública por CAE sigue siendo un endpoint anónimo y separado. |
| US-12 | El listado, el detalle y la paginación son la fuente del dashboard y del listado del portal. |
| US-13 | La emisión manual reutiliza los servicios de Application, no los endpoints de consulta. |
| US-16 | Las consultas SOAP reutilizan `ListarComprobantesService` y `ConsultarComprobanteService`; el filtro por tenant no se duplica. |
| US-21 | OpenAPI, `FacturAR.Api.http` y la colección Postman se actualizan con los tres endpoints. |

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-01 | Empresa A con tres comprobantes emitidos; empresa B con uno. | `GET /v1/comprobantes` con API key de A, sin filtros. | `200` con `items` de 3 elementos, `total = 3`, `totalPages = 1`. No aparece el comprobante de B. |
| CP-02 | Igual que CP-01. | `GET /v1/comprobantes` con `pageSize=2&page=1` y luego `page=2`. | `200` en ambas: la primera devuelve 2 ítems y `totalPages = 2`, la segunda devuelve 1 ítem. Concatenadas no repiten ni saltan comprobantes. |
| CP-03 | 25 comprobantes emitidos. | `GET /v1/comprobantes?pageSize=10` y recorrer `page=1..3`. | `total = 25`, `totalPages = 3`, páginas de 10, 10 y 5 ítems. Ningún id se repite entre páginas. |
| CP-04 | Comprobantes en octubre y en noviembre de 2026. | `GET /v1/comprobantes?desde=2026-11-01&hasta=2026-11-30`. | Solo los de noviembre. Un comprobante del 30/11 a las 15:00 **entra** en el resultado. |
| CP-05 | Comprobantes de tipo `FacturaA` (1) y `FacturaC` (11). | `GET /v1/comprobantes?tipo=11`. | Solo los de tipo 11. `tipo=99` responde `400 VALIDACION`. |
| CP-06 | Comprobantes `Emitido` y `Anulado`. | `GET /v1/comprobantes?estado=2`. | Solo los anulados. `estado=9` responde `400 VALIDACION`. |
| CP-07 | Comprobante propio y comprobante de otra empresa. | `GET /v1/comprobantes/{id}` con el id propio. | `200` con el detalle, los ítems y los importes. |
| CP-08 | Comprobante de la empresa B. | `GET /v1/comprobantes/{id-de-B}` con la key de A. | `404 COMPROBANTE_NO_ENCONTRADO`. La respuesta no revela que el comprobante existe ni datos de B. |
| CP-09 | Ningún comprobante con ese id. | `GET /v1/comprobantes/{guid-inexistente}`. | `404 COMPROBANTE_NO_ENCONTRADO`. |
| CP-10 | Cualquier endpoint de consulta. | `GET /v1/comprobantes/not-a-guid`. | `400 VALIDACION`. No se consulta MongoDB. |
| CP-11 | El último comprobante de factura C tiene número 42. | `GET /v1/comprobantes/ultimo-autorizado?tipo=11`. | `200` con `numero = 42`. Sin `puntoVenta` la respuesta lo agrega por tipo y devuelve `puntoVenta = null`. |
| CP-12 | Empresa sin comprobantes de nota de débito A. | `GET /v1/comprobantes/ultimo-autorizado?tipo=2`. | `200` con `numero = 0`. No es un `404`. |
| CP-13 | Secuencia `(contribuyente, 1, 11)` con `UltimoNumero = 42` y `(contribuyente, 2, 11)` con `UltimoNumero = 7`. | `GET /v1/comprobantes/ultimo-autorizado?tipo=11&puntoVenta=2`. | `200` con `numero = 7` y `puntoVenta = 2`. |
| CP-14 | Cualquier endpoint de consulta. | Llamarlo sin `X-Api-Key` y sin cookie de sesión. | `401 API_KEY_AUSENTE`. No se ejecuta la consulta. |
| CP-15 | Una cookie de empresa con rol `Consulta`. | `GET /v1/comprobantes` con esa cookie. | `200`. El rol `Consulta` puede leer; el aislamiento por contribuyente sigue igual. |
| CP-16 | Cualquier respuesta de la historia. | Inspeccionar el cuerpo de las respuestas. | Ninguna contiene `contribuyenteId`, `idempotencyKey` ni `hash`. Los importes vienen como números decimales, sin formatear. |
| CP-17 | `pageSize=500`. | `GET /v1/comprobantes?pageSize=500`. | `400 VALIDACION`. `pageSize=100` sí se acepta. |

## Dependencias técnicas

Antes de empezar US-09 tiene que existir:

- **US-03 implementada**: esquema de autenticación `ApiKey` registrado en `AddAuthentication` y claim `contribuyenteId` emitido.
- **US-04 implementada**: `ITenantContext` en Application y su implementación en `FacturAR.Api.Security`, cookie `cookies.contribuyente`, políticas `EmpresaAutenticado` y `EmpresaConsulta`.
- **US-05 implementada**: entidad `Comprobante` y `ComprobanteItem`, índices `ix_comprobantes_contribuyente_fecha` e `ix_comprobantes_receptor_documento`, y `IComprobanteRepository` con `GetByCaeAsync` y `AddAsync`.
- **US-07 implementada**: entidad `Secuencia`, colección `secuencias` e `ISecuenciaRepository` con la consulta del último número. Sin US-07, el último autorizado se resuelve contra `comprobantes` y el criterio «el último autorizado refleja la secuencia persistida» no se cumple.
- **Application**: `ListarComprobantesService`, `ConsultarComprobanteService`, `ObtenerUltimoAutorizadoService` y `FiltroListadoComprobantes`. `FacturAR.Application` no referencia `MongoDB.Driver`.
- **Infrastructure**: `ComprobanteRepository` con los métodos nuevos y `MongoIndexInitializer` con `ix_comprobantes_contribuyente_fecha_numero_id`.
- **Api**: los tres handlers registrados y la política de autorización que acepta los dos esquemas de empresa.
- **Contracts**: `PaginaResponse<T>`, `ComprobanteResumenResponse`, `ComprobanteDetalleResponse`, `ComprobanteItemResponse` y `UltimoAutorizadoResponse`.
- **Tests**: `FacturAR.UnitTests` para la normalización de filtros y los cálculos de paginación; `FacturAR.IntegrationTests` con `MongoFact` para filtros, aislamiento, orden estable y último autorizado.
- **Documentación**: actualizar `docs/contratos-api.md` §4.2, §6 y §14, `docs/manual-integracion.md` §7 (el query del último autorizado pasa de `tipoComprobante` a `tipo`), `docs/postman/FacturAR.postman_collection.json` y `facturAR_backend/src/FacturAR.Api/FacturAR.Api.http`.

## Notas de alcance

- No implementa exportación CSV o Excel; eso corresponde a US-12.
- No implementa búsqueda de texto completo.
- No implementa consultas SOAP; eso corresponde a US-16.
