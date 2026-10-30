# US-28 — Activación de cuenta por invitación

**Épica:** Registro y multiempresa  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RF-25  
**Depende de:** US-04, US-27, US-29  
**Habilita:** US-02, US-14  
**Estado:** Completada

## Historia

Como solicitante aprobado, quiero abrir el link de invitación y definir mi contraseña para activar mi cuenta de administrador de la empresa.

## Contexto

La invitación es la prueba de control del email y el mecanismo de activación. El token se envía por email, se guarda hasheado y es de un solo uso. Al aceptarlo se crea el usuario administrador de la empresa con el `ContribuyenteId` de la invitación.

## Criterios de aceptación

- [x] El link de invitación tiene formato `/activar?token=...`.
- [x] `GET /v1/invitaciones/{token}` valida el token y devuelve razón social, email enmascarado y fecha de expiración.
- [x] Un token inválido, vencido, usado o revocado devuelve un error genérico sin revelar datos.
- [x] La página de activación muestra la empresa y el email, y no permite editarlos.
- [x] El formulario solicita contraseña y confirmación.
- [x] La contraseña cumple mínimo 10 caracteres, al menos una letra y un número, y no es trivial.
- [x] El email del usuario se toma de la invitación, no del formulario.
- [x] El `ContribuyenteId` se toma de la invitación, no del formulario ni de la URL.
- [x] El usuario se crea con rol `Administrador`, activo y con email verificado.
- [x] Un usuario existente con el mismo email en la empresa devuelve `409` y no duplica.
- [x] La invitación se marca como `Usada` en la misma operación que crea el usuario.
- [x] Dos aceptaciones simultáneas del mismo token generan un solo usuario.
- [x] Aceptar una invitación no inicia sesión automáticamente; redirige al login con mensaje de éxito.
- [x] La contraseña se hashea y nunca se devuelve ni se loguea.
- [x] La aceptación tiene rate limit.
- [x] Se registra auditoría de activación.
- [x] Si la empresa está inactiva, no se permite la activación.

## Escenarios Gherkin

```gherkin
Scenario: Activar una cuenta válida
  Given una invitación vigente para "solicitante@demo.com" de la empresa "Demo SRL"
  When el solicitante abre el link y define una contraseña válida
  Then se crea un usuario administrador para "Demo SRL"
  And la invitación queda marcada como usada
  And se redirige al login con el mensaje "Cuenta activada"
```

```gherkin
Scenario: Token vencido
  Given una invitación con fecha de expiración pasada
  When el solicitante intenta activar la cuenta
  Then la API devuelve 410 con código "INVITACION_VENCIDA"
  And no se crea ningún usuario
```

```gherkin
Scenario: Token ya usado
  Given una invitación que ya fue aceptada
  When se intenta usar el mismo token otra vez
  Then la API devuelve 409 con código "INVITACION_USADA"
  And no se crea otro usuario
```

```gherkin
Scenario: Contraseña débil
  Given una invitación vigente
  When el solicitante define la contraseña "123456"
  Then la API devuelve 400 con código "PASSWORD_DEBIL"
  And la invitación sigue vigente
```

```gherkin
Scenario: Aceptación concurrente
  Given una invitación vigente
  When dos requests aceptan el token al mismo tiempo
  Then solo uno crea el usuario
  And el otro recibe 409
```

## Tareas técnicas

- [x] Implementar `IInvitacionRepository` con búsqueda por hash de token.
- [x] Implementar `ValidarInvitacionService`.
- [x] Implementar `AceptarInvitacionService` con transacción y control de concurrencia.
- [x] Crear `GET /v1/invitaciones/{token}`.
- [x] Crear `POST /v1/invitaciones/{token}/aceptar`.
- [x] Crear la página `/activar` y el cliente HTTP.
- [x] Aplicar la política de contraseña.
- [x] Crear el usuario con `PasswordHasher`.
- [x] Marcar la invitación como usada y registrar auditoría.
- [x] Agregar rate limit.
- [x] Agregar tests de token válido, vencido, usado, revocado, contraseña débil y concurrencia.
- [x] Documentar el flujo y los códigos de error.

