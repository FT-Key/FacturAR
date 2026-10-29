# US-27 — Revisión y aprobación de solicitudes de empresa

**Épica:** Registro y multiempresa  
**Prioridad:** Alta  
**Estimación:** 3 días  
**Requisito:** RF-24  
**Depende de:** US-01, US-04, US-29  
**Habilita:** US-28

## Historia

Como administrador global de FacturAR, quiero revisar las solicitudes de alta, corregir sus datos y aprobarlas o rechazarlas para crear empresas verificadas y enviar una invitación al solicitante.

## Contexto

La aprobación es el punto de control humano del onboarding. Crea el contribuyente, su punto de venta inicial y una invitación. No crea el usuario todavía: eso ocurre cuando el solicitante acepta la invitación en US-28.

La operación debe ser atómica e idempotente: una solicitud no puede aprobarse dos veces ni generar dos contribuyentes.

## Criterios de aceptación

- [ ] El panel global lista solicitudes con estado, CUIT, razón social, email y fecha.
- [ ] Se puede filtrar por estado, CUIT, razón social y email.
- [ ] El detalle muestra todos los datos de la solicitud y su historial de revisión.
- [ ] El administrador puede editar razón social, condición IVA, email, teléfono y domicilio mientras la solicitud esté `Pendiente`.
- [ ] El CUIT se valida y normaliza antes de aprobar.
- [ ] No se puede aprobar una solicitud si el CUIT ya pertenece a un contribuyente.
- [ ] Aprobar crea `Contribuyente`, `PuntoVenta` número `1` "Casa Central" e `Invitacion` en una sola transacción.
- [ ] La solicitud pasa a `Aprobada` y registra administrador y fecha de revisión.
- [ ] La invitación se crea para el email del solicitante con rol `Administrador`, token hasheado y vencimiento de 48 horas.
- [ ] Se envía el email de invitación después de confirmar la transacción.
- [ ] Si el email falla, la aprobación no se revierte; el panel muestra una advertencia y permite reenviar.
- [ ] Aprobar dos veces la misma solicitud devuelve `409` y no duplica datos.
- [ ] Rechazar exige un motivo y deja la solicitud en `Rechazada`.
- [ ] Rechazar no crea contribuyente, punto de venta ni invitación.
- [ ] Un administrador `Soporte` puede ver y reenviar invitaciones, pero no editar, aprobar ni rechazar.
- [ ] Reenviar una invitación revoca la anterior y genera una nueva con nuevo token y vencimiento.
- [ ] Todas las acciones registran auditoría con actor global y solicitud.
- [ ] La UI muestra mensajes claros de éxito, conflicto y error de email.

## Escenarios Gherkin

```gherkin
Scenario: Aprobar una solicitud válida
  Given una solicitud pendiente con CUIT válido y email "solicitante@demo.com"
  And un administrador global con rol "SuperAdmin" autenticado
  When aprueba la solicitud
  Then se crea un contribuyente con ese CUIT
  And se crea el punto de venta 1 "Casa Central"
  And se crea una invitación para "solicitante@demo.com" con vencimiento a 48 horas
  And la solicitud queda en estado "Aprobada"
  And se envía el email de invitación
```

```gherkin
Scenario: Doble aprobación
  Given una solicitud ya aprobada
  When el administrador intenta aprobarla nuevamente
  Then la API devuelve 409 con código "SOLICITUD_YA_PROCESADA"
  And no se crea otro contribuyente
```

```gherkin
Scenario: CUIT duplicado al aprobar
  Given una solicitud pendiente con un CUIT que ya pertenece a otro contribuyente
  When el administrador intenta aprobarla
  Then la API devuelve 409 con código "CUIT_YA_REGISTRADO"
  And la solicitud permanece pendiente
  And no se crea una invitación
```

```gherkin
Scenario: Rechazo con motivo
  Given una solicitud pendiente
  When el administrador la rechaza con el motivo "Datos fiscales incompletos"
  Then la solicitud queda en estado "Rechazada"
  And se envía un email de rechazo con el motivo
  And no se crea ningún contribuyente
```

```gherkin
Scenario: Rechazo sin motivo
  Given una solicitud pendiente
  When el administrador intenta rechazarla sin motivo
  Then la API devuelve 400 con código "VALIDACION"
  And la solicitud permanece pendiente
```

