# Contratos transversales de la API

Este documento fija las convenciones que comparten **todas** las historias de usuario de FacturAR. Es normativo: si una historia necesita apartarse de acá, debe decirlo explícitamente en su propio archivo y justificarlo.

Sirve para dos cosas:

1. Que el equipo implemente cada historia sin inventar contratos incompatibles entre sí.
2. Que un tercero que consuma la API entienda el contrato completo sin leer el código.

## 1. Convenciones generales

| Aspecto | Regla |
| --- | --- |
| Prefijo de rutas | `/v1` para la API de negocio. `/health`, `/health/live` y `/` quedan fuera del versionado. |
| Formato | JSON en request y response. `Content-Type: application/json`. |
| Nombres de campos | `camelCase` (configuración por defecto de `System.Text.Json` en Minimal API). |
| Fechas | ISO 8601 en UTC, formato `2026-10-28T15:30:00Z`. Se usa `DateTime` con `Kind = Utc`. |
| Montos | `decimal` en el contrato y `Decimal128` en MongoDB. Nunca `double` ni `float`. |
| Identificadores | `Guid` en formato string (`"6ae24e50-..."`). |
| Documentación | OpenAPI en `/openapi/v1.json` en Development. |
| Puerto local | API `5171`, portal `5116`. En Docker: API `8080`, portal `8081`. |

> **Excepción SOAP.** La fachada de emisión y consultas por SOAP (US-15, US-16) usa SOAP 1.1 sobre XML en `/soap`; se documenta en §7.6. El resto de la API de negocio sigue siendo JSON.

## 2. Errores

Toda respuesta de error usa el mismo cuerpo:

```json
{
  "codigo": "COMPROBANTE_NO_ENCONTRADO",
  "mensaje": "No existe un comprobante emitido con el CAE indicado."
}
```

El DTO es `FacturAR.Contracts.Comunes.ErrorResponse` con los campos `Codigo` y `Mensaje`.

### 2.1 Códigos de error

Los códigos son `UPPER_SNAKE_CASE`, estables y parte del contrato público. Un cliente puede hacer switch sobre ellos.

| Código | HTTP | Cuándo |
| --- | --- | --- |
| `VALIDACION` | 400 | El cuerpo o un parámetro no cumple el contrato. Es el código genérico de validación. |
| `CUIT_INVALIDO` | 400 | El CUIT no supera la validación de dígito verificador. |
| `CAE_INVALIDO` | 400 | El CAE no tiene 14 dígitos válidos. |
| `PASSWORD_DEBIL` | 400 | La contraseña no cumple la política de contraseñas. |
| `EMISION_INVALIDA` | 400 | El comprobante a emitir no supera las reglas de negocio. |
| `TIPO_NOTA_INCOMPATIBLE` | 400 | Una nota de crédito o débito apunta a un comprobante de otro tipo base. |
| `IMPORTE_EXCEDIDO` | 400 | El importe de la nota supera el saldo ajustable del comprobante original. |
| `API_KEY_AUSENTE` | 401 | No se envió el header `X-Api-Key`. |
| `API_KEY_INVALIDA` | 401 | La API key no existe, está revocada o el hash no coincide. |
| `CREDENCIALES_INVALIDAS` | 401 | Email o contraseña incorrectos, o la cuenta está inactiva. |
| `SIN_PERMISOS` | 403 | El rol no alcanza para la operación. |
| `CUENTA_INACTIVA` | 403 | El contribuyente o el usuario está desactivado. |
| `EMPRESA_NO_ASOCIADA` | 403 | El usuario no tiene empresa activa asociada. |
| `SOLICITUD_NO_ENCONTRADA` | 404 | No existe la solicitud. |
| `INVITACION_NO_ENCONTRADA` | 404 | No existe la invitación. |
| `COMPROBANTE_NO_ENCONTRADO` | 404 | No existe un comprobante con el CAE o el id indicado. |
| `SOLICITUD_PENDIENTE` | 409 | Ya existe una solicitud en estado `Pendiente` para el mismo CUIT o email. Lo devuelve `POST /v1/solicitudes-alta` al visitante. |
| `CUIT_YA_REGISTRADO` | 409 | El CUIT de la solicitud ya pertenece a otro contribuyente. Lo devuelve `POST /v1/admin/solicitudes/{id}/aprobar` al administrador global. |
| `SOLICITUD_YA_PROCESADA` | 409 | La solicitud ya fue aprobada o rechazada. |
| `INVITACION_USADA` | 409 | El token de invitación ya fue aceptado. |
| `IDEMPOTENCIA_CONFLICTO` | 409 | La misma `Idempotency-Key` llegó con un payload diferente. |
| `COMPROBANTE_ANULADO` | 409 | El comprobante está anulado y no admite la operación. |
| `PUNTO_VENTA_INACTIVO` | 409 | El punto de venta no está activo. |
| `INVITACION_VENCIDA` | 410 | El token de invitación superó las 48 horas. |
| `RATE_LIMIT` | 429 | Se superó el límite de peticiones del endpoint. |
| `EMAIL_NO_ENVIADO` | 502 | Falló el envío del email. La operación de negocio **no** se revierte. |
| `MONGO_NO_DISPONIBLE` | 503 | No hay conexión con MongoDB. |
| `ERROR_INTERNO` | 500 | Falla inesperada. Se registra el detalle en el log, nunca en la respuesta. |

