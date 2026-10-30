# US-01 — Solicitud pública de alta de empresa

**Épica:** Registro y multiempresa  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RF-23  
**Depende de:** US-29 (email de confirmación)  
**Habilita:** US-27, US-28

## Historia

Como visitante que representa a una empresa, quiero enviar una solicitud de alta con mis datos fiscales y de contacto para que FacturAR revise mi empresa antes de habilitarme el acceso.

## Contexto

El alta no crea la empresa ni un usuario. Solo registra una solicitud pendiente. La aprobación y la creación del contribuyente, punto de venta e invitación se hacen en US-27 y US-28.

El CUIT es un dato público: no se usa como prueba de titularidad. La solicitud no debe revelar si un CUIT ya está registrado.

## Criterios de aceptación

- [x] El formulario público solicita razón social, CUIT, condición frente al IVA, nombre del solicitante, email, teléfono, domicilio, ciudad, provincia, código postal y aceptación de términos.
- [x] El CUIT se normaliza sin guiones y se valida con el dígito verificador.
- [x] El email se normaliza a minúsculas y se valida con formato.
- [x] Los campos obligatorios se validan en el borde y en el caso de uso.
- [x] La solicitud se guarda con estado `Pendiente` y fecha UTC.
- [x] No se crea `Contribuyente`, `Usuario`, `PuntoVenta` ni `ApiKey` en este paso.
- [x] No se permite otra solicitud pendiente con el mismo CUIT.
- [x] No se permite otra solicitud pendiente con el mismo email.
- [x] Si el CUIT ya pertenece a un contribuyente, se devuelve un mensaje genérico y se registra el intento para revisión.
- [x] La respuesta no expone datos de otras solicitudes ni confirma la existencia de un contribuyente.
- [x] Se envía o encola un email de confirmación al solicitante.
- [x] El endpoint público tiene rate limit por IP y por email.
- [ ] La página muestra un mensaje de éxito con el próximo paso y un mensaje claro ante errores.
- [x] La solicitud puede auditarse sin guardar datos sensibles innecesarios.

## Escenarios Gherkin

```gherkin
Scenario: Solicitud válida
  Given un visitante completa todos los datos obligatorios con un CUIT válido y un email válido
  And acepta los términos
  When envía la solicitud de alta
  Then la solicitud se guarda con estado "Pendiente"
  And no se crea ningún contribuyente ni usuario
  And se envía un email de confirmación al solicitante
  And el portal muestra el mensaje "Solicitud recibida"
```

```gherkin
Scenario: CUIT inválido
  Given un visitante ingresa un CUIT con dígito verificador incorrecto
  When envía la solicitud
  Then la API devuelve 400 con código "VALIDACION"
  And no se guarda la solicitud
  And el formulario muestra el error junto al campo CUIT
```

```gherkin
Scenario: Solicitud pendiente duplicada
  Given existe una solicitud pendiente con el CUIT 20123456786
  When un visitante envía otra solicitud con el mismo CUIT
  Then la API devuelve 409 con código "SOLICITUD_PENDIENTE"
  And no se crea una segunda solicitud
```

```gherkin
Scenario: CUIT ya registrado
  Given existe un contribuyente con el CUIT 20123456786
  When un visitante envía una solicitud con ese CUIT
  Then la API devuelve 202 con un mensaje genérico de revisión
  And no se confirma la existencia del contribuyente
  And se registra el intento para el administrador global
```

```gherkin
Scenario: Rate limit
  Given una IP superó el límite de solicitudes permitidas
  When envía otra solicitud
  Then la API devuelve 429 con código "RATE_LIMIT"
  And no se guarda la solicitud
```

## Tareas técnicas