```gherkin
Scenario: Soporte no puede aprobar
  Given un administrador global con rol "Soporte"
  When intenta aprobar una solicitud
  Then la API devuelve 403 con código "SIN_PERMISOS"
  And la solicitud permanece pendiente
```

```gherkin
Scenario: Reenviar invitación
  Given una solicitud aprobada con una invitación vencida
  When el administrador reenvía la invitación
  Then la invitación anterior queda revocada
  And se crea una nueva invitación con nuevo token
  And se envía un nuevo email
```

## Tareas técnicas

- [ ] Crear `Invitacion`, `EstadoInvitacion` y `TokenInvitacion` en Domain.
- [ ] Crear `IInvitacionRepository` y `ISolicitudAltaRepository` en Application.
- [ ] Implementar `AprobarSolicitudService` con transacción de MongoDB.
- [ ] Implementar `RechazarSolicitudService` con motivo obligatorio.
- [ ] Implementar `ReenviarInvitacionService` con revocación de la anterior.
- [ ] Crear endpoints `GET /v1/admin/solicitudes`, `GET /v1/admin/solicitudes/{id}` y `PUT /v1/admin/solicitudes/{id}`.
- [ ] Crear `POST /v1/admin/solicitudes/{id}/aprobar`.
- [ ] Crear `POST /v1/admin/solicitudes/{id}/rechazar`.
- [ ] Crear `POST /v1/admin/solicitudes/{id}/reenviar-invitacion`.
- [ ] Aplicar políticas `AdminGlobal` y `AdminSuper`.
- [ ] Crear el panel de solicitudes y el detalle en el portal.
- [ ] Integrar el envío de emails de US-29.
- [ ] Registrar auditoría de aprobación, rechazo y reenvío.
- [ ] Agregar índices para token de invitación y búsquedas del panel.
- [ ] Agregar tests de transacción, doble aprobación, CUIT duplicado y permisos.
- [ ] Documentar endpoints y códigos de error en OpenAPI y Postman.

## Definition of Done

- [ ] Una solicitud aprobada genera exactamente una empresa, un punto de venta y una invitación.
- [ ] Una solicitud rechazada no genera datos de empresa.
- [ ] El panel global permite revisar, editar, aprobar, rechazar y reenviar.
- [ ] Los permisos de `SuperAdmin` y `Soporte` están cubiertos por tests.
- [ ] Un fallo de email no pierde la aprobación y permite reenviar.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el proceso de alta.

## Contrato técnico

### Modelo de datos

US-27 agrega una colección nueva y extiende el modelo de auditoría. No duplica el modelo de la solicitud: consume `solicitudesAlta` de US-01.

| Elemento | Definición |
| --- | --- |
| Dominio | `FacturAR.Domain.Entities.Invitacion` |
| Dominio | `FacturAR.Domain.Enums.EstadoInvitacion`: `Pendiente = 1`, `Usada = 2`, `Vencida = 3`, `Revocada = 4` |
| Dominio | `FacturAR.Domain.ValueObjects.TokenInvitacion` (o clase equivalente en `Domain/Validation`): valor en memoria con `Token` plano, `Hash`, `Prefix` y `ExpiraAt` |
| Colección | `MongoCollections.Invitaciones = "invitaciones"` |
| Acceso | `MongoDbContext.Invitaciones` |

| Colección | Entidad | Estado en US-27 |
| --- | --- | --- |
| `solicitudesAlta` | `SolicitudAltaEmpresa` | La crea US-01. US-27 la lee, la edita y la transiciona. |
| `contribuyentes` | `Contribuyente` | La crea US-27 al aprobar. Ya existe el índice `ux_contribuyentes_cuit`. |
| `puntosVenta` | `PuntoVenta` | La crea US-27 al aprobar. Ya existe el índice `ux_puntos_venta_contribuyente_numero`. |
| `invitaciones` | `Invitacion` | **Nueva en US-27.** |
| `eventos` | `EventoAuditoria` | Se extiende la entidad en US-27 para soportar actores globales. |

Campos del documento `invitaciones` (alineados con `docs/modelos.md`):

