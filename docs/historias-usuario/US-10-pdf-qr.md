# US-10 — PDF con código QR

**Épica:** Documentos  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RF-09  
**Depende de:** US-05, US-09  
**Habilita:** US-12, US-13, US-21

## Historia

Como usuario emisor, quiero descargar un PDF con los datos fiscales y un código QR para entregar o archivar el comprobante y permitir su verificación pública.

## Contexto

El PDF se genera a partir de un comprobante persistido. El QR contiene la URL pública de verificación con el CAE. El PDF no reemplaza al comprobante ni al CAE simulado.

## Criterios de aceptación

- [ ] Se puede descargar el PDF de un comprobante propio.
- [ ] El PDF incluye emisor, receptor, tipo, punto de venta, número, fecha, CAE, vencimiento, ítems e importes.
- [ ] El PDF incluye la leyenda "Comprobante simulado sin validez fiscal".
- [ ] El QR apunta a `/verificar?cae=<CAE>` usando la URL base configurada.
- [ ] El QR se puede leer con un lector estándar.
- [ ] Un comprobante de otra empresa devuelve `404`.
- [ ] Un comprobante inexistente devuelve `404`.
- [ ] El PDF se genera en menos de 3 segundos en condiciones normales.
- [ ] El archivo descargado tiene un nombre legible y seguro.
- [ ] No se exponen datos internos ni secretos.
- [ ] Se puede regenerar el PDF sin alterar el comprobante.
- [ ] Hay tests de contenido, QR y aislamiento.

## Escenarios Gherkin

```gherkin
Scenario: Descargar PDF
  Given un comprobante propio autorizado
  When el usuario solicita su PDF
  Then recibe un archivo PDF
  And el PDF contiene número, CAE e importe total
  And el PDF incluye la leyenda de simulación
```

```gherkin
Scenario: QR de verificación
  Given un comprobante con CAE 12345678901234
  When se genera el PDF
  Then el QR contiene la URL de verificación con ese CAE
  And al escanearlo se abre la página de verificación
```

```gherkin
Scenario: PDF de otra empresa
  Given un comprobante de la empresa B
  When un usuario de la empresa A solicita el PDF
  Then la API devuelve 404
```

```gherkin
Scenario: Regeneración
  Given un comprobante ya descargado
  When se vuelve a solicitar el PDF
  Then el comprobante no cambia
  And el nuevo PDF conserva los mismos datos fiscales
```

## Tareas técnicas

- [ ] Crear `IPdfComprobanteService` en Application.
- [ ] Implementar la generación con QuestPDF.
- [ ] Generar el QR con QRCoder.
- [ ] Configurar `Portal__BaseUrl` para el link de verificación.
- [ ] Crear `GET /v1/comprobantes/{id}/pdf`.
- [ ] Aplicar autenticación y filtro de tenant.
- [ ] Agregar tests de contenido y QR.
- [ ] Documentar el endpoint y el formato del archivo.

## Definition of Done

- [ ] El PDF se descarga y contiene todos los datos requeridos.
- [ ] El QR abre la verificación pública correcta.
- [ ] No se puede descargar el PDF de otra empresa.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el manual.

## Contrato técnico

Fuente normativa: `docs/contratos-api.md` (§2 errores, §3 aislamiento, §12 correo y `Portal__BaseUrl`, §14 convenciones de API) y `docs/modelos.md` (entidad `Comprobante`). Esta sección no se aparta de esos documentos.

### Modelo de datos

US-10 **no agrega colecciones, entidades ni índices**. Lee `comprobantes` y no escribe nada: el PDF es una proyección del comprobante ya persistido.

| Colección | Documento | Qué usa US-10 |
| --- | --- | --- |
| `comprobantes` | `Comprobante` + `ComprobanteItem` | Datos fiscales, ítems e importes que se imprimen. |

El acceso es por `(Id, ContribuyenteId)`: usa el índice `_id` de MongoDB y descarta el documento si el tenant no coincide. No hace falta ningún índice nuevo.

### Librerías y paquetes

US-10 **agrega dos paquetes NuGet**, ambos en `FacturAR.Infrastructure.csproj`. Ni `FacturAR.Domain`, ni `FacturAR.Application`, ni `FacturAR.Contracts` ni `FacturAR.Api` los referencian.

