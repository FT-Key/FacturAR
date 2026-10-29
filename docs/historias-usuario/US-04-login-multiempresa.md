# US-04 — Autenticación, roles y aislamiento multiempresa

**Épica:** Registro y multiempresa  
**Prioridad:** Alta  
**Estimación:** 3 días  
**Requisito:** RF-11  
**Depende de:** ninguna  
**Habilita:** US-02, US-05, US-07, US-12, US-13, US-14, US-27, US-28

## Historia

Como usuario de una empresa, quiero iniciar sesión en el portal para administrar únicamente los datos de mi contribuyente. Como administrador global de FacturAR, quiero un acceso separado y con permisos propios para revisar solicitudes de alta sin mezclarme con los usuarios de empresa.

## Contexto

Existen dos planos de autenticación:

- **Empresa:** usuarios con `ContribuyenteId`, roles `Administrador`, `Emisor` y `Consulta`.
- **FacturAR:** administradores globales sin `ContribuyenteId`, roles `SuperAdmin` y `Soporte`.

Un administrador global no es un usuario de empresa. Un usuario de empresa no puede acceder al panel global. El `ContribuyenteId` siempre se resuelve desde la sesión o desde la invitación, nunca desde un id enviado por el cliente.

## Criterios de aceptación

- [x] El login de empresa valida email, contraseña y estado del usuario contra el hash almacenado.
- [x] Un usuario inactivo o de un contribuyente inactivo no puede iniciar sesión.
- [x] El login global valida email, contraseña y estado del administrador.
- [x] Los administradores globales usan un esquema de cookie y rutas separadas de los usuarios de empresa.
- [x] Las contraseñas se almacenan con un hash robusto y nunca se devuelven ni se loguean.
- [x] La sesión de empresa usa una cookie `HttpOnly`, `Secure` en producción y `SameSite=Lax`, con expiración absoluta de 8 horas.
- [x] La sesión global usa una cookie `HttpOnly`, `Secure` en producción y `SameSite=Strict`, con expiración absoluta de 2 horas.
- [x] Logout invalida la sesión y redirige al login correspondiente.
- [x] `GET /v1/auth/me` devuelve el usuario, rol y contribuyente de la sesión de empresa.
- [x] `GET /v1/admin/auth/me` devuelve el administrador global y su rol.
- [x] Las políticas de autorización distinguen `EmpresaAdministrador`, `EmpresaEmisor`, `EmpresaConsulta`, `AdminGlobal` y `AdminSuper`.
- [ ] Un usuario `Consulta` no puede emitir ni administrar API keys.
- [ ] Un administrador global `Soporte` puede ver solicitudes y reenviar invitaciones, pero no aprobar ni rechazar.
- [ ] Todas las consultas de negocio se filtran por el `ContribuyenteId` de la sesión.
- [ ] Un usuario no puede ver datos de otro contribuyente cambiando un id en la URL o en el body.
- [ ] El acceso cruzado devuelve `404` o `403` sin revelar la existencia del recurso.
- [x] Los intentos de login tienen rate limit y bloqueo temporal por usuario/IP.
- [x] El administrador global inicial se crea de forma idempotente desde configuración y se hashea al iniciar.
- [x] Las acciones de login, logout y acceso denegado relevantes se registran en auditoría.

## Escenarios Gherkin

```gherkin
Scenario: Login exitoso de empresa
  Given un usuario activo con email "admin@demo.com" y contraseña válida
  And su contribuyente está activo
  When inicia sesión en el portal
  Then la API devuelve 200 con el usuario, rol y contribuyente
  And se establece una cookie de sesión HttpOnly
  And el portal muestra el dashboard de su empresa
```

```gherkin
Scenario: Usuario inactivo
  Given un usuario con estado inactivo
  When intenta iniciar sesión con credenciales correctas
  Then la API devuelve 401 con código "CREDENCIALES_INVALIDAS"
  And no se establece una cookie de sesión
```

```gherkin
Scenario: Aislamiento entre empresas
  Given un usuario autenticado de la empresa A
  When solicita un comprobante que pertenece a la empresa B
  Then la API devuelve 404
  And la respuesta no revela datos de la empresa B
```

```gherkin
Scenario: Acceso al panel global
  Given un usuario de empresa autenticado
  When intenta abrir una ruta de administración global
  Then la API devuelve 403
  And el portal lo redirige a su dashboard
```

```gherkin
Scenario: Administrador Soporte no aprueba
  Given un administrador global con rol "Soporte"
  When intenta aprobar una solicitud de alta
  Then la API devuelve 403
  And la solicitud permanece pendiente
```

```gherkin
Scenario: Rate limit de login
  Given una IP superó los intentos de login permitidos
  When intenta iniciar sesión otra vez
  Then la API devuelve 429 con código "RATE_LIMIT"
```

## Tareas técnicas

