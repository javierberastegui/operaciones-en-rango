# QRCM Range Context MTF V1.2 — Guía de uso

Indicador de **contexto multi-temporalidad** para operar rangos que **tú dibujas manualmente**.
Su utilidad **no depende** de detectar soporte/resistencia: te dice qué lado del rango tiene ventaja
según las tendencias de **15m / 1H / 4H / 1D / 1W (o 1M)**.

> Indicador independiente. **No sustituye** a V1.0 ni a V1.1 (los tres conviven en `indicators/`).
> No es estrategia, no envía órdenes, no predice el mercado.

Archivo: `indicators/qrcm_range_context_mtf_v1_2.pine` (Pine Script v5).

---

## 1. La tabla (modo Simple)

```
QRCM Range Context V1.2        Bajista
RANGO MANUAL    No configurado
SESGO RANGO     Preferir SHORT en resistencia
RIESGO LONG     0.5x — contra 4H
RIESGO SHORT    1.5x — a favor 4H/1D
15m             Alcista débil
1H              Rango / Neutral
4H              Bajista fuerte
1D              Bajista
1W              Neutral
LECTURA         Rebote corto contra tendencia mayor
PLAN            No perseguir long; esperar resistencia para short
```

Aunque **no haya rango configurado**, la tabla sigue mostrando todo el contexto MTF
(la fila RANGO MANUAL dice `No configurado`, pero el resto funciona).

---

## 2. Jerarquía de temporalidades

- **4H** = sesgo principal.
- **1D** = confirma o contradice el 4H.
- **1H** = rango operativo.
- **15m** = timing (no cambia el sesgo principal, solo afina la lectura).
- **1W/1M** = contexto macro (informativo, **no bloquea**). El TF macro es configurable.

---

## 3. Reglas de sesgo (SESGO RANGO + riesgos)

| 4H | 1D | SESGO RANGO | RIESGO LONG | RIESGO SHORT |
|----|----|-------------|-------------|--------------|
| Bajista | Bajista | Preferir SHORT en resistencia | 0.25–0.5x | 1.5–2x |
| Alcista | Alcista | Preferir LONG en soporte | 1.5–2x | 0.25–0.5x |
| Neutral | Neutral | Rango equilibrado | 1x | 1x |
| Bajista | Alcista | Evitar longs contra 4H | 0.5x | 1x |
| Alcista | Bajista | Evitar shorts contra 4H | 1x | 0.5x |
| Bajista | Neutral | Preferir SHORT en resistencia | 0.5x | 1.5x |
| Alcista | Neutral | Preferir LONG en soporte | 1.5x | 0.5x |
| Neutral | Bajista/Alcista | Solo extremos, riesgo reducido | 0.5–1x | 0.5–1x |

Si el rango está configurado y el precio está en el **medio**, el SESGO pasa a
**`Esperar mejor extremo`** (los riesgos siguen indicando qué lado favorecer al llegar al extremo).

La fila de riesgo muestra multiplicador **y motivo**: `1.5x — a favor 4H/1D`, `0.5x — contra 4H`.

---

## 4. LECTURA y PLAN

**LECTURA** (4H manda, 15m es timing):
- 4H bajista + 15m alcista → `Rebote corto contra tendencia mayor`.
- 4H alcista + 15m bajista → `Pullback corto dentro de tendencia mayor`.
- 4H alcista → `Tendencia 4H alcista; favorecer longs en soporte`.
- 4H bajista → `Tendencia 4H bajista; favorecer shorts en resistencia`.
- 4H y 1D neutrales → `Rango equilibrado sin tendencia dominante`.

**PLAN** (acción concreta), p. ej.:
- `No perseguir long; esperar resistencia para short` (rebote contra tendencia).
- `Buscar soporte para long` (pullback dentro de tendencia).
- `Vender resistencia; comprar soporte solo pequeño` (sesgo SHORT).
- `Comprar soporte; vender resistencia solo pequeño` (sesgo LONG).
- `Operar ambos extremos con tamaño normal` (rango equilibrado).
- `En medio del rango: esperar soporte o resistencia`.

---

## 5. Rango manual (opcional)

- `Usar rango manual` (on por defecto) + `Soporte manual` / `Resistencia manual` (`input.price`).
  Escribe el precio o arrastra los puntos en el gráfico. **0 = no configurado** (no bloquea nada).
- Si está configurado: se dibujan soporte (verde), resistencia (roja), zona de soporte, zona de
  resistencia y **zona media en gris = NO OPERAR**; la fila RANGO MANUAL muestra la zona y el % de
  posición dentro del rango.

---

## 6. Modos de panel

- **Simple** (defecto): la tabla completa de arriba (limpia).
- **Compacto**: header, RANGO, SESGO, RIESGO LONG/SHORT, 4H, 1D, LECTURA.
- **Debug**: añade scores por TF, MTFBiasScore, ADX/KER/TQI/RSI, posición en rango y ATR.

No se pintan etiquetas sobre las velas.

---

## 7. Alertas JSON

Condición **"Any alert() function call"**. Eventos: `qrcm.bias_changed`, `qrcm.risk_changed`,
`qrcm.reading_changed`, `qrcm.zone_changed` (este último solo si el rango está configurado).
Ejemplo:

```json
{
  "event_type": "qrcm.bias_changed",
  "symbol": "BINANCE:BTCUSDT",
  "timeframe": "60",
  "sesgo": "Preferir SHORT en resistencia",
  "risk_long": "0.5x",
  "risk_short": "1.5x",
  "b15": "bullish",
  "b1h": "neutral",
  "b4h": "bearish_strong",
  "b1d": "bearish",
  "bmacro": "neutral",
  "mtf_bias": -38,
  "lectura": "Rebote corto contra tendencia mayor",
  "plan": "No perseguir long; esperar resistencia para short",
  "range_status": "not_configured",
  "zone": "none",
  "support": null,
  "resistance": null,
  "range_position": null
}
```

Se disparan al **cierre de vela** y solo en cambios de estado.

---

## 8. No repaint y limitaciones

- `request.security` con `lookahead_off` (+ barra HTF cerrada). Sin `strategy.*`.
- No predice el mercado ni sustituye tu gestión de riesgo. El macro (1W/1M) es informativo.
- El sesgo se basa en 4H+1D; el 15m solo afina la lectura/timing.
