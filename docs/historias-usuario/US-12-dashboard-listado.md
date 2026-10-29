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

## Contrato técnico

Fuente normativa: `docs/contratos-api.md` (§2 errores, §3 aislamiento, §6 paginación, §14 convenciones de API) y `docs/modelos.md` (colección `comprobantes`). Esta sección no se aparta de esos documentos.

### Modelo de datos

US-12 **no agrega colecciones, entidades ni índices**. El portal no lee MongoDB: consume la API.

| Colección | Documento | Quién la lee |
| --- | --- | --- |
| `comprobantes` | `Comprobante` + `ComprobanteItem` | La API (US-09) en nombre del portal. |
| `secuencias` | `Secuencia` | La API (US-07/US-09) para el último número autorizado. |

El único cálculo nuevo es la métrica del dashboard, que se resuelve **en la API** con una agregación de MongoDB. El repositorio expone un método que devuelve un valor plano, de modo que Application no conoce el driver:

| Tipo | Capa | Responsabilidad |
| --- | --- | --- |
| `Comprobantes.MetricasDashboard` (record) | Application | `CantidadComprobantes`, `ImporteTotal`. |
| `Abstractions.IComprobanteRepository.ObtenerMetricasAsync(contribuyenteId, desde, hasta, ct)` | Application | Declara el método; devuelve `MetricasDashboard`. |
| `Repositories/ComprobanteRepository` | Infrastructure | Arma el `$match` (tenant + `Estado = Emitido` + rango de fechas) y el `$group` (`$sum` de `ImporteTotal`, `$count`). |

El portal nunca ve `Decimal128` ni documentos de Mongo: recibe números decimales del contrato.

### Endpoints

US-12 **agrega un endpoint** y reutiliza los de US-09.

| Método | Ruta | Autenticación | Query | Respuesta | Errores |
| --- | --- | --- | --- | --- | --- |
| `GET` | `/v1/comprobantes/dashboard` | API key (US-03) o `cookies.contribuyente` + `EmpresaConsulta` (US-04) | `mes` (`YYYY-MM`, opcional; default: mes en UTC) | `200` `DashboardResponse` | `400 VALIDACION`, `401 API_KEY_AUSENTE`, `401 API_KEY_INVALIDA`, `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS`, `503 MONGO_NO_DISPONIBLE`, `500 ERROR_INTERNO` |

`DashboardResponse`:

```json
{
  "periodo": { "desde": "2026-10-01T00:00:00Z", "hasta": "2026-11-01T00:00:00Z" },
  "cantidadComprobantes": 12,
  "importeTotal": 150000.00,
  "ultimoCae": "12345678901234",
  "ultimoNumeroAutorizado": 42,
  "ultimosComprobantes": [
    {
      "id": "6ae24e50-1c3e-4f0a-9f3e-9d2f0b7a1c22",
      "tipoComprobante": 11,
      "tipoComprobanteDescripcion": "Factura C",
      "puntoVenta": 1,
      "numero": 42,
      "fechaEmision": "2026-10-25T14:30:00Z",
      "cae": "12345678901234",
      "receptorRazonSocial": "Cliente Final",
      "receptorDocumentoNumero": "20345678901",
      "importeTotal": 121000.00,
      "moneda": "ARS",
      "estado": "Emitido"
    }
  ]
}
```

- El periodo es `[desde, hasta)` en UTC: `mes=2026-10` va del 1 de octubre a las 00:00:00Z del 1 de noviembre.
- `cantidadComprobantes` e `importeTotal` cuentan **solo** comprobantes en estado `Emitido`: los anulados, rechazados y borradores quedan fuera.
- `ultimoCae` es el CAE del comprobante emitido más reciente; `null` si la empresa no tiene comprobantes.
- `ultimoNumeroAutorizado` es el máximo `UltimoNumero` de las secuencias de la empresa (agregado, sin punto de venta ni tipo).
- `ultimosComprobantes` son los 5 más recientes por `FechaEmision` descendente, con la misma forma de `ComprobanteResumenResponse`.

Endpoints reutilizados sin cambios:

| Método | Ruta | Uso en el portal |
| --- | --- | --- |
| `GET` | `/v1/comprobantes` | Listado con filtros y paginación. |
| `GET` | `/v1/comprobantes/{id:guid}` | Detalle del comprobante. |
| `GET` | `/v1/comprobantes/{id:guid}/pdf` | Descarga del PDF (US-10). |

La ruta `/v1/comprobantes/dashboard` convive con `/v1/comprobantes/{id:guid}` porque el id tiene restricción `:guid`: el segmento literal tiene prioridad y no hay ambigüedad.