| Paquete | Versión | Licencia | Para qué | Por qué |
| --- | --- | --- | --- | --- |
| `QuestPDF` | Actual compatible con .NET 10 | MIT | Composición del PDF | API declarativa con layout, tablas y código QR en un solo paquete. Es puro .NET: no requiere binario externo (ni Chromium, ni wkhtmltopdf, ni LibreOffice), así el PDF se genera igual en el contenedor de la API y en un runner de CI. La licencia MIT permite el uso académico e industrial del proyecto. |
| `QRCoder` | Actual compatible con .NET 10 | MIT | Generar la imagen del código QR | Genera el QR como PNG **en memoria**, sin dependencias nativas ni de sistema de archivos, y con corrección de errores suficiente para leerse con un lector estándar. |

Alternativas descartadas:

- `iTextSharp` / `itext7`: licencia AGPL o comercial con restricciones; no admisible.
- `PdfSharpCore`: es una biblioteca de bajo nivel; el layout con tablas, encabezados y QR sería manual y frágil.
- Conversión HTML → PDF: exige un binario del sistema operativo en la imagen Docker, lo que rompe el build reproducible y agrega superficie de ataque.
- Generación del QR con JavaScript en el navegador: movería la composición del documento al cliente, el PDF dejaría de ser un artefacto del servidor y el QR no se podría verificar con un test de integración.

Configuración obligatoria de QuestPDF: hay que fijar la licencia al arrancar, por ejemplo `QuestPDF.Settings.License = LicenseType.Community;`, en el registro de DI de `FacturAR.Infrastructure`. Sin eso la librería lanza una excepción al generar el documento.

### Endpoints

| Método | Ruta | Autenticación | Query | Respuesta | Errores |
| --- | --- | --- | --- | --- | --- |
| `GET` | `/v1/comprobantes/{id:guid}/pdf` | API key (US-03) o `cookies.contribuyente` + `EmpresaConsulta` (US-04) | — | `200` `application/pdf` con `Content-Disposition: inline; filename="0001-00000001.pdf"` | `400 VALIDACION`, `401 API_KEY_AUSENTE`, `401 API_KEY_INVALIDA`, `401 CREDENCIALES_INVALIDAS`, `403 SIN_PERMISOS`, `404 COMPROBANTE_NO_ENCONTRADO`, `503 MONGO_NO_DISPONIBLE`, `500 ERROR_INTERNO` |

- El `Content-Type` es `application/pdf` y el `Content-Disposition` es **`inline`**: el navegador abre el PDF en una pestaña en lugar de descargarlo, y el portal lo abre en una pestaña nueva.
- El nombre del archivo es `{PuntoVenta:D4}-{Numero:D8}.pdf`, es decir `0001-00000001.pdf` para el punto de venta 1 y el número 1. Solo contiene dígitos y guiones: no hay separadores de ruta ni caracteres que puedan inyectarse en un header.
- Un id que no es `Guid` responde `400 VALIDACION`.
- `GET /v1/comprobantes/verificar/{cae}` **no cambia**: sigue siendo anónimo.

### Casos de uso y servicios

**Application** (`FacturAR.Application`)

| Tipo | Responsabilidad |
| --- | --- |
| `Abstractions.IPdfComprobanteService` | `Task<byte[]> GenerarAsync(Comprobante comprobante, CancellationToken ct)`. Recibe la entidad ya filtrada por tenant y devuelve los bytes del PDF. |
| `Comprobantes.ConsultarComprobanteService` | Reutilizado de US-09: resuelve `(id, contribuyente)` y devuelve `null` si no existe o es de otra empresa. |

**Infrastructure** (`FacturAR.Infrastructure`)

| Tipo | Responsabilidad |
| --- | --- |
| `Pdf/QuestPdfComprobanteService` | Implementa `IPdfComprobanteService`. Arma el documento y devuelve `document.GeneratePdf()`. |
| `Pdf/ComprobantePdfModelo` | Definición del documento QuestPDF: encabezado con emisor y QR, bloque de receptor, tabla de ítems, totales y pie con la leyenda de simulación. |
| `Pdf/QrGenerador` | `byte[] GenerarPng(string contenido, int pixelesPorModulo)`. Envuelve `QRCodeGenerator` y `PngByteQRCode` de `QRCoder`. |