Los códigos `VALIDACION`, `SOLICITUD_PENDIENTE`, `CUIT_YA_REGISTRADO`, `RATE_LIMIT`, `SOLICITUD_YA_PROCESADA`, `INVITACION_VENCIDA`, `INVITACION_USADA`, `PASSWORD_DEBIL`, `TIPO_NOTA_INCOMPATIBLE`, `IMPORTE_EXCEDIDO`, `CREDENCIALES_INVALIDAS`, `CAE_INVALIDO` y `COMPROBANTE_NO_ENCONTRADO` están fijados por los escenarios Gherkin de US-01, US-04, US-08, US-11, US-27 y US-28. Son intocables.

### 2.2 Reglas de la respuesta de error

- El `mensaje` es para humanos y puede cambiar sin romper clientes.
- El `codigo` no cambia. Si se necesita más detalle, se agregan campos opcionales al DTO en una historia futura, no se altera el existente.
- Los errores de validación **no** enumeran los campos que fallaron salvo que la historia lo pida explícitamente.
- **Nunca** se devuelve un `500` por datos inválidos del cliente: es un `400` con código específico.
- **SOAP:** los errores de negocio viajan en HTTP `200` con `Resultado = "R"`; los de autenticación o protocolo viajan como `SOAP Fault` con el HTTP del código. Los códigos son los mismos de §2.1. Ver §7.6.

## 3. Identidad, roles y aislamiento multiempresa

### 3.1 Dos planos de usuario

El sistema tiene dos planos que no se mezclan. Cada plano tiene su colección, su entidad y su tipo de rol.

| Plano | Colección | Entidad | Rol |
| --- | --- | --- | --- |
| Contribuyente | `usuarios` | `Usuario` | `RolUsuario` |
| Plataforma | `administradores` | `AdministradorFacturar` | `RolGlobal` |

`FacturAR.Domain.Enums.RolUsuario` queda así:

```csharp
public enum RolUsuario
{
    Administrador = 1,
    Emisor = 2,
    Consulta = 3
}
```

`FacturAR.Domain.Enums.RolGlobal` es nuevo y se define en US-04:

```csharp
public enum RolGlobal
{
    SuperAdmin = 1,
    Soporte = 2
}
```

`FacturAR.Domain.Entities.Usuario` **no cambia**: su `ContribuyenteId` sigue siendo `Guid` no nulo, porque todo usuario de empresa pertenece a un contribuyente. Los administradores globales **no** son `Usuario`: son `FacturAR.Domain.Entities.AdministradorFacturar`, con `Id`, `Nombre`, `Email`, `PasswordHash`, `Rol` (`RolGlobal`), `Activo`, `UltimoAcceso` y `CreatedAt`.

`FacturAR.Domain.Entities.EventoAuditoria` suma `Guid? AdministradorId` y `string ActorTipo` (`"Empresa"`, `"Administrador"` o `"Sistema"`), y su `ContribuyenteId` pasa a `Guid?`: `null` para acciones de plataforma.

El modelo completo, con campos e índices, está en [`docs/modelos.md`](modelos.md).

### 3.2 Matriz de permisos

| Operación | Administrador | Emisor | Consulta | SuperAdmin | Soporte |
| --- | --- | --- | --- | --- | --- |
| Ver comprobantes propios | Sí | Sí | Sí | Sí | Sí |
| Emitir comprobantes | Sí | Sí | No | No | No |
| Anular comprobante | No | No | No | Sí | No |
| Administrar API keys | Sí | No | No | No | No |
| Administrar usuarios de la empresa | Sí | No | No | No | No |
| Ver / aprobar solicitudes de alta | No | No | No | Sí | No |
| Responder una solicitud | No | No | No | No | Sí |
| Reenviar invitación | No | No | No | No | Sí |
| Ver auditoría global | No | No | No | Sí | No |

`SuperAdmin` **aprueba y rechaza** solicitudes. `Soporte` **solo consulta y reenvía invitaciones**. `Soporte` nunca aprueba ni rechaza.

### 3.3 Aislamiento

