# US-17 — Auditoría y outbox

**Épica:** Operación  
**Prioridad:** Media  
**Estimación:** 2 días  
**Requisito:** RF-17  
**Depende de:** US-05, US-27, US-28  
**Habilita:** US-23, US-30

## Historia

Como equipo de operación, quiero registrar eventos de negocio y encolar notificaciones de forma confiable para auditar el sistema y reintentar tareas sin perder información.

## Contexto

La auditoría registra acciones de usuarios de empresa y administradores globales. El outbox guarda eventos pendientes y un worker los procesa con reintentos. Los emails de onboarding pueden migrar al outbox cuando US-29 esté implementada.

## Criterios de aceptación

- [ ] Se registran eventos de login, logout, alta de solicitud, aprobación, rechazo, activación, creación/revocación de API key y emisión.
- [ ] Cada evento incluye actor, acción, entidad, entidad id, fecha UTC y datos de contexto.
- [ ] Los eventos de administradores globales no requieren `ContribuyenteId`.
- [ ] No se guardan contraseñas, tokens, API keys ni datos sensibles completos.
- [ ] El outbox guarda tipo, payload, estado, intentos, fecha y último error.
- [ ] Un worker procesa mensajes pendientes con reintentos y backoff.
- [ ] Un mensaje procesado no se vuelve a procesar.
- [ ] Un mensaje fallido queda en estado `Fallido` después del máximo de intentos.
- [ ] Se puede consultar el estado de un mensaje para soporte.
- [ ] Los índices soportan la consulta por estado y fecha.
- [ ] Hay tests de auditoría, procesamiento, reintento e idempotencia.
- [ ] No se registran secretos en logs ni en el outbox.

## Escenarios Gherkin

```gherkin
Scenario: Auditar aprobación
  Given un administrador global aprueba una solicitud
  When se completa la operación
  Then se registra un evento "SolicitudAprobada"
  And el evento incluye el administrador y la solicitud
  And no incluye el token de invitación
```

```gherkin
Scenario: Procesar outbox
  Given un mensaje pendiente en el outbox
  When el worker lo procesa correctamente
  Then el mensaje queda "Procesado"
  And no se vuelve a procesar
```

```gherkin
Scenario: Reintento
  Given un mensaje que falla al procesarse
  When el worker reintenta
  Then aumenta el contador de intentos
  And se registra el último error
```

```gherkin
Scenario: Máximo de intentos
  Given un mensaje que supera el máximo de intentos
  When el worker lo procesa otra vez
  Then el mensaje queda "Fallido"
  And queda disponible para revisión
```

## Tareas técnicas

- [ ] Extender `EventoAuditoria` con actor global y `ContribuyenteId` nullable.
- [ ] Crear `IAuditoriaService` y el repositorio.
- [ ] Registrar eventos en los casos de uso principales.
- [ ] Crear `OutboxMessage` y `IOutboxRepository`.
- [ ] Implementar el worker de outbox con reintentos y backoff.
- [ ] Crear índices de auditoría y outbox.
- [ ] Integrar emails de onboarding si US-29 lo permite.
- [ ] Agregar tests de auditoría, worker y reintentos.
- [ ] Documentar eventos y operación.

## Definition of Done

- [ ] Las acciones críticas quedan auditadas.
- [ ] El outbox procesa y reintenta sin duplicar.
- [ ] Los secretos no se persisten en auditoría ni outbox.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el proceso de alta.

## Contrato técnico

### Modelo de datos

#### `eventos` — auditoría

La colección `eventos` ya existe (`MongoCollections.Eventos`). US-17 **extiende** la entidad `EventoAuditoria` sin cambiar su nombre ni su clave primaria:

| Campo | Tipo | Cambio | Notas |
| --- | --- | --- | --- |
| `Id` | `Guid` | Sin cambio | Clave primaria |
| `ContribuyenteId` | `Guid?` | **Cambia** de `Guid` a `Guid?` | `null` para eventos de plataforma |
| `UsuarioId` | `Guid?` | Sin cambio | Actor de empresa |
| `AdministradorId` | `Guid?` | **Nuevo** | Actor global |
| `ActorTipo` | `string` | **Nuevo** | `Empresa`, `Administrador` o `Sistema` |
| `Accion` | `string` | Sin cambio | Verbo de negocio |
| `Entidad` | `string` | Sin cambio | Entidad afectada |
| `EntidadId` | `string?` | Sin cambio | Identificador |
| `Datos` | `string?` | Sin cambio | JSON de contexto sin secretos |
| `CreatedAt` | `DateTime` | Sin cambio | UTC |