### Casos de uso y servicios

**Api** (`FacturAR.Api`)

| Tipo | Responsabilidad |
| --- | --- |
| `Comprobantes/DashboardEndpoint.cs` | Resuelve el tenant con `ITenantContext`, valida `mes` y arma la respuesta con `ObtenerUltimoAutorizadoService` (US-09) y la métrica agregada. |
| `Comprobantes/ComprobanteConsultaEndpoints.cs` (US-09) | Listado y detalle que consume el portal. |

**Contracts** (`FacturAR.Contracts`)

- `Comprobantes/DashboardResponse.cs`, con `PeriodoDashboard` (`Desde`, `Hasta`) y la lista de últimos comprobantes.
- Se reutilizan `PaginaResponse<T>`, `ComprobanteResumenResponse` y `ComprobanteDetalleResponse` de US-09.

**Web** (`FacturAR.Web`)

| Tipo | Responsabilidad |
| --- | --- |
| `Services/FacturarApiClient.ListarComprobantesAsync(filtro, ct)` | `GET /v1/comprobantes` con los filtros como query string. |
| `Services/FacturarApiClient.ObtenerComprobanteAsync(id, ct)` | `GET /v1/comprobantes/{id}`. |
| `Services/FacturarApiClient.ObtenerDashboardAsync(mes, ct)` | `GET /v1/comprobantes/dashboard`. |
| `Services/FacturarApiClient.DescargarPdfAsync(id, ct)` | `GET /v1/comprobantes/{id}/pdf`; devuelve el `byte[]` para mostrarlo o descargarlo. |
| `Services/ComprobanteFiltros` | Value object con los filtros del listado (`Desde`, `Hasta`, `Tipo`, `Estado`, `PuntoVenta`, `Receptor`, `Page`, `PageSize`). Vive en `FacturAR.Web`, no en `Contracts`. |

El `FacturarApiClient` reenvía el header `Cookie` de la petición entrante, como ya hace US-04, y usa el `CancellationToken` del circuito en todas las llamadas. No se guarda ninguna API key en el portal.

### UI: rutas del portal, componentes y estados

Las pantallas autenticadas de US-12 son **componentes Blazor Server** en `Components/Pages/`, porque necesitan estado de carga, reintento y navegación asíncrona sin recargar la página. `Pages/` queda para las pantallas públicas server-rendered (`/verificar` ya existe; `/login`, `/solicitud`, `/activar` y `/admin/login` las agregan US-04, US-01 y US-28).

| Ruta | Archivo | Contenido |
| --- | --- | --- |
| `/` | `Components/Pages/Dashboard.razor` | Métricas del mes, último CAE, último número autorizado y los últimos cinco comprobantes. |
| `/comprobantes` | `Components/Pages/Comprobantes.razor` | Listado con filtros, paginación y descarga de PDF. |
| `/comprobantes/{id:guid}` | `Components/Pages/ComprobanteDetalle.razor` | Detalle con ítems, importes y descarga de PDF. |

**Colisión de ruta en `/`.** Hoy `Components/Pages/Home.razor` declara `@page "/"` (landing público). Al convertir `/` en el dashboard, ese archivo pasa a `@page "/inicio"`: se conservan el contenido y el enlace a `/verificar`, y `README.md` se actualiza en el mismo cambio. Sin ese traslado habría dos rutas para `/` y ASP.NET Core respondería con una ruta ambigua. El `NavMenu.razor` suma las entradas «Dashboard», «Comprobantes» y «API keys» (US-14), y «Inicio» apunta a `/inicio`.

Componentes compartidos en `Components/Shared/`:

| Componente | Uso |
| --- | --- |
| `TarjetaMetrica.razor` | Cada indicador del dashboard (cantidad, importe total, último CAE, último número). |
| `FilaComprobante.razor` | Fila del listado, con `@key` por id. |
| `EstadoComprobanteBadge.razor` | Distintivo de estado. No depende **solo** del color: lleva el texto del estado. |
| `ImporteTexto.razor` | Formatea importes con `ToString("N2", CultureInfo.GetCultureInfo("es-AR"))`. |
| `Paginador.razor` | Anterior / Siguiente con la cantidad de páginas y el total. |
| `EstadoVacio.razor` | Estado vacío con título, explicación y acción primaria. |
| `MensajeError.razor` | Error con botón «Reintentar». |

Estados obligatorios de cada pantalla:

| Estado | Comportamiento |
| --- | --- |
| Carga | Indicador visual con `aria-busy="true"` y `role="status"`; los filtros y el paginador quedan deshabilitados mientras la consulta está en vuelo. |
| Con datos | Listado o detalle renderizado. |
| Vacío | `EstadoVacio` con el motivo: en el dashboard, « todavía no emitiste comprobantes» con enlace a `/comprobantes/nuevo` (US-13); en el listado, «no hay comprobantes para estos filtros» con acción para limpiar los filtros. |
| Error | `MensajeError` con el mensaje devuelto por la API y botón «Reintentar». Nunca un mensaje genérico sin salida. |

Formato y accesibilidad:

- **Importes**: `valor.ToString("N2", CultureInfo.GetCultureInfo("es-AR"))`. El separador de miles es `.` y el decimal es `,`. La API devuelve el número crudo; el formateo es del portal.
- **Fechas**: se muestran en la zona local del navegador (`ToLocalTime()`) y viajan a la API en UTC.
- **Responsive**: grilla de Bootstrap; la tabla de ítems del detalle va dentro de un contenedor `table-responsive`.
- **Teclado**: todos los controles son alcanzables con Tab, el foco es visible, los botones de icono llevan `aria-label` y los enlaces de navegación usan `aria-current` cuando están activos.
- Sin JavaScript pesado: la interacción es Blazor Server.

### Validaciones

- Los filtros del listado se envían como query string: `desde`, `hasta`, `tipo`, `estado`, `puntoVenta`, `receptor`, `page`, `pageSize`.
- `pageSize` del portal es 20 y **nunca supera 100**: un valor mayor se recorta a 100 antes de llamar a la API.
- El portal valida el formato de las fechas antes de llamar a la API; si el formato es inválido muestra el error en el filtro y no dispara la consulta.
- Un `404` del detalle (por ejemplo, un id de otra empresa) se muestra como «No encontrado». No se distingue de un comprobante inexistente y no se muestran datos de la otra empresa.
- Un `401` redirige a `/login`; un `403` muestra «Acceso denegado» y oculta las acciones que el rol no permite.
- El portal **nunca** envía `contribuyenteId`: no viaja en el body, ni en el query string, ni en un header propio. La API lo resuelve del contexto.

### Paginación y filtros

- Los filtros se conservan al cambiar de página: el cambio de `page` no borra `desde`, `hasta`, `tipo`, `estado`, `puntoVenta` ni `receptor`.
- El botón «Siguiente» se deshabilita cuando `page >= totalPages`; el de «Anterior», cuando `page <= 1`.
- Con `total = 0`, el paginador no se muestra y se muestra el estado vacío.
- El listado pide **solo la página actual**: no se descarga el total de comprobantes para paginar en el cliente.

### Integración con otras historias