## Definition of Done

- [x] El flujo invitación → activación → login funciona de punta a punta.
- [x] Un token no puede usarse dos veces.
- [x] El usuario creado queda asociado a la empresa correcta.
- [x] El email y el contribuyente no se pueden manipular desde el formulario.
- [x] `dotnet test FacturAR.slnx` pasa.
- [x] La historia está enlazada desde el índice y el proceso de alta.

## Evidencia

Verificado el 2026-10-30 sobre la rama `feature/us-28-invitacion-cuenta` (apilada sobre US-27), con MongoDB 8.2 local en replica set.

| Qué | Cómo se verificó |
| --- | --- |
| Política de contraseña, validación y aceptación | `ActivacionInvitacionServiceTests` (38 tests unitarios): reglas de `PoliticaContrasena`, enmascarado del email, token inexistente/revocado/vencido/usado, empresa inactiva, contraseña débil, confirmación distinta, carrera perdida en el compare-and-set y usuario existente. |
| Endpoints contra Mongo real | `MongoInvitacionesTests` (10 tests `MongoFact`): CP-28-01 a CP-28-14, incluida la **aceptación concurrente** (4 requests simultáneos → 1 usuario; repetida 5 veces sin fallos), campos extra del body ignorados, ausencia de `Set-Cookie` y `429 RATE_LIMIT` con `Retry-After`. |
| Flujo de punta a punta | `FlujoCompleto_AprobarActivarEIniciarSesion`: aprobación de US-27 → token tomado del email → `GET` → `POST` → `POST /v1/auth/login` con rol `Administrador`. |
| Portal | QA en Chromium headless con API y portal locales: el link del email abre `/activar`, muestra empresa y email enmascarado (el email completo no aparece), rechaza una contraseña trivial y una confirmación distinta, activa, redirige a `/login?exito=cuenta-activada` con el aviso, el login de empresa funciona, el link reusado muestra «ya fue usado» y un token inventado muestra «no es válido». Tras validar, el token sale de la barra de direcciones. |
| Suite completa | `dotnet test FacturAR.slnx -c Release`: 185 unitarios y 103 de integración en verde. `dotnet format --verify-no-changes` sin cambios. |

Decisiones tomadas durante la implementación:

- `GET` con un token ya usado responde `409 INVITACION_USADA`, igual que el `POST`; el contrato no lo listaba para el `GET` pero es el error más claro y no expone datos.
- Antes del compare-and-set se comprueba que no exista el usuario en la empresa; si existe, la transacción se aborta sin marcar la invitación (`409 INVITACION_USADA`), así el administrador puede reenviarla. El índice `ux_usuarios_contribuyente_email` sigue siendo la barrera final.
- `UsuarioRepository` ahora participa de la transacción con `MongoSesionActual`, igual que los repositorios de US-27.
- Rate limit por IP con ventana fija de una hora, configurable en `RateLimit:Invitacion:ValidacionesPorIp` (20) y `RateLimit:Invitacion:ActivacionesPorIp` (10). Con `RateLimit:Invitacion:ConfiarEnProxy` en `true` (default `false`), la IP sale de la última entrada de `X-Forwarded-For`.
- La página `/activar` es interactiva sin prerender, para validar el token una sola vez y conservarlo solo en memoria.

### Revisión de calidad (2026-10-30)

Comandos ejecutados en la rama `feature/us-28-invitacion-cuenta`, con los cambios de la revisión sin commitear y sin Docker ni MongoDB local:

| Comando | Resultado |
| --- | --- |
| `dotnet build FacturAR.slnx -c Release` | 0 advertencias, 0 errores. |
| `dotnet test FacturAR.slnx -c Release --no-build` | UnitTests: 187 superados, 0 fallos. IntegrationTests: 53 superados, 0 fallos, 55 omitidos. |
| `dotnet format FacturAR.slnx --verify-no-changes` | Limpio, sin cambios. |

