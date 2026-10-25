# US-05 — Emisión de facturas A, B y C

**Épica:** Emisión  
**Prioridad:** Alta  
**Estimación:** 3 días  
**Requisito:** RF-04  
**Depende de:** US-03, US-07  
**Habilita:** US-06, US-08, US-09, US-11, US-13, US-17, US-18, US-21, US-24

## Historia

Como usuario emisor o sistema cliente, quiero emitir una factura con sus ítems para obtener un número correlativo y un CAE simulado, respetando la condición frente al IVA del emisor y del receptor.

## Contexto

La emisión usa el `ContribuyenteId` autenticado. El número y el CAE se asignan dentro de una transacción. La factura se persiste con una copia histórica del emisor y del receptor.

## Criterios de aceptación

- [ ] Se aceptan facturas A (`1`), B (`6`) y C (`11`) con los códigos AFIP correspondientes.
- [ ] El emisor se resuelve desde la sesión o la API key; no se acepta en el body.
- [ ] Se valida que el contribuyente y el punto de venta estén activos y pertenezcan al emisor.
- [ ] Se valida el receptor: tipo y número de documento, razón social y condición IVA.
- [ ] Se validan los ítems: descripción, cantidad mayor que cero, precio no negativo y alícuota permitida.
- [ ] Una factura A se emite a responsables inscriptos y discrimina IVA.
- [ ] Una factura B se emite a consumidor final, monotributista o exento; el IVA se incluye y no se discrimina.
- [ ] Una factura C se emite a consumidor final y no discrimina IVA.
- [ ] El total se calcula como neto + IVA y no se acepta un total inconsistente.
- [ ] La emisión asigna número y CAE dentro de una transacción.
- [ ] El CAE tiene 14 dígitos, es único y vence a los 10 días.
- [ ] La respuesta `201` incluye id, número, CAE, vencimiento, importes y estado.
- [ ] Un error de validación devuelve `400` con `ErrorResponse` y detalles por campo.
- [ ] Un punto de venta ajeno o inexistente devuelve `404`.
- [ ] Un contribuyente inactivo devuelve `403`.
- [ ] La emisión registra un evento de auditoría.
- [ ] El comprobante queda disponible para consulta y verificación por CAE.

## Escenarios Gherkin

```gherkin
Scenario: Emitir una factura C
  Given una empresa monotributista autenticada con el punto de venta 1 activo
  When emite una factura C para consumidor final por 10000
  Then la API devuelve 201
  And el número asignado es el siguiente de la secuencia
  And el CAE tiene 14 dígitos
  And el comprobante queda persistido
```

```gherkin
Scenario: Factura A con IVA discriminado
  Given una empresa responsable inscripto y un receptor responsable inscripto
  When emite una factura A con alícuota 21
  Then el importe neto es la suma de los subtotales
  And el IVA se discrimina en la respuesta
  And el total es neto más IVA
```

```gherkin
Scenario: Total inconsistente
  Given un request de factura con un total que no coincide con neto más IVA
  When se procesa la emisión
  Then la API devuelve 400 con código "VALIDACION"
  And no se consume un número de secuencia
```

```gherkin
Scenario: Punto de venta ajeno
  Given una API key de la empresa A
  When intenta emitir con un punto de venta de la empresa B
  Then la API devuelve 404
  And no se crea el comprobante
```

```gherkin
Scenario: CAE único
  Given dos emisiones simultáneas
  When ambas generan CAE
  Then los CAE son distintos
  And el índice único impide duplicados
```

## Tareas técnicas

- [ ] Crear `Comprobante`, `ComprobanteItem` y enums si no existen.
- [ ] Crear `IEmitirComprobanteService` y `IComprobanteRepository`.
- [ ] Implementar validaciones de emisor, receptor, ítems e importes.
- [ ] Implementar el cálculo de neto, IVA y total por tipo.
- [ ] Integrar la asignación atómica de número de US-07.
- [ ] Generar el CAE con `CaeGenerator` y calcular el vencimiento.
- [ ] Persistir el comprobante y sus ítems en una transacción.
- [ ] Crear `POST /v1/comprobantes` con autenticación por API key.
- [ ] Registrar auditoría.
- [ ] Agregar tests de factura A, B, C, validaciones y aislamiento.
- [ ] Documentar el contrato, ejemplos y errores en OpenAPI, Postman y manual.

## Definition of Done

- [ ] Se emiten facturas A, B y C con numeración y CAE correctos.
- [ ] El comprobante se recupera por CAE.
- [ ] No se consumen números ni se persisten comprobantes ante errores.
- [ ] El aislamiento por empresa está cubierto por tests.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Notas de alcance

- No implementa notas de crédito o débito; eso corresponde a US-08.
- No implementa idempotencia; eso corresponde a US-06.
- No implementa PDF ni QR; eso corresponde a US-10.
- No llama a AFIP/ARCA.
