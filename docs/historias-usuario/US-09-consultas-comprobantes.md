# US-09 — Consultas y último autorizado

**Épica:** Consultas  
**Prioridad:** Alta  
**Estimación:** 1 día  
**Requisito:** RF-08  
**Depende de:** US-03, US-05  
**Habilita:** US-10, US-12, US-16, US-21

## Historia

Como usuario o sistema cliente, quiero consultar comprobantes, ver su detalle y conocer el último número autorizado para operar y conciliar mi facturación.

## Contexto

Todas las consultas se filtran por el `ContribuyenteId` autenticado. La verificación pública por CAE es un endpoint separado y anónimo.

## Criterios de aceptación

- [ ] `GET /v1/comprobantes` lista solo comprobantes del contribuyente autenticado.
- [ ] Se puede filtrar por fecha desde/hasta, punto de venta, tipo de comprobante, estado y receptor.
- [ ] La lista usa paginación con `page` y `pageSize`, con límite máximo.
- [ ] La lista se ordena por fecha de emisión descendente y número descendente.
- [ ] `GET /v1/comprobantes/{id}` devuelve el detalle con ítems.
- [ ] Un id de otra empresa devuelve `404`.
- [ ] `GET /v1/comprobantes/ultimo-autorizado` devuelve el último número por punto de venta y tipo.
- [ ] Si no hay comprobantes, el último autorizado es `0`.
- [ ] Las fechas se interpretan en UTC y se documentan.
- [ ] La respuesta no expone datos internos, hashes ni API keys.
- [ ] La consulta usa índices por contribuyente y fecha.
- [ ] Hay tests de filtros, paginación, aislamiento y último autorizado.

## Escenarios Gherkin

```gherkin
Scenario: Listar comprobantes
  Given una empresa con tres comprobantes emitidos
  When consulta la lista sin filtros
  Then recibe los tres comprobantes de su empresa
  And no recibe comprobantes de otras empresas
```

```gherkin
Scenario: Filtrar por fecha
  Given comprobantes emitidos en octubre y noviembre
  When consulta con desde 2026-11-01 y hasta 2026-11-30
  Then recibe solo los de noviembre
```

```gherkin
Scenario: Detalle de otra empresa
  Given un comprobante de la empresa B
  When un usuario de la empresa A solicita su detalle
  Then la API devuelve 404
```

```gherkin
Scenario: Último autorizado
  Given el último comprobante de factura C tiene número 42
  When consulta el último autorizado para factura C
  Then la API devuelve 42
```

```gherkin
Scenario: Sin comprobantes
  Given una empresa sin comprobantes de nota de débito A
  When consulta el último autorizado para ese tipo
  Then la API devuelve 0
```

## Tareas técnicas

- [ ] Crear DTOs de listado, detalle y último autorizado.
- [ ] Implementar `ListarComprobantesService` y `ConsultarComprobanteService`.
- [ ] Implementar `ObtenerUltimoAutorizadoService`.
- [ ] Crear `GET /v1/comprobantes`, `GET /v1/comprobantes/{id}` y `GET /v1/comprobantes/ultimo-autorizado`.
- [ ] Aplicar filtro de tenant y paginación.
- [ ] Agregar índices de consulta.
- [ ] Agregar tests de filtros, aislamiento y paginación.
- [ ] Documentar parámetros y respuestas en OpenAPI y Postman.

## Definition of Done

- [ ] Un cliente solo ve sus comprobantes.
- [ ] La paginación y los filtros funcionan.
- [ ] El último autorizado refleja la secuencia persistida.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Notas de alcance

- No implementa exportación CSV o Excel; eso corresponde a US-12.
- No implementa búsqueda de texto completo.
- No implementa consultas SOAP; eso corresponde a US-16.