- [ ] Crear `AdministradorFacturar`, `RolGlobal` y `EstadoSolicitud` en Domain si no existen.
- [x] Extender `Usuario` con `UltimoAcceso` y `EmailVerificadoAt` si el modelo lo requiere.
- [x] Configurar autenticación por cookies con dos esquemas: empresa y admin global.
- [x] Implementar `PasswordHasher` para usuarios y administradores.
- [x] Crear `ITenantContext` y resolverlo desde los claims de la sesión.
- [x] Crear políticas de autorización por rol.
- [x] Implementar `POST /v1/auth/login`, `POST /v1/auth/logout` y `GET /v1/auth/me`.
- [x] Implementar `POST /v1/admin/auth/login`, `POST /v1/admin/auth/logout` y `GET /v1/admin/auth/me`.
- [x] Implementar el bootstrap idempotente del administrador global.
- [x] Agregar rate limit y bloqueo temporal de login.
- [x] Crear páginas `/login`, `/logout`, `/admin/login` y `/admin/logout`.
- [x] Proteger las rutas del portal y del panel global.
- [ ] Agregar filtros de tenant en repositorios y consultas.
- [ ] Agregar tests de login, roles, aislamiento y acceso cruzado.
- [x] Documentar cookies, roles, códigos de error y variables de configuración.

## Definition of Done

- [x] El portal redirige al login cuando no hay sesión.
- [x] El panel global solo es accesible por administradores globales.
- [ ] El aislamiento multiempresa está cubierto por tests de integración.
- [x] Los roles se aplican en API y portal.
- [ ] No se puede acceder a datos de otra empresa manipulando ids.
- [x] `dotnet test FacturAR.slnx` pasa.
- [x] La historia está enlazada desde el índice y el proceso de alta.

## Contrato técnico

Fuente normativa: `docs/contratos-api.md` (§3 identidad y roles, §4 autenticación) y `docs/modelos.md` (entidades e índices). Esta sección no se aparta de esos documentos.

### Modelo de datos

#### Enumeraciones

`FacturAR.Domain.Enums.RolUsuario` **no cambia**: `Administrador = 1`, `Emisor = 2`, `Consulta = 3`.

Se **agrega** `FacturAR.Domain.Enums.RolGlobal`, que hoy **no existe** en el código:

```csharp
namespace FacturAR.Domain.Enums;

public enum RolGlobal
{
    SuperAdmin = 1,
    Soporte = 2
}
```

#### Colección `usuarios`

`FacturAR.Domain.Entities.Usuario` modela **solo** el plano de empresa. `ContribuyenteId` sigue siendo `Guid` no nulo y **no cambia**: todo usuario de empresa pertenece a un contribuyente.

| Campo | Tipo | Obligatorio | Notas |
| --- | --- | --- | --- |
| `Id` | `Guid` | Sí | Clave primaria. |
| `ContribuyenteId` | `Guid` | Sí | Tenant. No pasa a nullable. |
| `Email` | `string` | Sí | Normalizado: `Trim()` + minúsculas. |
| `PasswordHash` | `string` | Sí | PBKDF2-HMAC-SHA256. Ver «Hash de contraseñas». |
| `Rol` | `RolUsuario` | Sí | `Administrador`, `Emisor` o `Consulta`. |
| `Activo` | `bool` | Sí | Baja lógica. |
| `EmailVerificadoAt` | `DateTime?` | No | Se completa al aceptar la invitación (US-28). |
| `UltimoAcceso` | `DateTime?` | No | Último login exitoso. |
| `CreatedAt` | `DateTime` | Sí | UTC. |

#### Colección `administradores`

`FacturAR.Domain.Entities.AdministradorFacturar` es una entidad **separada**: el administrador global no pertenece a un contribuyente y **no** es un `Usuario`.

| Campo | Tipo | Obligatorio | Notas |
| --- | --- | --- | --- |
| `Id` | `Guid` | Sí | Clave primaria. |
| `Nombre` | `string` | Sí | Nombre visible. |
| `Email` | `string` | Sí | Normalizado: `Trim()` + minúsculas. Único en la plataforma. |
| `PasswordHash` | `string` | Sí | PBKDF2-HMAC-SHA256. Ver «Hash de contraseñas». |
| `Rol` | `RolGlobal` | Sí | `SuperAdmin` o `Soporte`. |
| `Activo` | `bool` | Sí | Baja lógica. |
| `UltimoAcceso` | `DateTime?` | No | Último login exitoso. |
| `CreatedAt` | `DateTime` | Sí | UTC. |

#### Índices

| Índice | Colección | Claves | Tipo | Estado |
| --- | --- | --- | --- | --- |
| `ux_usuarios_contribuyente_email` | `usuarios` | `ContribuyenteId: 1, Email: 1` | Único | Ya existe. Garantiza un único usuario por email dentro de la empresa. |
| `ux_administradores_email` | `administradores` | `Email: 1` | Único | **Nuevo.** El email del administrador global es único en la plataforma. |
| `ux_contribuyentes_cuit` | `contribuyentes` | `Cuit: 1` | Único | Ya existe. Sin cambios. |

`ux_administradores_email` se agrega en `MongoIndexInitializer.InitializeAsync`, dentro del bloque `context.Administradores.Indexes.CreateManyAsync`, con `new CreateIndexOptions { Unique = true, Name = "ux_administradores_email" }`.

#### Colección `eventos`

`FacturAR.Domain.Entities.EventoAuditoria` cambia para poder registrar acciones del plano global:

| Campo | Tipo | Nota |
| --- | --- | --- |
| `ContribuyenteId` | `Guid?` | **Cambia de `Guid` a `Guid?`.** `null` en acciones de plataforma. |
| `UsuarioId` | `Guid?` | Actor del plano empresa. |
| `AdministradorId` | `Guid?` | **Campo nuevo.** `AdministradorFacturar.Id` del actor del plano plataforma. |
| `ActorTipo` | `string` | **Campo nuevo.** `Empresa`, `Administrador` o `Sistema`. |

