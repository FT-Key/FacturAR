# US-22 — Anulación administrativa MVC

**Épica:** Administración  
**Prioridad:** Fuera de alcance  
**Estimación:** 2 días  
**Requisito:** RF-19  
**Depende de:** US-04, US-05, US-08  
**Estado:** Fuera de alcance

## Historia

Como administrador de una empresa, quiero anular un comprobante desde una vista MVC dejando un motivo para corregir errores sin eliminar la trazabilidad fiscal.

## Contexto

La anulación es lógica: el comprobante conserva número, CAE y datos históricos, pero cambia de estado. No se borra físicamente.

## Criterios de aceptación

- [ ] La vista MVC lista comprobantes anulables de la empresa.
- [ ] Solo un usuario `Administrador` puede anular.
- [ ] La anulación exige un motivo.
- [ ] El comprobante pasa a estado `Anulado`.
- [ ] Se conservan número, CAE, fecha e importes.
- [ ] No se puede anular un comprobante ya anulado.
- [ ] No se puede anular un comprobante de otra empresa.
- [ ] La anulación registra auditoría.
- [ ] La verificación pública muestra el estado `Anulado`.
- [ ] Hay tests de anulación y aislamiento.

## Escenarios Gherkin

```gherkin
Scenario: Anular un comprobante
  Given un comprobante emitido de la empresa
  When el administrador lo anula con un motivo
  Then el comprobante queda en estado "Anulado"
  And conserva su número y CAE
```

```gherkin
Scenario: Doble anulación
  Given un comprobante anulado
  When se intenta anular otra vez
  Then la operación se rechaza
  And el comprobante no cambia
```

```gherkin
Scenario: Comprobante ajeno
  Given un comprobante de otra empresa
  When el administrador intenta anularlo
  Then la API devuelve 404
```

## Tareas técnicas

- [ ] Crear la vista MVC de anulación.
- [ ] Implementar `AnularComprobanteService`.
- [ ] Validar estado, tenant y rol.
- [ ] Registrar auditoría.
- [ ] Actualizar la verificación pública.
- [ ] Agregar tests.
- [ ] Documentar el flujo.

## Definition of Done

- [ ] La anulación conserva la trazabilidad.
- [ ] El estado se refleja en consultas y verificación.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice.

## Notas de alcance

- No implementa nota de crédito automática.
- No implementa eliminación física.
- No implementa anulación masiva.