- [x] Crear `SolicitudAltaEmpresa` y `EstadoSolicitud` en Domain.
- [x] Crear `ISolicitudAltaRepository` en Application.
- [x] Implementar el repositorio MongoDB y los índices parciales de unicidad.
- [x] Crear DTOs `SolicitudAltaRequest` y `SolicitudAltaResponse` en Contracts.
- [x] Implementar `CrearSolicitudAltaService` con normalización y validaciones.
- [x] Crear `POST /v1/solicitudes-alta` con rate limit y `ErrorResponse`.
- [x] Crear la página de solicitud en el portal y el cliente HTTP.
- [x] Enviar el email de confirmación mediante US-29.
- [x] Agregar tests unitarios de validación y duplicados.
- [ ] Agregar tests de integración del endpoint y de los índices.
- [x] Documentar el endpoint en OpenAPI y Postman.

## Definition of Done

- [ ] Una solicitud válida queda persistida y visible para el administrador global.
- [x] Los duplicados y errores no crean registros parciales.
- [ ] El email de confirmación se genera en desarrollo.
- [x] El formulario funciona en desktop y mobile.
- [x] `dotnet test FacturAR.slnx` pasa.
- [x] La historia está enlazada desde el índice y el proceso de alta.

## Contrato técnico

### Modelo de datos

Colección nueva: `solicitudesAlta`.

| Elemento | Definición |
| --- | --- |
| Dominio | `FacturAR.Domain.Entities.SolicitudAltaEmpresa` |
| Enum | `FacturAR.Domain.Enums.EstadoSolicitud`: `Pendiente = 1`, `Aprobada = 2`, `Rechazada = 3` |
| Colección | `MongoCollections.SolicitudesAlta = "solicitudesAlta"` |
| Acceso | `MongoDbContext.SolicitudesAlta` |

Campos del documento (alineados con `docs/modelos.md`):

| Campo | Tipo | Obligatorio | Notas |
| --- | --- | --- | --- |
| `Id` | `Guid` | Sí | Clave primaria. |
| `RazonSocial` | `string` | Sí | 3..200 caracteres. |
| `Cuit` | `string` | Sí | Normalizado a 11 dígitos, dígito verificador válido. |
| `CondicionIva` | `CondicionIva` | Sí | Valor del enum `CondicionIva` existente. |
| `NombreSolicitante` | `string` | Sí | 2..120 caracteres. |
| `EmailSolicitante` | `string` | Sí | Normalizado a minúsculas. |
| `Telefono` | `string` | Sí | Solo dígitos, espacios, `+`, `-` y paréntesis. |
| `Domicilio` | `string` | Sí | 5..200 caracteres. |
| `Ciudad` | `string` | Sí | 2..100 caracteres. |
| `Provincia` | `string` | Sí | 2..100 caracteres. |
| `CodigoPostal` | `string` | Sí | 4..10 caracteres alfanuméricos. |
| `Mensaje` | `string?` | No | Hasta 1000 caracteres. |
| `AceptaTerminos` | `bool` | Sí | Siempre `true` en las solicitudes persistidas. |
| `TerminosAceptadosAt` | `DateTime` | Sí | UTC, asignado por el servidor. |
| `Estado` | `EstadoSolicitud` | Sí | `Pendiente` al crear. |
| `MotivoRechazo` | `string?` | No | Lo completa US-27. |
| `ContribuyenteId` | `Guid?` | No | `null` hasta que US-27 aprueba. |
| `InvitacionId` | `Guid?` | No | Lo completa US-27. |
| `RevisadaPorAdministradorId` | `Guid?` | No | Lo completa US-27. |
| `RevisadaAt` | `DateTime?` | No | Lo completa US-27. |
| `CreatedAt` | `DateTime` | Sí | UTC, asignado por el servidor. |
| `UpdatedAt` | `DateTime` | Sí | UTC, se actualiza en cada edición (US-27). |

Índices (nombres exactos, agregados en `MongoIndexInitializer`):