Acciones que US-04 registra: `LoginEmpresa`, `LoginAdminGlobal`, `LogoutEmpresa`, `LogoutAdminGlobal`, `LoginFallido`, `AccesoDenegado`, `AdminGlobalCreado`. El campo `Datos` nunca incluye contraseña, hash, cookie, ticket ni token.

### Autenticación

#### Esquemas de cookie

Dos esquemas ASP.NET Core independientes. Cada uno tiene nombre de cookie, política y expiración propias. **Un ticket emitido por un esquema no es aceptado por el otro.**

| Esquema (`AuthenticationScheme.Name`) | Cookie física | Plano | `SameSite` | Expiración absoluta | `LoginPath` · `LogoutPath` |
| --- | --- | --- | --- | --- | --- |
| `cookies.contribuyente` | `.FacturAR.Contribuyente` | Empresa | `Lax` | 8 horas | `/login` · `/logout` |
| `cookies.admin` | `.FacturAR.Admin` | Consola global | `Strict` | 2 horas | `/admin/login` · `/admin/logout` |

Opciones comunes a ambos:

- `HttpOnly = true`.
- `SecurePolicy = CookieSecurePolicy.Always` en Production y `SameAsRequest` en Development (el portal local es HTTP).
- `IsPersistent = true`, `SlidingExpiration = false` y `ExpireTimeSpan` = expiración absoluta de la tabla. El ticket además lleva expiración propia, de modo que un ticket robado no se renueva por actividad.
- `Cookie.Path = "/"`.
- `Cookie.Domain` vacío por defecto (cookie *host-only*), configurable con `Auth__Cookie__Dominio` para despliegues con hosts distintos.
- `Events.OnRedirectToLogin` y `Events.OnRedirectToAccessDenied` devuelven `401`/`403` con `ErrorResponse` cuando la petición es de API (`/v1/**`) y redirigen a la página HTML cuando la petición viene del portal.

`Lax` en la cookie de empresa permite la navegación normal y el retorno desde un link externo. `Strict` en la consola global evita que la cookie de administración viaje en una navegación originada por un sitio externo.

#### Claims

Los dos esquemas emiten el mismo juego de claims:

| Claim | Tipo | Plano empresa | Plano plataforma |
| --- | --- | --- | --- |
| `ClaimTypes.NameIdentifier` | `string` (Guid) | `Usuario.Id` | `AdministradorFacturar.Id` |
| `email` | `string` | Email normalizado | Email normalizado |
| `rol` | `string` | `Administrador` · `Emisor` · `Consulta` | — |
| `rolGlobal` | `string` | — | `SuperAdmin` · `Soporte` |
| `contribuyenteId` | `string` (Guid) | `Usuario.ContribuyenteId` | — |
| `plano` | `string` | `Empresa` | `Administrador` |

El claim `contribuyenteId` es el **único** origen del tenant en el plano empresa. Un administrador global no lo tiene: su `ITenantContext.ContribuyenteId` es `null`.

#### Hash de contraseñas

**Decisión: PBKDF2-HMAC-SHA256 con `System.Security.Cryptography.Rfc2898DeriveBytes`, que ya viene en el runtime de .NET. No se agrega ningún paquete NuGet.**

Formato almacenado en `Usuario.PasswordHash` y en `AdministradorFacturar.PasswordHash`, autodescriptivo en un único string:

```text
pbkdf2-sha256$<iteraciones>$<salt base64url>$<hash base64url>
```

- Algoritmo: PBKDF2 con HMAC-SHA256.
- Iteraciones: `600000` por defecto, configurables con `Auth__PasswordHashIterations` (valor recomendado por OWASP para PBKDF2-HMAC-SHA256).
- Salt: 16 bytes aleatorios por contraseña con `RandomNumberGenerator.GetBytes(16)`, en base64url sin padding.
- Clave derivada: 32 bytes, en base64url sin padding.
- Verificación: se decodifican ambos hashes a bytes y se comparan con `CryptographicOperations.FixedTimeEquals`, que devuelve `false` si las longitudes difieren.
- Rehash: si al iniciar sesión las iteraciones guardadas son menores que las configuradas, la contraseña se re-hashea con los parámetros nuevos y se persiste. Permite subir el costo sin invalidar cuentas.

**Por qué no `Microsoft.AspNetCore.Identity` ni `Microsoft.Extensions.Identity.Core`:**

- `Microsoft.AspNetCore.Identity` arrastra EF Core, los *stores* de Identity, la integración con cookies, los *token providers* y el middleware de gestión de usuarios. El MVP no usa nada de eso: la persistencia es MongoDB.
- `Microsoft.Extensions.Identity.Core` aporta `PasswordHasher<TUser>`, pero su API está pensada para el flujo de Identity (`UserManager` + `IUserStore`), que obligaría a escribir un *store* de Identity para MongoDB o a acoplar `Usuario` al esquema de Identity. `PasswordHasher<T>` no es una utilidad independiente: no es autosuficiente fuera de ese modelo y su formato de hash no es autodescriptivo, así que el costo con el que se generó no queda expuesto.
- `Rfc2898DeriveBytes` está en `System.Security.Cryptography`, parte del framework compartido: control explícito del costo, del salt y del formato, sin dependencias nuevas y sin acoplar el dominio a Identity.
- Resultado: **no se agrega `Microsoft.AspNetCore.Identity` ni `Microsoft.Extensions.Identity.Core`** a ningún `.csproj`.