- El `ContribuyenteId` se resuelve **siempre** desde la sesión o desde la API key. **Nunca** se acepta desde el body, el query string ni un header del cliente.
- Toda consulta a `comprobantes`, `apiKeys`, `puntosVenta`, `secuencias` y `eventos` filtra por `ContribuyenteId`.
- Las entidades de negocio (`ApiKey`, `Comprobante`, `PuntoVenta`, `Secuencia`) llevan `ContribuyenteId` no nulo.
- Los índices de MongoDB que soportan el aislamiento **incluyen `ContribuyenteId` como prefijo**.
- Los endpoints públicos (`/v1/comprobantes/verificar/{cae}`) no filtran por contribuyente y **no exponen** campos internos: solo los de `VerificacionResponse`.

## 4. Autenticación

Hay dos mecanismos, según quién consume la API.

### 4.1 API key — para integraciones (US-02, US-03)

Header:

```http
X-Api-Key: fak_<prefijo>_<secreto>
```

Formato exacto: `fak_` + `prefix` (12 caracteres hexadecimales) + `_` + `secret` (43 caracteres base64url, resultado de 32 bytes aleatorios). La expresión regular de validación es `^fak_[0-9a-f]{12}_[A-Za-z0-9_-]{43}$`.

- El valor completo se muestra **una sola vez**, al crearla. No se puede recuperar después.
- En Mongo se guarda `ApiKey.Prefix` (los 12 hex, para buscar) y `ApiKey.Hash` (SHA-256 hexadecimal en minúsculas del valor completo). **Nunca** el valor completo.
- La comparación del hash usa tiempo constante (`CryptographicOperations.FixedTimeEquals`).
- Un `GET /v1/api-keys` **nunca** devuelve `Hash` ni el valor de la key. Devuelve `prefix` con el formato `fak_<prefix>`.
- `ApiKey.UltimoUso` se actualiza en cada autenticación exitosa, con una granularidad mínima de 1 minuto para no generar escrituras por petición.
- La fachada SOAP de emisión y consultas (US-15, US-16) reutiliza la misma API key: por el header `X-Api-Key` o por `Auth/Token` dentro del sobre. `Auth/Sign` se ignora. Ver §7.6.

### 4.2 Cookie de sesión — para el portal (US-04)

- El portal usa Blazor Server con cookie de sesión de ASP.NET Core.
- **Dos esquemas de cookie** con políticas separadas:
  - `cookies.contribuyente`: login de empresas clientes.
  - `cookies.admin`: login de la consola global (`SuperAdmin` / `Soporte`).
- El login de empresa **no** puede crear sesión de consola global y viceversa.
- La sesión del portal (`cookies.contribuyente`) **sí** autoriza los endpoints de consulta de comprobantes: `GET /v1/comprobantes`, `GET /v1/comprobantes/{id}` y `GET /v1/comprobantes/ultimo-autorizado` (US-09), con rol `Administrador`, `Emisor` o `Consulta`. Es el camino que usa el portal (US-12) para leer los comprobantes sin duplicar reglas ni acceder a MongoDB. `POST /v1/comprobantes` acepta API key o la cookie de empresa con rol `Administrador` o `Emisor`; el `ContribuyenteId` se resuelve desde la credencial y nunca desde el body. `POST /v1/comprobantes/presupuesto` y `GET /v1/puntos-venta` usan la misma cookie con rol `Administrador` o `Emisor`.
- La cookie de empresa **no** habilita `/v1/soap/**` (US-15, US-16) ni `/v1/admin/**`, y no convierte en autenticados los endpoints públicos: la verificación por CAE sigue siendo anónima.

## 5. Idempotencia (US-06)

- Header: `Idempotency-Key`, **obligatorio** en `POST /v1/comprobantes`. Longitud máxima: 128 caracteres.
- Alcance: `(ContribuyenteId, IdempotencyKey)`. La misma clave en otro contribuyente es una operación distinta.
- La clave se guarda **en el comprobante**: `Comprobante.IdempotencyKey` y `Comprobante.IdempotencyPayloadHash` (SHA-256 hexadecimal del payload canónico). No hay colección aparte.
- El índice único `ux_comprobantes_idempotencia` sobre `(ContribuyenteId, IdempotencyKey)` impide duplicados.
- Si la misma clave llega con **otro** payload, se responde `409 IDEMPOTENCIA_CONFLICTO`.
- Si la misma clave llega con el **mismo** payload, se devuelve el comprobante original con `200` y **no** se renumera ni se vuelve a generar el CAE.
- El reintento devuelve el mismo `id`, `numero` y `cae`.
- Todas las respuestas de `POST /v1/comprobantes`, incluidas `401` y `403`, devuelven el header `Idempotency-Key` con la clave recibida.
- No hay expiración de claves: es una nota de alcance explícita de US-06.
- No hay límite de intentos ni ventana temporal de reintentos: un replay con la misma clave y el mismo payload siempre devuelve el comprobante original.
- La clave no se usa para otras operaciones (notas, pagos, consultas).

