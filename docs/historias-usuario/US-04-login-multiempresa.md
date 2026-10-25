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

- [ ] El login de empresa valida email, contraseña y estado del usuario contra el hash almacenado.
- [ ] Un usuario inactivo o de un contribuyente inactivo no puede iniciar sesión.
- [ ] El login global valida email, contraseña y estado del administrador.
- [ ] Los administradores globales usan un esquema de cookie y rutas separadas de los usuarios de empresa.
- [ ] Las contraseñas se almacenan con un hash robusto y nunca se devuelven ni se loguean.
- [ ] La sesión de empresa usa una cookie `HttpOnly`, `Secure` en producción y `SameSite=Lax`, con expiración absoluta de 8 horas.
- [ ] La sesión global usa una cookie `HttpOnly`, `Secure` en producción y `SameSite=Strict`, con expiración absoluta de 2 horas.
- [ ] Logout invalida la sesión y redirige al login correspondiente.
- [ ] `GET /v1/auth/me` devuelve el usuario, rol y contribuyente de la sesión de empresa.
- [ ] `GET /v1/admin/auth/me` devuelve el administrador global y su rol.
- [ ] Las políticas de autorización distinguen `EmpresaAdministrador`, `EmpresaEmisor`, `EmpresaConsulta`, `AdminGlobal` y `AdminSuper`.
- [ ] Un usuario `Consulta` no puede emitir ni administrar API keys.
- [ ] Un administrador global `Soporte` puede ver solicitudes y reenviar invitaciones, pero no aprobar ni rechazar.
- [ ] Todas las consultas de negocio se filtran por el `ContribuyenteId` de la sesión.
- [ ] Un usuario no puede ver datos de otro contribuyente cambiando un id en la URL o en el body.
- [ ] El acceso cruzado devuelve `404` o `403` sin revelar la existencia del recurso.
- [ ] Los intentos de login tienen rate limit y bloqueo temporal por usuario/IP.
- [ ] El administrador global inicial se crea de forma idempotente desde configuración y se hashea al iniciar.
- [ ] Las acciones de login, logout y acceso denegado relevantes se registran en auditoría.

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
- [ ] Extender `Usuario` con `UltimoAcceso` y `EmailVerificadoAt` si el modelo lo requiere.
- [ ] Configurar autenticación por cookies con dos esquemas: empresa y admin global.
- [ ] Implementar `PasswordHasher` para usuarios y administradores.
- [ ] Crear `ITenantContext` y resolverlo desde los claims de la sesión.
- [ ] Crear políticas de autorización por rol.
- [ ] Implementar `POST /v1/auth/login`, `POST /v1/auth/logout` y `GET /v1/auth/me`.
- [ ] Implementar `POST /v1/admin/auth/login`, `POST /v1/admin/auth/logout` y `GET /v1/admin/auth/me`.
- [ ] Implementar el bootstrap idempotente del administrador global.
- [ ] Agregar rate limit y bloqueo temporal de login.
- [ ] Crear páginas `/login`, `/logout`, `/admin/login` y `/admin/logout`.
- [ ] Proteger las rutas del portal y del panel global.
- [ ] Agregar filtros de tenant en repositorios y consultas.
- [ ] Agregar tests de login, roles, aislamiento y acceso cruzado.
- [ ] Documentar cookies, roles, códigos de error y variables de configuración.

## Definition of Done

- [ ] El portal redirige al login cuando no hay sesión.
- [ ] El panel global solo es accesible por administradores globales.
- [ ] El aislamiento multiempresa está cubierto por tests de integración.
- [ ] Los roles se aplican en API y portal.
- [ ] No se puede acceder a datos de otra empresa manipulando ids.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el proceso de alta.

## Notas de alcance

- No implementa el formulario de solicitud ni la aprobación; eso corresponde a US-01, US-27 y US-28.
- No implementa recuperación de contraseña ni doble factor.
- No implementa gestión de administradores globales desde el portal; el bootstrap es por configuración.
- No usar JWT para el portal en el MVP; cookies de sesión son suficientes y más seguras para Blazor Server.
