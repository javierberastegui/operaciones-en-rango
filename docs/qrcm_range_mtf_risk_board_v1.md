# QRCM Range MTF Risk Board V1.0 — Guía de uso

Indicador de **contexto, régimen, riesgo y gestión emocional** para operativa de **rango**
en futuros (timeframe principal 1H). Inspirado en QRCE-Lite V1.1, pero **es un indicador
independiente**: no sustituye ni modifica al anterior.

> ⚠️ **No es una estrategia.** No abre ni cierra órdenes (`strategy.entry/exit` no se usan).
> No predice el mercado. Es una capa de decisión para reducir emociones y ordenar el criterio.

Archivo: `indicators/qrcm_range_mtf_risk_board_v1.pine` (Pine Script v5).

---

## 1. Qué responde el panel

| Fila | Qué te dice |
|------|-------------|
| **Acción** | `OPERAR` / `CAUTELA` / `NO OPERAR` / `ESPERAR` + lado y multiplicador de riesgo |
| **Gate** | Estado de la puerta (`ABIERTA` / `SEMIABIERTA` / `CERRADA` / `EN ESPERA`) + motivo |
| **Modo** | Estado del rango (válido, débil, estrecho, volátil, transición, posible ruptura, no rango) |
| **Zona** | Dónde está el precio: soporte, zona baja, **medio (no operar)**, zona alta, resistencia, ruptura |
| **Riesgo** | Multiplicador sugerido: `0x · 0.25x · 0.5x · 1x · 1.5x · 2x` |
| **Swing 10%** | Si tiene sentido dejar el runner del 10% al llegar al extremo opuesto |
| **Bias MTF** | Sesgo agregado ponderado de las 5 temporalidades |
| **15m / 1H / 4H / 1D / 1M** | Dirección por temporalidad (etiqueta y color) |
| **Rango** | Posición %, anchura en ATR y calidad de rango (Q) |
| **ADX / KER / TQI / RSI** | Métricas de fuerza/calidad del TF operativo |
| **Agotamiento** | No / Leve / Moderado / Alto (según el extremo en que estés) |
| **Volatilidad / Volumen** | Salud de la volatilidad (percentil ATR) y volumen relativo (`n/a` si no es fiable) |

Tres modos de panel: **Full**, **Compact** y **Mini** (input `Modo panel`).

---

## 2. Lógica de decisión (resumen)

1. **Motor de rango** decide si estás en rango real. Combina ADX, KER, Choppiness,
   anchura en ATR y volatilidad. Si no hay rango o hay ruptura/volatilidad extrema → gate cerrado.
2. **Motor de ubicación** sitúa el precio. **El medio del rango cierra el gate** (no operar por aburrimiento).
3. **Motor MTF** calcula un `biasScore` (-100..+100) por temporalidad combinando EMAs,
   pendiente normalizada por ATR, estructura (HH/HL vs LL/LH con pivotes confirmados),
   ADX/DI, KER y RSI de régimen. Se agrega con pesos (15m 15%, 1H 25%, 4H 30%, 1D 20%, 1M 10%).
4. **Motor de riesgo** sube el multiplicador cuando la operación de rango va **a favor**
   de 4H/1D/1M y lo baja (o lo anula) cuando va **en contra** o hay agotamiento.
5. **Motor runner 10%** evalúa si dejar el 10% en swing al llegar al extremo opuesto.

### Multiplicador de riesgo (orientativo)

**LONG en soporte:**
- 4H y 1D alcistas o no bajistas → `1x` / `1.5x`.
- 4H + 1D + 1M alineados alcistas y sin agotamiento → `2x` (si `Permitir 2x` está activo).
- 4H o 1D bajista fuerte → máx `0.5x`.
- Medio del rango o ruptura a la baja → `0x`.

**SHORT en resistencia:** simétrico.

Si el rango es puro y los TF altos son neutrales → máximo `1x` (no se permite `2x` sin sesgo MTF).