| Campo | Tipo | Obligatorio | Notas |
| --- | --- | --- | --- |
| `Id` | `Guid` | Sí | Clave primaria. |
| `ContribuyenteId` | `Guid` | Sí | Empresa a activar. |
| `SolicitudId` | `Guid?` | No | Solicitud que la originó. |
| `Email` | `string` | Sí | Destinatario, normalizado a minúsculas. |
| `Rol` | `RolUsuario` | Sí | `Administrador` al aprobar. |
| `TokenHash` | `string` | Sí | SHA-256 hexadecimal en minúsculas del token. **Nunca** el token completo. |
| `TokenPrefix` | `string` | Sí | Primeros 8 caracteres del token, para que Soporte pueda identificarlo. |
| `ExpiraAt` | `DateTime` | Sí | UTC, `CreatedAt + 48 h`. |
| `UsadaAt` | `DateTime?` | No | UTC. |
| `RevocadaAt` | `DateTime?` | No | UTC. |
| `CreadaPorAdministradorId` | `Guid` | Sí | Administrador global que la creó o la reenvió. |
| `UltimoEnvioAt` | `DateTime?` | No | UTC, último intento de envío. |
| `IntentosEnvio` | `int` | Sí | Contador de intentos de envío. |
| `CreatedAt` | `DateTime` | Sí | UTC. |

El token plano **nunca** se persiste: existe solo en memoria mientras se crea la invitación y se arma el email. `TokenInvitacion` es un valor efímero.

Índices (nombres exactos, agregados en `MongoIndexInitializer`):

| Índice | Colección | Clave | Tipo | Único | Propósito |
| --- | --- | --- | --- | --- | --- |
| `ux_invitaciones_token_hash` | `invitaciones` | `TokenHash` | Simple | Sí | Búsqueda del token en US-28 y detección de token repetido. |
| `ix_invitaciones_contribuyente_email_expira` | `invitaciones` | `ContribuyenteId` asc, `Email` asc, `ExpiraAt` asc | Compuesto | No | Localizar la invitación vigente de una empresa. |
| `ix_invitaciones_expira` | `invitaciones` | `ExpiraAt` | Simple | No | Barrido de invitaciones vencidas. |
| `ix_eventos_administrador_fecha` | `eventos` | `AdministradorId` asc, `CreatedAt` desc | Compuesto | No | Auditoría global por administrador (solo `SuperAdmin`). |

El índice compuesto `ix_solicitudes_alta_estado_fecha` lo agrega US-01.

### Endpoints

Todos requieren cookie de sesión de la consola global (`cookies.admin`, de US-04) y un `AdministradorFacturar` activo.

| Método | Ruta | Política | Body | Respuesta | Errores |
| --- | --- | --- | --- | --- | --- |
| `GET` | `/v1/admin/solicitudes` | `AdminGlobal` | — (query: `page`, `pageSize`, `estado`, `cuit`, `razonSocial`, `email`) | `200` con `{ items, page, pageSize, total, totalPages }` | `400 VALIDACION`, `403 SIN_PERMISOS` |
| `GET` | `/v1/admin/solicitudes/{id}` | `AdminGlobal` | — | `200` con `SolicitudAltaDetalleResponse` | `403 SIN_PERMISOS`, `404 SOLICITUD_NO_ENCONTRADA` |
| `PUT` | `/v1/admin/solicitudes/{id}` | `AdminSuper` | `EditarSolicitudRequest` | `200` con la solicitud actualizada | `400 VALIDACION`, `403 SIN_PERMISOS`, `404 SOLICITUD_NO_ENCONTRADA`, `409 SOLICITUD_YA_PROCESADA` |
| `POST` | `/v1/admin/solicitudes/{id}/aprobar` | `AdminSuper` | — (sin body) | `200` con `AprobarSolicitudResponse` | `403 SIN_PERMISOS`, `404 SOLICITUD_NO_ENCONTRADA`, `409 SOLICITUD_YA_PROCESADA`, `409 CUIT_YA_REGISTRADO` |
| `POST` | `/v1/admin/solicitudes/{id}/rechazar` | `AdminSuper` | `RechazarSolicitudRequest { motivo }` | `200` con `RechazarSolicitudResponse` | `400 VALIDACION` (motivo vacío), `403 SIN_PERMISOS`, `404 SOLICITUD_NO_ENCONTRADA`, `409 SOLICITUD_YA_PROCESADA` |
| `POST` | `/v1/admin/solicitudes/{id}/reenviar-invitacion` | `AdminGlobal` | — (sin body) | `200` con `ReenviarInvitacionResponse` | `403 SIN_PERMISOS`, `404 SOLICITUD_NO_ENCONTRADA`, `404 INVITACION_NO_ENCONTRADA`, `502 EMAIL_NO_ENVIADO` |