`ActorTipo` es un `string` (no enum) para no agregar conversores de serialización en la capa de dominio. Los tres valores admitidos son `Empresa`, `Administrador` y `Sistema`, exactamente como los define `docs/modelos.md`.

Reglas de coherencia por tipo de actor:

| `ActorTipo` | `ContribuyenteId` | `UsuarioId` | `AdministradorId` | Cuándo |
| --- | --- | --- | --- | --- |
| `Empresa` | Obligatorio | Obligatorio | `null` | Acción de un usuario de empresa autenticado |
| `Administrador` | `null` | `null` | Obligatorio | Acción de un administrador global |
| `Sistema` | `null` o el contribuyente afectado | `null` | `null` | Procesos automáticos: worker de la outbox, seed y tareas programadas |

Nunca se escriben los dos actores a la vez. Un evento de plataforma siempre lleva `ContribuyenteId = null`.

**Datos que nunca se auditan.** El campo `Datos` es un JSON con lista blanca por acción. Quedan prohibidos:

- Tokens de invitación, completos o con prefijo.
- API keys completas, sus prefijos y sus hashes (`ApiKey.Hash`).
- Contraseñas y sus hashes (`PasswordHash`).
- Emails completos del solicitante o del usuario: se enmascaran (`j***@ejemplo.com`) o se omiten.
- Tokens de sesión, cookies y secretos de configuración.

El único identificador de API key admisible es el `Id` interno de la key, nunca su `Prefix` ni su `Hash`.

#### `outbox` — cola transaccional

La colección `outbox` ya existe (`MongoCollections.Outbox`). `OutboxMessage` se mantiene y se le documenta el ciclo de estados completo:

| Campo | Tipo | Notas |
| --- | --- | --- |
| `Id` | `Guid` | Clave primaria |
| `Tipo` | `string` | Tipo de efecto, por ejemplo `EmailSolicitudAprobada` |
| `Payload` | `string` | JSON serializado, **sin secretos** |
| `Estado` | `string` | `Pendiente`, `Procesando`, `Procesado`, `Fallido` |
| `Intentos` | `int` | Intentos de procesamiento ya realizados |
| `CreatedAt` | `DateTime` | UTC |
| `ProcesadoAt` | `DateTime?` | UTC, se setea al pasar a `Procesado` |
| `Error` | `string?` | Último error, sin secretos ni stack traces |

Transiciones válidas:

```text
Pendiente --(claim)--> Procesando --(ok)--> Procesado
Procesando --(error, intentos < 5)--> Pendiente
Procesando --(error, intentos = 5)--> Fallido
```

`Procesando` es un estado transitorio que el worker usa para reclamar el mensaje de forma atómica. Es la única ampliación a la lista de estados de `docs/modelos.md` (`Pendiente`, `Procesado`, `Fallido`) y US-17 debe actualizar ese documento.

#### Índices

| Colección | Índice | Tipo | Estado |
| --- | --- | --- | --- |
| `eventos` | `ix_eventos_contribuyente_fecha` — `ContribuyenteId` + `CreatedAt` desc | Compuesto | Ya existe; se mantiene |
| `eventos` | `ix_eventos_administrador_fecha` — `AdministradorId` + `CreatedAt` desc | Compuesto | **Nuevo** en US-17 |
| `outbox` | `ix_outbox_estado_fecha` — `Estado` + `CreatedAt` | Compuesto | Ya existe; soporta la consulta por estado y fecha |
| `outbox` | Claim por `Id` | `_id` por defecto | El reclamo se hace por `_id` + condición de estado, no necesita un índice nuevo |

El índice de `eventos` con `AdministradorId` no puede tener `ContribuyenteId` como prefijo porque para eventos de plataforma `ContribuyenteId` es `null`.

### Operaciones

US-17 no expone endpoints REST públicos. Las operaciones son internas:

