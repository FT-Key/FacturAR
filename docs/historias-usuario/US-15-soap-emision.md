# US-15 — SOAP de emisión

**Épica:** Interoperabilidad  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RF-15  
**Depende de:** US-03, US-05  
**Habilita:** US-16, US-21, US-24

## Historia

Como sistema cliente legado, quiero invocar `FECAESolicitar` por SOAP para emitir comprobantes usando un contrato compatible con el flujo de AFIP.

## Contexto

El SOAP es una fachada del mismo caso de uso de emisión. No duplica reglas ni accede a Mongo directamente. La autenticación usa la misma API key o un mecanismo equivalente definido en el contrato.

## Criterios de aceptación

- [ ] Se expone `FECAESolicitar` con un contrato XML compatible con el flujo AFIP simulado.
- [ ] El request incluye `Auth` (token y sign) o la API key según el contrato acordado.
- [ ] El servicio resuelve el contribuyente autenticado.
- [ ] El servicio reutiliza `EmitirComprobanteService`.
- [ ] La respuesta incluye `Cae`, `CaeFchVto`, `Resultado` y observaciones.
- [ ] Un error de validación devuelve un `Fault` o un resultado `R` con mensaje claro.
- [ ] Un cliente no puede emitir para otra empresa.
- [ ] La numeración y el CAE son los mismos que en REST.
- [ ] El WSDL está disponible y documentado.
- [ ] Hay tests de request exitoso, error y aislamiento.
- [ ] El SOAP no omite autenticación ni validaciones.

## Escenarios Gherkin

```gherkin
Scenario: Emisión SOAP exitosa
  Given un sistema cliente autenticado
  When invoca FECAESolicitar con una factura C válida
  Then recibe Resultado "A"
  And recibe un CAE de 14 dígitos
  And el comprobante queda persistido
```

```gherkin
Scenario: Error de validación SOAP
  Given un request con un punto de venta inexistente
  When invoca FECAESolicitar
  Then recibe un resultado "R" con el error
  And no se consume un número
```

```gherkin
Scenario: Aislamiento SOAP
  Given una API key de la empresa A
  When intenta emitir con datos de la empresa B
  Then el servicio rechaza la operación
  And no se crea el comprobante
```

## Tareas técnicas

- [ ] Definir el contrato SOAP y el WSDL.
- [ ] Configurar SoapCore o CoreWCF.
- [ ] Implementar `FECAESolicitar`.
- [ ] Resolver autenticación y tenant.
- [ ] Reutilizar el caso de uso de emisión.
- [ ] Mapear errores a `Fault` o resultado `R`.
- [ ] Publicar el WSDL.
- [ ] Agregar tests de contrato y aislamiento.
- [ ] Documentar un ejemplo de request y response.

## Definition of Done

- [ ] `FECAESolicitar` emite un comprobante válido.
- [ ] El SOAP y el REST producen el mismo resultado funcional.
- [ ] El WSDL se puede consumir desde SoapUI o Postman.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Notas de alcance

- No implementa WSAA real ni certificados AFIP.
- No implementa todos los campos de AFIP.
- No implementa SOAP de consultas; eso corresponde a US-16.
