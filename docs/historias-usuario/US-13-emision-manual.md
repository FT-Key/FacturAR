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

## Contrato técnico

Fuente normativa: `docs/contratos-api.md` (§2 errores, §3 roles, §5 idempotencia, §7 emisión, §14 convenciones de API) y `docs/modelos.md` (entidades `Comprobante`, `PuntoVenta`, `Secuencia`). Esta sección no se aparta de esos documentos.

### Modelo de datos

US-13 **no agrega colecciones, entidades ni índices**. El portal no escribe en MongoDB: emite por la API.

| Colección | Documento | Quién la escribe |
| --- | --- | --- |
| `comprobantes` | `Comprobante` + `ComprobanteItem` | La API (US-05/US-06), con la `Idempotency-Key` que manda el portal. |
| `secuencias` | `Secuencia` | La API (US-07), dentro de la misma transacción de la emisión. |
| `puntosVenta` | `PuntoVenta` | Solo lectura para el combo de punto de venta del formulario. |

El portal **nunca** genera el número, el CAE ni el `IdempotencyKey` persistido: solo arma el request y la clave de idempotencia.

### Endpoints

| Método | Ruta | Autenticación | Body / Query | Respuesta | Errores |
| --- | --- | --- | --- | --- | --- |
| `POST` | `/v1/comprobantes` | `cookies.contribuyente` + `EmpresaEmisor` (US-04/US-05) | `EmitirComprobanteRequest` + header `Idempotency-Key` | `201` `ComprobanteResponse` | `400 VALIDACION`, `400 EMISION_INVALIDA`, `400 CUIT_INVALIDO`, `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS`, `404 COMPROBANTE_NO_ENCONTRADO` (punto de venta inexistente o ajeno), `409 PUNTO_VENTA_INACTIVO`, `409 IDEMPOTENCIA_CONFLICTO`, `503 MONGO_NO_DISPONIBLE`, `500 ERROR_INTERNO` |
| `POST` | `/v1/comprobantes/presupuesto` | `cookies.contribuyente` + `EmpresaEmisor` | `EmitirComprobanteRequest` | `200` `PresupuestoResponse` | `400 VALIDACION`, `400 EMISION_INVALIDA`, `400 CUIT_INVALIDO`, `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS` |
| `GET` | `/v1/puntos-venta` | `cookies.contribuyente` + `EmpresaEmisor` | — | `200` `PuntoVentaListResponse` | `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS`, `503 MONGO_NO_DISPONIBLE`, `500 ERROR_INTERNO` |
| `GET` | `/v1/comprobantes/{id:guid}/pdf` | `cookies.contribuyente` + `EmpresaConsulta` (US-10) | — | `200` `application/pdf` | `404 COMPROBANTE_NO_ENCONTRADO` |

- `POST /v1/comprobantes` **ya existe** (US-05) y US-13 no lo modifica: el portal lo consume con la **cookie de sesión**, que es la vía que US-05 ya declara («API key `X-Api-Key` o sesión de portal»). El `ContribuyenteId` sale del claim de la sesión; el body **no** lleva `contribuyenteId`.
- `POST /v1/comprobantes/presupuesto` es **nuevo** y es el que permite mostrar los importes antes de confirmar. Devuelve el cálculo de la API y **no persiste nada**: no inserta comprobante, no consume número, no genera CAE y no escribe eventos de auditoría.
- `GET /v1/puntos-venta` es **nuevo** y existe porque el formulario tiene que ofrecer los puntos de venta de la empresa. Devuelve solo los puntos de venta del contribuyente, con `numero`, `descripcion` y `activo`.

`PresupuestoResponse`:

```json
{
  "importeNeto": 2000.00,
  "importeIva": 420.00,
  "importeTotal": 2420.00,
  "items": [
    { "descripcion": "Servicio", "cantidad": 2, "precioUnitario": 1000, "alicuotaIva": 21, "subtotal": 2000.00 }
  ]
}
```

Los importes del presupuesto salen de `FacturAR.Application.Comprobantes.CalculadoraImportes` y de `FacturAR.Domain.Validation.ComprobanteValidator`, los mismos que usa la emisión: el portal no tiene su propia regla de cálculo.

### Casos de uso y servicios

**Application** (`FacturAR.Application`)

| Tipo | Responsabilidad |
| --- | --- |
| `Comprobantes.EmitirComprobanteService` (US-05) | Emite: valida, calcula importes, pide el número (US-07), genera el CAE, inserta y audita. US-13 no lo modifica. |
| `Comprobantes.CalcularPresupuestoService` | **Nuevo.** Valida el payload con `ComprobanteValidator` y devuelve los importes calculados con `CalculadoraImportes`. No escribe nada. |
| `Abstractions.IPuntoVentaRepository` (US-05) | Se le agrega `ListarPorContribuyenteAsync(contribuyenteId, ct)`. |

