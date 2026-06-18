# QRCM Range Context MTF Mobile V1.3 — Guía de uso

`QRCM Range Context MTF Mobile V1.3` añade una presentación específica para TradingView móvil sin cambiar el motor MTF del indicador V1.2.

Archivo: `indicators/qrcm_range_context_mtf_mobile_v1_3.pine`.

---

## Objetivo

Reducir el panel a una lectura rápida y accionable para móviles, evitando que la tabla tape el gráfico.

La lógica MTF se mantiene:

- **4H**: sesgo principal.
- **1D**: confirmación del sesgo 4H.
- **15m**: timing o rebote corto.
- **1H**: operativo, visible en modos de escritorio.
- **1W/1M**: macro opcional, visible en modos de escritorio.

---

## Modos visuales

El input **Modo visual** incluye:

1. **Mobile**: panel ultra resumido para móvil.
2. **Simple**: modo por defecto para escritorio.
3. **Full**: vista extendida de escritorio sin métricas internas.
4. **Debug**: vista completa para análisis interno.

`Simple` sigue siendo el valor por defecto. `Mobile` es opcional por input.

---

## Modo Mobile

El modo Mobile muestra un máximo de **6 filas**:

```text
QRCM Mobile
SESGO
4H
1D
15m
PLAN
```

Ejemplo:

```text
QRCM Mobile
SESGO   Preferir SHORT
4H      Bajista fuerte
1D      Bajista
15m     Rebote alcista
PLAN    Esperar resistencia
```

Características del modo Mobile:

- Usa `text_size = tiny` siempre.
- Posición Mobile independiente, por defecto **Top Left**.
- Borde y frame de tabla reducidos.
- Textos cortos: `Preferir SHORT`, `Preferir LONG`, `Solo extremos`, `Esperar soporte`, `Esperar resistencia`.
- No muestra la zona media sombreada ni zonas grandes de soporte/resistencia.
- No muestra etiquetas de señal aunque el input de escritorio esté activado.

Si el rango manual no está configurado o es inválido, el panel Mobile no se bloquea: la cabecera muestra `RANGO no configurado` y las filas MTF siguen actualizándose.

---

## Fila PLAN

La fila **PLAN** traduce el contexto MTF a una acción humana corta:

- `Esperar resistencia` cuando predomina el sesgo bajista o el plan favorece shorts.
- `Esperar soporte` cuando predomina el sesgo alcista o el plan favorece longs.
- `Rango equilibrado` cuando 4H y 1D no dan ventaja clara.
- `Solo extremos` cuando 4H y 1D se contradicen.
- `Extremos cautela` cuando no hay suficiente claridad direccional.

En escritorio, los modos `Simple`, `Full` y `Debug` conservan planes más descriptivos.

---

## Rango manual opcional

El rango S/R sigue siendo opcional:

- Desactivado: muestra `RANGO no configurado` y conserva el contexto MTF.
- Activado con niveles válidos: permite dibujar líneas y calcular zona.
- Activado con niveles inválidos: no bloquea el panel; solo reporta el estado del rango.

Pine no puede leer líneas dibujadas manualmente con las herramientas nativas de TradingView. Para que el script calcule zonas debe usarse `input.price`, pero la lectura MTF funciona aunque el trader dibuje el rango por su cuenta.

---

## No repaint

- `request.security` usa `lookahead_off`.
- La opción **HTF sin repaint (barra cerrada)** mantiene la lectura sobre barras cerradas de timeframes superiores.
- Los pivotes de estructura se usan confirmados.
