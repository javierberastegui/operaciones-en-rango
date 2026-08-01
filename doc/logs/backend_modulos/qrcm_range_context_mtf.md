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
- No repaint: request.security con patrón canónico `lookahead_on` + offset `[1]` cuando
  `confirmHTF` (por defecto); `lookahead_off` intrabar si se desactiva. Alertas al cierre.
- Reutiliza el motor `f_trendPack` (EMAs, pendiente/ATR, estructura, ADX/DI, KER, RSI).
- Sin `strategy.*`. JSON usa `null` para support/resistance/posición cuando no hay rango configurado.

### Revisión Codex (2026-08-01) — atendida
- P1 (lookahead): a petición del usuario, se cambió a `lookahead_on` + `[1]` (patrón canónico
  anti-repaint) cuando `confirmHTF`; `lookahead_off` + `[0]` en modo responsivo. Sin fuga de futuro.
- P2 (PLAN en medio del rango): `f_plan` ahora prioriza el override "Esperar mejor extremo"
  antes que las lecturas de rebote/pullback, para no contradecir el SESGO en MEDIO.
- P2 (rango manual): `rangeConfigured` exige ahora `manualSupport > 0` además de
  `manualResistance > manualSupport` (evita rango de base cero si solo se pone resistencia).

### Revisión Codex — 2ª ronda (2026-08-01) — atendida
- P1 (TF inferiores): el patrón `lookahead_on` + `[1]` solo se aplica ahora a TFs iguales o
  superiores al del gráfico (comprobado con `timeframe.in_seconds`). Los TFs inferiores (p. ej.
  15m en 1H) usan `lookahead_off` + `[0]`, evitando el repaint que provocaba `lookahead_on` en LTF.
- P2 (frecuencia de alertas): al cierre (`alertOncePerBarClose`) se usa `alert.freq_all` en lugar de
  `freq_once_per_bar`, protegido por `barstate.isconfirmed`, para que todos los eventos que cambian
  en la misma barra se emitan (antes el primer `alert()` podía suprimir a los siguientes). En modo
  intrabar se mantiene `freq_once_per_bar`.

### Revisión Codex — 3ª ronda (2026-08-01) — atendida
- P1 (agregado MTF con `na`): `mtfBias` se calcula ahora solo sobre los TF disponibles
  (numerador y denominador ignoran los `na`); si ninguno está disponible -> `na`. El JSON serializa
  `mtf_bias` como `null` cuando es `na`, la etiqueta del header muestra "n/a" y el TQI usa `nz`.
  Evita que un TF `na` (p. ej. semanal en activo recién listado) contamine el agregado o emita `NaN`.
- P2 (umbrales de zona): se saneaan/ordenan (`zSupPct <= zMidLoPct <= zMidHiPct <= zResPct`, 0..100)
  antes de clasificar zonas y dibujar, para que inputs desordenados no dejen zonas inalcanzables.
- P2 (etiqueta de bias): el corte intermedio de `f_biasLabel` se deriva de
  `(biasWeakThresh + biasStrongThresh)/2` en vez de un 35 fijo, para no etiquetar "Alcista/Bajista"
  un score que `f_bull/f_biasKey` consideran neutral cuando `biasWeakThresh` > 35.

### Revisión Codex — 4ª ronda (2026-08-01) — atendida (P1/P2) + 1 pendiente de decisión
- P1 (warm-up con `b4h`/`b1d` na): `f_biasDecision` devuelve ahora "Contexto MTF no disponible"
  con riesgo 0x/0x cuando 4H o 1D aún no son fiables (antes daba "Rango equilibrado 1x/1x" falso).
  `f_lectura` y `f_plan` tienen rama de warm-up; el override de "medio" no pisa ese estado.
- P2 (umbrales de bias): `biasWeakThresh`/`biasStrongThresh` se derivan de `min`/`max` de los
  inputs, así el fuerte nunca queda por debajo del débil (etiqueta y decisión ya no se contradicen).
- P2 (eventos intrabar): las alertas usan `alert.freq_all` en ambos modos; el guard `prev*` evita
  duplicados por tick y garantiza que todos los tipos de evento que cambian se emitan.
- P2 PENDIENTE (etiquetas 4H/1D con TF configurables): en decisión del usuario (ver más abajo).

### Pendiente / próximos pasos
- Validar compilación y comportamiento en TradingView (BTC/ETH 1H, FX, activo de bajo volumen).
- Posible V1.3: persistencia de niveles por símbolo, divergencias, ajuste de umbrales por activo.

### Limitaciones conocidas
- El macro (1W/1M) es informativo, no bloquea. El sesgo se apoya en 4H+1D; 15m solo afina timing.
- El indicador no predice ni gestiona riesgo por sí mismo.
