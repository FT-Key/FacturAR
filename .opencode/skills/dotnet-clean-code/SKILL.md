---
name: dotnet-clean-code
description: Aplica DRY, KISS y SOLID en C# y .NET de FacturAR, incluyendo nombres, async, nullable, DTOs y refactors. Usar al implementar o revisar archivos .cs de la solución.
---

# Código .NET limpio en FacturAR

## Principios

- **KISS**: la solución más simple que cumple el criterio. No agregues capas, interfaces ni patrones sin un consumidor real.
- **DRY**: extraé cuando la duplicación es real y estable; no crees abstracciones para dos líneas parecidas.
- **SOLID**: una clase con una responsabilidad, dependencias explícitas por constructor, interfaces pequeñas y contratos estables. No fuerces todos los principios.

## Checklist

- Nombres claros en español o inglés, consistentes con el archivo. Sin abreviaturas crípticas.
- Métodos cortos, guard clauses al inicio y retorno temprano.
- Sin comentarios salvo pedido explícito; el código debe explicarse solo.
- `nullable` habilitado: evitá `!` salvo invariantes justificadas.
- `async`/`await` en toda la cadena. Nunca `.Result`, `.Wait()` ni `Task.Run` para I/O.
- `CancellationToken` en operaciones asíncronas de Application, Infrastructure y Api.
- `decimal` para dinero, UTC para fechas, `DateTimeKind.Utc` explícito.
- DTOs como `record` en `FacturAR.Contracts`; entidades fuera de respuestas HTTP.
- Validación en el borde (endpoint o modelo), no dispersa en cada capa.
- Excepciones solo para fallas inesperadas; errores esperados se representan con resultados o códigos.
- Dependencias por DI; evitar estado estático mutable y singletons con estado por request.
- No exponer `IQueryable`, entidades ni tipos de Mongo desde Application.
- Mantener los límites de capas de `AGENTS.md`.

## Refactors

1. Cambiá lo mínimo para cumplir la historia.
2. Si extraés, hacelo después de tener tests que cubran el comportamiento.
3. No mezcles refactors grandes con la funcionalidad de la historia.
4. Verificá con `dotnet build` y los tests afectados.

## Salida esperada

Código compilable, sin warnings nuevos, con tests del comportamiento y sin deuda innecesaria. Si detectás un problema fuera de alcance, reportalo; no lo arregles en la misma historia.