Contratos en `FacturAR.Contracts.Solicitudes`:

```csharp
public sealed record EditarSolicitudRequest(
    string RazonSocial,
    int CondicionIva,
    string Email,
    string Telefono,
    string Domicilio);

public sealed record RechazarSolicitudRequest(string Motivo);

public sealed record AprobarSolicitudResponse(
    Guid ContribuyenteId,
    Guid InvitacionId,
    bool EmailEnviado,
    string? AdvertenciaEmail);

public sealed record RechazarSolicitudResponse(
    string Estado,
    bool EmailEnviado,
    string? AdvertenciaEmail);

public sealed record ReenviarInvitacionResponse(
    Guid InvitacionId,
    bool EmailEnviado,
    string? AdvertenciaEmail);
```

`AdvertenciaEmail` es un texto para la UI del panel ("No se pudo enviar el email de invitación. Podés reenviarlo."). No expone el token ni detalles del servidor de correo.

### Roles y políticas

| Política | Quién la cumple | Permite |
| --- | --- | --- |
| `AdminGlobal` | `SuperAdmin` y `Soporte` | Listar, ver el detalle y **reenviar invitaciones**. |
| `AdminSuper` | Solo `SuperAdmin` | **Editar**, **aprobar** y **rechazar** solicitudes. |

`Soporte` **nunca** aprueba ni rechaza: no alcanza para la política `AdminSuper`, así que recibe `403 SIN_PERMISOS`. La matriz de `docs/contratos-api.md` §3.2 se aplica así:

| Operación | SuperAdmin | Soporte |
| --- | --- | --- |
| Ver listado y detalle de solicitudes | Sí | Sí (consulta) |
| Editar una solicitud `Pendiente` | Sí | No |
| Aprobar una solicitud | Sí | No |
| Rechazar una solicitud | Sí | No |
| Reenviar una invitación | Sí | Sí |
| Ver la auditoría global | Sí | No |

El actor global se resuelve **siempre** de la sesión (`cookies.admin`), nunca del body ni de la URL. `ContribuyenteId` es `null` para toda acción de plataforma.

### Casos de uso y servicios

| Pieza | Tipo | Namespace | Responsabilidad |
| --- | --- | --- | --- |
| `AprobarSolicitudService` | Clase | `FacturAR.Application.Solicitudes` | Transacción: contribuyente + punto de venta + invitación + solicitud + auditoría. |
| `RechazarSolicitudService` | Clase | `FacturAR.Application.Solicitudes` | Motivo obligatorio, transición a `Rechazada` y auditoría. |
| `ReenviarInvitacionService` | Clase | `FacturAR.Application.Solicitudes` | Revoca la invitación anterior y crea una nueva con token y vencimiento nuevos. |
| `EditarSolicitudService` | Clase | `FacturAR.Application.Solicitudes` | Edita los campos permitidos solo mientras la solicitud está `Pendiente`. |
| `ListarSolicitudesService` / `ObtenerSolicitudService` | Clases | `FacturAR.Application.Solicitudes` | Consulta con filtros y paginación. |
| `ISolicitudAltaRepository` | Interfaz | `FacturAR.Application.Abstractions` | Reutilizada de US-01. Se le suma el filtro de estado para el compare-and-set. |
| `IInvitacionRepository` | Interfaz | `FacturAR.Application.Abstractions` | `InsertarAsync`, `ObtenerPorTokenHashAsync`, `ObtenerVigentePorSolicitudAsync`, `RevocarAsync`, `RegistrarEnvioAsync`. |
| `IContribuyenteRepository` | Interfaz | `FacturAR.Application.Abstractions` | Reutilizada de US-01. |
| `IPuntoVentaRepository` | Interfaz | `FacturAR.Application.Abstractions` | `InsertarAsync`. |
| `IEventoAuditoriaRepository` | Interfaz | `FacturAR.Application.Abstractions` | Reutilizada de US-01. |
| `ITransaccionMongo` | Interfaz | `FacturAR.Application.Abstractions` | Abstracción de la transacción. La implementa `FacturAR.Infrastructure.Mongo.TransaccionMongo` con `MongoDB.Driver`. Permite que Application no dependa del driver. |
| `TokenInvitacion` | Value object | `FacturAR.Domain` | Genera el token (32 bytes aleatorios, base64url), calcula `Hash` (SHA-256 hex minúsculas) y `Prefix` (8 caracteres), y fija `ExpiraAt = ahora + 48 h`. |