| Índice | Clave | Tipo | Único | Propósito |
| --- | --- | --- | --- | --- |
| `ux_solicitudes_alta_cuit_pendiente` | `Cuit` | Parcial (`Estado = Pendiente`) | Sí | Garantiza una sola solicitud pendiente por CUIT. |
| `ux_solicitudes_alta_email_pendiente` | `EmailSolicitante` | Parcial (`Estado = Pendiente`) | Sí | Garantiza una sola solicitud pendiente por email. |
| `ix_solicitudes_alta_estado_fecha` | `Estado` asc, `CreatedAt` desc | Compuesto | No | Listado del panel por estado y orden por fecha. |

Los dos índices parciales son la **garantía real** contra duplicados: la validación en el caso de uso es una comprobación rápida y el índice es la barrera final ante concurrencia.

### Endpoints

| Método | Ruta | Auth | Body | Respuesta | Errores |
| --- | --- | --- | --- | --- | --- |
| `POST` | `/v1/solicitudes-alta` | Público, con rate limit por IP y por email | `SolicitudAltaRequest` | `202 Accepted` con `SolicitudAltaResponse` | `400 VALIDACION`, `409 SOLICITUD_PENDIENTE`, `429 RATE_LIMIT`, `500 ERROR_INTERNO`, `503 MONGO_NO_DISPONIBLE` |

Contratos en `FacturAR.Contracts.Solicitudes`:

```csharp
public sealed record SolicitudAltaRequest(
    string RazonSocial,
    string Cuit,
    int CondicionIva,
    string NombreSolicitante,
    string Email,
    string Telefono,
    string Domicilio,
    string Ciudad,
    string Provincia,
    string CodigoPostal,
    string? Mensaje,
    bool AceptaTerminos);

public sealed record SolicitudAltaResponse(string Codigo, string Mensaje);
```

`SolicitudAltaResponse` es **idéntica** en todos los casos de éxito, incluido el caso en que el CUIT ya pertenece a un contribuyente:

```json
{ "codigo": "SOLICITUD_RECIBIDA", "mensaje": "Recibimos tu solicitud. Revisamos los datos y te escribimos al email de contacto." }
```

No devuelve `id`, ni CUIT, ni email, ni fecha. No hay ningún endpoint público de lectura, listado ni consulta de solicitudes: el visitante no puede enumerar solicitudes ajenas.

### Idempotencia de la solicitud

Este endpoint **no** usa el header `Idempotency-Key`: ese mecanismo es de US-06 y aplica a `POST /v1/comprobantes`. La idempotencia de la solicitud se apoya en el par (dato, estado):

| Regla | Comportamiento |
| --- | --- |
| Mismo CUIT + solicitud `Pendiente` | `409 SOLICITUD_PENDIENTE`. No se crea la segunda solicitud ni se envía un segundo email. |
| Mismo email + solicitud `Pendiente` | `409 SOLICITUD_PENDIENTE`. Igual comportamiento. |
| Mismo CUIT + solicitud `Aprobada` | El CUIT ya pertenece a un contribuyente: `202` genérico, sin crear solicitud. |
| Mismo CUIT + solicitud `Rechazada` | Se permite una nueva solicitud `Pendiente`: el índice parcial único no la bloquea. |
| Dos requests concurrentes con el mismo CUIT | El índice `ux_solicitudes_alta_cuit_pendiente` hace que solo una inserte; la otra recibe `409 SOLICITUD_PENDIENTE` por clave duplicada. |

### Rate limit (RNF-23)

Usa el rate limiter integrado de ASP.NET Core (`Microsoft.AspNetCore.RateLimiting`, dentro del framework compartido: **sin paquete NuGet nuevo**).

| Política | Partición | Límite por defecto | Clave de configuración |
| --- | --- | --- | --- |
| `solicitud-alta-ip` | IP del cliente | 5 solicitudes por minuto | `RateLimit__SolicitudAlta__PorIp` |
| `solicitud-alta-email` | Email normalizado del body | 3 solicitudes por hora | `RateLimit__SolicitudAlta__PorEmail` |