**Infrastructure** (`FacturAR.Infrastructure`)

- `Repositories/PuntoVentaRepository`: implementa el listado, filtrando por `ContribuyenteId`.
- No hay cambios en `ComprobanteRepository` ni en los índices.

**Contracts** (`FacturAR.Contracts`)

- `Comprobantes/PresupuestoResponse.cs` y `Comprobantes/PresupuestoItemResponse.cs`.
- `PuntosVenta/PuntoVentaListResponse.cs` y `PuntosVenta/PuntoVentaResumenResponse.cs` (`id`, `numero`, `descripcion`, `activo`).
- Se reutiliza `EmitirComprobanteRequest` y `ComprobanteResponse` de US-05.

**Web** (`FacturAR.Web`)

| Tipo | Responsabilidad |
| --- | --- |
| `Services/FacturarApiClient.ObtenerPuntosVentaAsync(ct)` | `GET /v1/puntos-venta`. |
| `Services/FacturarApiClient.CalcularPresupuestoAsync(request, ct)` | `POST /v1/comprobantes/presupuesto`. |
| `Services/FacturarApiClient.EmitirComprobanteAsync(request, idempotencyKey, ct)` | `POST /v1/comprobantes` con el header `Idempotency-Key`. |
| `Services/FacturarApiClient.DescargarPdfAsync(id, ct)` | `GET /v1/comprobantes/{id}/pdf` (US-10). |
| `Pages/…` → `Components/Pages/ComprobanteNuevo.razor` | Formulario de emisión. |

### Idempotencia desde el portal

- El portal genera la clave con `Guid.NewGuid().ToString("N")`: 32 caracteres hexadecimales, dentro del máximo de 128 caracteres del contrato (§5).
- La clave se genera **una vez por formulario** y se conserva mientras el payload no cambie. Un reintento con el mismo payload reutiliza la misma clave: la API devuelve el comprobante original (`200`) y no renumera ni regenera el CAE.
- Si el usuario **cambia el payload** (ítems, receptor, tipo o punto de venta), el portal genera una clave nueva. Reutilizar la clave vieja con otro payload devolvería `409 IDEMPOTENCIA_CONFLICTO`.
- La clave vive en memoria, en el componente. No se guarda en `localStorage`, ni en `sessionStorage`, ni en una cookie, ni en la URL.
- Doble clic: el botón «Emitir» se deshabilita mientras la petición está en vuelo y la clave es la misma, así que la segunda pulsación no puede crear un segundo comprobante.

### Validaciones

**En el portal (validación por campo, para dar feedback inmediato):**

| Campo | Regla | Mensaje |
| --- | --- | --- |
| Tipo de comprobante | Obligatorio. Solo `FacturaA` (1), `FacturaB` (6) y `FacturaC` (11): US-13 no emite notas. | «Elegí el tipo de comprobante.» |
| Punto de venta | Obligatorio; debe ser uno de los puntos de venta activos de la empresa. | «Elegí el punto de venta.» |
| Receptor · documento | Obligatorio. Tipo A: CUIT con dígito verificador válido. Tipo B: CUIT de consumidor final, monotributista o exento, o DNI. Tipo C: consumidor final. | «El documento del receptor no es válido para este tipo de comprobante.» |
| Receptor · razón social | Obligatoria, entre 1 y 200 caracteres. | «Ingresá la razón social del receptor.» |
| Ítem · descripción | Obligatoria, entre 1 y 500 caracteres. | «La descripción del ítem es obligatoria.» |
| Ítem · cantidad | Mayor que 0. | «La cantidad debe ser mayor que cero.» |
| Ítem · precio unitario | Mayor o igual a 0. | «El precio unitario no puede ser negativo.» |
| Ítem · alícuota IVA | Una de `0`, `5`, `10,5`, `21`, `27`. | «La alícuota de IVA no está permitida.» |
| Ítems | Entre 1 y 500. | «Agregá al menos un ítem.» |

- La validación del portal **no reemplaza** a la API: la API vuelve a validar y es la autoridad. Si aun así devuelve `400`, el portal muestra el mensaje del `ErrorResponse` como error general y **conserva todos los datos cargados**.
- El `ErrorResponse` del contrato solo tiene `codigo` y `mensaje` (§2). El detalle por campo que pide el criterio de aceptación es la validación local del portal; no se agregan campos al DTO público.
- El portal **no** manda `importeTotal`: lo calcula la API. Mandarlo con un valor distinto del calculado produce `400 EMISION_INVALIDA` (US-05).
- El `ContribuyenteId` no viaja en el body.

