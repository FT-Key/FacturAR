# Requisitos funcionales

## Objetivo

Definir el comportamiento funcional de FacturAR, un simulador de facturación electrónica de AFIP/ARCA con onboarding de empresas, API REST/SOAP, portal web, emails transaccionales y persistencia en MongoDB.

## Actores

| Actor | Descripción |
| --- | --- |
| Visitante | Consulta la verificación pública, el manual y envía una solicitud de alta. |
| Solicitante | Persona que representa a una empresa y completa la solicitud. |
| Administrador global | Revisa, aprueba o rechaza solicitudes de alta. |
| Usuario administrador de empresa | Activa su cuenta, administra usuarios, puntos de venta y API keys. |
| Usuario emisor | Emite comprobantes desde el portal o la API. |
| Usuario consulta | Consulta comprobantes de su empresa. |
| Sistema cliente | Integración externa autenticada con API key. |
| Sistema AFIP simulado | Autoriza comprobantes y devuelve un CAE simulado. |

## Requisitos

| ID | Requisito | Descripción | Prioridad | Historia |
| --- | --- | --- | --- | --- |
| RF-01 | Onboarding de empresas | Proceso completo de solicitud, aprobación, invitación y activación de una empresa. | Alta | US-01, US-27, US-28 |
| RF-02 | Gestión de API keys | Crear, listar, revocar y regenerar API keys por contribuyente. | Alta | US-02 |
| RF-03 | Autenticación de integraciones | Validar la API key en cada request y resolver el contribuyente asociado. | Alta | US-03 |
| RF-04 | Emisión de comprobantes | Emitir facturas A, B y C con ítems, importes, receptor y CAE simulado. | Alta | US-05 |
| RF-05 | Idempotencia | Devolver el mismo comprobante ante reintentos con la misma clave de idempotencia. | Alta | US-06 |
| RF-06 | Numeración atómica | Asignar números correlativos sin duplicados por contribuyente, punto de venta y tipo. | Alta | US-07 |
| RF-07 | Notas de crédito y débito | Emitir notas asociadas a un comprobante autorizado. | Alta | US-08 |
| RF-08 | Consultas | Consultar comprobantes, detalle y último número autorizado. | Alta | US-09 |
| RF-09 | PDF y QR | Generar un PDF con los datos fiscales y un QR de verificación. | Alta | US-10 |
| RF-10 | Verificación pública | Permitir la consulta anónima de un comprobante por CAE. | Alta | US-11 |
| RF-11 | Autenticación y multiempresa | Autenticar usuarios de empresa y administradores globales, con roles y aislamiento por contribuyente. | Alta | US-04 |
| RF-12 | Emisión manual | Permitir emitir y descargar comprobantes desde el portal. | Media | US-13 |
| RF-13 | Listado de comprobantes | Listar, filtrar y descargar comprobantes del contribuyente. | Media | US-12 |
| RF-14 | API keys desde el portal | Administrar API keys sin usar la API. | Media | US-14 |
| RF-15 | SOAP de emisión | Exponer `FECAESolicitar` compatible con el flujo de AFIP. | Alta | US-15 |
| RF-16 | SOAP de consultas | Exponer `FECompUltimoAutorizado`, `FECompConsultar` y `FEDummy`. | Media | US-16 |
| RF-17 | Auditoría y outbox | Registrar eventos y encolar notificaciones de forma confiable. | Media | US-17 |
| RF-18 | Parámetros y seed | Proveer datos iniciales de prueba y parámetros del sistema. | Media | US-18 |
| RF-19 | Anulación administrativa | Anular comprobantes desde una vista MVC. | Baja (stretch) | US-22 |
| RF-20 | Webhooks | Notificar eventos a sistemas externos. | Baja (stretch) | US-23 |
| RF-21 | OpenAPI y salud | Publicar OpenAPI y endpoints de health check. | Alta | US-19, US-20, US-31 |
| RF-22 | Adaptador AFIP real | Permitir cambiar el simulador por el web service real. | Baja (stretch) | US-24 |
| RF-23 | Solicitud pública de alta | Registrar una solicitud de empresa sin crear contribuyente ni usuario. | Alta | US-01 |
| RF-24 | Aprobación de solicitudes | Revisar, corregir, aprobar o rechazar solicitudes y crear la empresa. | Alta | US-27 |
| RF-25 | Invitación y activación | Activar la cuenta del solicitante con un token de un solo uso. | Alta | US-28 |
| RF-26 | Emails transaccionales | Enviar confirmación, aprobación, rechazo e invitación por email. | Alta | US-29 |
| RF-27 | Manual de integración | Publicar una guía online para usar la API y las API keys. | Media | US-30 |

## Reglas de negocio

### Onboarding de empresas

