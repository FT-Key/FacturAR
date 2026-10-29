# US-29 — Notificaciones transaccionales por email

**Épica:** Registro y multiempresa  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RF-26  
**Depende de:** US-19 (configuración Docker)  
**Habilita:** US-01, US-27, US-28

## Historia

Como sistema, quiero enviar emails transaccionales de solicitud, aprobación, rechazo e invitación para que el solicitante reciba las instrucciones de acceso sin depender de un proveedor externo durante el desarrollo.

## Contexto

En desarrollo se usa un remitente que escribe el email completo en un archivo local; el log solo registra metadatos. En producción se configura SMTP por variables de entorno. Un fallo de email no debe perder la solicitud ni la empresa: el flujo principal se completa y el administrador puede reenviar la invitación.

El envío directo es suficiente para el MVP. US-17 puede agregar outbox y reintentos automáticos.

## Criterios de aceptación

- [ ] Existe una abstracción `IEmailSender` en Application.
- [ ] Existe un remitente de desarrollo que escribe el email completo en un archivo local; el log solo registra metadatos sin tokens.
- [ ] Existe un remitente SMTP configurable por variables de entorno.
- [ ] El remitente se elige por configuración sin recompilar.
- [ ] Las plantillas incluyen asunto, cuerpo HTML y texto plano.
- [ ] Los datos dinámicos se codifican para evitar inyección HTML.
- [ ] Las plantillas son: solicitud recibida, nueva solicitud para administración, solicitud aprobada con invitación, solicitud rechazada e invitación reenviada.
- [ ] El email de invitación incluye el link completo con token y la fecha de expiración.
- [ ] El token no se escribe en logs ni en errores.
- [ ] El email de rechazo incluye el motivo.
- [ ] El remitente y la URL base del portal se configuran por variables de entorno.
- [ ] Un fallo de envío devuelve un resultado controlado; no lanza una excepción que revierta la operación de negocio.
- [ ] El panel muestra una advertencia si el email no se pudo enviar y permite reenviar.
- [ ] Se puede enviar una notificación opcional al email de administración cuando entra una solicitud.
- [ ] Los emails de desarrollo no se versionan ni se incluyen en la imagen de producción.
- [ ] Hay tests unitarios de composición y tests de integración del remitente de desarrollo.

## Escenarios Gherkin

```gherkin
Scenario: Email de solicitud recibida en desarrollo
  Given el remitente de desarrollo está configurado
  When un visitante envía una solicitud válida
  Then se genera un archivo de email para el solicitante
  And el asunto indica que la solicitud fue recibida
  And el cuerpo incluye el próximo paso
```

```gherkin
Scenario: Invitación con link de activación
  Given una solicitud aprobada
  When se envía el email de invitación
  Then el cuerpo incluye el link "/activar?token=..."
  And el link usa la URL base configurada del portal
  And el email indica que vence en 48 horas
```

```gherkin
Scenario: Fallo de SMTP
  Given el servidor SMTP no responde
  When el administrador aprueba una solicitud
  Then la empresa y la invitación se crean igualmente
  And el panel muestra una advertencia de email no enviado
  And el administrador puede reenviar la invitación
```

```gherkin
Scenario: Token no logueado
  Given un email de invitación con token
  When se registra el envío
  Then el log no contiene el token completo
```

## Tareas técnicas

- [ ] Crear `IEmailSender`, `EmailMessage` y `EmailResult` en Application.
- [ ] Crear `FileEmailSender` y `SmtpEmailSender` en Infrastructure.
- [ ] Registrar el remitente según `Email__Provider`.
- [ ] Crear plantillas HTML y texto para los cinco emails.
- [ ] Implementar `EmailService` con composición y codificación segura.
- [ ] Configurar `Email__From`, `Email__Host`, `Email__Port`, `Email__User`, `Email__Password`, `Email__UseSsl` y `Portal__BaseUrl`.
- [ ] Configurar `Admin__NotificationEmail` opcional.
- [ ] Integrar el envío en US-01, US-27 y US-28.
- [ ] Agregar tests de plantillas y del remitente de desarrollo.
- [ ] Documentar configuración y carpeta de emails de desarrollo.

