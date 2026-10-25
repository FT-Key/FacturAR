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

## Notas de alcance

- No implementa una consola de auditoría avanzada.
- No implementa alertas o monitoreo externo.
- No implementa webhooks; eso corresponde a US-23.