### Runner 10%

- **Permitido**: bias MTF claramente a favor (≥ `runnerMinMTFBias`), 4H a favor,
  1D no contrario fuerte, ADX y KER subiendo, sin agotamiento extremo, y (opcional)
  ruptura confirmada.
- **Solo si rompe**: contexto a favor pero falta confirmación de ruptura.
- **Cerrar en resistencia / soporte**: contexto en contra, agotamiento o rechazo en el extremo.

---

## 3. Inputs principales

- **Timeframes (MTF)**: `tfFast=15`, `tfOperative` (vacío = gráfico), `tfHTF=240`,
  `tfDaily=D`, `tfMacro=M` + pesos. `HTF sin repaint` usa la barra HTF cerrada.
- **Trend Engine**: EMA 21/55, ADX 14, RSI 14, KER 20, slope 5, pivotes 3/3.
- **Range Engine**: lookback 100, modo S/R (Donchian/Pivots/Fusion), umbrales ADX/KER,
  Choppiness, Bollinger, anchura mín/máx en ATR, zonas (20/40/60/80%).
- **Risk**: score mínimo OPERAR (65) / CAUTELA (50), permitir 2x, riesgo máx contra HTF,
  bias mínimo para runner, exigir confirmación de ruptura.
- **Visual**: panel on/off, modo (Full/Compact/Mini), posición, tamaño, líneas/zonas S/R,
  fondo por bias, color de velas, etiquetas de gate, debug.
- **Alerts**: habilitar, por cambio de gate/bias/riesgo/runner/ruptura, solo al cierre de vela.

---

## 4. Alertas (JSON estructurado)

Crea una alerta con condición **"Any alert() function call"** para recibir los eventos en JSON.
Eventos: `gate_changed`, `bias_changed`, `risk_changed`, `runner_changed`, `breakout_risk`,
`range_recovered`. Ejemplo de payload:

```json
{
  "event_type": "range_mtf.gate_changed",
  "symbol": "BINANCE:BTCUSDT",
  "timeframe": "60",
  "gate": "OPEN_LONG_SUPPORT",
  "score": 72.4,
  "risk_multiplier": "1.5x",
  "zone": "support",
  "mtf_bias": "bullish_moderate",
  "runner_10": "allowed",
  "adx": 18.4,
  "ker": 0.22,
  "tqi": 0.61,
  "rsi": 44.9
}
```

También hay `alertcondition` básicos (mensajes con `{{ticker}}` / `{{interval}}`) para alertas simples.

Las alertas se disparan **al cierre de vela** por defecto (`barstate.isconfirmed`) y solo cuando cambia el estado.

---

## 5. No repaint

- Todos los `request.security` usan `lookahead_off`.
- Con `HTF sin repaint` activo se lee la **última barra HTF cerrada** (sin datos futuros).
- Pivotes siempre **confirmados** (introducen un retardo de `pivotRight` barras, pero no repintan).
- Alertas al cierre de vela por defecto.

---

## 6. Limitaciones reales

- **No predice el mercado** ni sustituye tu gestión de riesgo.
- Los soportes/resistencias automáticos (Donchian/pivotes) son **aproximaciones**, no zonas dibujadas a mano.
- En activos con **volumen falso o inexistente**, el módulo de volumen muestra `n/a` y pierde peso.
- El `2x` exige alineación MTF real; úsalo con criterio.
- Con `HTF sin repaint` hay un pequeño retardo a cambio de robustez.

---

## 7. Checklist de validación recomendada

Probar en BTC/USDT 1H, ETH/USDT 1H, un par FX (EUR/USD) y un activo de bajo volumen.
Revisar escenarios: rango claro, medio del rango, soporte con 4H alcista/bajista,
resistencia con 4H bajista/alcista, ruptura alcista/bajista, tendencia fuerte sin rango,
volatilidad extrema.