#### `ITenantContext` y aislamiento multiempresa

`FacturAR.Application.Abstractions.ITenantContext`, interfaz pura (Application no conoce HTTP):

```csharp
public interface ITenantContext
{
    Guid? ContribuyenteId { get; }
    Guid? UsuarioId { get; }
    Guid? AdministradorId { get; }
}
```

Implementación `FacturAR.Api.Security.TenantContext`, `Scoped`, sobre `IHttpContextAccessor`: lee `contribuyenteId`, `ClaimTypes.NameIdentifier` y `rolGlobal` del principal autenticado. Si no hay principal autenticado, `ContribuyenteId` es `null` y todo repositorio que filtre por tenant **falla en la consulta** en vez de devolver datos sin filtrar.

Reglas de aislamiento:

- Todo endpoint de negocio toma el `ContribuyenteId` **desde `ITenantContext`** y lo pasa explícitamente al repositorio. Nunca se lee del body, del query string ni de un header del cliente.
- Los repositorios de `comprobantes`, `apiKeys`, `puntosVenta`, `secuencias` y `eventos` filtran por `ContribuyenteId` **en el filtro de la consulta**, no en memoria.
- El acceso a un recurso de otro contribuyente responde `404` (`COMPROBANTE_NO_ENCONTRADO`, `API_KEY_INVALIDA`), nunca `403`: un `403` confirmaría que el recurso existe. `403 SIN_PERMISOS` queda reservado para el rol insuficiente sobre un recurso propio.
- Los índices que soportan el aislamiento llevan `ContribuyenteId` como prefijo (contrato §3.3).

#### Rate limit y bloqueo de login (RNF-23)

Dos mecanismos complementarios:

**Rate limit por IP.** `builder.Services.AddRateLimiter(...)`; el middleware `Microsoft.AspNetCore.RateLimiting` viene en el framework, no requiere paquete nuevo. Política `login`: `FixedWindow`, `PermitLimit = Auth__Login__RateLimitPorIp` (default `10`), `Window = Auth__Login__VentanaRateLimitMinutos` (default `5`), `QueueLimit = 0`. Se aplica con `.RequireRateLimiting("login")` sobre `POST /v1/auth/login` y `POST /v1/admin/auth/login`. El `OnRejected` devuelve `429` con `ErrorResponse("RATE_LIMIT", ...)`.

**Bloqueo temporal por usuario.** `ILoginIntentStore`, un singleton en memoria (implementación con `ConcurrentDictionary`), con la clave normalizada del email:

- Cada login con contraseña incorrecta suma un intento fallido para ese email.
- Al alcanzar `Auth__Login__MaxIntentosFallidos` (default `5`), el email queda bloqueado hasta `now + Auth__Login__MinutosBloqueo` (default `15`) y el contador vuelve a `0`.
- Mientras el email está bloqueado, el login responde `401 CREDENCIALES_INVALIDAS` con el **mismo mensaje genérico** que una contraseña incorrecta: no se revela que la cuenta existe ni que está bloqueada.
- Un login exitoso resetea el contador, limpia el bloqueo y actualiza `UltimoAcceso` del documento.
- El bloqueo no aplica a la API key: US-03 no tiene login interactivo.

El estado del bloqueo **no** se persiste en MongoDB: ni `Usuario` ni `AdministradorFacturar` agregan campos de bloqueo, porque el modelo canónico no los tiene. Es un estado de vida corta (15 minutos por defecto) y el MVP corre una sola instancia de la API; si en el futuro hubiera varias réplicas, el store pasa a un backend compartido.

#### Bootstrap del administrador global

`AdminGlobalBootstrapService` corre **al arrancar la API**, antes de atender tráfico, y es **idempotente**:

1. Lee `BootstrapAdmin__Email`, `BootstrapAdmin__Nombre` y `BootstrapAdmin__Password` de la configuración.
2. Si no hay email configurado, no hace nada y lo deja asentado en el log.
3. Busca un `AdministradorFacturar` con ese email en la colección `administradores`. Si existe, **no** lo modifica: no cambia la contraseña ni reactiva una cuenta dada de baja.
4. Si no existe, inserta un `AdministradorFacturar` con `Rol = SuperAdmin`, `Activo = true`, `Nombre` y `PasswordHash` calculados con PBKDF2.
5. Si la inserción choca con `ux_administradores_email`, se interpreta como «ya existía» y se sale sin error.

`BootstrapAdmin__Password` llega **siempre** por variable de entorno o secret manager. No se escribe en `appsettings.json` versionado, ni en `.env` versionado, ni en logs, ni en la auditoría. La creación se audita como `AdminGlobalCreado` con `ActorTipo = Sistema`.

### Endpoints

