# US-16 — SOAP de consultas

**Épica:** Interoperabilidad  
**Prioridad:** Media  
**Estimación:** 1 día  
**Requisito:** RF-16  
**Depende de:** US-03, US-09  
**Habilita:** US-21

## Historia

Como sistema cliente legado, quiero consultar el último comprobante autorizado, obtener un comprobante y verificar el estado del servicio por SOAP para integrarme sin usar REST.

## Contexto

El SOAP de consultas reutiliza los casos de uso de US-09 y el contexto de tenant. `FEDummy` es un health check del servicio simulado.

## Criterios de aceptación

- [ ] Se expone `FECompUltimoAutorizado` con punto de venta y tipo de comprobante.
- [ ] Se expone `FECompConsultar` por tipo, punto de venta y número.
- [ ] Se expone `FEDummy` con estado del servicio.
- [ ] Las consultas se filtran por el contribuyente autenticado.
- [ ] Un comprobante de otra empresa no se devuelve.
- [ ] El último autorizado devuelve `0` si no hay comprobantes.
- [ ] Los errores se devuelven como `Fault` o resultado controlado.
- [ ] El WSDL incluye las tres operaciones.
- [ ] Hay tests de consulta, aislamiento y `FEDummy`.
- [ ] El contrato está documentado con ejemplos.

## Escenarios Gherkin

```gherkin
Scenario: Último autorizado
  Given una empresa con última factura C número 42
  When invoca FECompUltimoAutorizado para factura C
  Then recibe 42
```

```gherkin
Scenario: Consultar comprobante
  Given un comprobante propio número 42
  When invoca FECompConsultar
  Then recibe los datos fiscales del comprobante
```

```gherkin
Scenario: Comprobante ajeno
  Given un comprobante de la empresa B
  When la empresa A lo consulta por SOAP
  Then recibe un error controlado sin datos
```

```gherkin
Scenario: FEDummy
  Given el servicio está operativo
  When invoca FEDummy
  Then recibe estado "OK"
```

## Tareas técnicas

- [ ] Definir las operaciones SOAP y el WSDL.
- [ ] Implementar `FECompUltimoAutorizado`.
- [ ] Implementar `FECompConsultar`.
- [ ] Implementar `FEDummy`.
- [ ] Resolver autenticación y tenant.
- [ ] Reutilizar los casos de uso de consulta.
- [ ] Mapear errores.
- [ ] Agregar tests de contrato y aislamiento.
- [ ] Documentar ejemplos.

## Definition of Done

- [ ] Las tres operaciones responden según contrato.
- [ ] Las consultas están aisladas por empresa.
- [ ] El WSDL se puede consumir.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Notas de alcance

- No implementa todos los campos de AFIP.
- No implementa WSAA real.
- No implementa consultas masivas.
