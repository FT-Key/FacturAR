# US-08 — Notas de crédito y débito

**Épica:** Emisión  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RF-07  
**Depende de:** US-05  
**Habilita:** US-09, US-10, US-12, US-13

## Historia

Como usuario emisor, quiero emitir notas de crédito y débito asociadas a una factura autorizada para corregir o ajustar una operación.

## Contexto

Una nota debe referenciar un comprobante autorizado del mismo contribuyente. Conserva el tipo A, B o C de la factura original y usa una secuencia propia por tipo.

## Criterios de aceptación

- [ ] Se aceptan notas de débito A (`2`), crédito A (`3`), débito B (`7`), crédito B (`8`), débito C (`12`) y crédito C (`13`).
- [ ] La nota referencia un comprobante autorizado del mismo contribuyente.
- [ ] No se puede referenciar un comprobante de otra empresa.
- [ ] No se puede referenciar un comprobante anulado o rechazado.
- [ ] El tipo de nota corresponde al tipo de la factura original.
- [ ] La nota tiene su propio número correlativo por punto de venta y tipo.
- [ ] La nota tiene su propio CAE y vencimiento.
- [ ] El importe de la nota no puede superar el saldo ajustable de la factura original.
- [ ] Una nota de crédito disminuye el saldo; una de débito lo aumenta.
- [ ] La respuesta `201` incluye el comprobante asociado y el CAE.
- [ ] El comprobante original conserva su número y CAE.
- [ ] Se registra auditoría de la emisión.
- [ ] Hay tests de tipos, referencia inválida, saldo y aislamiento.

## Escenarios Gherkin

```gherkin
Scenario: Nota de crédito válida
  Given una factura A autorizada por 12100
  When se emite una nota de crédito A por 1210
  Then la nota se crea con CAE propio
  And el saldo ajustable de la factura queda en 10890
```

```gherkin
Scenario: Nota de otra empresa
  Given una factura de la empresa A
  When la empresa B intenta emitir una nota sobre esa factura
  Then la API devuelve 404
  And no se crea la nota
```

```gherkin
Scenario: Tipo incompatible
  Given una factura C
  When se intenta emitir una nota de crédito A
  Then la API devuelve 400 con código "TIPO_NOTA_INCOMPATIBLE"
```

```gherkin
Scenario: Importe superior al saldo
  Given una factura con saldo ajustable de 1000
  When se emite una nota de crédito por 1500
  Then la API devuelve 400 con código "IMPORTE_EXCEDIDO"
  And no se consume un número
```

## Tareas técnicas

- [ ] Extender el modelo de comprobante con la referencia al comprobante original.
- [ ] Implementar el cálculo de saldo ajustable.
- [ ] Validar tipo, tenant y estado del comprobante original.
- [ ] Integrar numeración y CAE de US-05 y US-07.
- [ ] Crear el endpoint de notas o reutilizar `POST /v1/comprobantes` con tipo de nota.
- [ ] Registrar auditoría.
- [ ] Agregar tests de notas A, B, C y validaciones.
- [ ] Documentar tipos, referencia y errores.

## Definition of Done

- [ ] Se emiten notas de crédito y débito A, B y C.
- [ ] Las notas quedan asociadas a la factura original.
- [ ] No se ajusta una factura de otra empresa.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Notas de alcance

- No implementa anulación administrativa; eso corresponde a US-22.
- No implementa notas en moneda extranjera.
- No implementa devoluciones ni pagos.