| Método | Ruta | Autenticación | Body | Respuesta | Errores |
| --- | --- | --- | --- | --- | --- |
| `POST` | `/v1/auth/login` | Anónimo + rate limit `login` | `{ "email": "...", "password": "..." }` | `200 SesionEmpresaResponse` + `Set-Cookie` | `400 VALIDACION`, `401 CREDENCIALES_INVALIDAS`, `429 RATE_LIMIT` |
| `POST` | `/v1/auth/logout` | `cookies.contribuyente` | — | `204` + `Set-Cookie` que borra la cookie | `401 CREDENCIALES_INVALIDAS` |
| `GET` | `/v1/auth/me` | `cookies.contribuyente` | — | `200 SesionEmpresaResponse` | `401 CREDENCIALES_INVALIDAS` |
| `POST` | `/v1/admin/auth/login` | Anónimo + rate limit `login` | `{ "email": "...", "password": "..." }` | `200 SesionAdminResponse` + `Set-Cookie` | `400 VALIDACION`, `401 CREDENCIALES_INVALIDAS`, `429 RATE_LIMIT` |
| `POST` | `/v1/admin/auth/logout` | `cookies.admin` | — | `204` + `Set-Cookie` que borra la cookie | `401 CREDENCIALES_INVALIDAS` |
| `GET` | `/v1/admin/auth/me` | `cookies.admin` | — | `200 SesionAdminResponse` | `401 CREDENCIALES_INVALIDAS` |

Cuerpos de respuesta:

```json
{
  "usuarioId": "6ae24e50-7f0e-4a1a-9a4e-0f0a1b2c3d4e",
  "email": "admin@demo.com",
  "rol": "Administrador",
  "contribuyenteId": "1b0f2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d",
  "contribuyenteRazonSocial": "Demo SRL",
  "expiraAtUtc": "2026-10-28T23:30:00Z"
}
```

```json
{
  "administradorId": "9c8d7e6f-5a4b-3c2d-1e0f-9a8b7c6d5e4f",
  "email": "admin@facturar.ar",
  "nombre": "Administración FacturAR",
  "rolGlobal": "SuperAdmin",
  "expiraAtUtc": "2026-10-28T17:30:00Z"
}
```

`rol` y `rolGlobal` se serializan **por nombre** (string), no por número, igual que `Estado` en `VerificacionResponse`. La sesión de la consola global no lleva `contribuyenteId`: el administrador global no pertenece a ninguna empresa.

### Casos de uso y servicios

**Domain**

- `FacturAR.Domain.Enums.RolGlobal` (nuevo).
- `FacturAR.Domain.Entities.AdministradorFacturar` (nuevo).
- `FacturAR.Domain.Entities.Usuario` (extendido: `EmailVerificadoAt`, `UltimoAcceso`).
- `FacturAR.Domain.Entities.EventoAuditoria` (extendido: `Guid? ContribuyenteId`, `AdministradorId`, `ActorTipo`).

**Application** (`FacturAR.Application`)

| Tipo | Responsabilidad |
| --- | --- |
| `Abstractions.ITenantContext` | `ContribuyenteId`, `UsuarioId` y `AdministradorId` del contexto actual. |
| `Abstractions.IPasswordHasher` | `Hash(string password)`, `Verify(string password, string hash)`, `NeedsRehash(string hash)`. |
| `Abstractions.ILoginIntentStore` | `RegistrarFalloAsync(email, ct)`, `EstaBloqueadoAsync(email, ct)`, `RegistrarExitoAsync(email, ct)`. Estado en memoria del bloqueo temporal por usuario. |
| `Abstractions.IUsuarioRepository` | `GetByEmailAsync(email, ct)`, `GetByIdAsync(id, ct)`, `InsertAsync(usuario, ct)`, `UpdateAsync(usuario, ct)`. |
| `Abstractions.IAdministradorRepository` | `GetByEmailAsync(email, ct)`, `GetByIdAsync(id, ct)`, `ExistsByEmailAsync(email, ct)`, `InsertAsync(administrador, ct)`, `UpdateAsync(administrador, ct)`. |
| `Abstractions.IContribuyenteRepository` | `GetByIdAsync(id, ct)`. |
| `Abstractions.IEventoAuditoriaRepository` | `AddAsync(evento, ct)` para los eventos de login/logout/acceso denegado. |
| `Auth.AutenticarEmpresaService` | Valida credenciales, estado del usuario y estado del contribuyente; devuelve el payload de sesión o `null`. |
| `Auth.AutenticarAdminGlobalService` | Valida credenciales y estado del `AdministradorFacturar`; devuelve el payload de sesión o `null`. |
| `Auth.RegistrarAccesoService` | Escribe los eventos de auditoría y actualiza el estado de intentos de login. |
| `Auth.AdminGlobalBootstrapService` | Bootstrap idempotente del `SuperAdmin`. |

**Infrastructure** (`FacturAR.Infrastructure`)

- `Repositories/UsuarioRepository`, `Repositories/AdministradorRepository`, `Repositories/ContribuyenteRepository`, `Repositories/EventoAuditoriaRepository` sobre `MongoDbContext`.
- `MongoIndexInitializer`: alta de `ux_administradores_email` en la colección `administradores`.

**Api** (`FacturAR.Api`)

- `Auth/CookieAuthenticationExtensions.cs`: `AddAuthentication` con `cookies.contribuyente` y `cookies.admin`, `AddAuthorization` con las políticas, `AddRateLimiter` con la política `login`.
- `Auth/AuthEndpoints.cs`: los seis endpoints de la tabla.
- `Security/TenantContext.cs`: implementación de `ITenantContext`.
- `Auth/AdminGlobalBootstrapHostedService.cs`: ejecuta el bootstrap al arrancar.

**Web** (`FacturAR.Web`, Blazor Server)