El endpoint declara **las dos políticas** (`RequireRateLimiting` por cada una): ambas tienen que permitir la petición. El rechazo responde `429` con `ErrorResponse("RATE_LIMIT", "Demasiadas solicitudes. Intentá de nuevo más tarde.")` y `Retry-After`. La IP se toma del header `X-Forwarded-For` si hay un proxy delante; si no, de `HttpContext.Connection.RemoteIpAddress`.

### Casos de uso y servicios

| Pieza | Tipo | Namespace | Responsabilidad |
| --- | --- | --- | --- |
| `CrearSolicitudAltaService` | Clase | `FacturAR.Application.Solicitudes` | Normaliza, valida, verifica duplicados y CUIT ya registrado, inserta la solicitud `Pendiente`, dispara el email y audita. |
| `ISolicitudAltaRepository` | Interfaz | `FacturAR.Application.Abstractions` | `InsertarAsync`, `ExistePendientePorCuitAsync`, `ExistePendientePorEmailAsync`. |
| `SolicitudAltaRepository` | Clase | `FacturAR.Infrastructure.Repositories` | Implementación MongoDB. Traduce la violación de índice único en `409 SOLICITUD_PENDIENTE`. |
| `IContribuyenteRepository` | Interfaz | `FacturAR.Application.Abstractions` | `ExistePorCuitAsync(cuit)`. Lo introduce US-01 y lo reutiliza US-27. |
| `ContribuyenteRepository` | Clase | `FacturAR.Infrastructure.Repositories` | Implementación MongoDB sobre `contribuyentes`. |
| `IEventoAuditoriaRepository` | Interfaz | `FacturAR.Application.Abstractions` | `RegistrarAsync(evento)`. **Lo introduce US-01** y lo reutilizan US-27 y US-28. |
| `EventoAuditoriaRepository` | Clase | `FacturAR.Infrastructure.Repositories` | Implementación MongoDB sobre `eventos`. |
| `SolicitudAltaRequest` / `SolicitudAltaResponse` | Records | `FacturAR.Contracts.Solicitudes` | Contrato HTTP. |

Flujo de `CrearSolicitudAltaService`:

1. Normaliza CUIT (`CuitValidator.Normalize`) y email (`Trim` + `ToLowerInvariant`).
2. Valida los campos (ver tabla de validaciones). Si algo falla → `400 VALIDACION`.
3. Verifica que el email no tenga otra solicitud `Pendiente` → si tiene, `409 SOLICITUD_PENDIENTE`.
4. Verifica que el CUIT no tenga otra solicitud `Pendiente` → si tiene, `409 SOLICITUD_PENDIENTE`.
5. Verifica que el CUIT no pertenezca a un contribuyente (`IContribuyenteRepository.ExistePorCuitAsync`). Si pertenece → **no inserta**, registra el intento para el administrador global (auditoría `solicitud.cuit_registrado`) y devuelve el mismo `202` genérico.
6. Inserta la solicitud con `Estado = Pendiente`, `CreatedAt` y `UpdatedAt` en UTC. Una única inserción: **no requiere transacción ni replica set**.
7. Envía el email `SolicitudRecibida` con `EmailService` de US-29. Si falla, la solicitud **ya está persistida**: no se revierte.
8. Si `Admin__NotificationEmail` está configurado, envía `NuevaSolicitud`. Si falla, tampoco revierte nada.
9. Audita `solicitud.creada` y `email.enviado` / `email.fallido` vía `IEventoAuditoriaRepository`.

### Validaciones

Todas las reglas de campo devuelven `400 VALIDACION` con un mensaje para humanos. El catálogo de `docs/contratos-api.md` §2.1 define `VALIDACION` como el código genérico de validación del cuerpo o de un parámetro.