| Operación | Mecanismo | Autenticación | Request | Response | Errores |
| --- | --- | --- | --- | --- | --- |
| Registrar evento de auditoría | `IAuditoriaService.RegistrarAsync` (Application) | Contexto de la petición: `ContribuyenteId` / `UsuarioId` / `AdministradorId` / `ActorTipo` | `Accion`, `Entidad`, `EntidadId`, `Datos` (JSON enmascarado) | Documento insertado en `eventos` | `MONGO_NO_DISPONIBLE` 503, `ERROR_INTERNO` 500 |
| Encolar efecto externo | `IOutboxRepository.EncolarAsync` | Contexto de la operación de negocio | `Tipo`, `Payload` | Documento insertado en `outbox` con `Estado` = `Pendiente` | `MONGO_NO_DISPONIBLE` 503, `ERROR_INTERNO` 500 |
| Procesar mensajes pendientes | `OutboxWorker : BackgroundService` | Ninguna (proceso interno) | Lote de mensajes `Pendiente` | Mensajes en `Procesado` o `Fallido` | Errores por mensaje: se registran en `Error` y se reintentan |
| Consultar estado de un mensaje para soporte | Consulta de **solo lectura** sobre `outbox` (filtro por `Id`, o por `Estado` + `CreatedAt` con `ix_outbox_estado_fecha`) | Operativa (soporte con acceso a la base) | — | Documento `outbox` con `Estado`, `Intentos`, `ProcesadoAt`, `Error` | `MONGO_NO_DISPONIBLE` 503 |

La consulta de soporte no se expone como endpoint HTTP: una consola de auditoría queda fuera del alcance de la historia. El filtro y el índice a usar quedan documentados para que soporte pueda ejecutarlos.

### Acciones auditadas

| `Accion` | `Entidad` | `ActorTipo` | Origen |
| --- | --- | --- | --- |
| `LoginExitoso`, `LoginFallido`, `Logout` | `Usuario` | `Empresa` | US-04 |
| `SolicitudCreada` | `SolicitudAltaEmpresa` | `Empresa` (visitante) o `Sistema` | US-01 |
| `SolicitudAprobada`, `SolicitudRechazada` | `SolicitudAltaEmpresa` | `Administrador` | US-27 |
| `InvitacionEnviada`, `InvitacionReenviada` | `Invitacion` | `Administrador` | US-27, US-29 |
| `CuentaActivada` | `Usuario` | `Empresa` | US-28 |
| `ApiKeyCreada`, `ApiKeyRevocada`, `ApiKeyRegenerada` | `ApiKey` | `Empresa` | US-02 |
| `ComprobanteEmitido` | `Comprobante` | `Empresa` | US-05 |
| `NotaEmitida` | `Comprobante` | `Empresa` | US-08 |
| `OutboxProcesado`, `OutboxFallido` | `OutboxMessage` | `Sistema` | US-17 |

`EntidadId` guarda el `Id` de la entidad afectada, formateado como string.

### Casos de uso y servicios

| Pieza | Capa | Responsabilidad |
| --- | --- | --- |
| `IAuditoriaService` | `FacturAR.Application/Abstractions` | Contrato de escritura de eventos. La implementación vive en Infrastructure |
| `AuditoriaRepository` | `FacturAR.Infrastructure` | Inserción en `eventos` |
| `IOutboxRepository` | `FacturAR.Application/Abstractions` | `EncolarAsync`, `ReclamarPendientesAsync`, `MarcarProcesadoAsync`, `MarcarFallidoAsync`, `ConsultarAsync` |
| `OutboxRepository` | `FacturAR.Infrastructure` | Implementación sobre `outbox` con el reclamo atómico |
| `OutboxWorker` | `FacturAR.Infrastructure/Outbox` | `BackgroundService` que barre, reclama, procesa y reintenta |
| `IOutboxHandler` | `FacturAR.Application/Abstractions` | Un handler por `OutboxMessage.Tipo`. `HandleAsync(OutboxMessage, CancellationToken)` |
| `IOutboxHandlerResolver` | `FacturAR.Infrastructure` (o Application con registro en DI) | Resuelve el handler según `Tipo`. Un `Tipo` sin handler deja el mensaje en `Fallido` con el error "no hay handler registrado" |