- `Pages/Login.cshtml` (+ `.cs`), `Pages/Logout.cshtml` (+ `.cs`), `Pages/Admin/Login.cshtml` (+ `.cs`), `Pages/Admin/Logout.cshtml` (+ `.cs`).
- `Services/FacturarApiClient`: métodos `LoginEmpresaAsync`, `LoginAdminGlobalAsync`, `GetMeEmpresaAsync`, `GetMeAdminAsync`, `LogoutEmpresaAsync`, `LogoutAdminGlobalAsync`, y reenvío del header `Cookie` de la petición entrante a las llamadas salientes.
- `Components/App.razor` y `Components/Routes.razor`: `CascadingAuthenticationState` + `AuthorizeRouteView` con `<NotAuthorized>` redirigiendo a `/login` o `/admin/login` según la ruta.

**Regla de capas del portal:** el portal **no** valida contraseñas ni hashes y **no** toca MongoDB. Delega la autenticación en la API y solo consume el resultado. La cookie la emite y la valida **siempre** la API; el portal reenvía la cookie y cachea el resultado de `/v1/auth/me` por circuito.

### Autorización

Políticas registradas en `AddAuthorization`. Todas declaran `AddAuthenticationSchemes("cookies.contribuyente", "cookies.admin")` para que el principal replique los claims de cualquiera de los dos planos; por eso **anónimo da `401` y plano equivocado da `403`**.

| Política | Requisitos | Uso típico |
| --- | --- | --- |
| `EmpresaAutenticado` | `RequireAuthenticatedUser()` + claim `contribuyenteId` | `/v1/auth/me` y endpoints de empresa. |
| `EmpresaAdministrador` | `EmpresaAutenticado` + claim `rol` = `Administrador` | US-02 (API keys), US-14, gestión de usuarios de la empresa. |
| `EmpresaEmisor` | `EmpresaAutenticado` + claim `rol` ∈ {`Administrador`, `Emisor`} | US-05 (emisión). |
| `EmpresaConsulta` | `EmpresaAutenticado` + claim `rol` ∈ {`Administrador`, `Emisor`, `Consulta`} | US-09 (consultas). |
| `AdminGlobal` | `RequireAuthenticatedUser()` + claim `rolGlobal` ∈ {`SuperAdmin`, `Soporte`} | US-27 (ver solicitudes), US-28 (reenviar invitación). |
| `AdminSuper` | `RequireAuthenticatedUser()` + claim `rolGlobal` = `SuperAdmin` | US-27 (aprobar/rechazar). |

Matriz de permisos según `docs/contratos-api.md` §3.2. Puntos que esta historia fija:

- `SuperAdmin` **aprueba y rechaza** solicitudes de alta.
- `Soporte` **solo consulta y reenvía invitaciones**: nunca aprueba ni rechaza.
- Un usuario de empresa que llega a una ruta global: `403 SIN_PERMISOS`.
- Un administrador global que llega a una ruta de empresa: `403 SIN_PERMISOS`.
- Los roles se aplican **en la API** (autorización real) y **en el portal** (solo para ocultar acciones de la UI).

### Validaciones

- Email: obligatorio, `Trim()`, minúsculas, formato válido, longitud máxima `256`.
- Contraseña: obligatoria, longitud entre `1` y `128`. En el login **no** se valida complejidad: la política de contraseña se aplica al crear el usuario (US-28).
- Body JSON malformado o con campos de más: `400 VALIDACION`.
- No se diferencia en la respuesta entre «usuario inexistente», «contraseña incorrecta», «usuario inactivo», «contribuyente inactivo» y «cuenta bloqueada»: todos devuelven `401 CREDENCIALES_INVALIDAS` con el mismo mensaje genérico.
- El rol, el `contribuyenteId` y el estado del usuario **no** se aceptan del body: se derivan del documento almacenado.
- Cookies con nombres distintos de `.FacturAR.Contribuyente` y `.FacturAR.Admin` se ignoran.
- `GET /v1/auth/me` no acepta parámetros: el usuario, el rol y el contribuyente salen **solo** de la cookie.

### Configuración

| Clave | Tipo | Default | Uso |
| --- | --- | --- | --- |
| `Auth__Cookie__Dominio` | `string` | vacío | `Cookie.Domain`. Vacío = cookie host-only. |
| `Auth__Cookie__NombreContribuyente` | `string` | `.FacturAR.Contribuyente` | Nombre físico de la cookie de empresa. |
| `Auth__Cookie__NombreAdmin` | `string` | `.FacturAR.Admin` | Nombre físico de la cookie global. |
| `Auth__Cookie__HorasSesionEmpresa` | `int` | `8` | Expiración absoluta de la cookie de empresa. |
| `Auth__Cookie__HorasSesionAdmin` | `int` | `2` | Expiración absoluta de la cookie global. |
| `Auth__PasswordHashIterations` | `int` | `600000` | Iteraciones PBKDF2-HMAC-SHA256. |
| `Auth__Login__MaxIntentosFallidos` | `int` | `5` | Intentos fallidos antes del bloqueo. |
| `Auth__Login__MinutosBloqueo` | `int` | `15` | Duración del bloqueo temporal. |
| `Auth__Login__RateLimitPorIp` | `int` | `10` | Requests de login por IP por ventana. |
| `Auth__Login__VentanaRateLimitMinutos` | `int` | `5` | Tamaño de la ventana del rate limit. |
| `BootstrapAdmin__Email` | `string` | — | Email del `SuperAdmin` inicial. Sin valor, no se crea. |
| `BootstrapAdmin__Nombre` | `string` | — | Nombre visible del administrador global. |
| `BootstrapAdmin__Password` | `string` | — | **Solo por variable de entorno o secret manager.** Nunca en `appsettings.json` versionado. |