| Campo | Regla | Mensaje | Código |
| --- | --- | --- | --- |
| `razonSocial` | Obligatorio, 3..200 caracteres | `La razón social es obligatoria.` / `La razón social debe tener entre 3 y 200 caracteres.` | `VALIDACION` (400) |
| `cuit` | Obligatorio, 11 dígitos, dígito verificador válido (`CuitValidator.TryNormalize`) | `El CUIT no es válido.` | `VALIDACION` (400) |
| `condicionIva` | Obligatorio, valor existente de `CondicionIva` (1, 4, 5, 6, 7) | `La condición frente al IVA no es válida.` | `VALIDACION` (400) |
| `nombreSolicitante` | Obligatorio, 2..120 caracteres | `El nombre del solicitante es obligatorio.` | `VALIDACION` (400) |
| `email` | Obligatorio, formato válido, hasta 254 caracteres, normalizado a minúsculas | `El email no es válido.` | `VALIDACION` (400) |
| `telefono` | Obligatorio, 8..32 caracteres, solo dígitos, espacios, `+`, `-` y paréntesis | `El teléfono no es válido.` | `VALIDACION` (400) |
| `domicilio` | Obligatorio, 5..200 caracteres | `El domicilio es obligatorio.` | `VALIDACION` (400) |
| `ciudad` | Obligatorio, 2..100 caracteres | `La ciudad es obligatoria.` | `VALIDACION` (400) |
| `provincia` | Obligatorio, 2..100 caracteres | `La provincia es obligatoria.` | `VALIDACION` (400) |
| `codigoPostal` | Obligatorio, 4..10 caracteres alfanuméricos | `El código postal no es válido.` | `VALIDACION` (400) |
| `mensaje` | Opcional, hasta 1000 caracteres | `El mensaje es demasiado largo.` | `VALIDACION` (400) |
| `aceptaTerminos` | Obligatorio y `true` | `Tenés que aceptar los términos para enviar la solicitud.` | `VALIDACION` (400) |
| `terminosAceptadosAt` | No lo manda el cliente; lo asigna el servidor en UTC | — | — |
| Límite de frecuencia por IP | 5 por minuto | `Demasiadas solicitudes. Intentá de nuevo más tarde.` | `RATE_LIMIT` (429) |
| Límite de frecuencia por email | 3 por hora | `Demasiadas solicitudes. Intentá de nuevo más tarde.` | `RATE_LIMIT` (429) |

Las validaciones se aplican **en el borde** (endpoint) y **en el caso de uso** (`CrearSolicitudAltaService`), que es el que se testea de forma aislada. El borde devuelve el mensaje al formulario; el caso de uso lanza una excepción de dominio propia que el endpoint traduce al `ErrorResponse`.

### Estados

| Estado actual | Transición | Quién la dispara | Efecto |
| --- | --- | --- | --- |
| — | → `Pendiente` | `CrearSolicitudAltaService` (US-01) | Se inserta la solicitud. |
| `Pendiente` | → `Aprobada` | US-27 | Se crean `Contribuyente`, `PuntoVenta` e `Invitacion`. |
| `Pendiente` | → `Rechazada` | US-27 | Se guarda `MotivoRechazo`. No se crea nada de empresa. |

`Aprobada` y `Rechazada` son **terminales**. US-01 solo conoce `Pendiente`; las otras transiciones las implementa US-27.

`Vencida` **no** es un estado de la solicitud: una solicitud `Pendiente` no caduca. Solo las invitaciones tienen vencimiento.

### No revela si un CUIT ya existe (RNF-25)

Reglas obligatorias:

| Situación | Respuesta HTTP | Cuerpo | Efecto interno |
| --- | --- | --- | --- |
| Solicitud nueva y válida | `202` | `SOLICITUD_RECIBIDA` | Se crea la solicitud `Pendiente` y se envía el email. |
| CUIT ya registrado como contribuyente | `202` | `SOLICITUD_RECIBIDA` — **idéntico** | No se crea solicitud. Se registra el intento para el administrador global. |
| CUIT con solicitud `Pendiente` | `409` | `SOLICITUD_PENDIENTE` | No se crea nada. |
| Email con solicitud `Pendiente` | `409` | `SOLICITUD_PENDIENTE` | No se crea nada. |