| Historia | Punto de integración |
| --- | --- |
| US-04 | Cookie `cookies.contribuyente`, `ITenantContext`, claim `contribuyenteId` y reenvío de la cookie en `FacturarApiClient`. |
| US-09 | Endpoints de listado, detalle y filtros; `PaginaResponse<T>` y `ComprobanteResumenResponse`. |
| US-10 | Descarga del PDF desde el listado y desde el detalle. |
| US-13 | El estado vacío del dashboard enlaza a `/comprobantes/nuevo` y el menú muestra «Emitir comprobante» a los roles `Administrador` y `Emisor`. |
| US-14 | El menú muestra «API keys» solo a los `Administrador`. |
| US-11 | La verificación pública sigue en `/verificar` y no se toca desde el portal autenticado. |
| US-21 | Pruebas de los endpoints del portal y prueba manual registrada. |

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-01 | Empresa con 12 comprobantes `Emitido` en el mes en curso. | Abrir `/`. | Se ve la cantidad (12), el importe total del mes, el último CAE y el último número autorizado, más los últimos cinco comprobantes. |
| CP-02 | Empresa sin comprobantes. | Abrir `/`. | Estado vacío con la explicación y un enlace para emitir el primer comprobante. No es un error ni una pantalla en blanco. |
| CP-03 | Empresa con comprobantes de otra empresa también cargados. | Abrir `/` y luego `/comprobantes`. | Solo aparecen comprobantes de la empresa propia. El total del dashboard y el del listado no cuentan los ajenos. |
| CP-04 | Empresa con facturas A y C. | En `/comprobantes`, filtrar por tipo C y avanzar a la página 2. | Solo se ven facturas C. La página 2 conserva el filtro y el paginador muestra el total de facturas C. |
| CP-05 | Un comprobante propio en el listado. | Pulsar «Descargar PDF» en su fila. | Se descarga el PDF del comprobante correcto (mismo número y CAE). |
| CP-06 | Comprobante de la empresa B. | Abrir `/comprobantes/{id-de-B}` con la sesión de A. | La pantalla muestra «No encontrado». No se revela ningún dato de la empresa B. |
| CP-07 | Comprobante propio. | Abrir `/comprobantes/{id}` y revisar los importes. | Los importes se muestran con formato argentino: punto para miles y coma para los decimales, con dos decimales (`121.000,00`). |
| CP-08 | Cualquier listado. | Cambiar el filtro de fecha y la página, y recargar con F5. | El filtro y la página se conservan: la URL los mantiene como query string. |
| CP-09 | API sin respuesta. | Abrir `/comprobantes` con la API caída. | Estado de error con mensaje accionable y botón «Reintentar». No queda la pantalla en carga infinita. |
| CP-10 | API lenta. | Abrir `/comprobantes`. | While loading se ve el indicador de carga con `aria-busy`; el paginador y los filtros están deshabilitados hasta que llegue la respuesta. |
| CP-11 | Cualquier pantalla autenticada. | Recorrer la página solo con el teclado. | Todos los controles son alcanzables, el foco es visible y los botones de icono tienen nombre accesible. |
| CP-12 | Pantalla de 375 px de ancho. | Abrir `/` y `/comprobantes`. | El contenido se reacomoda sin scroll horizontal ni solapamientos, y los importes siguen legibles. |
| CP-13 | Empresa con comprobantes en el mes anterior y en el actual. | Abrir `/` y comparar el importe del mes con el listado filtrado por el mes actual. | El importe del dashboard coincide con la suma del listado filtrado. |
| CP-14 | Rol `Consulta`. | Abrir `/`. | El dashboard se muestra. Las acciones de emisión no aparecen en el menú. |
| CP-15 | `GET /v1/comprobantes/dashboard` con API key de A y comprobantes de A y B. | Consultar el endpoint. | `200`. `cantidadComprobantes` e `importeTotal` cuentan solo los de A. |
| CP-16 | `GET /v1/comprobantes/dashboard?mes=2026-10`. | Consultar el endpoint. | `periodo` va de `2026-10-01T00:00:00Z` a `2026-11-01T00:00:00Z` y solo cuenta comprobantes de octubre. Un mes con formato inválido responde `400 VALIDACION`. |

## Dependencias técnicas

Antes de empezar US-12 tiene que existir:

- **US-04 implementada**: cookie `cookies.contribuyente`, `ITenantContext`, reenvío del header `Cookie` en `FacturarApiClient`, páginas `/login` y `/logout`, y `CascadingAuthenticationState` en `Routes.razor` para proteger las rutas del portal.
- **US-09 implementada**: `GET /v1/comprobantes`, `GET /v1/comprobantes/{id:guid}`, `GET /v1/comprobantes/ultimo-autorizado`, `PaginaResponse<T>`, `ComprobanteResumenResponse` y `ComprobanteDetalleResponse`.
- **US-10 implementada**: `GET /v1/comprobantes/{id:guid}/pdf`.
- **US-05 y US-07 implementadas**: la colección `comprobantes` y la colección `secuencias` con `UltimoNumero`.
- **Application**: `MetricasDashboard` y `IComprobanteRepository.ObtenerMetricasAsync`. `FacturAR.Application` no referencia `MongoDB.Driver`: la agregación se arma en Infrastructure.
- **Infrastructure**: la agregación de métricas en `ComprobanteRepository`.
- **Api**: el endpoint `/v1/comprobantes/dashboard` con la política que acepta API key y cookie de empresa.
- **Contracts**: `DashboardResponse` y `PeriodoDashboard`.
- **Web**: los componentes `Components/Pages/Dashboard.razor`, `Comprobantes.razor` y `ComprobanteDetalle.razor`; los componentes compartidos de `Components/Shared/`; los métodos nuevos de `FacturarApiClient`; y el `NavMenu.razor` con las entradas nuevas.
- **Tests**: `FacturAR.IntegrationTests` con `MongoFact` para las métricas y los filtros del listado; prueba manual del portal en escritorio y en móvil, registrada en la historia.
- **Documentación**: `README.md` (la ruta `/` pasa a ser el dashboard y la landing queda en `/inicio`), `docs/manual-integracion.md` y la colección Postman.

## Notas de alcance

- No implementa gráficos históricos avanzados.
- No implementa exportación CSV o Excel.
- No implementa edición o anulación de comprobantes.