### Transacción de aprobación

La operación de negocio de "aprobar" es una **única transacción de MongoDB**. Requiere que la base sea un **replica set** (ver "Dependencias técnicas").

Dentro de `session.WithTransactionAsync(...)`:

1. Se relee la solicitud y se exige `Estado == Pendiente`.
2. Se exige que no exista un `Contribuyente` con ese CUIT. Si existe → se aborta la transacción y se responde `409 CUIT_YA_REGISTRADO` (el mensaje aclara que el CUIT ya está en uso; la respuesta es para un administrador global autenticado, no para el visitante).
3. Se inserta el `Contribuyente` (`Cuit` normalizado, `RazonSocial`, `CondicionIva`, `Email`, `Activo = true`).
4. Se inserta el `PuntoVenta` (`ContribuyenteId`, `Numero = 1`, `Descripcion = "Casa Central"`, `Activo = true`).
5. Se genera el token y se inserta la `Invitacion` (`ContribuyenteId`, `SolicitudId`, `Email`, `Rol = Administrador`, `TokenHash`, `TokenPrefix`, `ExpiraAt = now + 48 h`, `CreadaPorAdministradorId`, `Estado = Pendiente`).
6. Se actualiza la solicitud con el filtro `{ Id, Estado: Pendiente }` → `Estado = Aprobada`, `ContribuyenteId`, `InvitacionId`, `RevisadaPorAdministradorId`, `RevisadaAt`, `UpdatedAt`. Si `ModifiedCount == 0`, otra aprobación ganó la carrera → se aborta con `409 SOLICITUD_YA_PROCESADA`.
7. Se inserta el `EventoAuditoria` `solicitud.aprobada`.

**Después** del commit, y nunca antes, se envía el email `SolicitudAprobada` con el token plano. Si el envío falla, la transacción ya está confirmada: no se revierte nada, se responde `200` con `emailEnviado = false` y `advertenciaEmail`, y se registra auditoría `email.fallido`.

Rechazar es una transacción de una sola actualización + auditoría:

1. Se exige `motivo` no vacío (tras `Trim`), entre 10 y 500 caracteres → si no, `400 VALIDACION`.
2. Se actualiza la solicitud con el filtro `{ Id, Estado: Pendiente }` → `Estado = Rechazada`, `MotivoRechazo`, `RevisadaPorAdministradorId`, `RevisadaAt`, `UpdatedAt`.
3. Se inserta la auditoría `solicitud.rechazada`.

No se crea `Contribuyente`, `PuntoVenta` ni `Invitacion`. Después del commit se envía `SolicitudRechazada` con el motivo.

Reenviar invitación es una transacción de dos operaciones:

1. Se localiza la invitación vigente de la solicitud. Si no hay ninguna → `404 INVITACION_NO_ENCONTRADA`.
2. Se revoca la anterior (`Estado = Revocada`, `RevocadaAt = now`).
3. Se inserta la nueva (`TokenHash` y `TokenPrefix` nuevos, `ExpiraAt = now + 48 h`, `Estado = Pendiente`, `CreadaPorAdministradorId` = quien reenvía, `IntentosEnvio = 0`).
4. Se registra auditoría `invitacion.reenviada`.

Después del commit se envía `InvitacionReenviada`. Si el envío falla, se responde `502 EMAIL_NO_ENVIADO` (el envío **era** la operación pedida) y la nueva invitación sigue vigente para reenviar.

### Validaciones