El `409` revela que hay una solicitud **en revisión**, no que exista una empresa. Esa distinción está permitida por el criterio de aceptación ("No se permite otra solicitud pendiente con el mismo CUIT") y no contradice RNF-25, que protege la existencia de un contribuyente.

Además:

- El mensaje de error **no** enumera campos ni distingue "CUIT ya registrado" de "email duplicado": el texto es el mismo en los dos casos de `409`.
- El caso "CUIT ya registrado" **no** es un error: devuelve `202` con el mismo cuerpo que el éxito, de modo que un atacante no puede distinguirlo por el status, por el cuerpo ni por el tiempo de respuesta.
- El log de auditoría registra el intento (`solicitud.cuit_registrado`) para el administrador global, pero ese dato **no** sale por la API.

### Integración con otras historias

| Historia | Qué aporta | Qué recibe | Orden |
| --- | --- | --- | --- |
| US-29 | `IEmailSender`, `EmailService`, `Portal__BaseUrl`, `Admin__NotificationEmail` | `SolicitudRecibida` y `NuevaSolicitud` | **Primero**: US-01 depende de US-29 |
| US-04 | `EstadoSolicitud` si no existe, `RolGlobal`, `AdministradorFacturar`, `IEventoAuditoriaRepository` | Nada de esta historia | Puede ir en paralelo; US-01 crea `EstadoSolicitud` si US-04 todavía no lo hizo |
| US-27 | Estado `Aprobada` / `Rechazada`, `ContribuyenteId`, `InvitacionId` | `ISolicitudAltaRepository`, `IEventoAuditoriaRepository` | **Después** de US-01 |
| US-28 | Nada | Nada | Después de US-27 |
| US-17 | Outbox y auditoría completa | Los eventos `solicitud.creada` que US-01 ya escribe | **Después**: puede migrar los emails a la outbox |

### Dependencias técnicas

