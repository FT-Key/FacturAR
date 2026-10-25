# US-25 — gRPC o GraphQL

**Épica:** Interoperabilidad  
**Prioridad:** Baja (stretch)  
**Estimación:** 3 días  
**Requisito:** RNF-15  
**Depende de:** US-05, US-09  
**Estado:** Stretch

## Historia

Como sistema cliente moderno, quiero una interfaz gRPC o GraphQL para emitir y consultar comprobantes con un contrato tipado, sin abandonar la API REST existente.

## Contexto

Se elige una sola tecnología para evitar duplicar mantenimiento. La nueva interfaz reutiliza los casos de uso y el aislamiento por tenant.

## Criterios de aceptación

- [ ] Se elige gRPC o GraphQL y se documenta la decisión.
- [ ] La interfaz permite emitir una factura.
- [ ] La interfaz permite consultar un comprobante y el último autorizado.
- [ ] La autenticación usa API key o el mecanismo equivalente definido.
- [ ] El tenant se resuelve desde la credencial.
- [ ] Los errores respetan el contrato de la tecnología elegida.
- [ ] La interfaz reutiliza los casos de uso existentes.
- [ ] No duplica reglas de negocio.
- [ ] Hay tests de contrato y aislamiento.
- [ ] La documentación incluye ejemplos.

## Escenarios Gherkin

```gherkin
Scenario: Emitir por la nueva interfaz
  Given un cliente autenticado
  When emite una factura C
  Then recibe número y CAE
  And el comprobante es visible por REST
```

```gherkin
Scenario: Aislamiento
  Given una credencial de la empresa A
  When consulta un comprobante de la empresa B
  Then recibe un error controlado
  And no obtiene datos de B
```

```gherkin
Scenario: Paridad REST
  Given el mismo request por REST y por la nueva interfaz
  When se emiten ambos
  Then las reglas y los importes coinciden
```

## Tareas técnicas

- [ ] Elegir gRPC o GraphQL y registrar la decisión.
- [ ] Definir el esquema o los protos.
- [ ] Implementar la autenticación.
- [ ] Reutilizar los casos de uso.
- [ ] Mapear errores.
- [ ] Agregar tests de contrato y aislamiento.
- [ ] Documentar ejemplos.

## Definition of Done

- [ ] La interfaz elegida funciona para emisión y consulta.
- [ ] No duplica reglas de negocio.
- [ ] El aislamiento está cubierto.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice.

## Notas de alcance

- No se implementan ambas tecnologías.
- No se reemplaza REST.
- No se implementa suscripciones en tiempo real.