Los 55 tests de integración omitidos son los marcados `MongoFact`: se omiten en local porque no hay MongoDB en replica set y se validan en CI. En esta revisión **no** se verificó la suite `MongoFact` ni se corrió QA manual con Playwright, porque no había Docker ni MongoDB local.

Correcciones aplicadas en la revisión:

1. El rate limit de invitaciones soporta `RateLimit:Invitacion:ConfiarEnProxy` y usa `X-Forwarded-For` cuando está activo; se extrajo `IpCliente` y `SolicitudAltaRateLimitPolicy` lo reutiliza.
2. `AceptarInvitacionService` revalida que el contribuyente siga activo dentro de la transacción (cierra el TOCTOU) y devuelve `403 CUENTA_INACTIVA`.
3. Si ya existe un usuario con ese email en la empresa, el `409 INVITACION_USADA` trae un mensaje claro y la invitación sigue `Pendiente`.
4. `EnmascararEmail` no revela un local-part de un solo carácter (`a@b.ar` → `***@b.ar`).
5. `Activar.razor` pasó a `OnParametersSetAsync`, así un segundo link de invitación en la misma sesión carga el token nuevo y el token se sigue quitando de la URL.
6. El portal baja `System.Net.Http.HttpClient` a `Warning` para no loguear la URL con el token.

## Contrato técnico

### Modelo de datos

US-28 **no crea colecciones nuevas**. Consume `invitaciones` (de US-27) y escribe en `usuarios`.

| Colección | Entidad | Acción en US-28 |
| --- | --- | --- |
| `invitaciones` | `Invitacion` | La crea US-27. US-28 la lee y la marca `Usada`. |
| `usuarios` | `Usuario` | US-28 crea el usuario administrador de la empresa. |
| `contribuyentes` | `Contribuyente` | US-28 la lee para validar que la empresa esté activa. |
| `eventos` | `EventoAuditoria` | US-28 registra la activación. |

Índices que hacen posible el flujo (creados en US-27 y US-04):

| Índice | Colección | Clave | Tipo | Uso en US-28 |
| --- | --- | --- | --- | --- |
| `ux_invitaciones_token_hash` | `invitaciones` | `TokenHash` | Simple, **único** | Resuelve el token a la invitación en una sola lectura. |
| `ux_usuarios_contribuyente_email` | `usuarios` | `ContribuyenteId` asc, `Email` asc | Compuesto, **único** | Garantiza que no haya dos usuarios con el mismo email **en la misma empresa**, y solo en esa empresa. |
| `ix_invitaciones_contribuyente_email_expira` | `invitaciones` | `ContribuyenteId`, `Email`, `ExpiraAt` | Compuesto | Respalda el listado de invitaciones de la empresa. |

El índice `ux_usuarios_contribuyente_email` es la barrera final contra el usuario duplicado: la comprobación previa en el caso de uso es una optimización, no la garantía.

El token:

| Aspecto | Definición |
| --- | --- |
| Generación | 32 bytes aleatorios con `RandomNumberGenerator`, codificados en base64url (43 caracteres), como las API keys. |
| Almacenamiento | `Invitacion.TokenHash` = SHA-256 hexadecimal en minúsculas del token. `Invitacion.TokenPrefix` = los primeros 8 caracteres del token. |
| En claro | Solo en memoria, entre la creación de la invitación y el armado del email. **Nunca** se persiste, **nunca** se loguea, **nunca** se devuelve por la API. |
| Comparación | Búsqueda por índice exacto sobre `TokenHash`. No se necesita comparación de tiempo constante porque no se compara un hash contra un hash, sino contra el índice único. |
| Vencimiento | `ExpiraAt = CreatedAt + 48 h` (RNF-21). |
| Un solo uso | El estado `Usada` + el compare-and-set sobre `Estado = Pendiente` en la transacción. |
| Asociación a email | `Invitacion.Email` es la única fuente del email del usuario. |

