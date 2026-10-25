# US-11 — Verificación pública por CAE

**Épica:** Consultas  
**Prioridad:** Alta  
**Estimación:** 2 días  
**Requisito:** RF-10  
**Depende de:** US-05 y US-18 para el flujo completo; la base actual se validó con datos insertados directamente  
**Habilita:** US-10, US-30

## Historia

Como visitante, quiero verificar un comprobante por su CAE para confirmar que fue emitido por FacturAR y ver sus datos fiscales públicos.

## Contexto

La verificación es anónima. Solo expone datos fiscales no sensibles. El CAE debe tener 14 dígitos y existir en la colección de comprobantes.

## Criterios de aceptación

- [x] La consulta es anónima y no requiere API key ni login.
- [x] Un CAE válido y existente devuelve los datos fiscales públicos.
- [x] Un CAE con formato inválido devuelve `400`.
- [x] Un CAE inexistente devuelve `404`.
- [x] La respuesta no expone datos internos ni credenciales.
- [ ] La página `/verificar` permite ingresar el CAE y muestra el resultado con un comprobante existente.
- [x] La búsqueda usa el índice único por CAE.
- [x] Un CAE con espacios o separadores se normaliza antes de validar.

## Escenarios Gherkin

```gherkin
Scenario: CAE existente
  Given un comprobante emitido con CAE "12345678901234"
  When un visitante consulta ese CAE
  Then la API devuelve 200 con emisor, tipo, número, fecha e importe
  And no devuelve datos internos
```

```gherkin
Scenario: CAE inválido
  Given un visitante ingresa "123"
  When consulta la verificación
  Then la API devuelve 400 con código "CAE_INVALIDO"
```

```gherkin
Scenario: CAE inexistente
  Given un CAE de 14 dígitos que no existe
  When un visitante consulta la verificación
  Then la API devuelve 404 con código "COMPROBANTE_NO_ENCONTRADO"
```

```gherkin
Scenario: Verificación desde el portal
  Given un comprobante existente
  When el visitante ingresa el CAE en "/verificar"
  Then la página muestra los datos públicos del comprobante
  And no muestra botones de administración
```

## Tareas técnicas

- [x] Crear `GET /v1/comprobantes/verificar/{cae}`.
- [x] Crear `VerificarComprobanteService`.
- [x] Crear la página Razor `/verificar` y el cliente HTTP.
- [x] Validar y normalizar el formato del CAE.
- [x] Agregar tests del caso de uso.
- [x] Agregar un test de integración con MongoDB.
- [ ] Probar el flujo completo desde `/verificar` con un CAE existente.
- [x] Documentar códigos de error.

## Definition of Done

- [ ] La verificación funciona desde el portal y por API con un comprobante real.
- [x] Los códigos de error están documentados.
- [x] El endpoint es anónimo y no filtra datos internos.
- [ ] La historia está enlazada desde el índice y el manual.

## Evidencia

- `MongoApiTests` valida la API contra MongoDB real: comprobante existente, `404`, ausencia de datos internos y creación del índice único `ux_comprobantes_cae`.
- `ApiEndpointsTests` valida `400 CAE_INVALIDO`.
- `dotnet test FacturAR.slnx -c Release`: 18 tests unitarios y 9 de integración aprobados.
- Pendiente: probar el flujo completo desde `/verificar` con un CAE existente para cerrar la historia. La emisión que genera ese CAE depende de US-05.

## Notas de alcance

- No implementa verificación de QR; el QR se agrega en US-10.
- No implementa consulta SOAP; eso corresponde a US-16.
- No implementa datos del receptor que puedan ser sensibles.