### UI: rutas del portal, componentes y estados

| Ruta | Archivo | Contenido |
| --- | --- | --- |
| `/comprobantes/nuevo` | `Components/Pages/ComprobanteNuevo.razor` | Formulario de emisión manual. |

Es un componente Blazor Server con `EditForm` y validación en vivo, porque el formulario tiene una grilla dinámica de ítems y un resumen que se recalcula.

| Componente | Uso |
| --- | --- |
| `Components/Shared/ComprobanteItemsEditor.razor` | Grilla de ítems: agregar, quitar y editar. Cada fila es un `InputNumber` con su mensaje de validación al lado. |
| `Components/Shared/PresupuestoResumen.razor` | Muestra neto, IVA y total devueltos por la API. |
| `Components/Shared/ConfirmDialog.razor` | Diálogo de confirmación reutilizable. |

Estados:

| Estado | Comportamiento |
| --- | --- |
| Carga | Mientras se obtienen los puntos de venta, el formulario aparece deshabilitado con indicador de carga. |
| Con datos | El resumen muestra el presupuesto vigente y el botón «Emitir» está habilitado. |
| Error de validación | Los mensajes aparecen junto al campo que falló y **no se pierde nada de lo cargado**. |
| Error de la API | Mensaje general con el código y el mensaje devueltos. Los datos se conservan. |
| Enviando | El botón «Emitir» queda deshabilitado y con texto «Emitiendo…» para evitar el doble clic. |
| Emitido | Pantalla de resultado con número, CAE, vencimiento, total y botón «Descargar PDF». |

- El resumen se recalcula llamando a `POST /v1/comprobantes/presupuesto` cuando cambian los ítems, el tipo o la alícuota. **El portal no calcula el IVA**: muestra los importes que devuelve la API.
- El botón «Descargar PDF» apunta a `GET /v1/comprobantes/{id}/pdf` y abre el comprobante recién emitido en una pestaña nueva.
- Después de emitir, el formulario se limpia y la clave de idempotencia se regenera para el próximo comprobante.
- El enlace «Emitir comprobante» del menú solo aparece para los roles `Administrador` y `Emisor`. Un rol `Consulta` que abra `/comprobantes/nuevo` recibe `403 SIN_PERMISOS` de la API y el portal muestra «Acceso denegado» con un enlace de vuelta al dashboard.

### Integración con otras historias