### Endpoints

Ambos son públicos, con rate limit por IP, y **no** aceptan ni devuelven nada que permita identificar a la empresa más allá de la razón social.

| Método | Ruta | Auth | Body | Respuesta | Errores |
| --- | --- | --- | --- | --- | --- |
| `GET` | `/v1/invitaciones/{token}` | Público, rate limit | — | `200` con `InvitacionValidacionResponse` | `404 INVITACION_NO_ENCONTRADA`, `410 INVITACION_VENCIDA`, `403 CUENTA_INACTIVA`, `429 RATE_LIMIT` |
| `POST` | `/v1/invitaciones/{token}/aceptar` | Público, rate limit | `AceptarInvitacionRequest` | `200` con `AceptarInvitacionResponse` | `404 INVITACION_NO_ENCONTRADA`, `410 INVITACION_VENCIDA`, `409 INVITACION_USADA`, `400 PASSWORD_DEBIL`, `400 VALIDACION` (confirmación distinta), `403 CUENTA_INACTIVA`, `429 RATE_LIMIT` |

Contratos en `FacturAR.Contracts.Invitaciones`:

```csharp
public sealed record InvitacionValidacionResponse(
    string RazonSocial,
    string EmailEnmascarado,
    DateTime ExpiraAt);

public sealed record AceptarInvitacionRequest(
    string Password,
    string ConfirmacionPassword);

public sealed record AceptarInvitacionResponse(string Mensaje);
```

`InvitacionValidacionResponse` expone **solo** razón social, email enmascarado y vencimiento. No expone `Id`, `TokenHash`, `TokenPrefix`, `ContribuyenteId`, `SolicitudId` ni `CreadaPorAdministradorId`.

El enmascarado del email muestra el primer carácter y el dominio: `s***@demo.com`. Si el local-part tiene un solo carácter, se reemplaza completo (`a@b.ar` → `***@b.ar`). Si el email no tiene dominio reconocible, se muestra solo el primer carácter y una máscara.

### Casos de uso y servicios

| Pieza | Tipo | Namespace | Responsabilidad |
| --- | --- | --- | --- |
| `ValidarInvitacionService` | Clase | `FacturAR.Application.Invitaciones` | Resuelve el token, valida estado y vencimiento, verifica que la empresa esté activa y arma la respuesta enmascarada. |
| `AceptarInvitacionService` | Clase | `FacturAR.Application.Invitaciones` | Valida la contraseña y ejecuta la transacción que marca la invitación como usada y crea el usuario. |
| `IInvitacionRepository` | Interfaz | `FacturAR.Application.Abstractions` | Reutilizada de US-27: `ObtenerPorTokenHashAsync`, `MarcarUsadaAsync` (compare-and-set), `RegistrarEnvioAsync`. |
| `IUsuarioRepository` | Interfaz | `FacturAR.Application.Abstractions` | `ExistePorContribuyenteYEmailAsync`, `InsertAsync`. |
| `IContribuyenteRepository` | Interfaz | `FacturAR.Application.Abstractions` | Reutilizada de US-01: `GetByIdAsync`. |
| `IPasswordHasher` | Interfaz | `FacturAR.Application.Abstractions` | `Hash(string password)`, `Verify(string password, string hash)`. La implementa US-04. |
| `ITransaccionMongo` | Interfaz | `FacturAR.Application.Abstractions` | Reutilizada de US-27. |
| `IEventoAuditoriaRepository` | Interfaz | `FacturAR.Application.Abstractions` | Reutilizada de US-01. |
| `PoliticaContrasena` | Clase estática | `FacturAR.Domain.Validation` | Reglas de contraseña. |

### Política de contraseña

