# US-18 — Parámetros y datos semilla

**Épica:** Operación  
**Prioridad:** Media  
**Estimación:** 1 día  
**Requisito:** RF-18  
**Depende de:** US-02, US-04, US-07  
**Habilita:** US-11, US-21

## Historia

Como equipo de desarrollo, quiero contar con parámetros configurables y datos semilla idempotentes para demostrar el sistema y ejecutar pruebas sin cargar datos a mano.

## Contexto

Los parámetros incluyen alícuotas de IVA, días de vencimiento del CAE, moneda, punto de venta inicial y límites de paginación. Los datos semilla se cargan solo en Development o mediante un comando explícito.

## Criterios de aceptación

- [ ] Existe una fuente de parámetros para alícuotas, vencimiento de CAE, moneda y límites.
- [ ] Los valores por defecto coinciden con las reglas del MVP.
- [ ] Los parámetros se pueden sobrescribir por configuración sin recompilar.
- [ ] El seed es idempotente: ejecutarlo dos veces no duplica datos.
- [ ] El seed crea un contribuyente demo con CUIT válido y punto de venta 1.
- [ ] El seed crea usuarios demo `Administrador`, `Emisor` y `Consulta`.
- [ ] El seed crea secuencias iniciales si corresponde.
- [ ] El seed puede crear una API key demo solo en Development y muestra el valor una vez.
- [ ] El seed no se ejecuta en Production sin una bandera explícita.
- [ ] Las contraseñas demo se toman de configuración y se hashean.
- [ ] Los datos demo están rotulados como simulados.
- [ ] Hay tests de idempotencia y de parámetros.
- [ ] El README documenta cómo ejecutar el seed.

## Escenarios Gherkin

```gherkin
Scenario: Seed en Development
  Given una base vacía y el entorno Development
  When se ejecuta el seed
  Then se crea la empresa demo, sus usuarios y su punto de venta
  And se puede iniciar sesión con el usuario administrador demo
```

```gherkin
Scenario: Seed idempotente
  Given un seed ya ejecutado
  When se ejecuta nuevamente
  Then no se duplican contribuyentes, usuarios, puntos de venta ni secuencias
```

```gherkin
Scenario: Seed en Production
  Given el entorno Production y la bandera de seed deshabilitada
  When arranca la aplicación
  Then no se cargan datos demo
```

```gherkin
Scenario: Parámetro de vencimiento
  Given el parámetro de días de CAE en 10
  When se emite un comprobante
  Then el vencimiento es la fecha de emisión más 10 días
```

## Tareas técnicas

- [ ] Crear `ParametrosFacturacion` y su lectura desde configuración.
- [ ] Crear `SeedService` idempotente.
- [ ] Crear los datos demo de empresa, usuarios, punto de venta y secuencias.
- [ ] Crear la API key demo solo en Development si está configurada.
- [ ] Agregar la bandera `Seed__Enabled`.
- [ ] Integrar el seed al arranque o a un comando documentado.
- [ ] Agregar tests de idempotencia y parámetros.
- [ ] Documentar datos demo y credenciales de desarrollo sin secretos reales.

## Definition of Done

- [ ] Un entorno nuevo se puede demoar con el seed.
- [ ] El seed no duplica datos.
- [ ] Production no carga datos demo por accidente.
- [ ] `dotnet test FacturAR.slnx` pasa.
- [ ] La historia está enlazada desde el índice y el README.

## Notas de alcance

- No implementa un panel de administración de parámetros.
- No implementa migraciones de datos.
- No crea datos reales de AFIP.
- No commitear contraseñas ni API keys demo reales.
