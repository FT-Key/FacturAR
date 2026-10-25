# US-12 — Dashboard y listado de comprobantes

**Épica:** Portal  
**Prioridad:** Media  
**Estimación:** 2 días  
**Requisito:** RF-13  
**Depende de:** US-04, US-09, US-10  
**Habilita:** US-21

## Historia

Como usuario de una empresa, quiero ver un dashboard con el estado de mi facturación y un listado filtrable de comprobantes para hacer seguimiento y descargar documentos.

## Contexto

El dashboard y el listado solo muestran datos del contribuyente autenticado. La descarga de PDF usa US-10.

## Criterios de aceptación

- [ ] El dashboard muestra cantidad de comprobantes emitidos, importe total del mes, último CAE y último número autorizado.
- [ ] El dashboard muestra los últimos cinco comprobantes.
- [ ] El listado permite filtrar por fecha, tipo, punto de venta, estado y receptor.
- [ ] El listado tiene paginación y conserva los filtros.
- [ ] Cada fila muestra tipo, número, fecha, receptor, importe y estado.
- [ ] Se puede abrir el detalle de un comprobante.
- [ ] Se puede descargar el PDF de un comprobante propio.
- [ ] Un usuario no puede ver comprobantes de otra empresa cambiando filtros o ids.
- [ ] El estado vacío explica cómo emitir el primer comprobante.
- [ ] La interfaz es responsive y accesible por teclado.
- [ ] Los importes se muestran con formato argentino.
- [ ] Las fechas se muestran en zona local, pero se envían en UTC.
- [ ] Hay tests de endpoints y una prueba manual del portal.

## Escenarios Gherkin

```gherkin
Scenario: Dashboard con comprobantes
  Given una empresa con comprobantes emitidos en el mes
  When el usuario abre el dashboard
  Then ve la cantidad y el importe total del mes
  And ve el último CAE y los últimos comprobantes
```

```gherkin
Scenario: Dashboard vacío
  Given una empresa sin comprobantes
  When el usuario abre el dashboard
  Then ve un estado vacío con un enlace para emitir
```

```gherkin
Scenario: Filtrar listado
  Given una empresa con facturas A y C
  When el usuario filtra por tipo C
  Then solo ve las facturas C
  And la paginación conserva el filtro
```

```gherkin
Scenario: Descargar PDF desde el listado
  Given un comprobante propio en el listado
  When el usuario descarga su PDF
  Then recibe el archivo del comprobante correcto
```

```gherkin
Scenario: Aislamiento
  Given un usuario de la empresa A
  When intenta abrir el detalle de un comprobante de la empresa B
  Then el portal muestra "No encontrado"
  And no revela datos de la empresa B
```

## Tareas técnicas

- [ ] Crear el endpoint o servicio de métricas del dashboard.
- [ ] Crear la página del dashboard en el portal.
- [ ] Crear la página de listado con filtros y paginación.
- [ ] Integrar el detalle y la descarga de PDF.
- [ ] Aplicar el contexto de tenant.
- [ ] Formatear importes y fechas.
- [ ] Agregar estados de carga, vacío y error.
- [ ] Agregar tests de métricas y filtros.
- [ ] Probar el flujo en desktop y mobile.

## Definition of Done

- [ ] El dashboard muestra datos correctos y aislados por empresa.
- [ ] El listado filtra, pagina y descarga PDF.
- [ ] El portal es usable en desktop y mobile.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Notas de alcance

- No implementa gráficos históricos avanzados.
- No implementa exportación CSV o Excel.
- No implementa edición o anulación de comprobantes.