## 6. Paginación (US-09, US-12)

Parámetros de query: `page` (1-based, default `1`) y `pageSize` (default `20`, máximo `100`).

Respuesta:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "total": 0,
  "totalPages": 0
}
```

Orden del listado de comprobantes: `{ FechaEmision: -1, Numero: -1, Id: 1 }`. El `Id` cierra el orden para que la concatenación de páginas no repita ni saltee ítems cuando dos comprobantes comparten fecha y número (por ejemplo, distinto punto de venta o tipo).

## 7. Emisión de comprobantes (US-05, US-07, US-08)

### 7.1 Tipos

| Tipo | Código `TipoComprobante` | Receptor |
| --- | --- | --- |
| Factura A | `FacturaA = 1` | Identificado, responsable inscripto |
| Nota de débito A | `NotaDebitoA = 2` | Identificado |
| Nota de crédito A | `NotaCreditoA = 3` | Identificado |
| Factura B | `FacturaB = 6` | Identificado, monotributo / sociale |
| Nota de débito B | `NotaDebitoB = 7` | Identificado |
| Nota de crédito B | `NotaCreditoB = 8` | Identificado |
| Factura C | `FacturaC = 11` | Consumidor final |
| Nota de débito C | `NotaDebitoC = 12` | Consumidor final |
| Nota de crédito C | `NotaCreditoC = 13` | Consumidor final |

### 7.2 Cálculo de importes

Por ítem:

```text
Subtotal        = Cantidad × PrecioUnitario
ImporteNeto     = Σ Subtotal
ImporteTotal    = ImporteNeto + ImporteIva
```

Alícuotas admitidas: `0`, `5`, `10,5`, `21`, `27`. El cálculo es con `decimal` y se redondea a **2 decimales** con `MidpointRounding.AwayFromZero`. Los importes se persisten en `Decimal128`.

Tratamiento del IVA según el tipo, según `docs/requisitos-funcionales.md` §Reglas de emisión:

| Tipo | Comportamiento |
| --- | --- |
| A (`FacturaA`) | **Discrimina IVA.** `ImporteIva = Σ (Subtotal × AlicuotaIva / 100)` y `ImporteTotal = ImporteNeto + ImporteIva`. |
| B (`FacturaB`) | El IVA **se incluye y no se discrimina**. `ImporteIva = 0` y `ImporteTotal = ImporteNeto`. |
| C (`FacturaC`) | **No discrimina IVA**. `ImporteIva = 0` y `ImporteTotal = ImporteNeto`. |

Reglas de validación:

- `Cantidad` mayor a 0. `PrecioUnitario` mayor o igual a 0.
- `Descripcion` obligatoria, entre 1 y 500 caracteres.
- Un comprobante tiene entre 1 y 500 ítems.
- Tipo C: el receptor es consumidor final; `ReceptorDocumentoTipo` es `99` o el CUIT del receptor.
- Tipo A: el receptor **debe** tener CUIT válido.
- Tipo B: el receptor es consumidor final, monotributista o exento.
- La condición frente al IVA del **emisor** se valida contra `Contribuyente.CondicionIva`. Un emisor `ConsumidorFinal` no puede emitir una factura A.

### 7.3 Numeración (US-07)

- La numeración es **por contribuyente, punto de venta y tipo**: `(ContribuyenteId, PuntoVenta, TipoComprobante)`.
- La colección es `secuencias`, con índice único `ux_secuencias_contribuyente_punto_tipo`.
- El número se asigna con una operación atómica de incremento sobre `Secuencia.UltimoNumero` (`findOneAndUpdate` con `$inc`). Nunca se calcula en memoria de la aplicación.
- **La secuencia se crea de forma implícita.** Si todavía no existe para la combinación `(contribuyente, punto de venta, tipo)`, el primer comprobante la crea con `UltimoNumero = 0` y recibe el número `1`. No hay un error de "secuencia no configurada".
- El punto de venta debe existir y estar activo. Si no, `409 PUNTO_VENTA_INACTIVO`.
- La asignación del número y la inserción del comprobante ocurren en la **misma transacción**. Si la transacción falla, el número no queda consumido.
- El número completo expuesto es `Numero` (entero largo) junto con `PuntoVenta`.

> **Prerrequisito de infraestructura.** Las transacciones de MongoDB exigen un **replica set**. Atlas M0 ya es replica set. El MongoDB local de `docker-compose.yml` debe correr con `--replSet` e inicializarse con `rs.initiate()`. Sin eso, US-05, US-07 y US-08 no pueden pasar sus tests de concurrencia y rollback.

### 7.4 CAE

- El CAE se genera con `FacturAR.Application.Comprobantes.CaeGenerator.Generar()`: **14 dígitos aleatorios**, sin dígito verificador. El CAE es **simulado** y no reproduce el algoritmo de la AFIP.
- `CaeVencimiento` es la fecha de emisión más `Facturacion__DiasVencimientoCae` (default `10`). El caso de uso de emisión resuelve los días con `IParametrosResolver`, incluido el override por contribuyente, y los pasa a `CaeGenerator.CalcularVencimiento`.
- El CAE es **único** en toda la base: índice `ux_comprobantes_cae`, único y sin filtro.

### 7.5 Notas de crédito y débito (US-08)

- `Comprobante` suma el campo `Guid? ComprobanteOriginalId` y `decimal? ImporteOriginalAjustado` (saldo pendiente del comprobante original). Los agregó US-08.
- Una nota **debe** referenciar un comprobante original del mismo contribuyente, del mismo tipo base (A/B/C) y en estado `Emitido`. No puede referenciar uno `Anulado` o `Rechazado`.
- Referenciar un comprobante de otra empresa responde `404 COMPROBANTE_NO_ENCONTRADO`, sin revelar su existencia.
- El tipo de nota debe corresponder al de la factura original; si no, `400 TIPO_NOTA_INCOMPATIBLE`.
- **La nota tiene su propio CAE, su propio vencimiento y su propia secuencia** por punto de venta y tipo. El comprobante original conserva su número y su CAE.
- El saldo ajustable del original es `ImporteTotal - Σ importes de notas de crédito + Σ importes de notas de débito`.
- Una **nota de crédito** no puede superar el saldo ajustable del original: si lo supera, `400 IMPORTE_EXCEDIDO` y **no** se consume número ni CAE. Una **nota de débito** aumenta el saldo y no está limitada por él.
- Emitir una nota actualiza el `ImporteOriginalAjustado` del comprobante original dentro de la misma transacción.

### 7.6 SOAP de emisión y consultas (US-15, US-16)

La emisión y las consultas también se exponen por **SOAP 1.1 sobre XML** en `POST /soap`. El WSDL está en `GET /soap?wsdl` y el namespace es `http://ar.gov.afip.dif.FEV1Service/`. No es un camino paralelo: `FECAESolicitar` delega en `EmitirComprobanteService` (US-05) y las consultas `FECompUltimoAutorizado` y `FECompConsultar` delegan en `ObtenerUltimoAutorizadoService` y `ConsultarComprobanteService` (US-09).

