# US-13 — Emisión manual desde el portal

**Épica:** Portal  
**Prioridad:** Media  
**Estimación:** 2 días  
**Requisito:** RF-12  
**Depende de:** US-04, US-05, US-10  
**Habilita:** US-21

## Historia

Como usuario emisor, quiero emitir una factura desde el portal completando un formulario para generar el comprobante y descargar su PDF sin escribir código.

## Contexto

La emisión manual usa el mismo caso de uso que la API. El `ContribuyenteId` se toma de la sesión. El formulario debe calcular los importes antes de confirmar.

## Criterios de aceptación

- [ ] Solo usuarios con rol `Administrador` o `Emisor` pueden emitir.
- [ ] El formulario permite elegir punto de venta y tipo de comprobante.
- [ ] El formulario permite cargar receptor y varios ítems.
- [ ] El total se calcula en vivo y se muestra antes de confirmar.
- [ ] Se valida que los ítems tengan cantidad mayor que cero y precio no negativo.
- [ ] Se valida la condición IVA del receptor según el tipo de comprobante.
- [ ] Al confirmar se llama al mismo caso de uso de emisión de la API.
- [ ] Se usa una clave de idempotencia generada por el portal para evitar dobles envíos.
- [ ] La respuesta muestra número, CAE, vencimiento y total.
- [ ] Se ofrece descargar el PDF.
- [ ] Un error de validación se muestra por campo y no pierde los datos cargados.
- [ ] Un usuario no puede emitir para otra empresa.
- [ ] La emisión queda auditada.
- [ ] Hay tests del caso de uso y una prueba manual del formulario.

## Escenarios Gherkin

```gherkin
Scenario: Emitir factura C desde el portal
  Given un usuario emisor autenticado
  When completa una factura C con un ítem de 10000
  And confirma la emisión
  Then ve el número y el CAE generados
  And puede descargar el PDF
```

```gherkin
Scenario: Cálculo de total
  Given un ítem con cantidad 2 y precio 1000
  When el usuario cambia la alícuota a 21
  Then el formulario muestra neto 2000, IVA 420 y total 2420
```

```gherkin
Scenario: Error de validación
  Given un formulario con cantidad 0
  When el usuario intenta emitir
  Then ve el error junto al campo cantidad
  And no se consume un número
```

```gherkin
Scenario: Doble clic
  Given un formulario completo
  When el usuario hace doble clic en emitir
  Then se envía una sola emisión
  And se muestra un solo comprobante
```

```gherkin
Scenario: Usuario Consulta
  Given un usuario con rol "Consulta"
  When intenta abrir el formulario de emisión
  Then el portal le muestra acceso denegado
```

## Tareas técnicas

- [ ] Crear la página de emisión manual.
- [ ] Crear el formulario de ítems y el cálculo de totales.
- [ ] Integrar el caso de uso de emisión.
- [ ] Generar y enviar `Idempotency-Key`.
- [ ] Mostrar resultado y descarga de PDF.
- [ ] Aplicar autorización por rol.
- [ ] Agregar validaciones y mensajes de error.
- [ ] Agregar tests del flujo y prueba manual.
- [ ] Documentar el flujo en el manual.

## Definition of Done

- [ ] Se emite una factura desde el portal y se descarga el PDF.
- [ ] Los cálculos y validaciones coinciden con la API.
- [ ] El doble envío no duplica comprobantes.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Notas de alcance

- No implementa emisión de notas; eso corresponde a US-08.
- No implementa plantillas de factura recurrentes.
- No implementa carga masiva.