Todas se pueden sobrescribir por entorno con el prefijo `Auth__` / `BootstrapAdmin__`.

### Integración con otras historias

| Historia | Punto de integración |
| --- | --- |
| US-02 | Los endpoints de API keys usan `cookies.contribuyente` + `EmpresaAdministrador`. El `ContribuyenteId` sale de `ITenantContext`. |
| US-03 | `ITenantContext` es la misma abstracción: el handler de API key escribe el claim `contribuyenteId` y el `TenantContext` lo lee igual. La cookie **no** habilita la API de negocio. |
| US-05, US-07, US-09 | Los endpoints de negocio exigen API key (US-03). La sesión del portal no los autoriza. |
| US-12, US-13, US-14 | El portal usa las políticas `Empresa*` solo para ocultar acciones de la UI; la autorización real es la del API. |
| US-27, US-28 | La consola global usa `cookies.admin`; `AdminSuper` aprueba y rechaza, `AdminGlobal` consulta y reenvía invitaciones. |
| US-18 | El seed crea el `SuperAdmin` de plataforma en la colección `administradores` con `Seed__Password`. Convive con el bootstrap: ambos son idempotentes y ninguno pisa una cuenta existente. |
| US-29 | El alta y la invitación crean usuarios del plano empresa; el login es el mismo esquema `cookies.contribuyente`. |
| US-17 | Los eventos de auditoría de esta historia se escriben en la colección `eventos`. |

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-01 | Un `Usuario` activo de empresa con rol `Administrador` y contribuyente activo. | `POST /v1/auth/login` con email y contraseña correctos. | `200` con `SesionEmpresaResponse` (usuario, rol, contribuyente). Se emite cookie `cookies.contribuyente` `HttpOnly`, `SameSite=Lax`, con expiración absoluta de 8 h. Se audita `LoginEmpresa`. |
| CP-02 | Mismo usuario de CP-01. | `POST /v1/auth/login` con contraseña incorrecta. | `401 CREDENCIALES_INVALIDAS` con mensaje genérico. No se emite cookie. El contador de intentos fallidos de ese email aumenta en 1. Se audita `LoginFallido`. |
| CP-03 | Un `Usuario` con `Activo = false`. | `POST /v1/auth/login` con credenciales correctas. | `401 CREDENCIALES_INVALIDAS`. No se emite cookie. Mismo mensaje que CP-02. |
| CP-04 | Un `Usuario` activo cuyo `Contribuyente` tiene `Activo = false`. | `POST /v1/auth/login` con credenciales correctas. | `401 CREDENCIALES_INVALIDAS`. No se emite cookie. |
| CP-05 | Un `AdministradorFacturar` activo con `Rol = SuperAdmin`. | `POST /v1/admin/auth/login` con credenciales correctas. | `200` con `SesionAdminResponse`. Se emite cookie `cookies.admin` `HttpOnly`, `SameSite=Strict`, expiración absoluta de 2 h. El claim `contribuyenteId` **no** está presente. |
| CP-06 | Un usuario de empresa autenticado con cookie `cookies.contribuyente`. | `GET /v1/admin/auth/me`. | `403 SIN_PERMISOS`. No se filtran datos de la consola global. |
| CP-07 | Un administrador global con `Rol = Soporte`. | `POST` a una acción de aprobación (consola global). | `403 SIN_PERMISOS`. La solicitud queda pendiente, sin cambios. |
| CP-08 | Un usuario de empresa autenticado. | `GET` de un comprobante que pertenece a otra empresa. | `404 COMPROBANTE_NO_ENCONTRADO`. La respuesta no revela datos de la otra empresa. |
| CP-09 | Un usuario de empresa autenticado. | Modificar el id en la URL de un recurso propio para apuntar a un recurso de otra empresa. | `404`. El `ContribuyenteId` de la sesión manda siempre; el id del body o del query no cambia el tenant. |
| CP-10 | Un usuario autenticado. | `POST /v1/auth/logout`. | `204`. La cookie se borra. Un `GET /v1/auth/me` posterior devuelve `401`. Se audita `LogoutEmpresa`. |
| CP-11 | Una IP que agotó `Auth__Login__RateLimitPorIp` intentos en la ventana. | `POST /v1/auth/login`. | `429 RATE_LIMIT`. El cuerpo es un `ErrorResponse` con `codigo` y `mensaje`. |
| CP-12 | Un usuario con `Auth__Login__MaxIntentosFallidos` - 1 intentos fallidos. | `POST /v1/auth/login` con contraseña incorrecta `MaxIntentosFallidos` veces y luego con la correcta. | Tras el último intento, el email queda bloqueado hasta `Auth__Login__MinutosBloqueo`. El login con la contraseña correcta devuelve `401 CREDENCIALES_INVALIDAS` hasta que venza el bloqueo. |
| CP-13 | Cualquier usuario. | Inspeccionar la respuesta y los logs de un login exitoso. | El hash de la contraseña, la contraseña en claro y el valor de la cookie no aparecen en ninguna respuesta ni en el log. `Usuario.PasswordHash` sigue con el formato `pbkdf2-sha256$...`. |
| CP-14 | La API arranca sin administrador global previo. | Arrancar la API con `BootstrapAdmin__Email`, `BootstrapAdmin__Nombre` y `BootstrapAdmin__Password`. | Se crea un `AdministradorFacturar` con `Rol = SuperAdmin` en la colección `administradores`. Al arrancar de nuevo, no se crea un segundo administrador ni se cambia la contraseña. |
| CP-15 | Un `Usuario` de empresa con `RolUsuario.Emisor`. | `GET /v1/api-keys` (endpoint de US-02). | `403 SIN_PERMISOS`. El rol `Emisor` no administra API keys. |