- `FacturAR.Domain`: crear `SolicitudAltaEmpresa` y `EstadoSolicitud`. Reutilizar `CondicionIva`, `CuitValidator` y `EventoAuditoria` (este último se extiende a `Guid? ContribuyenteId`, `Guid? UsuarioId`, `Guid? AdministradorId` y `ActorTipo`, según `docs/modelos.md`).
- `FacturAR.Application`: crear `ISolicitudAltaRepository`, `IContribuyenteRepository`, `IEventoAuditoriaRepository` y `CrearSolicitudAltaService`. **Application no referencia `MongoDB.Driver`.**
- `FacturAR.Infrastructure`: crear `SolicitudAltaRepository`, `ContribuyenteRepository` y `EventoAuditoriaRepository`; agregar `SolicitudesAlta` a `MongoCollections`, `MongoDbContext` y los tres índices en `MongoIndexInitializer`.
- `FacturAR.Contracts`: crear el namespace `Solicitudes` con `SolicitudAltaRequest` y `SolicitudAltaResponse`. Los DTO públicos van en Contracts; **no** se expone la entidad de dominio.
- `FacturAR.Api`: registrar el endpoint `POST /v1/solicitudes-alta` con `.Produces<ErrorResponse>(400/409/429)`, el rate limiter y la traducción de excepciones de dominio a `ErrorResponse`.
- `FacturAR.Web`: página pública del portal (`Components/Pages/SolicitarAlta.razor`, ruta `/solicitar-alta`) y ampliación de `FacturarApiClient` con `EnviarSolicitudAltaAsync`. El portal ya tiene `Api:BaseUrl` configurado en `Program.cs`.
- Rate limiting: `Microsoft.AspNetCore.RateLimiting` viene en el framework compartido de .NET 10. **Sin paquete NuGet nuevo.**
- Sin replica set: US-01 hace una sola inserción, no necesita transacciones.
- Tests: `FacturAR.UnitTests` cubre `CrearSolicitudAltaService` con repositorios falsos; `FacturAR.IntegrationTests` cubre el endpoint con `WebApplicationFactory` y, si hay `MONGO_CONNECTION_STRING`, los índices parciales reales (búsqueda por clave duplicada).

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-01-01 | No hay solicitudes ni contribuyentes; remitente de archivo activo | `POST /v1/solicitudes-alta` con CUIT `20123456786`, email `Solicitante@Demo.COM` y los 12 campos obligatorios | `202` con `{ "codigo": "SOLICITUD_RECIBIDA", ... }`; la solicitud queda `Pendiente` con CUIT `20123456786` y email `solicitante@demo.com`; se genera el email `SolicitudRecibida`; **no** se crean `Contribuyente`, `Usuario`, `PuntoVenta` ni `ApiKey` |
| CP-01-02 | — | Enviar la misma solicitud con CUIT `20123456787` (dígito verificador incorrecto) | `400 VALIDACION` con mensaje sobre el CUIT; no se guarda nada; el formulario muestra el error junto al campo CUIT |
| CP-01-03 | — | Enviar con email sin arroba y con razón social vacía | `400 VALIDACION`; el mensaje no enumera todos los campos fallados; no se guarda nada |
| CP-01-04 | Existe una solicitud `Pendiente` con CUIT `20123456786` | Enviar otra solicitud con ese CUIT y otro email | `409 SOLICITUD_PENDIENTE`; sigue habiendo una sola solicitud; no se envía email |
| CP-01-05 | Existe una solicitud `Pendiente` con email `solicitante@demo.com` | Enviar otra solicitud con otro CUIT y ese email | `409 SOLICITUD_PENDIENTE`; no se crea la segunda solicitud |
| CP-01-06 | Existe un `Contribuyente` con CUIT `20123456786` | Enviar una solicitud con ese CUIT | `202` con cuerpo **idéntico** al del caso válido; no se confirma la existencia del contribuyente; no se crea solicitud; se registra auditoría `solicitud.cuit_registrado` para el administrador global |
| CP-01-07 | La IP ya superó 5 solicitudes en el minuto | Enviar otra solicitud válida | `429 RATE_LIMIT` con `Retry-After`; no se guarda la solicitud |
| CP-01-08 | El email ya superó 3 solicitudes en la hora, IP nueva | Enviar otra solicitud válida | `429 RATE_LIMIT`; no se guarda la solicitud |
| CP-01-09 | Dos requests concurrentes con el mismo CUIT y el mismo email | Lanzar ambos a la vez | Solo queda una solicitud `Pendiente`; la otra recibe `409 SOLICITUD_PENDIENTE` por clave duplicada del índice parcial; no hay solicitudes duplicadas |
| CP-01-10 | El remitente falla (SMTP caído) | Enviar una solicitud válida | La solicitud queda persistida `Pendiente`; la respuesta es `202`; se registra auditoría `email.fallido`; el flujo no se revierte |
| CP-01-11 | Hay solicitudes de otras empresas y otros solicitantes | Consultar la API pública | Solo existe `POST /v1/solicitudes-alta`: no hay endpoint público de lectura, listado ni detalle; no se puede enumerar ni leer solicitudes ajenas |

## Notas de alcance

- No implementa aprobación, creación de empresa ni invitación; eso corresponde a US-27 y US-28.
- No implementa recuperación de contraseña ni login.
- No consulta AFIP/ARCA.
- No se debe guardar el token de invitación en esta historia.

## Estado de implementación

**Qué está implementado:**

- **Domain:** `SolicitudAltaEmpresa`, `EstadoSolicitud`, `SolicitudAltaValidationException` y `SolicitudPendienteException`.
- **Application:** `ISolicitudAltaRepository`, `IContribuyenteRepository`, `IEventoAuditoriaRepository`, `CrearSolicitudAltaService` (normalización, validaciones, duplicados, CUIT ya registrado, inserción `Pendiente` en UTC, email y auditoría) y `SolicitudAltaValidator`.
- **Infrastructure:** `SolicitudAltaRepository`, `ContribuyenteRepository`, `EventoAuditoriaRepository` y los tres índices (`ux_solicitudes_alta_cuit_pendiente`, `ux_solicitudes_alta_email_pendiente`, `ix_solicitudes_alta_estado_fecha`) en `MongoIndexInitializer`.
- **Contracts:** `SolicitudAltaRequest` y `SolicitudAltaResponse`.
- **Api:** `POST /v1/solicitudes-alta` con rate limit, `ErrorResponse` y traducción de `503 MONGO_NO_DISPONIBLE`.
- **Web:** página `/solicitar-alta` y `FacturarApiClient.EnviarSolicitudAltaAsync`.

