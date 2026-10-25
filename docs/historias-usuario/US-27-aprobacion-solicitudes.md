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
  Then la API devuelve 403 con código "SIN_PERMISO"
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

## Notas de alcance

- No crea el usuario administrador; eso ocurre en US-28.
- No permite aprobar solicitudes con CUIT duplicado.
- No implementa edición de contribuyentes ya creados.
- No implementa eliminación física de solicitudes.
