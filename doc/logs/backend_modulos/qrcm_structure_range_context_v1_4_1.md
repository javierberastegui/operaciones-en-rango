# Log - QRCM Structure Range Context V1.4.1

## 2026-06-18

- Creada versión `indicators/qrcm_structure_range_context_v1_4_1.pine`.
- Creada variante panel `indicators/qrcm_structure_range_context_panel_v1_4_1.pine` con `overlay=false` y `plot(regimeScore)` visible.
- Reemplazado Auto S/R O(N²) por motor incremental con arrays persistentes y límite de candidatos.
- Evitado conteo de toques para niveles `na`.
- Reducido `srLookback` por defecto a 150.
- Añadidos `maxSrCandidates`, `minReactions`, `autoSrMode`, `panelOnlyMode` y `confirmHTF`.
- Ajustado Mobile a máximo 5 filas, texto tiny y sin dibujo de S/R.
- Añadido patrón MTF confirmado mediante valores HTF cerrados cuando `confirmHTF=true`.
