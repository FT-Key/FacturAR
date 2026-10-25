# US-24 — Adaptador AFIP real

**Épica:** Interoperabilidad  
**Prioridad:** Baja (stretch)  
**Estimación:** 3 días  
**Requisito:** RF-22  
**Depende de:** US-05, US-15  
**Estado:** Stretch

## Historia

Como operador de FacturAR, quiero reemplazar el simulador por el web service real de AFIP/ARCA para emitir comprobantes con validez fiscal.

## Contexto

El adaptador real implementa la misma interfaz que el simulador. Requiere certificado digital, clave privada, WSAA y WSFEv1. Las credenciales nunca se versionan.

## Criterios de aceptación

- [ ] Existe una abstracción `IAfipGateway` usada por la emisión.
- [ ] El simulador y el adaptador real son intercambiables por configuración.
- [ ] El adaptador real autentica con WSAA y renueva el token antes de vencer.
- [ ] El adaptador real invoca `FECAESolicitar` con el contrato correcto.
- [ ] Se manejan códigos y observaciones de AFIP.
- [ ] Los errores de AFIP se traducen a errores de FacturAR.
- [ ] Las credenciales se cargan desde secretos o archivos fuera del repositorio.
- [ ] El modo simulado sigue funcionando sin credenciales.
- [ ] Hay tests con mocks o sandbox, no con producción.
- [ ] La documentación explica cómo configurar certificados y modo real.

## Escenarios Gherkin

```gherkin
Scenario: Modo simulado
  Given la configuración "Afip__Modo" en "Simulado"
  When se emite un comprobante
  Then se usa el generador de CAE simulado
```

```gherkin
Scenario: Modo real sin credenciales
  Given la configuración en "Real" sin certificado
  When se intenta emitir
  Then la API devuelve un error de configuración claro
  And no se genera un CAE falso
```

```gherkin
Scenario: Token WSAA vencido
  Given un token WSAA vencido
  When se emite un comprobante
  Then el adaptador renueva el token
  And reintenta la operación
```

## Tareas técnicas

- [ ] Crear `IAfipGateway`.
- [ ] Implementar `AfipSimuladoGateway` con el código actual.
- [ ] Implementar `AfipRealGateway` con WSAA y WSFEv1.
- [ ] Configurar certificado, clave y modo.
- [ ] Traducir errores de AFIP.
- [ ] Agregar tests con mocks.
- [ ] Documentar configuración y riesgos.

## Definition of Done

- [ ] El modo real se puede activar sin recompilar.
- [ ] El modo simulado sigue siendo el predeterminado.
- [ ] Las credenciales no están en el repositorio.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice.

## Notas de alcance

- No implementa todos los comprobantes de AFIP.
- No implementa delegación de servicios web.
- No usa producción en tests.
