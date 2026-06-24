# Log de dominio — QRCM Range Context MTF

Bitácora del módulo de indicador `indicators/qrcm_range_context_mtf_v1_2.pine`.
(Log por dominio "backend_modulos" = motores/lógica del indicador. No usar `historico.md` como bitácora principal.)

---

## 2026-06-18 — V1.2 (alta inicial)

**Tipo:** feature
**Autor:** equipo / Claude
**Estado:** entregado, pendiente de validación en TradingView por el usuario.

### Motivación
Refactor de QRCM Range Executor V1.1 hacia un indicador centrado en **contexto MTF** para operar
rangos **dibujados manualmente**. Su utilidad ya **no depende** de detectar soporte/resistencia.
No sustituye a V1.0 ni V1.1 (los tres conviven en `indicators/`).

### Qué se ha creado
- `indicators/qrcm_range_context_mtf_v1_2.pine` (Pine v5, independiente).
- `docs/qrcm_range_context_mtf_v1_2.md` (guía de uso).

### Cambios clave respecto a V1.1
- **Rango manual OPCIONAL y NO bloqueante**: si no está configurado, la fila RANGO MANUAL dice
  "No configurado" pero la tabla sigue mostrando todo el contexto MTF. Sin auto-detección de S/R.
- **Tabla de contexto**: SESGO RANGO / RIESGO LONG / RIESGO SHORT / 15m / 1H / 4H / 1D / 1W(1M) /
  LECTURA / PLAN (+ fila RANGO MANUAL y header con bias agregado).
- **Jerarquía explícita**: 4H = sesgo principal, 1D confirma, 1H operativo, 15m timing,
  1W/1M macro (no bloquea, configurable).
- **Reglas de sesgo** 4H+1D → SESGO + RIESGO LONG/SHORT con multiplicador y motivo
  ("1.5x — a favor 4H/1D", "0.5x — contra 4H").
- **Decisiones de contexto** (no solo comprar/vender): Preferir LONG/SHORT, Rango equilibrado,
  Solo extremos riesgo reducido, Evitar longs/shorts contra 4H, Esperar mejor extremo.
- **LECTURA/PLAN** combinando 4H con 15m (rebote contra tendencia / pullback dentro de tendencia).
- **Modos** Simple (limpio) / Compacto / Debug (métricas internas).
- **Sin spam de etiquetas** en el gráfico (solo líneas/zonas del rango manual si se configura).
- **Alertas JSON** reducidas: qrcm.bias_changed, qrcm.risk_changed, qrcm.reading_changed,
  qrcm.zone_changed.

### Decisiones técnicas
- No repaint: request.security lookahead_off + barra HTF cerrada opcional, alertas al cierre.
- Reutiliza el motor `f_trendPack` (EMAs, pendiente/ATR, estructura, ADX/DI, KER, RSI).
- Sin `strategy.*`. JSON usa `null` para support/resistance/posición cuando no hay rango configurado.

### Pendiente / próximos pasos
- Validar compilación y comportamiento en TradingView (BTC/ETH 1H, FX, activo de bajo volumen).
- Posible V1.3: persistencia de niveles por símbolo, divergencias, ajuste de umbrales por activo.

### Limitaciones conocidas
- El macro (1W/1M) es informativo, no bloquea. El sesgo se apoya en 4H+1D; 15m solo afina timing.
- El indicador no predice ni gestiona riesgo por sí mismo.
