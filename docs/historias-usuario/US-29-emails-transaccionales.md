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

## Notas de alcance

- No implementa outbox, reintentos automáticos ni webhooks; eso corresponde a US-17 y US-23.
- No implementa plantillas editables desde el portal.
- No implementa email marketing ni notificaciones masivas.
- No se envían API keys por email.