| Regla | Mensaje | Dónde | Código |
| --- | --- | --- | --- |
| `motivo` del rechazo obligatorio (tras `Trim`, 10..500 caracteres) | `El motivo del rechazo es obligatorio.` | `RechazarSolicitudService` | `VALIDACION` (400) |
| `razonSocial` de la edición: 3..200 caracteres | `La razón social debe tener entre 3 y 200 caracteres.` | `EditarSolicitudService` | `VALIDACION` (400) |
| `email` de la edición con formato válido, normalizado a minúsculas | `El email no es válido.` | `EditarSolicitudService` | `VALIDACION` (400) |
| `telefono` de la edición: 8..32 caracteres | `El teléfono no es válido.` | `EditarSolicitudService` | `VALIDACION` (400) |
| `domicilio` de la edición: 5..200 caracteres | `El domicilio es obligatorio.` | `EditarSolicitudService` | `VALIDACION` (400) |
| `condicionIva` de la edición: valor existente del enum | `La condición frente al IVA no es válida.` | `EditarSolicitudService` | `VALIDACION` (400) |
| El CUIT de la solicitud se valida y normaliza antes de aprobar | `El CUIT de la solicitud no es válido.` | `AprobarSolicitudService` | `VALIDACION` (400) |
| La solicitud existe | `No existe la solicitud indicada.` | Endpoint | `SOLICITUD_NO_ENCONTRADA` (404) |
| La solicitud ya fue aprobada o rechazada | `La solicitud ya fue resuelta.` | Endpoint | `SOLICITUD_YA_PROCESADA` (409) |
| El CUIT ya pertenece a un contribuyente | `El CUIT ya está en uso por otra empresa.` | Endpoint | `CUIT_YA_REGISTRADO` (409) |
| El rol no alcanza | `No tenés permisos para realizar esta acción.` | Política | `SIN_PERMISOS` (403) |
| No hay invitación para reenviar | `No hay una invitación para reenviar.` | Endpoint | `INVITACION_NO_ENCONTRADA` (404) |
| El envío del email falló | `No se pudo enviar el email. Podés reenviar la invitación.` | Endpoint | `EMAIL_NO_ENVIADO` (502) |

### Estados

**Solicitud** (`EstadoSolicitud`):

```
            CrearSolicitudAltaService (US-01)
                         │
                         ▼
                    ┌─────────┐   editar (mientras sea Pendiente)   ┌─────────┐
                    │ Pendiente│ ─────────────────────────────────►  │ Pendiente│
                    └─────────┘                                      └─────────┘
                      │        │
      aprobar (US-27) │        │ rechazar (US-27)
                      ▼        ▼
                 ┌─────────┐  ┌───────────┐
                 │ Aprobada │  │ Rechazada │
                 └─────────┘  └───────────┘
                   (terminal)     (terminal)
```

**Invitación** (`EstadoInvitacion`):

```
                     ┌───────────┐
   Crear (aprobar) ─►│ Pendiente │
                     └───────────┘
                        │     │
      Aceptar (US-28)   │     │   Reenviar (US-27)
                        ▼     ▼
                   ┌────────┐ ┌──────────┐
                   │  Usada │ │ Revocada │
                   └────────┘ └──────────┘
                     (terminal)  (terminal)
```

`Vencida` es un estado **derivado** de `ExpiraAt`: el documento puede seguir en `Pendiente` y la vista lo resuelve como vencido cuando `ExpiraAt < UtcNow`. No hace falta un job que lo reescriba.

Transiciones válidas y único responsable:

Solicitud (`SolicitudAltaEmpresa`):

| De | A | Quién | Qué hace |
| --- | --- | --- | --- |
| — | `Pendiente` | US-01 | Inserta la solicitud. |
| `Pendiente` | `Pendiente` | US-27 (editar) | Actualiza los campos permitidos y `UpdatedAt`. |
| `Pendiente` | `Aprobada` | US-27 | Crea contribuyente, punto de venta e invitación. |
| `Pendiente` | `Rechazada` | US-27 | Guarda el motivo. |
| `Aprobada` / `Rechazada` | — | — | **Terminales.** Cualquier intento devuelve `409 SOLICITUD_YA_PROCESADA`. |

Invitación (`Invitacion`):

| De | A | Quién | Qué hace |
| --- | --- | --- | --- |
| — | `Pendiente` | US-27 (aprobar o reenviar) | Inserta la invitación con token y vencimiento de 48 h. |
| `Pendiente` | `Usada` | US-28 | Crea el usuario administrador en la misma transacción. |
| `Pendiente` | `Revocada` | US-27 | Reenvío: la anterior queda sin efecto. |
| `Usada` / `Revocada` | — | — | **Terminales.** Cualquier intento devuelve `404 INVITACION_NO_ENCONTRADA`. |


### Integración con otras historias