`PoliticaContrasena.Validar(string password)` devuelve un resultado con el mensaje de error concreto. Se aplica **en el borde** (endpoint, para el mensaje de campo) y **en el caso de uso** (`AceptarInvitacionService`, que es el que se testea aislado).

| Regla | Mensaje | Código |
| --- | --- | --- |
| Obligatoria, no nula ni vacía | `La contraseña es obligatoria.` | `PASSWORD_DEBIL` (400) |
| Longitud mínima 10 | `La contraseña debe tener al menos 10 caracteres.` | `PASSWORD_DEBIL` (400) |
| Longitud máxima 128 | `La contraseña es demasiado larga.` | `PASSWORD_DEBIL` (400) |
| Al menos una letra | `La contraseña debe incluir al menos una letra.` | `PASSWORD_DEBIL` (400) |
| Al menos un número | `La contraseña debe incluir al menos un número.` | `PASSWORD_DEBIL` (400) |
| No trivial: no puede ser una secuencia repetida (`aaaaaaaaaa`), una secuencia ascendente o descendente consecutive (`1234567890`, `0987654321`) ni una de las contraseñas triviales frecuentes (`password123`, `contrasena1`, `qwerty12345`, `admin12345`, `123456789a`). | `La contraseña es demasiado predecible. Elegí otra.` | `PASSWORD_DEBIL` (400) |
| `ConfirmacionPassword` idéntica a `Password` | `Las contraseñas no coinciden.` | `VALIDACION` (400) |

La contraseña se hashea con `IPasswordHasher` (US-04). **Nunca** se devuelve en la respuesta, **nunca** se loguea y **nunca** se compara en texto plano. En caso de fallo de validación **no** se crea ni se modifica nada: la invitación sigue vigente.

### Transacción de activación

La activación se resuelve **en una sola transacción de MongoDB** (requiere replica set, ver "Dependencias técnicas"):

1. Se calcula `TokenHash = SHA-256(token)` y se lee la invitación con `ObtenerPorTokenHashAsync`.
2. Se valida la invitación (ver "Estados"). Si algo falla → se devuelve el error sin tocar nada.
3. Se valida la contraseña con `PoliticaContrasena` y se hashea con `IPasswordHasher`.
4. Dentro de `session.WithTransactionAsync(...)`:
   a. **Compare-and-set**: `UpdateOne` sobre `invitaciones` con el filtro `{ Id, Estado: Pendiente, ExpiraAt: { $gt: ahora } }` y el update `{ $set: { Estado: Usada, UsadaAt: ahora } }`. Si `ModifiedCount == 0`, otra aceptación ganó la carrera o la invitación cambió de estado → se aborta y se responde `409 INVITACION_USADA`. **Este paso es el que garantiza que un token se use una sola vez y que dos aceptaciones simultáneas generen un solo usuario.**
   b. Se inserta el `Usuario`: `ContribuyenteId` = el de la invitación, `Email` = el de la invitación, `PasswordHash` = el hash, `Rol = Administrador`, `Activo = true`, `EmailVerificadoAt = ahora`, `CreatedAt` = ahora.
   c. Se inserta el `EventoAuditoria` `cuenta.activada` con `ContribuyenteId` = la empresa, `UsuarioId` = el usuario nuevo y `ActorTipo = "Empresa"`.
5. Se responde `200` con `{ "mensaje": "Cuenta activada. Ya podés iniciar sesión." }`.

Si la inserción del usuario viola `ux_usuarios_contribuyente_email` (ya existe un usuario con ese email en esa empresa), la transacción se aborta y se responde `409 INVITACION_USADA`: **no** se duplica el usuario y la invitación queda sin usar, para que el administrador pueda reenviarla.

La activación **no** inicia sesión: no se emite ninguna cookie. El portal redirige a `/login?exito=cuenta-activada` y el login normal (US-04) crea la sesión.

### Estados