## Definition of Done

- [ ] El flujo de onboarding funciona sin SMTP en desarrollo.
- [ ] El flujo funciona con SMTP configurado en staging.
- [ ] Un fallo de email no revierte la aprobación ni la creación de la empresa.
- [ ] Los tokens y contraseñas no aparecen en logs.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el proceso de alta.

## Contrato técnico

### Modelo de datos

US-29 **no agrega colecciones ni documentos de MongoDB**. Es un servicio sin persistencia: compone el mensaje y lo entrega al remitente configurado.

| Colección | Relación con US-29 | Acción en esta historia |
| --- | --- | --- |
| `emails` | Ya reservada en `docs/modelos.md` para registrar los envíos (entidad `EmailMessage`, índice `ix_emails_estado_fecha` sobre `Estado + CreatedAt`, **no único**). | **No se crea.** La persistencia de los envíos y la outbox son de US-17. |

Consecuencia práctica: si el remitente falla, la traza queda en el log estructurado y en el `EventoAuditoria` que escribe el **caso de uso llamador** (US-01, US-27 o US-28), nunca en una colección nueva.

El record `EmailMessage` de Application es el **mensaje compuesto que se entrega al remitente** (`Para`, `Asunto`, `CuerpoHtml`, `CuerpoTexto`, `Plantilla`). No es la entidad persistida `EmailMessage` de `docs/modelos.md`, que registra los envíos en la colección `emails` y que crea US-17 junto con la outbox: son dos tipos distintos, en proyectos distintos.

Tipos que introduce:

| Tipo | Namespace | Responsabilidad |
| --- | --- | --- |
| `IEmailSender` | `FacturAR.Application.Notificaciones` | Único puerto de envío. |
| `EmailMessage` | `FacturAR.Application.Notificaciones` | Mensaje compuesto, listo para entregar. |
| `EmailResult` | `FacturAR.Application.Notificaciones` | Resultado controlado del envío. |
| `EmailService` | `FacturAR.Application.Notificaciones` | Compone las 5 plantillas y aplica codificación segura. |
| `PlantillasEmail` | `FacturAR.Application.Notificaciones` | Cuerpo HTML y texto plano de cada plantilla. |
| `NotificacionesOptions` | `FacturAR.Application.Notificaciones` | `Portal__BaseUrl` y `Admin__NotificationEmail`. |
| `EmailOptions` | `FacturAR.Infrastructure.Notificaciones` | Transporte: `Provider`, `From`, `FromName`, `Host`, `Port`, `User`, `Password`, `UseSsl`, `DirectorioSalida`, `TimeoutSegundos`. |
| `FileEmailSender` | `FacturAR.Infrastructure.Notificaciones` | Remitente de desarrollo. |
| `SmtpEmailSender` | `FacturAR.Infrastructure.Notificaciones` | Remitente de producción. |

Contratos:

```csharp
public sealed record EmailMessage(
    string Para,
    string Asunto,
    string CuerpoHtml,
    string CuerpoTexto,
    string Plantilla);

public sealed record EmailResult(bool Exitoso, DateTime? EnviadoAt, string? Error)
{
    public static EmailResult Ok(DateTime enviadoAt) => new(true, enviadoAt, null);
    public static EmailResult Fallido(string error) => new(false, null, error);
}

public interface IEmailSender
{
    Task<EmailResult> EnviarAsync(EmailMessage mensaje, CancellationToken cancellationToken = default);
}
```

`EmailResult.Error` **nunca** contiene el token, la contraseña de SMTP ni el cuerpo del email: solo el tipo de excepción y un mensaje sanitizado.

### Remitentes

**`FileEmailSender`** (desarrollo).