## Dependencias técnicas

Antes de empezar US-04 tiene que existir:

- **Domain**: `FacturAR.Domain.Enums.RolGlobal` (nuevo), `FacturAR.Domain.Entities.AdministradorFacturar` (nuevo) y `FacturAR.Domain.Entities.Usuario` extendido con `EmailVerificadoAt` y `UltimoAcceso`. `FacturAR.Domain.Entities.EventoAuditoria` extendido con `Guid? ContribuyenteId`, `AdministradorId` y `ActorTipo`.
- **Application**: `ITenantContext`, `IPasswordHasher`, `ILoginIntentStore`, `IUsuarioRepository`, `IAdministradorRepository`, `IContribuyenteRepository`, `IEventoAuditoriaRepository` y los servicios de `Auth`. `FacturAR.Application` no referencia `MongoDB.Driver`.
- **Infrastructure**: `MongoDbContext` con las colecciones `Usuarios`, `Contribuyentes` y `Eventos` (ya existen) más la nueva `Administradores`, los repositorios correspondientes y el índice `ux_administradores_email` en `MongoIndexInitializer`.
- **Api**: `Program.cs` con `AddAuthentication` (dos esquemas de cookie), `AddAuthorization` (las seis políticas), `AddRateLimiter` (política `login`) y el registro de `ITenantContext` como `Scoped`.
- **Contracts**: `FacturAR.Contracts.Auth.LoginRequest`, `SesionEmpresaResponse`, `SesionAdminResponse`. Los roles se serializan por nombre.
- **Web**: `FacturarApiClient` capaz de reenviar el header `Cookie` y los seis endpoints de la API consumibles desde el portal; páginas `/login`, `/logout`, `/admin/login`, `/admin/logout`; `CascadingAuthenticationState` en `Routes.razor`.
- **Tests**: `FacturAR.UnitTests` para `PasswordHasher` y las políticas; `FacturAR.IntegrationTests` con `WebApplicationFactory` y `MongoFact` para login, aislamiento, rate limit y bootstrap.
- **Configuración**: valores en `appsettings.json` **sin secretos** y secretos (`BootstrapAdmin__Password`) solo por variable de entorno o secret manager.

## Notas de alcance

- No implementa el formulario de solicitud ni la aprobación; eso corresponde a US-01, US-27 y US-28.
- No implementa recuperación de contraseña ni doble factor.
- No implementa gestión de administradores globales desde el portal; el bootstrap es por configuración.
- No usar JWT para el portal en el MVP; cookies de sesión son suficientes y más seguras para Blazor Server.

## Estado de implementación

**Estado:** Base implementada.

Evidencia ejecutada:

- `dotnet build FacturAR.slnx -c Release` → 0 errores, 0 advertencias.
- `dotnet test FacturAR.slnx -c Release --no-build` con `MONGO_CONNECTION_STRING` → **87 tests correctos, 0 fallos, 0 omitidos** (58 unitarios + 29 de integración).
- `dotnet format FacturAR.slnx --verify-no-changes` → sin cambios.
- Tests unitarios: `PasswordHasherTests`, `LoginIntentStoreTests`, `AutenticarEmpresaServiceTests`, `AutenticarAdminGlobalServiceTests`.
- Tests de integración: `AuthEndpointsTests` (CP-01 a CP-07, CP-10 a CP-14, auditoría), `AdminBootstrapTests` (bootstrap idempotente e índice único) y `AuthorizationPolicyTests` (matriz de políticas por rol y por plano).

Pendiente (depende de historias posteriores, no de US-04):

- CP-08, CP-09 y CP-15 requieren endpoints de negocio de US-02, US-05 y US-09; no son verificables todavía.
- Los criterios de aislamiento multiempresa (consultas filtradas por `ContribuyenteId`, acceso cruzado `404`) se completan cuando existan esos endpoints.
- Las políticas `EmpresaAdministrador`, `EmpresaEmisor` y `EmpresaConsulta` están registradas y cubiertas por `AuthorizationPolicyTests`; se ejercitan de punta a punta cuando existan los endpoints de esas historias.

Notas de implementación:

- `EstadoSolicitud` no se creó: pertenece al modelo de US-01 y esta historia no lo usa.
- La sesión por cookie es sin estado; el logout borra la cookie del navegador y no revoca el ticket en el servidor. La revocación server-side no está en el alcance del MVP de una sola instancia.
- `MongoSerialization.Configure()` registra `Guid` como `Standard` y **debe ejecutarse antes de crear cualquier `MongoClient`**. Es idempotente y thread-safe porque cada host de pruebas lo invoca al arrancar; crear un cliente antes de arrancar el host fija `GuidRepresentation.Unspecified` y rompe la serialización.