**Evidencia de comandos:**

- `dotnet build FacturAR.slnx -c Release` → 0 errores, 0 advertencias.
- `dotnet test FacturAR.slnx -c Release --no-build` → UnitTests 113/113; IntegrationTests 49 superados, 27 omitidos (`MongoFact` sin `MONGO_CONNECTION_STRING`), 0 fallos; total 162 superados.
- `dotnet format FacturAR.slnx --verify-no-changes` → limpio (exit 0).
- Cobertura CP-01-01 a CP-01-11 mapeada a tests en `CrearSolicitudAltaServiceTests.cs`, `SolicitudAltaEndpointsTests.cs` y `MongoSolicitudAltaTests.cs` (estos últimos omitidos sin Mongo).
- QA con Playwright contra API (5171) y portal (5116) en local, **sin Mongo**: `/solicitar-alta` carga sin sesión; 12 campos presentes; CUIT con DV incorrecto (`20123456787`) muestra "El CUIT no es válido." junto al campo (`#cuit-error`, `aria-invalid=true`, `aria-describedby=cuit-error`) y no envía; CUIT con guiones (`20-12345678-6`) aceptado por la validación de cliente; sin aceptar términos muestra "Tenés que aceptar los términos para enviar la solicitud." y no envía; envío válido sin Mongo devuelve `503 {"codigo":"MONGO_NO_DISPONIBLE",...}` mostrado como `role=alert` claro, sin stack trace, con el formulario disponible y el botón rehabilitado; mobile 375×844 sin desbordes ni scroll horizontal con los 12 campos; consola del navegador con 0 errores.

**Dependencia agregada:** `Microsoft.Extensions.Logging.Abstractions` 10.0.0 en `FacturAR.Application`, para loguear las fallas best-effort de email y auditoría sin PII (justificación exigida por `AGENTS.md`).

**Verificación pendiente:**

- Los 8 tests `MongoFact` de `MongoSolicitudAltaTests` no se ejecutaron: persistencia, duplicados reales, CUIT de contribuyente, creación de índices, solicitud rechazada, concurrencia y email fallido.
- QA del camino feliz con Mongo real: `202`, `409` por duplicado, `429` por rate limit, email de confirmación real, índices parciales y persistencia en `solicitudesAlta`.
- El mensaje de éxito en la página y la generación del email de confirmación en un archivo real quedan sin verificar.

### Desvíos respecto del contrato técnico

a) El contrato propone dos políticas de rate limit (`solicitud-alta-ip`, `solicitud-alta-email`) declaradas con `RequireRateLimiting`. ASP.NET Core guarda un único `EnableRateLimitingAttribute` por endpoint y solo aplica el último, así que se registra **una** política `solicitud-alta` que encadena ambos límites (IP 5/min, email 3/h) con el rate limiter del framework. Se mantienen los mismos límites, el mismo `429 RATE_LIMIT` y el mismo `Retry-After`.

b) Limitación conocida: el portal llama a la API desde el servidor (Blazor Server), así que en despliegue todos los visitantes comparten la IP del contenedor del portal para el límite por IP; el límite por email sigue siendo por usuario. Queda para despliegue/ingress.

c) Limitación conocida: el envío de email ocurre dentro del request (según el flujo 7 del contrato), así que la latencia del SMTP afecta la respuesta y el caso "CUIT ya registrado" responde más rápido. La migración a outbox corresponde a US-17.