- Escribe un archivo por email en `EmailOptions.DirectorioSalida` (por defecto `emails`, relativo a la raíz de la aplicación).
- Nombre del archivo: `{yyyyMMdd-HHmmss-fff}_{Plantilla}_{Guid:N}.html`. El contenido es un documento HTML completo con el asunto, el destinatario y el cuerpo renderizado.
- El log registra **solo metadatos**: `plantilla`, `exito`, `archivo`, `duracionMs` y `destinatario` enmascarado (`j***@dominio.com`). Nunca el cuerpo, nunca el token.
- Solo se registra si el entorno es `Development`. Si `Email__Provider=File` se configura fuera de `Development`, la API **falla al arrancar** (validación de opciones): evita dejar tokens de invitación en disco en producción.
- El archivo con el link de activación es un secreto local: su carpeta tiene que estar fuera del control de versiones y de la imagen de Docker.

**`SmtpEmailSender`** (producción).

- Usa `System.Net.Mail.SmtpClient` y `System.Net.Mail.MailMessage`, que forman parte del framework compartido. **No se agrega ningún paquete NuGet**: `SmtpClient` no está obsoleto en .NET 10 (verificado contra el SDK 10.0.400) y el stack no tiene otra alternativa de SMTP.
- El envío es asíncrono y acotado en el tiempo: `await cliente.SendAsync(mensaje).WaitAsync(timeout, cancellationToken)` con `timeout = EmailOptions.TimeoutSegundos` (por defecto 15 s). `SmtpClient` no acepta `CancellationToken`, por eso el límite se impone con `WaitAsync`.
- `EnableSsl = EmailOptions.UseSsl`, `Port = EmailOptions.Port` (por defecto 587), `Credentials` solo si `Email__User` está informado.
- `From` y `FromName` salen de `Email__From` y `Email__FromName`. El remitente es el único que arma el encabezado `De`; `EmailMessage` no lo lleva.
- Cualquier excepción de transporte (timeout, autenticación, TLS, destino inválido) se captura y se devuelve como `EmailResult.Fallido(...)`. **Nunca** se propaga.

Alternativa a futuro, fuera de alcance: si el servidor de correo exigiera OAuth2/XOAUTH2 o cancelación por mensaje, la actualización justificada sería `MailKit`. No se incorpora en el MVP.

### Los 5 emails

Link base: `Portal__BaseUrl` sin barra final. Link de activación: `{Portal__BaseUrl}/activar?token={token}`.

| Plantilla | Destinatario | Cuándo se envía | Asunto | Cuerpo |
| --- | --- | --- | --- | --- |
| `SolicitudRecibida` | `EmailSolicitante` | Al crear la solicitud (US-01) | `Recibimos tu solicitud de acceso a FacturAR` | Confirma que la solicitud quedó registrada, muestra la razón social declarada y **no** promete acceso: indica que la revisión es manual y que el próximo contacto llega por email si la solicitud es aprobada. No incluye el CUIT completo. |
| `NuevaSolicitud` | `Admin__NotificationEmail` | Al crear la solicitud, **solo** si esa clave está configurada | `Nueva solicitud de alta: {RazonSocial}` | Notifica a la administración global: razón social, CUIT, email del solicitante y fecha de recepción. Incluye el link al panel `/admin/solicitudes`. |
| `SolicitudAprobada` | `EmailSolicitante` | Al aprobar (US-27) | `Tu solicitud fue aprobada: activá tu cuenta` | Confirma la aprobación, muestra la razón social, incluye el **link completo de activación** y la fecha de expiración (48 h). Indica que el link es de un solo uso. |
| `SolicitudRechazada` | `EmailSolicitante` | Al rechazar (US-27) | `Tu solicitud de acceso fue rechazada` | Indica el rechazo y **incluye el motivo** textual. No incluye datos de terceros ni de otras solicitudes. |
| `InvitacionReenviada` | `EmailSolicitante` | Al reenviar la invitación (US-27) | `Reenviamos tu invitación de acceso a FacturAR` | Nuevo link de activación con la nueva fecha de expiración y la aclaración de que el link anterior queda sin efecto. |

Reglas comunes a las 5 plantillas:

- Cada plantilla produce **HTML y texto plano** con el mismo contenido.
- Todo valor dinámico se inserta con `System.Net.WebUtility.HtmlEncode` en el HTML. El motivo de rechazo y la razón social son los casos reales de inyección.
- El HTML es un documento autocontenido: sin CSS externo, sin imágenes remotas, sin JavaScript.
- El texto plano no lleva etiquetas y normaliza los saltos de línea a `CRLF`.
- Ninguna plantilla incluye API keys, contraseñas ni hashes.

### Configuración

| Clave | Tipo | Default | Uso |
| --- | --- | --- | --- |
| `Email__Provider` | `File` \| `Smtp` | `File` en `Development`, `Smtp` en el resto | Selecciona el remitente **sin recompilar**. Se registra con `builder.Services.AddSingleton<IEmailSender, ...>()` según el valor. |
| `Email__From` | string | `facturar@localhost` | Dirección del remitente. |
| `Email__FromName` | string | `FacturAR` | Nombre visible del remitente. |
| `Email__Host` | string | — | Host SMTP. Obligatorio si `Provider = Smtp`. |
| `Email__Port` | int | `587` | Puerto SMTP. `465` si `UseSsl = true` y el servidor espera TLS implícito. |
| `Email__User` | string | — | Usuario SMTP. Vacío = sin autenticación. |
| `Email__Password` | string | — | Contraseña SMTP. **Nunca** se loguea ni se versiona. |
| `Email__UseSsl` | bool | `true` | TLS/STARTTLS. |
| `Email__DirectorioSalida` | string | `emails` | Carpeta del remitente de archivo (solo `Development`). |
| `Email__TimeoutSegundos` | int | `15` | Límite del envío SMTP. |
| `Portal__BaseUrl` | string | `http://localhost:5116` | URL pública del portal; base de los links de activación. |
| `Admin__NotificationEmail` | string | — | Email de la administración global. **Opcional**: si está vacío, `NuevaSolicitud` no se envía. |

Todas llegan por variables de entorno (`EMAIL__PROVIDER`, `EMAIL__HOST`, `PORTAL__BASEURL`, etc.) o por `appsettings`. El valor de `Portal__BaseUrl` es el que usan US-01, US-27 y US-28 para armar los links.

### Endpoints

US-29 **no define endpoints**. Los endpoints que disparan emails pertenecen a otras historias:

| Método | Ruta | Historia | Email disparado |
| --- | --- | --- | --- |
| `POST` | `/v1/solicitudes-alta` | US-01 | `SolicitudRecibida` y, si corresponde, `NuevaSolicitud` |
| `POST` | `/v1/admin/solicitudes/{id}/aprobar` | US-27 | `SolicitudAprobada` |
| `POST` | `/v1/admin/solicitudes/{id}/rechazar` | US-27 | `SolicitudRechazada` |
| `POST` | `/v1/admin/solicitudes/{id}/reenviar-invitacion` | US-27 | `InvitacionReenviada` |

### Casos de uso y servicios

| Pieza | Tipo | Namespace | Responsabilidad |
| --- | --- | --- | --- |
| `IEmailSender` | Interfaz | `FacturAR.Application.Notificaciones` | Puerto único de envío. |
| `EmailService` | Clase | `FacturAR.Application.Notificaciones` | Orquesta: arma el `EmailMessage` con la plantilla correcta, aplica `HtmlEncode`, llama al `IEmailSender` y devuelve el `EmailResult` **sin propagar excepciones**. |
| `PlantillasEmail` | Clase estática | `FacturAR.Application.Notificaciones` | Los 5 cuerpos HTML y texto. |
| `FileEmailSender` | Clase | `FacturAR.Infrastructure.Notificaciones` | Remitente de archivo. |
| `SmtpEmailSender` | Clase | `FacturAR.Infrastructure.Notificaciones` | Remitente SMTP. |

Firmas de `EmailService` (una por plantilla):

