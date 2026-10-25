# US-07 — Numeración atómica

**Épica:** Emisión  
**Prioridad:** Alta  
**Estimación:** 1 día  
**Requisito:** RF-06  
**Depende de:** US-04  
**Habilita:** US-05, US-08, US-18

## Historia

Como usuario emisor, quiero que cada comprobante reciba un número correlativo sin duplicados para cumplir con la numeración fiscal simulada.

## Contexto

La secuencia se lleva por contribuyente, punto de venta y tipo de comprobante. La asignación usa una operación atómica y participa de la misma transacción que la inserción del comprobante.

## Criterios de aceptación

- [ ] La secuencia se identifica por `ContribuyenteId`, `PuntoVenta` y `TipoComprobante`.
- [ ] El número se obtiene con `findOneAndUpdate` y `$inc`.
- [ ] La asignación del número y la inserción del comprobante ocurren en la misma transacción.
- [ ] Dos emisiones simultáneas obtienen números distintos.
- [ ] Si la transacción falla, el número no queda consumido.
- [ ] El primer número emitido es `1`.
- [ ] El número es correlativo y no se reutiliza.
- [ ] La secuencia no se comparte entre contribuyentes, puntos de venta ni tipos.
- [ ] El índice único de numeración impide duplicados.
- [ ] La consulta de último número autorizado refleja el último número persistido.
- [ ] Hay tests de concurrencia y rollback.

## Escenarios Gherkin

```gherkin
Scenario: Primera emisión
  Given una secuencia inexistente para el punto de venta 1 y factura C
  When se emite el primer comprobante
  Then el número asignado es 1
  And la secuencia queda en 1
```

```gherkin
Scenario: Emisiones concurrentes
  Given dos emisiones simultáneas para la misma secuencia
  When ambas solicitan número
  Then obtienen números distintos y consecutivos
  And no se produce un duplicado
```

```gherkin
Scenario: Rollback
  Given una emisión que falla después de asignar el número
  When la transacción se revierte
  Then el número no queda consumido
  And la siguiente emisión puede usar ese número
```

```gherkin
Scenario: Secuencias independientes
  Given dos puntos de venta del mismo contribuyente
  When se emite un comprobante en cada uno
  Then cada punto de venta tiene su propia secuencia
```

## Tareas técnicas

- [ ] Crear `Secuencia` y `ISecuenciaRepository`.
- [ ] Implementar `ObtenerSiguienteNumeroAsync`.
- [ ] Configurar transacciones de MongoDB.
- [ ] Crear el índice único de secuencias.
- [ ] Integrar la asignación en `EmitirComprobanteService`.
- [ ] Agregar tests de concurrencia y rollback.
- [ ] Documentar la regla de numeración.

## Definition of Done

- [ ] No existen números duplicados en `comprobantes`.
- [ ] La prueba de concurrencia pasa de forma repetida.
- [ ] Un fallo no consume números.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el modelo.

## Notas de alcance

- No implementa reinicio anual de secuencias.
- No implementa numeración por sucursal fuera del punto de venta.
- No implementa autorización de números por AFIP.
