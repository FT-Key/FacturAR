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

## Notas de alcance

- No implementa firma digital del PDF.
- No implementa envío por email.
- No implementa plantillas personalizables por empresa.
- No implementa PDF de notas si US-08 no está implementada.