| Historia | Punto de integración |
| --- | --- |
| US-04 | Cookie `cookies.contribuyente`, claim `contribuyenteId` y política `EmpresaEmisor`. |
| US-05 | `POST /v1/comprobantes`, `EmitirComprobanteRequest`, `ComprobanteResponse`, `CalculadoraImportes` y `ComprobanteValidator`. |
| US-06 | Semántica de `Idempotency-Key`: reintento con el mismo payload devuelve el comprobante original; payload distinto, `409 IDEMPOTENCIA_CONFLICTO`. |
| US-07 | Numeración atómica por (contribuyente, punto de venta, tipo) dentro de la transacción de emisión. |
| US-08 | No aplica: US-13 no emite notas. |
| US-10 | Descarga del PDF del comprobante recién emitido. |
| US-12 | El dashboard y el listado enlazan a `/comprobantes/nuevo`; el estado vacío del dashboard ofrece la misma ruta. |
| US-17 | La auditoría de la emisión la escribe `EmitirComprobanteService` (US-05/US-17). US-13 no agrega eventos. |
| US-21 | Documentación del flujo en el manual y prueba manual del formulario. |

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-01 | Usuario con rol `Emisor` autenticado. | Completar una factura C con un ítem de 10000 y confirmar. | Se ve el número y el CAE generados, el vencimiento y el total 10000, y el botón «Descargar PDF» funciona. |
| CP-02 | Formulario con un ítem de cantidad 2 y precio 1000, alícuota 21. | Cambiar la alícuota a 21. | El resumen muestra neto 2000, IVA 420 y total 2420, con los valores que devuelve la API. |
| CP-03 | Formulario con cantidad 0 en el primer ítem. | Intentar emitir. | Error junto al campo cantidad, el formulario no se envía, no se consume número ni CAE y los datos cargados siguen ahí. |
| CP-04 | Formulario completo y válido. | Hacer doble clic en «Emitir». | Se envía una sola emisión: el botón se deshabilita mientras la petición está en vuelo y se muestra un solo comprobante. |
| CP-05 | Formulario con un ítem y una factura C emitida con la clave `K`. | Volver a enviar el mismo formulario sin cambios. | La API responde `200` con el comprobante original: mismo número y mismo CAE, y no se crea un comprobante nuevo. |
| CP-06 | Formulario con la clave `K` y un ítem de 10000. | Cambiar el precio a 20000 y volver a enviar. | El portal genera una clave nueva y se emite un comprobante nuevo. Si se reusara la clave anterior, la API respondería `409 IDEMPOTENCIA_CONFLICTO`. |
| CP-07 | Usuario con rol `Consulta`. | Abrir `/comprobantes/nuevo`. | El portal muestra «Acceso denegado» y no renderiza el formulario. La API responde `403 SIN_PERMISOS`. |
| CP-08 | Usuario con rol `Consulta`. | Enviar a mano un POST a `/v1/comprobantes` con la cookie de la sesión. | `403 SIN_PERMISOS`. No se emite nada. |
| CP-09 | Empresa con dos puntos de venta, uno inactivo. | Abrir `/comprobantes/nuevo`. | El combo ofrece los puntos de venta de la empresa; el inactivo no se puede elegir. |
| CP-10 | Formulario con un ítem. | Inspeccionar el request que sale del navegador. | No incluye `contribuyenteId`, ni `importeTotal`, ni el hash de ninguna API key. Incluye el header `Idempotency-Key` con 32 caracteres hexadecimales. |
| CP-11 | Cualquier emisión. | Inspeccionar el documento guardado y la respuesta de la API. | El comprobante guardado tiene `ContribuyenteId` igual al de la sesión, número correlativo y CAE de 14 dígitos. El `IdempotencyKey` guardado es el que mandó el portal. |
| CP-12 | API de presupuesto caída. | Cambiar la alícuota de un ítem. | El resumen queda en estado de error con botón «Reintentar» y el formulario conserva los datos. La emisión sigue disponible. |
| CP-13 | Formulario con un ítem de descripción vacía. | Intentar emitir. | Error junto al campo descripción y ningún request de emisión. |
| CP-14 | Comprobante emitido desde el portal. | Descargar el PDF desde la pantalla de resultado. | Se descarga el PDF del comprobante recién emitido, con el mismo número y CAE. |

## Dependencias técnicas

Antes de empezar US-13 tiene que existir:

- **US-04 implementada**: cookie `cookies.contribuyente`, claim `contribuyenteId`, política `EmpresaEmisor` y `FacturarApiClient` reenviando la cookie.
- **US-05 implementada**: `POST /v1/comprobantes`, `EmitirComprobanteRequest`, `ComprobanteResponse`, `CalculadoraImportes` y `ComprobanteValidator`.
- **US-06 implementada**: semántica de `Idempotency-Key` (mismo payload devuelve el original, payload distinto devuelve `409 IDEMPOTENCIA_CONFLICTO`).
- **US-07 implementada**: `secuencias` y numeración atómica dentro de la transacción de emisión.
- **US-10 implementada**: `GET /v1/comprobantes/{id:guid}/pdf`.
- **US-09 y US-12**: el listado y el detalle ya exponen los mismos DTOs que usa la pantalla de resultado.
- **Application**: `CalcularPresupuestoService`; `IPuntoVentaRepository.ListarPorContribuyenteAsync`. `FacturAR.Application` no referencia `MongoDB.Driver`.
- **Infrastructure**: el listado de puntos de venta en `PuntoVentaRepository`.
- **Contracts**: `PresupuestoResponse`, `PresupuestoItemResponse`, `PuntoVentaListResponse` y `PuntoVentaResumenResponse`.
- **Api**: los endpoints `/v1/comprobantes/presupuesto` y `/v1/puntos-venta`, con la política `EmpresaEmisor`.
- **Web**: `Components/Pages/ComprobanteNuevo.razor`, `Components/Shared/ComprobanteItemsEditor.razor`, `Components/Shared/PresupuestoResumen.razor` y los métodos nuevos de `FacturarApiClient`.
- **Tests**: `FacturAR.UnitTests` para la validación por campo y la regla de la clave de idempotencia; `FacturAR.IntegrationTests` con `MongoFact` para la emisión, el doble clic, el reintento y el aislamiento; prueba manual del formulario.
- **Documentación**: `docs/manual-integracion.md` (flujo de emisión manual) y la colección Postman con los endpoints nuevos.

## Notas de alcance

- No implementa emisión de notas; eso corresponde a US-08.
- No implementa plantillas de factura recurrentes.
- No implementa carga masiva.