- El alta es por solicitud, no por registro libre.
- Una solicitud no crea contribuyente, usuario, punto de venta ni API key.
- Una solicitud pendiente es única por CUIT y por email.
- El CUIT se normaliza y se valida con dígito verificador.
- El CUIT es público y no prueba la titularidad de la empresa.
- La aprobación crea el contribuyente, el punto de venta `1 Casa Central` y una invitación.
- La aprobación es una operación atómica e idempotente.
- Una solicitud aprobada o rechazada no se vuelve a procesar.
- El rechazo exige un motivo.
- La invitación se envía al email del solicitante, vence a las 48 horas y es de un solo uso.
- El token de invitación se guarda hasheado.
- La activación crea el usuario administrador con el `ContribuyenteId` de la invitación.
- El email del usuario no se puede cambiar durante la activación.
- El administrador global inicial se crea por configuración de forma idempotente.

### Autenticación y roles

- Los usuarios de empresa pertenecen a un único contribuyente.
- Los roles de empresa son Administrador, Emisor y Consulta.
- Los administradores globales no pertenecen a un contribuyente.
- Los roles globales son SuperAdmin y Soporte.
- `SuperAdmin` puede aprobar y rechazar solicitudes.
- `Soporte` puede ver solicitudes y reenviar invitaciones, pero no aprobar ni rechazar.
- Las sesiones usan cookies `HttpOnly`; `Secure` en producción.
- Las acciones que modifican datos requieren antiforgery o protección equivalente.
- Un usuario no puede acceder a datos de otro contribuyente cambiando ids.
- El `ContribuyenteId` se resuelve desde la sesión o la API key, nunca desde el body.

### API keys

- Una API key pertenece a un único contribuyente.
- El valor completo se muestra una única vez.
- Se guarda el hash y un prefijo visible único.
- Una key revocada o regenerada deja de autenticar de inmediato.
- Solo un administrador de empresa puede crear, revocar o regenerar keys.
- La API key se envía en el header `X-Api-Key`.

### Comprobantes

- Tipos soportados: Factura A (1), Nota de Débito A (2), Nota de Crédito A (3), Factura B (6), Nota de Débito B (7), Nota de Crédito B (8), Factura C (11), Nota de Débito C (12) y Nota de Crédito C (13).
- Una factura A se emite a responsables inscriptos y discrimina IVA.
- Una factura B se emite a consumidores finales, monotributistas o exentos; el IVA se incluye y no se discrimina.
- Una factura C se emite a consumidores finales y no discrimina IVA.
- El número se asigna dentro de una transacción atómica.
- El CAE simulado tiene 14 dígitos, es único y vence a los 10 días de la emisión.
- El importe total debe ser igual a neto + IVA, según la alícuota.
- Una nota de crédito o débito debe referenciar un comprobante autorizado del mismo contribuyente.
- Una nota no puede superar el saldo ajustable del comprobante original.
- Un comprobante anulado conserva su número y su CAE para trazabilidad.

### Idempotencia

- La clave de idempotencia es única por contribuyente.
- Si llega un request repetido con la misma clave y el mismo payload, se devuelve el comprobante original sin volver a numerar ni generar CAE.
- Si el payload cambia con la misma clave, se devuelve `409`.
- La clave se envía en el header `Idempotency-Key`.

### Verificación pública

- La consulta por CAE es anónima.
- Solo se exponen datos fiscales no sensibles.
- Un CAE inexistente devuelve `404`.
- Un CAE con formato inválido devuelve `400`.
- La respuesta no expone datos internos, hashes ni API keys.

### Emails

- Los emails transaccionales son: solicitud recibida, notificación al administrador, aprobación con invitación, rechazo e invitación reenviada.
- El email de invitación incluye el link de activación y la fecha de expiración.
- Un fallo de email no revierte la aprobación ni la creación de la empresa.
- El administrador puede reenviar la invitación.
- Los tokens y contraseñas no se registran en logs.
- En desarrollo se escribe el email completo en un archivo local; el log solo guarda metadatos. En producción se usa SMTP configurable.

### Manual de integración

- El manual es público y accesible desde el portal.
- Explica solicitud, activación, API keys, autenticación, emisión, consultas, idempotencia, errores y seguridad.
- Los ejemplos usan marcadores de posición, nunca API keys reales.
- El manual aclara que el CAE es simulado y no tiene validez fiscal.

## Fuera de alcance obligatorio

- Facturación real contra AFIP/ARCA.
- Validación de identidad del representante legal y constancia de inscripción.
- Gestión contable, cobranzas o stock.
- Aplicación móvil nativa.
- Kubernetes.
- Integración con Mercado Pago.
- Email marketing, notificaciones masivas o WhatsApp.
- Gestión de administradores globales desde el portal.
- Recuperación de contraseña y doble factor.
