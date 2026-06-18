# QRCM Range Context MTF V1.2 — Guía de uso

`QRCM Range Context MTF V1.2` es un indicador Pine Script v5 centrado en el **contexto MTF de rangos**. No sustituye al ejecutor V1.1: es un archivo nuevo pensado para que el trader pueda dibujar o configurar manualmente un rango y leer qué lado tiene ventaja.

Archivo: `indicators/qrcm_range_context_mtf_v1_2.pine`.

---

## Objetivo

Mostrar en una tabla limpia el sesgo operativo del rango usando:

- **15m**: timing. No cambia el sesgo principal.
- **1H**: rango operativo.
- **4H**: timeframe principal de sesgo.
- **1D**: confirmación o contradicción del 4H.
- **1W/1M**: contexto macro configurable. No bloquea automáticamente.

El indicador **no depende de detectar soporte/resistencia** para ser útil. Si el rango no está configurado, la tabla mantiene toda la lectura MTF y muestra:

```text
RANGO MANUAL    No configurado
```

---

## Tabla Simple

El modo **Simple** muestra una lectura accionable:

```text
QRCM Range Context V1.2
SESGO RANGO     Preferir SHORT en resistencia
RANGO MANUAL    No configurado / Manual válido
RIESGO LONG     0.5x — contra 4H
RIESGO SHORT    1.5x — a favor 4H/1D
15m             Alcista débil
1H              Neutral
4H              Bajista fuerte
1D              Bajista
1W              Neutral
LECTURA         Rebote corto contra tendencia mayor
PLAN            No perseguir long; esperar resistencia para short
```

El modo **Compacto** añade estado de rango, zona y bias MTF. El modo **Debug** conserva métricas internas como ADX, KER, TQI, RSI, volatilidad, volumen, posición del rango y scores.

---

## Rango manual opcional

En el grupo **Rango** existe el input `Usar rango manual S/R`.

- Desactivado: el panel muestra `RANGO MANUAL: No configurado` y sigue calculando sesgo, riesgos, lectura y plan MTF.
- Activado + niveles válidos: dibuja soporte, resistencia y zonas del rango.
- Activado + niveles inválidos: no bloquea la tabla; solo reporta el estado del rango.

> Pine no puede leer líneas dibujadas con las herramientas normales de TradingView. Para que el script dibuje/ubique zonas debe usarse `input.price`, pero la lectura MTF sigue siendo útil aunque el trader dibuje el rango visualmente por su cuenta.

---

## Reglas de sesgo y riesgo

- **4H bajista + 1D bajista**:
  - `SESGO RANGO = Preferir SHORT en resistencia`.
  - `RIESGO SHORT = 1.5x` o `2x` si ambas tendencias son fuertes y `Permitir 2x` está activo.
  - `RIESGO LONG = 0.25x` o `0.5x`.
- **4H bajista + 15m alcista**:
  - `LECTURA = Rebote corto contra tendencia mayor`.
  - `PLAN = No perseguir long; esperar resistencia para short`.
- **4H alcista + 1D alcista**:
  - `SESGO RANGO = Preferir LONG en soporte`.
  - `RIESGO LONG = 1.5x` o `2x`.
  - `RIESGO SHORT = 0.25x` o `0.5x`.
- **4H alcista + 15m bajista**:
  - `LECTURA = Pullback corto dentro de tendencia mayor`.
  - `PLAN = Buscar soporte para long`.
- **4H neutral + 1D neutral**:
  - `SESGO RANGO = Rango equilibrado`.
  - Ambos riesgos quedan en `1x`.
- **4H y 1D contradictorios**:
  - `SESGO RANGO = Mixto / operar extremos con cautela`.
  - Riesgo reducido en ambos lados.

La decisión no se limita a comprar/vender. El panel usa textos como `Preferir LONG en soporte`, `Preferir SHORT en resistencia`, `Rango equilibrado`, `Solo extremos, riesgo reducido`, `Evitar longs contra 4H`, `Evitar shorts contra 4H` y `Esperar mejor extremo`.

---

## Visual y alertas

- Las etiquetas del gráfico están desactivadas por defecto para evitar spam visual.
- Si se activan, se mantiene una sola etiqueta en la última vela.
- El script incluye alertas básicas para cambio de sesgo de rango y rango roto.

---

## No repaint

- `request.security` usa `lookahead_off`.
- La opción `HTF sin repaint (barra cerrada)` mantiene la lectura sobre barras cerradas de timeframes superiores.
- Los pivotes de estructura se usan confirmados.

---

## Limitaciones

El indicador no predice el mercado ni reemplaza la gestión de riesgo del trader. El contexto macro 1W/1M es informativo y no bloquea automáticamente operaciones en el rango.