**Sobre la ubicación del worker.** `OutboxWorker` es un `BackgroundService`, así que necesita el abstrato de hosting. `FacturAR.Infrastructure` es una librería de clase sin `Microsoft.AspNetCore.App`; para no sumar un paquete NuGet se le agrega un `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, que es una referencia de framework y no una dependencia nueva. La alternativa —ubicar el worker en `FacturAR.Api`— se descarta para no mezclar el servicio de fondo con el hosting HTTP.

**Reclamo atómico.** El worker toma mensajes con una actualización condicional: filtro `{ _id: mensaje.Id, Estado: "Pendiente" }` y actualización a `Estado = "Procesando"` con `Intentos = Intentos + 1`. Si la actualización no modifica ningún documento, otro worker ya lo reclamó y se lo descarta. Dos workers nunca procesan el mismo mensaje.

**Ciclo de reintento.**

1. Se reclama el mensaje (`Pendiente` → `Procesando`, `Intentos` + 1).
2. Se invoca el handler.
3. Si termina bien: `Estado = "Procesado"`, `ProcesadoAt = UtcNow`, `Error = null`.
4. Si falla: se guarda `Error` (mensaje sin stack trace ni secretos). Si `Intentos < 5`, el mensaje vuelve a `Pendiente` y se espera el backoff. Si `Intentos = 5`, pasa a `Fallido` y queda disponible para revisión.

**Backoff exponencial.** El tiempo de espera antes del siguiente intento es `IntervaloBase × 2^(Intentos − 1)`, con el valor por defecto `IntervaloBase = 5 s`: 5 s, 10 s, 20 s, 40 s y 80 s. Se agrega un jitter aleatorio de hasta el 20 % para que varios mensajes fallidos no reintenten en el mismo instante.

**Idempotencia del procesamiento.** Un mensaje en `Procesado` no se vuelve a reclamar (el filtro exige `Estado = "Pendiente"`). Un handler debe poder ejecutarse más de vez si el proceso muere entre el efecto externo y el marcado: el handler es idempotente por diseño (por ejemplo, un email se identifica por su `Tipo` + `Id` de la entidad de origen).

**Escritura transaccional.** El mensaje de la outbox se escribe **en la misma operación** que el cambio de estado de negocio, nunca después. Si el cambio de negocio y el encolado ocurren en la misma transacción de MongoDB, la consistencia es total; si la operación de negocio no usa transacción, el orden obligatorio es: persistir el cambio de negocio y luego encolar, sincrónicamente, antes de responder.

**Integración con US-29 (opcional).** Los emails de onboarding **pueden** migrar a la outbox: `IEmailSender` pasa a ser un handler de outbox. **No es obligatorio** para cerrar US-17. Si US-29 ya está implementada con envío directo, US-17 se cierra igual, y la migración queda como mejora posterior. Lo que US-17 sí exige es que el `BackgroundService` exista, tenga reintentos y pueda registrar handlers.

### Configuración

| Clave | Tipo | Default | Uso |
| --- | --- | --- | --- |
| `Outbox__Habilitado` | `bool` | `true` | Permite desactivar el worker en un ambiente |
| `Outbox__MaxIntentos` | `int` | `5` | Cantidad máxima de intentos antes de `Fallido` |
| `Outbox__IntervaloBaseSegundos` | `int` | `5` | Base del backoff exponencial |
| `Outbox__IntervaloBarridoSegundos` | `int` | `10` | Espera entre barridos del worker |
| `Outbox__LoteTamano` | `int` | `20` | Cantidad máxima de mensajes reclamados por barrido |

Todas se leen con el prefijo `Outbox__` y se pueden sobrescribir por variable de entorno. Ninguna contiene secretos.

### Integración con otras historias

| Historia | Relación |
| --- | --- |
| US-02 | Aporta las API keys: los hashes nunca se auditan |
| US-05 | Aporta la emisión: se audita `ComprobanteEmitido` |
| US-27 | Aporta la aprobación y el rechazo: eventos de `Administrador` |
| US-28 | Aporta la activación: evento `CuentaActivada` |
| US-29 | **Opcional**: los emails pueden migrar a la outbox. No bloquea el cierre de US-17 |
| US-23 (stretch) | Los webhooks reutilizarán la outbox como mecanismo de entrega |
| US-30 | El manual online documentará los eventos de auditoría relevantes para integradores |

### Documentación a sincronizar

- `docs/modelos.md`: agregar `AdministradorId` y `ActorTipo` a `EventoAuditoria`, cambiar `ContribuyenteId` a `Guid?`, agregar el estado `Procesando` a `OutboxMessage` y registrar el índice `ix_eventos_administrador_fecha`.
- `docs/contratos-api.md` §13: alinear la descripción del ciclo de la outbox con los estados y el máximo de intentos definidos acá.
- `docs/proceso-alta-empresa.md`: documentar qué evento se emite en cada paso del onboarding, sin valores sensibles.

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-17.01 | Administrador global autenticado | Aprobar una solicitud | Evento `SolicitudAprobada` con `ActorTipo` = `Administrador`, `AdministradorId` informado, `ContribuyenteId` = `null` y `UsuarioId` = `null` |
| CP-17.02 | Usuario de empresa autenticado | Emitir un comprobante | Evento `ComprobanteEmitido` con `ActorTipo` = `Empresa`, `ContribuyenteId` y `UsuarioId` informados, `AdministradorId` = `null` |
| CP-17.03 | Worker de la outbox | Procesar un mensaje | Evento `OutboxProcesado` con `ActorTipo` = `Sistema` y sin actor de empresa ni de administrador |
| CP-17.04 | Aprobación de una solicitud | Inspeccionar el `Datos` del evento | No contiene el token de invitación, su prefijo ni el email completo del solicitante |
| CP-17.05 | Creación de una API key | Inspeccionar el evento `ApiKeyCreada` | No contiene la key, su `Prefix` ni su `Hash`; solo el `Id` interno y el nombre |
| CP-17.06 | Usuario de empresa | Iniciar sesión con contraseña incorrecta y con la correcta | Se registran `LoginFallido` y `LoginExitoso`; ningún evento contiene la contraseña ni su hash |
| CP-17.07 | Mensaje `Pendiente` en la outbox | Dejar correr el worker | El mensaje queda `Procesado`, con `ProcesadoAt` informado y `Intentos` = 1 |
| CP-17.08 | Worker con el mismo mensaje ya `Procesado` | Volver a barrer | El mensaje no se vuelve a reclamar ni a procesar; `Intentos` no cambia |
| CP-17.09 | Handler que siempre falla | Dejar correr el worker | `Intentos` aumenta, `Error` queda registrado y el mensaje vuelve a `Pendiente` |
| CP-17.10 | Handler que siempre falla | Dejar correr el worker hasta agotar los intentos | Tras el quinto intento el mensaje queda `Fallido` y disponible para revisión |
| CP-17.11 | Varios mensajes fallidos a la vez | Observar los tiempos entre intentos | El intervalo crece de forma exponencial (5 s, 10 s, 20 s, 40 s, 80 s con el valor por defecto) |
| CP-17.12 | Dos workers activos y un mismo mensaje `Pendiente` | Procesar en paralelo | Solo uno lo reclama; el otro descarta el mensaje porque la actualización condicional no modifica nada |
| CP-17.13 | Base con eventos de plataforma y de empresa | Consultar por `ContribuyenteId` y por `AdministradorId` | Cada consulta usa el índice compuesto correspondiente y devuelve solo los eventos de ese actor |
| CP-17.14 | Base con mensajes en los cuatro estados | Consultar por `Estado` + `CreatedAt` | La consulta se apoya en `ix_outbox_estado_fecha` y devuelve el conjunto esperado |
| CP-17.15 | `Outbox__MaxIntentos` = 2 | Ejecutar un handler que falla | El mensaje queda `Fallido` al segundo intento, con el valor configurado y no con el default |

## Dependencias técnicas

| Dependencia | Historia que la introduce | Detalle técnico requerido |
| --- | --- | --- |
| Modelo de emisor y notas | US-05, US-08 | Casos de uso donde auditar `ComprobanteEmitido` y `NotaEmitida` |
| Administrador global | US-27 | `AdministradorFacturar` con `RolGlobal` y `Id`, para poder poblar `AdministradorId` y `ActorTipo` = `Administrador` |
| Activación de cuenta | US-28 | `Invitacion` y creación de `Usuario`, para el evento `CuentaActivada` |
| API keys | US-02 | `ApiKey.Hash` y `ApiKey.Prefix`: datos que la auditoría nunca debe persistir |
| Colecciones `eventos` y `outbox` | Base existente | Ya están mapeadas en `MongoDbContext`. US-17 extiende `EventoAuditoria` y el ciclo de estados de `OutboxMessage` |
| Hosting de background services | Base existente | `Microsoft.Extensions.Hosting.Abstractions` vía `FrameworkReference` en `FacturAR.Infrastructure`; sin paquete NuGet nuevo |
| Emailing (opcional) | US-29 | Solo si se decide migrar los emails a la outbox. No bloquea US-17 |

## Notas de alcance

- No implementa una consola de auditoría avanzada.
- No implementa alertas o monitoreo externo.
- No implementa webhooks; eso corresponde a US-23.