| Historia | Qué aporta a US-27 | Qué recibe de US-27 | Orden |
| --- | --- | --- | --- |
| US-01 | Colección `solicitudesAlta`, `SolicitudAltaEmpresa`, `ISolicitudAltaRepository`, `IEventoAuditoriaRepository`, `IContribuyenteRepository` | — | **Primero** |
| US-04 | `RolGlobal`, `AdministradorFacturar`, `PasswordHasher`, políticas `AdminGlobal` y `AdminSuper`, cookie `cookies.admin`, abstracción del actor global | — | **Antes de US-27** |
| US-29 | `IEmailSender`, `EmailService`, `Portal__BaseUrl` | Las 3 plantillas de US-27 | **Antes de US-27** |
| US-28 | El alta del usuario administrador y el consumo de la invitación | La `Invitacion` con su `TokenHash` y `ExpiraAt` | **Después** |
| US-17 | Outbox, reintentos y auditoría completa | Los eventos `solicitud.aprobada`, `solicitud.rechazada`, `invitacion.reenviada` y `email.*` | **Después**: puede migrar los emails a la outbox |
| US-22 (stretch) | — | Podría anular solicitudes aprobadas | Fuera de alcance |

### Dependencias técnicas

- **MongoDB con replica set**: la aprobación, el rechazo y el reenvío usan transacciones (`WithTransactionAsync`). Un MongoDB standalone **no** soporta transacciones. En Atlas (M0 o superior) ya es replica set; en Docker local hay que correr el contenedor con `--replSet rs0` y ejecutar `rs.initiate()`. Si el entorno de desarrollo no es replica set, la suite de integración de transacciones no puede correr y hay que resolverlo antes del DoD.
- `FacturAR.Domain`: crear `Invitacion`, `EstadoInvitacion` y `TokenInvitacion`. Reutilizar `Contribuyente`, `PuntoVenta`, `RolUsuario.Administrador` y `CondicionIva`. **No** crear `Usuario` en esta historia: lo crea US-28.
- `FacturAR.Domain` (auditoría): US-01 ya extiende `EventoAuditoria` con `Guid? ContribuyenteId`, `Guid? UsuarioId`, `Guid? AdministradorId` y `string ActorTipo` (`Empresa`, `Administrador`, `Sistema`), según `docs/modelos.md`. US-27 la usa tal cual: las acciones globales usan `ContribuyenteId = null` y `AdministradorId` con valor.
- `FacturAR.Application`: crear `AprobarSolicitudService`, `RechazarSolicitudService`, `ReenviarInvitacionService`, `EditarSolicitudService`, `ListarSolicitudesService`, `ObtenerSolicitudService`, `IInvitacionRepository`, `IPuntoVentaRepository` e `ITransaccionMongo`. **Application no referencia `MongoDB.Driver`**: la transacción entra por la interfaz.
- `FacturAR.Infrastructure`: crear `InvitacionRepository`, `PuntoVentaRepository` y `TransaccionMongo`; agregar `Invitaciones` a `MongoCollections` y `MongoDbContext`; agregar `ux_invitaciones_token_hash`, `ix_invitaciones_contribuyente_email_expira`, `ix_invitaciones_expira` e `ix_eventos_administrador_fecha` en `MongoIndexInitializer`.
- `FacturAR.Contracts`: crear el namespace `Solicitudes` con los DTO de la tabla de endpoints. **Los DTOs públicos van en Contracts**; no se expone `SolicitudAltaEmpresa` ni `Invitacion`.
- `FacturAR.Api`: registrar los seis endpoints con las políticas `AdminGlobal` y `AdminSuper`, `.Produces<ErrorResponse>` y la traducción de excepciones de dominio.
- `FacturAR.Web`: página del panel (`Components/Pages/Admin/Solicitudes.razor`, ruta `/admin/solicitudes`), detalle (`Admin/SolicitudDetalle.razor`, ruta `/admin/solicitudes/{id}`) y el cliente HTTP en `FacturarApiClient`. El panel usa la sesión `cookies.admin`; nunca usa la sesión de empresa.
- `docs/contratos-api.md` §14 ya reserva estas rutas. No se agregan rutas nuevas.
- Tests: `FacturAR.UnitTests` con repositorios falsos para los servicios; `FacturAR.IntegrationTests` con `WebApplicationFactory` para los endpoints y, si hay `MONGO_CONNECTION_STRING`, para la transacción real y la doble aprobación concurrente.

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-27-01 | Solicitud `Pendiente` con CUIT `20123456786` y email `solicitante@demo.com`; `SuperAdmin` autenticado | `POST /v1/admin/solicitudes/{id}/aprobar` | `200`; se crea 1 contribuyente con ese CUIT, 1 `PuntoVenta` `1 Casa Central` activo y 1 `Invitacion` de rol `Administrador` con `ExpiraAt` a 48 h; la solicitud queda `Aprobada` con `RevisadaPorAdministradorId` y `RevisadaAt`; se envía `SolicitudAprobada` con el link; se registra auditoría `solicitud.aprobada` con el administrador global |
| CP-27-02 | Solicitud ya `Aprobada` | `POST .../aprobar` de nuevo | `409 SOLICITUD_YA_PROCESADA`; no se crea otro contribuyente, ni otro punto de venta, ni otra invitación; no se envía email |
| CP-27-03 | Existe un `Contribuyente` con el CUIT de la solicitud | `POST .../aprobar` | `409 CUIT_YA_REGISTRADO`; la solicitud sigue `Pendiente`; no se crea invitación ni punto de venta; no se envía email |
| CP-27-04 | Solicitud `Pendiente` | `POST .../rechazar` con motivo `Datos fiscales incompletos` | `200`; la solicitud queda `Rechazada` con el motivo; se envía `SolicitudRechazada` con ese motivo; **no** se crea contribuyente, punto de venta ni invitación |
| CP-27-05 | Solicitud `Pendiente` | `POST .../rechazar` con motivo vacío o de menos de 10 caracteres | `400 VALIDACION` con mensaje del motivo; la solicitud sigue `Pendiente`; no se envía email |
| CP-27-06 | Administrador global con rol `Soporte` | `POST .../aprobar` y `POST .../rechazar` | Ambos devuelven `403 SIN_PERMISOS`; la solicitud sigue `Pendiente`; no se crea nada |
| CP-27-07 | Administrador global con rol `Soporte` | `GET /v1/admin/solicitudes` y `POST .../reenviar-invitacion` | La consulta devuelve `200` con el listado; el reenvío devuelve `200`, la invitación anterior queda `Revocada`, la nueva tiene otro `TokenHash` y otra `ExpiraAt`, y se envía `InvitacionReenviada` |
| CP-27-08 | SMTP caído | `POST .../aprobar` | `200` con `emailEnviado = false` y `advertenciaEmail`; el contribuyente, el punto de venta y la invitación **existen**; se registra auditoría `email.fallido`; el panel muestra la advertencia y permite reenviar |
| CP-27-09 | Dos requests concurrentes de `aprobar` sobre la misma solicitud | Lanzar ambos a la vez | Solo una transacción confirma: un solo contribuyente, un punto de venta y una invitación; la otra recibe `409 SOLICITUD_YA_PROCESADA` |
| CP-27-10 | Solicitudes con estados, CUIT, razón social y email distintos | `GET /v1/admin/solicitudes?estado=Pendiente&cuit=20123456786` y variantes por `razonSocial` y `email` | Cada filtro devuelve solo las solicitudes que corresponde, con `items`, `page`, `pageSize`, `total` y `totalPages` |
| CP-27-11 | Solicitud `Pendiente` | `PUT /v1/admin/solicitudes/{id}` con razón social, condición IVA, email, teléfono y domicilio | `200`; los cinco campos quedan actualizados; el CUIT, el email inicial del solicitante fuera de los editables y el estado no cambian; el `UpdatedAt` se actualiza |
| CP-27-12 | Solicitud `Aprobada` | `PUT /v1/admin/solicitudes/{id}` | `409 SOLICITUD_YA_PROCESADA`; ningún campo cambia |
| CP-27-13 | Cualquier acción de la consola global | Aprobar, rechazar, editar y reenviar | Cada acción deja un `EventoAuditoria` con `AdministradorId` del actor, `ActorTipo = "Administrador"`, `ContribuyenteId = null` y `Entidad = "SolicitudAltaEmpresa"`; ningún evento contiene token, contraseña ni motivo completo fuera del motivo de rechazo |

## Notas de alcance

- No crea el usuario administrador; eso ocurre en US-28.
- No permite aprobar solicitudes con CUIT duplicado.
- No implementa edición de contribuyentes ya creados.
- No implementa eliminación física de solicitudes.