**Api** (`FacturAR.Api`)

- `Comprobantes/ComprobantePdfEndpoint.cs`: resuelve el tenant con `ITenantContext`, pide el comprobante, devuelve `Results.File(bytes, "application/pdf", nombreArchivo, enableRangeProcessing: false)`.
- Un fallo al componer el PDF responde `500 ERROR_INTERNO` con el detalle solo en el log. **Nunca** se devuelve un PDF parcial ni un cuerpo vacío con `200`.

**Contracts**: no se agregan DTOs. La respuesta es binaria.

### Validaciones

- `id` de ruta: `Guid`. Un valor no parseable responde `400 VALIDACION` y no llega al servicio.
- El comprobante se busca por `(Id, ContribuyenteId)`. Un comprobante de otra empresa responde `404 COMPROBANTE_NO_ENCONTRADO`, igual que inexistente: la respuesta no revela que el documento existe.
- **Contenido del QR**: `{Portal__BaseUrl}/verificar?cae={CAE}`, con el CAE codificado con `Uri.EscapeDataString`. Como el CAE son 14 dígitos, en la práctica no hay caracteres que escapar, pero la regla se aplica igual.
- `Portal__BaseUrl` se lee sin barra final. Si el valor no es una URL absoluta, la API falla al arrancar con `ERROR_INTERNO` y no genera PDFs con una URL rota.
- **Leyenda obligatoria**: el pie del PDF incluye el texto literal «Comprobante simulado sin validez fiscal».
- **Datos que el PDF no puede incluir**: `ContribuyenteId`, `IdempotencyKey`, `Hash`, `PasswordHash` ni ningún otro campo interno. El PDF muestra únicamente los datos fiscales del comprobante.
- **Sin escritura**: generar el PDF no inserta, no actualiza y no consume números ni CAE. Regenerarlo no altera el comprobante.
- **Reproducible**: los metadatos del documento se fijan con la `FechaEmision` del comprobante, de modo que dos generaciones del mismo comprobante producen el mismo contenido fiscal.
- **Rendimiento**: la generación debe completarse en menos de 3 segundos. Se mide con un `Stopwatch` en el test de integración.

### Integración con otras historias

| Historia | Punto de integración |
| --- | --- |
| US-05 | Entidad `Comprobante`, ítems, importes y datos del emisor que se imprimen. |
| US-09 | `ConsultarComprobanteService` y el aislamiento por `ContribuyenteId`. |
| US-11 | La página pública `/verificar` del portal (ya existe como Razor Page `Pages/Verificar.cshtml`) es el destino del QR. |
| US-12 | El listado del portal ofrece la descarga del PDF de cada comprobante propio. |
| US-13 | La emisión manual muestra el PDF descargable del comprobante recién emitido. |
| US-29 | `Portal__BaseUrl`: US-29 la introduce como parámetro formal de plataforma y US-10 la necesita desde el arranque. Si US-29 todavía no está, US-10 declara la clave con el mismo nombre en la configuración de la API para no romper el contrato. |
| US-21 | OpenAPI, `FacturAR.Api.http` y la colección Postman documentan el endpoint y el `Content-Disposition`. |

## Casos de prueba