**Invitación** (`EstadoInvitacion`), igual que en US-27. US-28 consume las transiciones existentes:

| Estado | Significado | Efecto en US-28 |
| --- | --- | --- |
| `Pendiente` | Vigente y sin usar | Es el único estado desde el que se puede activar. |
| `Usada` | La cuenta se creó | `POST .../aceptar` → `409 INVITACION_USADA`. |
| `Vencida` | Superó las 48 h | `POST .../aceptar` → `410 INVITACION_VENCIDA`. |
| `Revocada` | Reemplazada por un reenvío | `POST .../aceptar` → `404 INVITACION_NO_ENCONTRADA`. |

`Vencida` es un estado **derivado**: se resuelve comparando `ExpiraAt` con `UtcNow`, sin necesidad de reescribir el documento.

**Usuario** (`RolUsuario`):

| Campo | Valor al activar |
| --- | --- |
| `Rol` | `Administrador` (tomado de `Invitacion.Rol`, no del formulario). |
| `Activo` | `true`. |
| `EmailVerificadoAt` | `now` (UTC): el control de email se Cumple con la invitación. |
| `ContribuyenteId` | El de la invitación. |
| `Email` | El de la invitación, normalizado a minúsculas. |

La empresa debe estar `Activa`; si no, la activación se rechaza con `403 CUENTA_INACTIVA` y la invitación sigue vigente.

### Rate limit (RNF-23)

Rate limiter integrado de ASP.NET Core (`Microsoft.AspNetCore.RateLimiting`, en el framework compartido: **sin paquete NuGet nuevo**).

| Política | Endpoint | Partición | Límite por defecto | Clave de configuración |
| --- | --- | --- | --- | --- |
| `invitacion-validacion` | `GET /v1/invitaciones/{token}` | IP | 20 por hora | `RateLimit__Invitacion__ValidacionesPorIp` |
| `invitacion-activacion` | `POST /v1/invitaciones/{token}/aceptar` | IP | 10 por hora | `RateLimit__Invitacion__ActivacionesPorIp` |
| Configuración común | — | IP | — | `RateLimit__Invitacion__ConfiarEnProxy` (default `false`): con `true`, la partición usa la última IP de `X-Forwarded-For`; con `false`, `HttpContext.Connection.RemoteIpAddress`. |

La partición de ambas políticas es por IP. La IP se resuelve con `IpCliente.Obtener`: si `RateLimit:Invitacion:ConfiarEnProxy` es `true`, toma la **última** entrada de `X-Forwarded-For` (pensado para correr detrás de un proxy); si es `false` (default), usa `HttpContext.Connection.RemoteIpAddress`. `SolicitudAltaRateLimitPolicy` reutiliza la misma resolución.

El rechazo responde `429 RATE_LIMIT` con `Retry-After`.

### No revela datos de otras empresas (RNF-25)

| Regla | Implementación |
| --- | --- |
| Un token solo expone los datos de **su** invitación | La búsqueda es por `TokenHash`: el token determina la invitación y, con ella, la empresa. No hay forma de pedir otra empresa. |
| Los errores no exponen datos de la empresa | Un token inexistente o revocado devuelve `404 INVITACION_NO_ENCONTRADA`, uno ya usado `409 INVITACION_USADA` y uno vencido `410 INVITACION_VENCIDA`. Ninguno de esos cuerpos incluye razón social, email ni datos de la empresa. |
| El email nunca se devuelve completo | Se devuelve `EmailEnmascarado` (`s***@demo.com`). El email completo queda solo en el servidor y en el email enviado al solicitante. |
| El `ContribuyenteId` no se puede manipular | No está en el body ni en la URL: sale de la invitación. El body solo admite `password` y `confirmacionPassword`; cualquier campo extra se ignora. |
| El email no se puede manipular | Sale de `Invitacion.Email`. El body no lo admite. |
| No se filtran datos entre empresas | La unicidad del usuario es por `(ContribuyenteId, Email)`: un usuario de la empresa A nunca colisiona con uno de la empresa B. |
| No hay enumeración | El token es de 256 bits de entropía: no es adivinable ni enumerable. |