| Operación | Request | Response |
| --- | --- | --- |
| `FECAESolicitar` | `Auth` (`Token`, `Sign`), `Cuit`, `PtoVta`, `TipoComprobante`, `Concepto`, `DocTipo`, `DocNro`, `CondicionIVA`, `Items[]` (`Descripcion`, `Cantidad`, `PrecioUnitario`, `AlicuotaIVA`), `IdempotencyKey` opcional | `FeCabResp` con `Resultado` (`"A"`/`"R"`), `CodAutorizacion` (CAE de 14 dígitos), `FchVto` (`yyyyMMdd`) y `Observaciones` |
| `FECAEGenerar` | Misma estructura que `FECAESolicitar` | `Respuesta` con `Resultado` (`"A"`/`"R"`), `Error` (código canónico) y `Observaciones` (mensaje) |
| `FECompUltimoAutorizado` | `Auth` opcional, `PtoVta`, `TipoComprobante` | `Resultado` (`"A"`/`"R"`), `PtoVta`, `TipoComprobante`, `UltimoNro` (`0` si no hay emitidos), `Cae`, `FechaEmision` (`yyyyMMdd`), `FchVto` (`yyyyMMdd`) y `Observaciones` |
| `FECompConsultar` | `Auth` opcional, `PtoVta`, `TipoComprobante`, `Numero` | `Resultado` (`"A"`/`"R"`), `Cuit`, `PtoVta`, `TipoComprobante`, `Numero`, `FechaEmision` (`yyyyMMdd`), `Cae`, `FchVto` (`yyyyMMdd`), `DocTipo`, `DocNro`, `CondicionIVA`, `Moneda`, `ImporteNeto`, `ImporteIva`, `ImporteTotal`, `Estado` y `Observaciones` |
| `FEDummy` | `Auth` opcional | `Estado` (`"OK"`), `ServerTime` (ISO 8601 UTC), `Version` y `Service` (`"FacturAR.Api"`) |