```csharp
Task<EmailResult> EnviarSolicitudRecibidaAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct);
Task<EmailResult> EnviarNuevaSolicitudAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct);
Task<EmailResult> EnviarSolicitudAprobadaAsync(SolicitudAltaEmpresa solicitud, string token, DateTime expiraAt, CancellationToken ct);
Task<EmailResult> EnviarSolicitudRechazadaAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct);
Task<EmailResult> EnviarInvitacionReenviadaAsync(SolicitudAltaEmpresa solicitud, string token, DateTime expiraAt, CancellationToken ct);
```

Reglas de la orquestación:

- El token plano se recibe como parámetro y se usa **solo** para armar el link en memoria. No se persiste, no se loguea y no se devuelve en el `EmailResult`.
- `EmailService` no escribe en MongoDB. Quien audita el envío (éxito o fallo) es el caso de uso llamador, mediante `IEventoAuditoriaRepository`.
- `EmailService` no lanza excepciones de transporte. Solo propaga `OperationCanceledException` cuando el `CancellationToken` se cancela.

### Validaciones

| Regla | Mensaje al usuario | Dónde | Código del catálogo |
| --- | --- | --- | --- |
| `Email__Provider` es `File` o `Smtp` | — | Arranque (validación de opciones) | `ERROR_INTERNO` (500) si el arranque falla |
| `Email__Host` no vacío cuando `Provider = Smtp` | — | Arranque (validación de opciones) | `ERROR_INTERNO` (500) |
| `Portal__BaseUrl` es una URL absoluta | — | Arranque (validación de opciones) | `ERROR_INTERNO` (500) |
| `Admin__NotificationEmail` con formato válido si está informado | — | Arranque (validación de opciones) | `ERROR_INTERNO` (500) |
| El destinatario tiene formato de email válido | — | `EmailService`, antes de enviar | Sin respuesta HTTP: se registra `EmailResult.Fallido` |
| El motivo de rechazo no excede 500 caracteres | El motivo es demasiado largo | Lo valida US-27 antes de llamar | `VALIDACION` (400) |

Un fallo de envío **no es un error de validación del cliente**: por eso no tiene código HTTP propio salvo en el caso de US-27 documentado en contratos-api.md §12 (`EMAIL_NO_ENVIADO`, 502).

### Estados

US-29 no tiene máquina de estados propia. El resultado del envío tiene dos estados y el caso de uso llamador decide qué hacer:

| Estado | Significado | Efecto en la operación de negocio |
| --- | --- | --- |
| `Enviado` (`EmailResult.Ok`) | El remitente aceptó el mensaje. | La operación de negocio ya estaba confirmada; se registra auditoría `email.enviado`. |
| `Fallido` (`EmailResult.Fallido`) | El remitente no pudo entregar el mensaje. | **La operación de negocio NO se revierte** (RNF-22). Se registra auditoría `email.fallido` y el panel muestra una advertencia con la acción de reenvío. |

Única excepción, ya fijada en `docs/contratos-api.md` §12: cuando el envío **era** la operación pedida (`POST /v1/admin/solicitudes/{id}/reenviar-invitacion`), el endpoint responde `502 EMAIL_NO_ENVIADO`.

### Integración con otras historias

| Historia | Qué aporta a US-29 | Qué recibe de US-29 |
| --- | --- | --- |
| US-19 (Docker) | Variables de entorno y carpeta de emails de desarrollo. | — |
| US-01 | Dispara `SolicitudRecibida` y `NuevaSolicitud`; audita el resultado. | `IEmailSender`, `EmailService`, `Portal__BaseUrl`. |
| US-27 | Dispara `SolicitudAprobada`, `SolicitudRechazada` y `InvitacionReenviada`; audita el resultado. | `IEmailSender`, `EmailService`, `Portal__BaseUrl`. |
| US-28 | Ninguna: la activación no envía email. | — |
| US-17 (Auditoría y outbox) | Puede migrar los envíos a la outbox y persistirlos en la colección `emails`. | Los eventos `email.enviado` / `email.fallido` que ya se escriben. |