| ID | Precondición | Pasos | Resultado esperado |
| --- | --- | --- | --- |
| CP-01 | Comprobante propio `Emitido`, punto de venta 1, número 1. | `GET /v1/comprobantes/{id}/pdf`. | `200`, `Content-Type: application/pdf`, `Content-Disposition: inline; filename="0001-00000001.pdf"`. El cuerpo empieza con la firma `%PDF-`. |
| CP-02 | El mismo comprobante. | Abrir el PDF y revisar su contenido. | Incluye emisor, receptor, tipo, punto de venta, número, fecha, CAE, vencimiento, los ítems y los importes (neto, IVA y total). |
| CP-03 | El mismo comprobante. | Buscar el texto de la leyenda en el PDF. | Aparece «Comprobante simulado sin validez fiscal». |
| CP-04 | Comprobante con CAE `12345678901234` y `Portal__BaseUrl = http://localhost:5116`. | Generar el PDF y decodificar la imagen del QR. | El QR contiene `http://localhost:5116/verificar?cae=12345678901234`. Un lector estándar lo abre en la página de verificación. |
| CP-05 | Comprobante de la empresa B. | `GET /v1/comprobantes/{id-de-B}/pdf` con la key de A. | `404 COMPROBANTE_NO_ENCONTRADO`. No se devuelve PDF ni datos de B. |
| CP-06 | Ningún comprobante con ese id. | `GET /v1/comprobantes/{guid-inexistente}/pdf`. | `404 COMPROBANTE_NO_ENCONTRADO`. |
| CP-07 | Un comprobante ya descargado. | Descargar el PDF dos veces y comparar los datos fiscales. | El comprobante en MongoDB no cambia (mismo número, mismo CAE, mismo `UpdatedAt` nulo) y los dos PDF muestran los mismos importes. No se consume número ni CAE. |
| CP-08 | Un comprobante propio. | Medir el tiempo de la generación. | La generación completa en menos de 3 segundos. |
| CP-09 | Un comprobante propio. | Inspeccionar el texto del PDF. | No aparecen `ContribuyenteId`, `IdempotencyKey`, `Hash` ni rutas del servidor de MongoDB. |
| CP-10 | Un comprobante propio. | Generar el PDF con un nombre de archivo con punto de venta 12 y número 345. | `Content-Disposition: inline; filename="0012-00000345.pdf"`. El nombre solo tiene dígitos y guiones. |
| CP-11 | Cualquier endpoint de PDF. | Llamarlo sin `X-Api-Key` ni cookie de sesión. | `401 API_KEY_AUSENTE`. No se genera PDF. |
| CP-12 | La librería de PDF no está registrada o falla al componer. | Pedir el PDF de un comprobante propio. | `500 ERROR_INTERNO` con `ErrorResponse`. El log tiene el detalle. No se devuelve un PDF parcial. |

## Dependencias técnicas

Antes de empezar US-10 tiene que existir:

- **US-05 implementada**: entidad `Comprobante` con ítems e importes calculados.
- **US-09 implementada**: `ConsultarComprobanteService` con filtro por `ContribuyenteId`, el esquema de autenticación de los endpoints de comprobantes y la ruta `/v1/comprobantes/{id:guid}`.
- **US-04 implementada**: `ITenantContext`, cookie `cookies.contribuyente` y política `EmpresaConsulta`.
- **US-11 implementada o, como mínimo, la ruta pública `/verificar` del portal**: es el destino del QR y ya existe como `Pages/Verificar.cshtml` en `FacturAR.Web`.
- **Configuración**: `Portal__BaseUrl` declarada en la configuración de la API, con default `http://localhost:5116` y sin barra final. US-29 la normaliza después como parámetro de plataforma.
- **Paquetes**: `QuestPDF` y `QRCoder` agregados a `FacturAR.Infrastructure.csproj`, con la licencia de QuestPDF fijada en el arranque.
- **Application**: `Abstractions.IPdfComprobanteService` (interfaz pura, sin referencia a QuestPDF ni a QRCoder).
- **Infrastructure**: `Pdf/QuestPdfComprobanteService`, `Pdf/ComprobantePdfModelo` y `Pdf/QrGenerador`.
- **Api**: el handler que devuelve el archivo con `Results.File` y el `Content-Disposition` `inline`.
- **Tests**: `FacturAR.UnitTests` para la construcción del nombre de archivo y de la URL del QR; `FacturAR.IntegrationTests` con `MongoFact` para contenido, QR, aislamiento, regeneración y tiempo de generación.
- **Documentación**: `docs/contratos-api.md` §14, `docs/manual-integracion.md` (sección de descargas), `docs/postman/FacturAR.postman_collection.json` y `facturAR_backend/src/FacturAR.Api/FacturAR.Api.http`.

## Notas de alcance

- No implementa firma digital del PDF.
- No implementa envío por email.
- No implementa plantillas personalizables por empresa.
- No implementa PDF de notas si US-08 no está implementada.