- **Autenticación:** header `X-Api-Key` (canónica) o `Auth/Token` dentro del sobre. `Auth/Sign` es opcional e ignorado. Sin credencial: `401 API_KEY_AUSENTE`; credencial inválida o revocada: `401 API_KEY_INVALIDA`. La API key nunca aparece en el `Fault`, en `Observaciones` ni en el log. `FEDummy` también exige API key válida.
- **Tenant:** sale **solo** de la API key. En emisión, el `Cuit` del request se compara con el CUIT del contribuyente autenticado; si difieren, se devuelve `Resultado = "R"` con código `VALIDACION` y no se crea el comprobante. En consultas (`FECompUltimoAutorizado` y `FECompConsultar`), todas las lecturas filtran por el `ContribuyenteId` de la API key; consultar un comprobante inexistente o de otra empresa devuelve `Resultado = "R"` con `COMPROBANTE_NO_ENCONTRADO` y sin ningún dato fiscal.
- **`FECAESolicitar` es la única operación que persiste.** `FECAEGenerar`, `FECompUltimoAutorizado`, `FECompConsultar` y `FEDummy` son de solo lectura: no insertan comprobantes, no consumen números y no generan eventos de auditoría.
- **Errores de negocio:** HTTP `200` con `Resultado = "R"` y `Observaciones = "<CODIGO>: <mensaje>"`. **Errores de autenticación o protocolo:** `SOAP Fault` con `faultstring = "<CODIGO>: <mensaje>"`, `detail/codigo`, `detail/mensaje` y HTTP `401`/`400`; MongoDB no disponible `503`; error interno `500` sin detalle. Los códigos son los de §2.1.
- **Idempotencia:** la extensión opcional `IdempotencyKey` se mapea al mecanismo de US-06. Si se omite, cada reintento es una emisión nueva. La fachada reconoce el replay y `IDEMPOTENCIA_CONFLICTO`; la verificación end-to-end con MongoDB replica set sigue pendiente.
- **Alcance de tipos:** el contrato XML no transporta `RazonSocial` del receptor. Una Factura C se emite de punta a punta; las Facturas A y B requieren ese dato en el caso de uso y se rechazan por SOAP con `VALIDACION`.
- **Configuración:** `Soap__Habilitado` (default `true`) y `Soap__Ruta` (default `/soap`). Con `Soap__Habilitado=false` la ruta responde `404`.

## 8. Verificación pública por CAE (US-11)

```http
GET /v1/comprobantes/verificar/{cae}
```

- **Sin autenticación.**
- `200` con `VerificacionResponse` si existe.
- `400 CAE_INVALIDO` si el CAE no tiene exactamente 14 dígitos.
- `404 COMPROBANTE_NO_ENCONTRADO` si no existe.
- La respuesta **nunca** incluye `Id`, `ContribuyenteId`, `IdempotencyKey`, `Items`, `Observaciones` ni datos del emisor que no figuren en el comprobante.
- La respuesta de un comprobante anulado mantiene el estado `Anulado` visible. La anulación administrativa es US-22, fuera de alcance.

## 9. Salud del servicio

| Endpoint | Semántica | Respuesta |
| --- | --- | --- |
| `GET /health` | Readiness. Verifica MongoDB. | `200` con `{ status, database, timestamp, version, commit }`, o `503` con `{ status: "degraded", database: "unavailable", ... }`. |
| `GET /health/live` | Liveness. No consulta MongoDB. | `200` con `{ status, version, commit }`. |

- `version` y `commit` provienen de las variables de entorno `APP_VERSION` y `SOURCE_COMMIT`, inyectadas por el pipeline de build. Si no están, `version = "0.1.0"` y `commit = "unknown"`.
- El health check de Azure App Service debe apuntar a `/health/live`: así una caída transitoria de MongoDB no reinicia el contenedor en loop. `/health` sigue siendo el chequeo de readiness para uso humano y para `docker compose`.

## 10. Parámetros configurables (US-18)

Se leen de la sección `Facturacion` de la configuración y se permiten sobrescribir por entorno con el prefijo `Facturacion__`.

| Clave | Tipo | Default | Uso |
| --- | --- | --- | --- |
| `Facturacion__DiasVencimientoCae` | int | `10` | Días de vigencia del CAE. Es la **única fuente** del vencimiento. |
| `Facturacion__AlicuotasPermitidas` | lista | `0, 5, 10.5, 21, 27` | Alícuotas de IVA aceptadas. |
| `Facturacion__PuntoVentaPorDefecto` | int | `1` | Punto de venta sugerido al crear el contribuyente. |
| `Facturacion__Moneda` | string | `ARS` | Moneda única del MVP. |
| `Facturacion__PaginaPorDefecto` | int | `20` | `pageSize` por defecto de los listados (§6). |
| `Facturacion__PaginaMaxima` | int | `100` | `pageSize` máximo de los listados (§6). |

