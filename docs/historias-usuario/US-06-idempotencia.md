# US-06 — Idempotencia de emisión

**Épica:** Emisión  
**Prioridad:** Alta  
**Estimación:** 1 día  
**Requisito:** RF-05  
**Depende de:** US-05  
**Habilita:** US-21

## Historia

Como sistema cliente, quiero reintentar una emisión con la misma clave de idempotencia sin duplicar comprobantes para tolerar timeouts y fallos de red.

## Contexto

La clave se envía en el header `Idempotency-Key`. Es única por contribuyente. Si el request se repite con la misma clave y el mismo payload, se devuelve el comprobante original. Si el payload cambia, se rechaza.

## Criterios de aceptación

- [ ] La emisión acepta el header `Idempotency-Key`.
- [ ] La clave es única por contribuyente.
- [ ] Un request repetido con la misma clave y el mismo payload devuelve el comprobante original.
- [ ] No se vuelve a numerar ni a generar un CAE en un reintento idempotente.
- [ ] Un request con la misma clave y payload distinto devuelve `409`.
- [ ] Dos requests simultáneos con la misma clave generan un solo comprobante.
- [ ] La respuesta del reintento conserva el mismo `id`, `numero` y `cae`.
- [ ] La clave se guarda asociada al comprobante y al contribuyente.
- [ ] El índice único impide duplicados.
- [ ] La clave no se usa para otras operaciones.
- [ ] La respuesta documenta el comportamiento idempotente.
- [ ] Hay tests de reintento, conflicto y concurrencia.

## Escenarios Gherkin

```gherkin
Scenario: Reintento exitoso
  Given una emisión exitosa con la clave "pedido-001"
  When el cliente repite el mismo request con la misma clave
  Then la API devuelve el mismo comprobante
  And no se crea otro comprobante
  And no se consume otro número
```

```gherkin
Scenario: Conflicto de payload
  Given una emisión exitosa con la clave "pedido-001"
  When el cliente repite la clave con otro importe
  Then la API devuelve 409 con código "IDEMPOTENCIA_CONFLICTO"
  And no se modifica el comprobante original
```

```gherkin
Scenario: Reintentos concurrentes
  Given dos requests simultáneos con la misma clave
  When ambos llegan a la API
  Then solo uno crea el comprobante
  And el otro devuelve el mismo comprobante o 409 controlado
```

```gherkin
Scenario: Clave repetida en otra empresa
  Given una empresa A usó la clave "pedido-001"
  When la empresa B usa la misma clave
  Then la emisión de B se procesa como una operación nueva
```

## Tareas técnicas

- [ ] Agregar `IdempotencyKey` al comprobante y su índice único por contribuyente.
- [ ] Crear `IIdempotencyService` o integrar la lógica en `EmitirComprobanteService`.
- [ ] Guardar el hash del payload para detectar conflictos.
- [ ] Implementar la búsqueda previa y el manejo de carrera.
- [ ] Devolver el comprobante existente sin renumerar.
- [ ] Agregar tests de reintento, conflicto y concurrencia.
- [ ] Documentar el header y los errores en el manual.

## Definition of Done

- [ ] Un reintento no duplica comprobantes ni números.
- [ ] Un payload distinto con la misma clave devuelve `409`.
- [ ] La concurrencia está cubierta por tests.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Notas de alcance

- No implementa expiración de claves de idempotencia.
- No implementa idempotencia para notas, pagos ni otras operaciones.
- No reemplaza la numeración atómica de US-07.
