# QRCM Structure Range Context V1.4.1

Versión de estabilización de `QRCM Structure Range Context V1.4` para TradingView.

## Archivos

- `indicators/qrcm_structure_range_context_v1_4_1.pine`: versión principal `overlay=true` para operar soporte/resistencia sobre el gráfico de precio.
- `indicators/qrcm_structure_range_context_panel_v1_4_1.pine`: variante `overlay=false` para panel separado, sin dibujo de líneas/cajas de precio y con `plot(regimeScore)` visible.

## Cambios principales

### Auto S/R ligero

El motor Auto S/R ya no hace doble bucle `srLookback x srLookback`. La versión 1.4.1 usa arrays persistentes de candidatos:

- Soportes: niveles, toques, reacciones y última barra tocada.
- Resistencias: niveles, toques, reacciones y última barra tocada.

Cuando se confirma un pivote:

1. Se ignora si el pivote es `na`.
2. Se fusiona con un candidato existente si está dentro de la tolerancia ATR.
3. Se añade como candidato nuevo si no hay nivel cercano.
4. Se limita el número de candidatos con `maxSrCandidates`.

En cada vela solo se actualizan los candidatos existentes; no se reescanea todo el histórico completo.

### Inputs nuevos / ajustados

- `panelOnlyMode`: desactiva dibujo de S/R y zonas, dejando solo tabla y plot oculto de escala.
- `confirmHTF`: usa velas HTF cerradas en `request.security` para lectura MTF más segura/no repaint.
- `autoSrMode`: deja documentado el modo ligero/preciso. La ruta operativa por defecto es ligera e incremental.
- `maxSrCandidates`: por defecto 30.
- `srLookback`: por defecto 150.
- `minTouches`: por defecto 2.
- `minReactions`: por defecto 1.

### Mobile

Mobile queda limitado a 5 filas, texto `tiny`, frases cortas, sin cajas, sin zonas ni labels, y posición por defecto `Top Left`.

### Dibujo S/R

La versión principal mantiene `overlay=true`, pero:

- No dibuja si `rangeSource = "Sin S/R, solo contexto MTF"`.
- No dibuja si `panelOnlyMode=true`.
- No dibuja en modo Mobile.
- Actualiza objetos `var line` / `var box` persistentes en lugar de crear objetos por vela.
- Dibuja como máximo 1 soporte y 1 resistencia.

## Uso recomendado

- Para operar niveles sobre precio: usar `qrcm_structure_range_context_v1_4_1.pine` en el gráfico principal.
- Para panel separado: usar `qrcm_structure_range_context_panel_v1_4_1.pine`.
- Si TradingView o el dispositivo es lento, mantener `srLookback=150`, `maxSrCandidates=30` y Mobile activo.

## Nota sobre modo preciso

El modo preciso se conserva como opción/documentación de intención para revisión manual más pesada. La ruta de cálculo estable por defecto es el motor incremental ligero, diseñado para evitar bloqueos en TradingView.