La lista de alícuotas se configura por índice: `Facturacion__AlicuotasPermitidas__0` … `Facturacion__AlicuotasPermitidas__4`. El binder no toma una clave escalar `Facturacion__AlicuotasPermitidas` para un array. El default es `0, 5, 10.5, 21, 27`.

`CaeGenerator.CalcularVencimiento` recibe la fecha de emisión y los días ya resueltos. Con el default `10` el comportamiento es idéntico al de US-05; cualquier otro valor altera ese comportamiento y debe quedar registrado en la configuración del ambiente.

La colección `parametros` permite override por contribuyente con clave `(ContribuyenteId, Clave)`. El orden de resolución es: override del contribuyente → valor de la sección `Facturacion` → default de código. Los documentos globales que escribe el seed (`ContribuyenteId = null`) son informativos y **no** se consultan en la resolución.

## 11. Seed y datos de demostración (US-18)

El seed corre al arrancar la API únicamente cuando `Seed__Enabled=true`. Es la **única autorización**, incluso en Production: con `false` (default) no se carga nada en ningún ambiente. En Production con `true` la carga ocurre y la API registra una advertencia sin secretos. Es idempotente: se puede ejecutar varias veces sin duplicar datos ni reiniciar secuencias.

| Entidad | Datos |
| --- | --- |
| `contribuyentes` | 1 empresa demo con CUIT válido, razón social, email y `CondicionIva = ResponsableInscripto`. |
| `usuarios` | 3 usuarios de la empresa demo: `Administrador`, `Emisor` y `Consulta`. `Seed__Email` identifica al `Administrador`; `Emisor` y `Consulta` se derivan insertando `+emisor` y `+consulta` antes de la `@`. |
| `administradores` | 1 `SuperAdmin` de plataforma, con `Seed__Nombre` como nombre visible y el email derivado con `+superadmin`. |
| `puntosVenta` | Punto de venta `Facturacion__PuntoVentaPorDefecto` (default `1`) activo para el contribuyente demo. |
| `secuencias` | Secuencias `FacturaA`, `FacturaB` y `FacturaC` en `0`. Una secuencia existente nunca se reinicia. |
| `parametros` | Valores por defecto de la sección `Facturacion`, con `ContribuyenteId = null`. |
| `apiKeys` | En Development y con `Seed__ApiKeyDemo=true`, 1 API key demo de la empresa. |

`Seed__Password` es obligatoria cuando el seed está habilitado; se valida con la política de contraseñas, se hashea antes de persistir y nunca se escribe en logs ni en el repositorio. El CUIT se valida con `CuitValidator`. Si la configuración es inválida, el seed no escribe nada y la API registra una advertencia sin la contraseña.

En Development, el valor completo de la API key demo se entrega **una sola vez** en `seed/api-key-demo.txt`, relativo a la raíz de contenido. Nunca se escribe en un log ni en la base en claro: solo quedan `Prefix` y `Hash`. La carpeta `seed/` está excluida de git y de la imagen de Docker y debe tratarse como secreto local. Sin `Seed__ApiKeyDemo=true` no se crea ninguna API key; para una key de prueba se usa el flujo normal de US-02 o US-14.

## 12. Correo (US-29)

- Se abstrae con `IEmailSender`. Dos implementaciones: `FileEmailSender` (escribe en `emails/` en desarrollo) y `SmtpEmailSender` (producción).
- Configuración por la sección `Email`: `Email__From`, `Email__Host`, `Email__Port`, `Email__User`, `Email__Password`, `Email__UseSsl`.
- `Portal__BaseUrl` es la URL pública del portal, usada para armar los enlaces de los emails.
- Un fallo de email **no revierte** la operación de negocio. Se registra el evento de auditoría y se responde `502 EMAIL_NO_ENVIADO` solo cuando el email **era** la operación pedida (por ejemplo, reenviar una invitación).
- Los emails del MVP son cinco: `SolicitudRecibida` (al solicitante), `NuevaSolicitud` (a `Admin__NotificationEmail`, opcional), `SolicitudAprobada` (al solicitante, con el link de activación), `SolicitudRechazada` (al solicitante, con el motivo) e `InvitacionReenviada` (al solicitante, con el nuevo link).
- El email de **aprobación** es el que lleva el link de activación. No hay un email de invitación separado.

## 13. Auditoría y outbox (US-17)

- `EventoAuditoria` se escribe en la **misma** colección `eventos` con `Accion`, `Entidad`, `EntidadId` y `Datos` (JSON sin datos personales ni secretos).
- `EventoAuditoria.ContribuyenteId` es `Guid?`: `null` para eventos de plataforma.
- `OutboxMessage` es la cola de efectos externos (emails, webhooks). Se escribe **en la misma operación** que el cambio de estado de negocio, nunca después.
- El `BackgroundService` de la outbox toma mensajes `Pendiente`, los marca `Procesando`, reintenta con backoff exponencial y termina en `Fallido` tras el máximo configurado (`Outbox:MaxIntentos`, default 5). `Estado`, `Intentos`, `ProcesadoAt` y `Error` quedan registrados. Un mensaje `Procesado` no se vuelve a reclamar.

