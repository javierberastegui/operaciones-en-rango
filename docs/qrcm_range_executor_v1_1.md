# QRCM Range Executor V1.1 — Guía de uso

Versión **simple y ejecutable** de QRCM Range MTF Risk Board V1.0. Responde rápido a una sola
pregunta: **¿ENTRO O NO ENTRO?** en operativa de rango (TF principal 1H).

> Es un indicador independiente. **No sustituye** al V1.0 (que sigue en el repo). No es estrategia,
> no envía órdenes, no predice el mercado.

Archivo: `indicators/qrcm_range_executor_v1_1.pine` (Pine Script v5).

---

## 1. La tabla (modo Simple por defecto)

```
QRCM Range Executor V1.1       72
DECISIÓN                       COMPRAR SOPORTE
ACCIÓN                         ENTRAR 1.5x
RANGO                          MANUAL VÁLIDO
ZONA                           Soporte
TENDENCIA                      A favor 4H/1D
RUNNER 10%                     No aplica
```

- **DECISIÓN**: texto humano (COMPRAR SOPORTE, VENDER RESISTENCIA, NO OPERAR — MEDIO DEL RANGO, etc.).
- **ACCIÓN**: ENTRAR 2x/1.5x/1x/0.5x/0.25x, NO ENTRAR, CERRAR PARCIAL, CERRAR TODO, DEJAR RUNNER 10%.
- **RANGO**: MANUAL VÁLIDO / VÁLIDO / ESTRECHO — NO COMPENSA / VOLÁTIL — CAUTELA / ROTO / INVÁLIDO.
- **ZONA**: Soporte / Zona baja / Medio — no operar / Zona alta / Resistencia / Ruptura.
- **TENDENCIA**: A favor 4H/1D, En contra 4H/1D, Mixta, Neutral, Macro a favor/en contra.
- **RUNNER 10%**: ver sección 5.

### Modos
- **Simple** (defecto): 7 filas, entendible en 3 segundos.
- **Compacto**: añade RIESGO, BIAS MTF y las 5 temporalidades (15m/1H/4H/1D/1M).
- **Debug**: todo lo técnico (ADX, KER, TQI, RSI, volatilidad, volumen, Range Position,
  Range Width ATR, LongScore, ShortScore, RangeScore, MTFBiasScore, ExhaustionScore, BreakoutRiskScore).

Cambia con el input **Modo panel**. Debug está oculto por defecto.

---

## 2. Rango Manual (recomendado) y Auto

- **Manual** (por defecto): coloca **soporte** y **resistencia** con `input.price` (`confirm=true`),
  es decir, al añadir el indicador TradingView te pide hacer clic en el gráfico para fijar cada nivel.
  Después puedes ajustarlos arrastrando los puntos o desde los inputs.
- **Auto** (secundario): detecta S/R con Donchian + pivotes confirmados (`autoRangeLen`, `pivotLeft/Right`).

El script dibuja soporte, resistencia, zona de soporte (verde), zona de resistencia (roja) y
**zona media en gris como NO OPERAR**. Todo configurable.

> Pine no puede leer las líneas que dibujas con las herramientas normales de TradingView; por eso el
> rango manual usa niveles de precio movibles (`input.price`).

---

## 3. Zonas

`rangePosition = (close - soporte) / (resistencia - soporte) * 100`

| Posición | Zona |
|----------|------|
| < 20% | Soporte |
| 20–40% | Zona baja |
| 40–60% | **Medio — NO OPERAR** |
| 60–80% | Zona alta |
| > 80% | Resistencia |
| bajo soporte | Ruptura bajista |
| sobre resistencia | Ruptura alcista |

Umbrales configurables (`supportZonePct`, `middleLowPct`, `middleHighPct`, `resistanceZonePct`, `breakoutBufferATR`).

---

## 4. Riesgo y decisión

El indicador da un **multiplicador** (0 / 0.25 / 0.5 / 1 / 1.5 / 2x), no un tamaño exacto.

- LONG en soporte/zona baja; SHORT en resistencia/zona alta. **Medio = 0x**. Ruptura en contra = 0x.
- A favor de 4H **y** 1D → sube (hasta 2x si hay sesgo MTF y no hay agotamiento).
- 4H a favor y 1D neutral → riesgo normal (1x).
- 4H **o** 1D fuerte en contra → máx 0.5x. Ambos fuerte en contra → 0.25x.
- 1M (macro) en contra no bloquea, pero se avisa en TENDENCIA.

**Score 0–100** (apoyo, se muestra en el header): 35% ubicación en el extremo correcto, 25% validez
de rango, 25% tendencia MTF a favor, 10% volatilidad sana, 5% ausencia de agotamiento.
Interpretación: 80–100 excelente · 65–79 bueno · 50–64 cautela · <50 no operar.

---

## 5. Runner 10% (corregido)

El runner **solo se evalúa cuando gestionas una posición** y has llegado al extremo contrario.
Para ello existe el input **Contexto de posición**:

- **Sin posición** (defecto): RUNNER 10% = `NO APLICA`.
- **Long desde soporte**: el runner se evalúa al llegar a **resistencia/zona alta**.
  En soporte/zona baja/medio muestra `NO APLICA — PRIMERO LLEGAR A RESISTENCIA` (ya no dice tonterías).
- **Short desde resistencia**: el runner se evalúa al llegar a **soporte/zona baja**.

Resultados del runner en el extremo correcto:
- `SÍ — DEJAR 10%`: tendencia a favor y presión de ruptura confirmada.
- `SOLO SI ROMPE RESISTENCIA/SOPORTE`: contexto favorable pero sin confirmación.
- `NO — CERRAR EN RESISTENCIA/SOPORTE`: contexto en contra, agotamiento o rechazo.

---

## 6. Gráfico limpio (sin spam de etiquetas)

- `showSignalLabels = false` por defecto: **no** se pintan etiquetas sobre las velas.
- Si lo activas: con `showOnlyLastSignalLabel = true` solo hay **una** etiqueta (última vela);
  si lo desactivas, se crea una etiqueta solo **cuando cambia la decisión** (no en cada vela).
- `max_labels_count` bajo para evitar acumulación.

---

## 7. Alertas JSON

Crea una alerta con condición **"Any alert() function call"**. Eventos:
`qrcm.decision_changed`, `qrcm.zone_changed`, `qrcm.risk_changed`, `qrcm.runner_changed`,
`qrcm.range_broken`, `qrcm.range_recovered`. Ejemplo:

```json
{
  "event_type": "qrcm.decision_changed",
  "symbol": "BINANCE:BTCUSDT",
  "timeframe": "60",
  "decision": "COMPRAR SOPORTE",
  "action": "ENTRAR 1.5x",
  "position_context": "Sin posición",
  "range_mode": "Manual",
  "zone": "support",
  "risk_multiplier": "1.5x",
  "score": 72,
  "trend_context": "A favor 4H/1D",
  "runner_10": "NO APLICA",
  "support": 0.0,
  "resistance": 0.0,
  "range_position": 14.2
}
```

Se disparan al **cierre de vela** y solo en cambios de estado.

---

## 8. No repaint

- `request.security` con `lookahead_off` (+ opción de barra HTF cerrada).
- Pivotes confirmados.
- Alertas al cierre de vela.

## 9. Limitaciones

No predice el mercado ni sustituye tu gestión de riesgo. El S/R automático es una aproximación;
prioriza el modo Manual. En activos sin volumen fiable, el módulo de volumen muestra `n/a`.