### Integración con otras historias

| Historia | Qué aporta a US-28 | Qué recibe de US-28 | Orden |
| --- | --- | --- | --- |
| US-04 | `PasswordHasher`, `RolUsuario.Administrador`, `Usuario.EmailVerificadoAt`, cookie `cookies.contribuyente`, `ITransaccionMongo` | — | **Antes de US-28** |
| US-27 | Colección `invitaciones`, `Invitacion`, `EstadoInvitacion`, `TokenInvitacion`, `IInvitacionRepository`, índices `ux_invitaciones_token_hash` y `ux_usuarios_contribuyente_email` | — | **Antes de US-28** |
| US-29 | El email que transporta el link de activación (`Portal__BaseUrl`) | Ninguno: la activación no envía email. | Antes o en paralelo |
| US-01 | La solicitud que originó la invitación | El alta de la empresa | Antes de US-27 |
| US-02 | Gestión de API keys del usuario administrador | El usuario `Administrador` creado | **Después** |
| US-17 | Outbox y auditoría completa | El evento `cuenta.activada` | **Después** |

### Dependencias técnicas

- **MongoDB con replica set**: la activación usa una transacción. Un MongoDB standalone **no** la soporta. Atlas (M0 o superior) ya es replica set; en Docker local hay que correr el contenedor con `--replSet rs0` y ejecutar `rs.initiate()`.
- `FacturAR.Domain`: crear `PoliticaContrasena` en `Domain/Validation`. Reutilizar `RolUsuario.Administrador`, `TokenInvitacion` (de US-27) y `EventoAuditoria` (extendido en US-27 con `ActorTipo`).
- `FacturAR.Application`: crear `ValidarInvitacionService`, `AceptarInvitacionService`, `IUsuarioRepository`; reutilizar `IInvitacionRepository`, `IContribuyenteRepository`, `IPasswordHasher`, `ITransaccionMongo` e `IEventoAuditoriaRepository`. **Application no referencia `MongoDB.Driver`.**
- `FacturAR.Infrastructure`: crear `UsuarioRepository` (o extender el repositorio de US-04 si ya existe) y `TransaccionMongo` (o reutilizar el de US-27). **No** se crean colecciones nuevas en esta historia.
- `FacturAR.Contracts`: crear el namespace `Invitaciones` con `InvitacionValidacionResponse`, `AceptarInvitacionRequest` y `AceptarInvitacionResponse`. **Los DTOs públicos van en Contracts.**
- `FacturAR.Api`: registrar `GET /v1/invitaciones/{token}` y `POST /v1/invitaciones/{token}/aceptar`, con rate limit y `.Produces<ErrorResponse>`.
- `FacturAR.Web`: página de activación (`Components/Pages/Activar.razor`, ruta `/activar`), lectura del query string `?token=...`, formulario de contraseña y confirmación, y cliente HTTP en `FacturarApiClient`. Tras el `200`, redirige a `/login?exito=cuenta-activada`. La página no debe mostrar el token en la barra de direcciones más de lo necesario: se lee del query y se conserva solo en memoria.
- Rate limiting: `Microsoft.AspNetCore.RateLimiting` viene en el framework compartido de .NET 10. **Sin paquete NuGet nuevo.**
- Tests: `FacturAR.UnitTests` para `PoliticaContrasena` y `AceptarInvitacionService` con repositorios falsos; `FacturAR.IntegrationTests` para los endpoints con `WebApplicationFactory` y, si hay `MONGO_CONNECTION_STRING`, para la aceptación concurrente real y la unicidad del usuario.

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-28-01 | Invitación `Pendiente` y vigente para `solicitante@demo.com` de `Demo SRL` | `GET /v1/invitaciones/{token}` | `200` con `{ razonSocial: "Demo SRL", emailEnmascarado: "s***@demo.com", expiraAt }`; no devuelve `Id`, `TokenHash`, `TokenPrefix` ni `ContribuyenteId` |
| CP-28-02 | Token inexistente y token revocado | `GET` y `POST .../aceptar` con cada uno | Ambos devuelven `404 INVITACION_NO_ENCONTRADA` con **el mismo mensaje**; no se distingue cuál de los dos es; no se crea ni se modifica nada |
| CP-28-03 | Invitación con `ExpiraAt` pasado | `GET /v1/invitaciones/{token}` | `410 INVITACION_VENCIDA`; no se crea usuario |
| CP-28-04 | Invitación vigente | `POST .../aceptar` con `password = "123456"` | `400 PASSWORD_DEBIL` con el mensaje de longitud mínima; la invitación **sigue vigente**; no se crea usuario |
| CP-28-05 | Invitación vigente | `POST .../aceptar` con `password = "1234567890"` (solo dígitos) y luego con `"abcdefghij"` (solo letras) | Ambos devuelven `400 PASSWORD_DEBIL`; la invitación sigue vigente |
| CP-28-06 | Invitación vigente | `POST .../aceptar` con `password = "contrasena1"` | `400 PASSWORD_DEBIL` por contraseña trivial; la invitación sigue vigente |
| CP-28-07 | Invitación vigente para `solicitante@demo.com` de `Demo SRL` | `POST .../aceptar` con `password = "Facturar2026"` y confirmación distinta | `400 VALIDACION` con `Las contraseñas no coinciden.`; la invitación sigue vigente |
| CP-28-08 | Invitación vigente para `solicitante@demo.com` de `Demo SRL` | `POST .../aceptar` con contraseña válida | `200` con `Cuenta activada. Ya podés iniciar sesión.`; se crea 1 `Usuario` con `ContribuyenteId` de la invitación, `Email = solicitante@demo.com`, `Rol = Administrador`, `Activo = true`, `EmailVerificadoAt` seteado y `PasswordHash` (nunca la contraseña); la invitación queda `Usada` con `UsadaAt`; se registra auditoría `cuenta.activada`; **no** se emite ninguna cookie de sesión |
| CP-28-09 | Invitación vigente | Dos requests concurrentes con el mismo token y contraseñas válidas | Solo uno crea el usuario y marca la invitación `Usada`; el otro recibe `409 INVITACION_USADA`; en total hay 1 usuario y 1 invitación usada |
| CP-28-10 | Ya existe un usuario con `solicitante@demo.com` en `Demo SRL` | `POST .../aceptar` con contraseña válida | `409 INVITACION_USADA`; no se crea un segundo usuario; no se modifica el existente |
| CP-28-11 | El contribuyente de la invitación está `Activo = false` | `GET` y `POST .../aceptar` | Ambos devuelven `403 CUENTA_INACTIVA`; no se crea usuario; la invitación sigue vigente |
| CP-28-12 | Invitación vigente de `Demo SRL` | `POST .../aceptar` con un body que incluye `email`, `contribuyenteId` y `rol` | Esos campos se ignoran: el usuario se crea con el email y el `ContribuyenteId` de la invitación y con rol `Administrador` |
| CP-28-13 | Invitación de `Demo SRL` | `GET` con el token y revisar la respuesta | El email aparece enmascarado (`s***@demo.com`); no aparece el email completo ni el token; no se devuelven datos de ninguna otra empresa |
| CP-28-14 | La IP ya superar 10 activaciones en la hora | `POST .../aceptar` con una invitación válida | `429 RATE_LIMIT` con `Retry-After`; no se crea usuario |

## Notas de alcance

- No implementa recuperación de contraseña.
- No implementa doble factor.
- No permite cambiar el email durante la activación; si es incorrecto, el administrador debe reenviar la invitación al email correcto.
- No crea API keys; eso ocurre en US-02.