## 14. Convenciones de API para el cliente

- Endpoint de emisión: `POST /v1/comprobantes` (API key o cookie de empresa con rol `Administrador`/`Emisor`, + `Idempotency-Key` obligatorio).
- Presupuesto: `POST /v1/comprobantes/presupuesto` (cookie de empresa con rol `Administrador`/`Emisor`). Devuelve `PresupuestoResponse`; no persiste, no consume número, no genera CAE y no audita.
- Puntos de venta: `GET /v1/puntos-venta` (cookie de empresa con rol `Administrador`/`Emisor`). Devuelve solo los puntos de venta del contribuyente, activos e inactivos, ordenados por número.
- Los tres endpoints de consulta aceptan API key (`X-Api-Key`) o cookie de empresa (`cookies.contribuyente`) con rol `Administrador`, `Emisor` o `Consulta`.
- Listado: `GET /v1/comprobantes?page=1&pageSize=20&desde=2026-01-01&hasta=2026-01-31&tipo=1&estado=1&puntoVenta=1&receptor=20345678901`. `pageSize` máximo `100`; `desde` y `hasta` se interpretan en UTC y `hasta` incluye el día completo.
- Consulta por id: `GET /v1/comprobantes/{id}` (Guid). Un id inexistente o de otra empresa responde `404 COMPROBANTE_NO_ENCONTRADO`; un id que no es Guid responde `400 VALIDACION`.
- Último autorizado: `GET /v1/comprobantes/ultimo-autorizado?tipo=1&puntoVenta=1`. `tipo` es obligatorio y va en singular; `puntoVenta` es opcional. Sin secuencia responde `numero: 0`; sin `puntoVenta`, la respuesta lo devuelve como `null`.
- PDF: `GET /v1/comprobantes/{id}/pdf` → `application/pdf` con `Content-Disposition: inline; filename="0001-00000001.pdf"`.
- Dashboard: `GET /v1/comprobantes/dashboard?mes=2026-10` (API key o cookie de empresa). `mes` es opcional (`YYYY-MM`; default mes UTC) y la respuesta es `DashboardResponse`. Errores: `400 VALIDACION`, `401 API_KEY_AUSENTE`, `401 API_KEY_INVALIDA`, `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS`, `503 MONGO_NO_DISPONIBLE`, `500 ERROR_INTERNO`.
- API keys: `GET|POST /v1/api-keys`, `DELETE /v1/api-keys/{id}`, `POST /v1/api-keys/{id}/regenerar`.
- Solicitud de alta (público): `POST /v1/solicitudes-alta`.
- Activación (público): `GET /v1/invitaciones/{token}` y `POST /v1/invitaciones/{token}/aceptar`.
- Consola global: `GET /v1/admin/solicitudes`, `GET|PUT /v1/admin/solicitudes/{id}`, `POST /v1/admin/solicitudes/{id}/aprobar`, `POST /v1/admin/solicitudes/{id}/rechazar`, `POST /v1/admin/solicitudes/{id}/reenviar-invitacion`.

## 15. Qué NO existe en el MVP

- Registro libre de usuarios. El alta es **solicitud → aprobación → invitación → activación**.
- GraphQL ni gRPC.
- Multi-moneda.
- Webhooks (US-23, fuera de alcance).
- Integración real con AFIP (US-24, fuera de alcance).
- WSAA real con certificados X.509 ni padrón fiscal externo. El SOAP del MVP (US-15 y US-16) expone `FECAESolicitar`, `FECAEGenerar`, `FECompUltimoAutorizado`, `FECompConsultar` y `FEDummy` autenticados con API key.
- Kubernetes: no se usa. Decisión de plataforma en [`docs/plan-implementacion.md`](plan-implementacion.md) §2.

## 16. Trazabilidad

| Documento | Rol |
| --- | --- |
| `docs/requisitos-funcionales.md` | Qué debe hacer el sistema. |
| `docs/requisitos-no-funcionales.md` | Restricciones no funcionales y de costo. |
| `docs/contratos-api.md` (este) | Cómo es el contrato entre la API y sus clientes. |
| `docs/modelos.md` | Forma de los datos en MongoDB. |
| `docs/manual-integracion.md` | Guía para un tercero integrador. |
| `docs/proceso-alta-empresa.md` | Flujo de onboarding. |
| `docs/despliegue-azure.md` | Operación del despliegue. |
| `docs/plan-implementacion.md` | Orden de ejecución de las historias. |