Orden: US-29 → US-01 → US-27 → US-28. US-29 no depende de ninguna otra historia pendiente.

### Dependencias técnicas

- Proyecto `FacturAR.Application` (sin `MongoDB.Driver`) y `FacturAR.Infrastructure` existentes. `IEmailSender` no requiere repositorio: no hay acceso a MongoDB en esta historia.
- **Sin paquetes NuGet nuevos.** `System.Net.Mail` pertenece al framework compartido de .NET 10.
- Configuración: las claves `Email__*`, `Portal__BaseUrl` y `Admin__NotificationEmail` llegan por variables de entorno (`docker compose --env-file .env`). `Portal__BaseUrl` debe apuntar al portal (`http://localhost:5116` en local, `http://localhost:8081` en Docker).
- `.gitignore` y `.dockerignore` tienen que excluir la carpeta de emails de desarrollo (`emails/`) para que ni los archivos con tokens ni la carpeta entren en la imagen de producción. Es un cambio de esos dos archivos, fuera de esta historia.
- Registro en DI: `IEmailSender` se resuelve **una sola vez** según `Email__Provider`; no hay resolución condicional por request.
- Tests: `FacturAR.UnitTests` referencia solo `FacturAR.Domain` y `FacturAR.Application`, así que las plantillas y `EmailService` se testean ahí. El test de integración de `FileEmailSender` necesita `FacturAR.Infrastructure`: si el proyecto de tests de integración no lo referencia todavía, hay que agregar la referencia al `.csproj` (sin paquetes nuevos).

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-29-01 | `Email__Provider = File`, `Portal__BaseUrl = http://localhost:5116` | Componer y enviar `SolicitudRecibida` con razón social `ACME S.A.` | Se crea un archivo en `emails/` con asunto `Recibimos tu solicitud de acceso a FacturAR`, el cuerpo indica que la revisión es manual y el siguiente paso; el log registra `plantilla`, `exito` y `archivo` |
| CP-29-02 | Remitente de archivo configurado | Enviar un email con token en el cuerpo | El archivo contiene el link completo; el log **no** contiene el token ni el cuerpo |
| CP-29-03 | `Admin__NotificationEmail` no configurada | Crear una solicitud | No se envía `NuevaSolicitud`; solo `SolicitudRecibida`; ninguna operación falla |
| CP-29-04 | `Admin__NotificationEmail` configurada | Crear una solicitud | Se envían las dos plantillas; la de administración incluye el link `/admin/solicitudes` |
| CP-29-05 | Plantilla de rechazo | Renderizar el motivo `Datos <script>alert(1)</script> incompletos` | El HTML contiene el texto escapado (`&lt;script&gt;`), sin etiqueta ejecutable; el texto plano conserva el motivo literal |
| CP-29-06 | Invitación vigente | Enviar `SolicitudAprobada` | El cuerpo incluye `{Portal__BaseUrl}/activar?token=...` y la fecha de expiración a 48 h |
| CP-29-07 | Invitación vigente | Enviar `InvitacionReenviada` | El cuerpo incluye el link nuevo y la aclaración de que el anterior queda sin efecto |
| CP-29-08 | SMTP inaccesible | Enviar cualquier plantilla | `EmailResult.Exitoso = false` con `Error` sanitizado; **no** se lanza excepción; el token no aparece en `Error` |
| CP-29-09 | Dos entornos con distinta configuración | Arrancar con `Email__Provider = File` y luego con `Smtp` | Se instancia el remitente correspondiente sin recompilar; con `Provider = File` fuera de `Development` la API falla al arrancar |
| CP-29-10 | Plantillas de las 5 plantillas | Renderizar todas y revisar el resultado | Cada una produce HTML y texto plano con el mismo contenido; ninguna contiene API keys, contraseñas ni hashes |

## Notas de alcance

- No implementa outbox ni reintentos automáticos; eso corresponde a US-17. Los webhooks están fuera de alcance (US-23).
- No implementa plantillas editables desde el portal.
- No implementa email marketing ni notificaciones masivas.
- No se envían API keys por email.
