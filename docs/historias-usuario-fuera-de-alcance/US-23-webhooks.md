# US-23 — Webhooks de eventos

**Épica:** Integraciones  
**Prioridad:** Fuera de alcance  
**Estimación:** 2 días  
**Requisito:** RF-20  
**Depende de:** US-02, US-05, US-17  
**Estado:** Fuera de alcance

## Historia

Como sistema cliente, quiero suscribirme a eventos de FacturAR y recibir notificaciones HTTP firmadas para reaccionar a emisiones y cambios sin consultar constantemente.

## Contexto

Los webhooks se encolan en el outbox y se envían con reintentos. Cada entrega se firma con HMAC para que el cliente verifique el origen.

## Criterios de aceptación

- [ ] El administrador de empresa puede registrar una URL de webhook.
- [ ] Se puede elegir qué eventos recibir.
- [ ] Los eventos incluyen `comprobante.emitido`, `comprobante.anulado` y `solicitud.aprobada`.
- [ ] Cada entrega incluye un id de evento, timestamp y firma HMAC.
- [ ] El secreto de firma se muestra una única vez.
- [ ] Las entregas fallidas se reintentan con backoff.
- [ ] El cliente puede ver el historial de entregas.
- [ ] Se puede desactivar un webhook.
- [ ] No se envían secretos ni datos sensibles innecesarios.
- [ ] Hay tests de firma, reintento e idempotencia.

## Escenarios Gherkin

```gherkin
Scenario: Entrega de evento
  Given un webhook activo para "comprobante.emitido"
  When se emite un comprobante
  Then el cliente recibe un POST firmado
  And el payload incluye el id y el CAE del comprobante
```

```gherkin
Scenario: Firma inválida
  Given una entrega con firma HMAC
  When el cliente valida la firma con el secreto
  Then la validación es correcta
```

```gherkin
Scenario: Reintento
  Given un webhook que responde 500
  When falla la primera entrega
  Then el sistema reintenta con backoff
  And registra el último error
```

## Tareas técnicas

- [ ] Crear `Webhook` y `WebhookEntrega`.
- [ ] Implementar alta, listado y baja.
- [ ] Generar secreto y firma HMAC.
- [ ] Encolar entregas en el outbox.
- [ ] Implementar worker de entrega y reintentos.
- [ ] Crear endpoints de administración.
- [ ] Agregar tests.
- [ ] Documentar eventos, firma y errores.

## Definition of Done

- [ ] Las entregas se firman y reintentan.
- [ ] El cliente puede verificar el origen.
- [ ] No se filtran secretos.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice.

## Notas de alcance

- No implementa webhooks bidireccionales.
- No implementa filtros avanzados por tenant.
- No implementa entrega exactly-once; el cliente debe ser idempotente.
