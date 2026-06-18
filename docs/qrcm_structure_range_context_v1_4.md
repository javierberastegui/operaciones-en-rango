# QRCM Structure Range Context V1.4

Archivo: `indicators/qrcm_structure_range_context_v1_4.pine`.

## Objetivo

`QRCM Structure Range Context V1.4` refactoriza la lectura del rango hacia un contexto estructural operativo. El indicador ya no se limita a mostrar `Alcista / Bajista / Neutral`; prioriza frases de decisión como:

- `Bajista hacia soporte`
- `Pullback alcista`
- `Rango zona baja`
- `Soporte bajo presión`
- `Ruptura bajista confirmada`
- `Macro alcista en corrección`
- `No perseguir short en soporte`

La idea central es separar una caída hacia soporte de una ruptura real de soporte.

## Timeframes fijos

Por defecto el panel siempre evalúa los mismos marcos, aunque el gráfico esté en otro timeframe:

| Rol | Default |
| --- | --- |
| Timing | `15m` |
| Operativo | `1H` |
| HTF jefe | `4H` |
| Diario | `1D` |
| Macro | `1W`, configurable a `1M` |

El input `Usar timeframe del gráfico como operativo` viene desactivado. Solo si se activa, la fila operativa usa `timeframe.period`.

## Régimen estructural

El módulo de régimen combina:

1. Estructura de mercado con pivotes confirmados (`HH/HL`, `LH/LL`).
2. EMA 21/55 como contexto, no como verdad absoluta.
3. ADX, KER y CHOP para distinguir dirección frente a rango.
4. Ubicación respecto a soporte/resistencia.
5. Volumen relativo y vela amplia para validar rupturas.
6. Cierres confirmados, no mechas aisladas.

Regímenes posibles:

- `Tendencia alcista`
- `Tendencia bajista`
- `Rango`
- `Transición`
- `Pullback alcista`
- `Pullback bajista`
- `Ruptura alcista`
- `Ruptura bajista`
- `Soporte bajo presión`
- `Resistencia bajo presión`

## S/R automático horizontal

El modo `Auto SR` no calcula bandas dinámicas. Busca niveles horizontales operativos:

- pivotes confirmados dentro de `srLookback`;
- agrupación por tolerancia ATR (`touchToleranceATR`);
- mínimo de toques (`minTouches`);
- separación mínima entre toques (`minBarsBetweenTouches`);
- reacción cerrada mínima (`reactionATR`);
- selección del soporte válido más cercano debajo/cerca del precio;
- selección de la resistencia válida más cercana encima/cerca del precio.

Solo se dibujan dos niveles:

1. soporte operativo;
2. resistencia operativa.

Si no hay niveles válidos, el panel muestra `S/R no confirmado` y mantiene la lectura MTF.

## Rango manual persistente

El modo `Manual SR` usa:

- `Activar rango manual`;
- `Soporte manual`;
- `Resistencia manual`;
- `Manual: barras hacia atrás`.

El rango solo es válido si ambos precios son mayores que cero y `resistencia > soporte`. Las líneas manuales se gestionan con objetos persistentes `var line`, `extend.right`, y actualización en `barstate.islast`, evitando crear líneas nuevas en cada vela. Además se dejan plots ocultos para mantener referencias estables de los precios manuales.

Si el rango manual no está configurado, el panel no bloquea la decisión: muestra `S/R no configurado` y sigue usando contexto MTF.

## Zonas y rupturas

Cuando existe S/R válido:

```text
rangePosition = (close - support) / (resistance - support) * 100
```

Zonas:

- `Soporte`
- `Zona baja`
- `Medio`
- `Zona alta`
- `Resistencia`
- `Ruptura alcista`
- `Ruptura bajista`

Una ruptura requiere cierre fuera del nivel con buffer ATR y confirmación por volumen relativo o vela amplia:

- `breakoutCloseBufferATR`
- `breakoutVolumeMult`
- `breakoutConfirmBars`

## 4H como jefe

La fila de sesgo prioriza la lectura 4H:

- Si 4H está alcista o en pullback alcista y el precio está en soporte, el plan favorece `Long soporte con confirmación; SL bajo zona`.
- Si 4H está bajista y el precio está en soporte, evita perseguir tarde: `No perseguir short; long pequeño o esperar ruptura`.
- Si 4H está bajista y el precio rebota a resistencia, propone `Buscar short en resistencia`.
- Si 4H está en rango, recomienda extremos y evita operar el medio.

## Panel Mobile

El modo `Mobile` está diseñado para no cortarse en TradingView móvil:

- texto `tiny` obligatorio;
- posición por defecto `Top Left`;
- modo compacto de una columna;
- máximo 6 filas;
- frases abreviadas si `Texto Mobile = Ultra corto`;
- no depende del margen derecho del gráfico;
- no muestra debug ni etiquetas.

Ejemplo ultra corto:

```text
QRCM -34
No short tarde
4H PB↑
1D Rango bajo
Soporte
LONG conf
```

## Modos Desktop

### Simple

Incluye:

- cabecera;
- `SESGO`;
- `RANGO/SR`;
- `ZONA`;
- `15m`;
- `1H`;
- `4H`;
- `1D`;
- `Macro`;
- `PLAN`.

### Full

Añade:

- régimen;
- ADX/KER/CHOP 4H;
- posición dentro del rango;
- volumen relativo;
- ruptura confirmada;
- riesgo long/short resumido.

### Debug

Añade:

- bias raw por TF;
- fase numérica por TF;
- soporte/resistencia detectados;
- touch count;
- reaction count;
- breakout score;
- volume confirmation.

## Alertas JSON

El indicador emite eventos mediante `alert()` con payload JSON estructurado. Eventos disponibles:

- `qrcm.structure_context_changed`
- `qrcm.support_detected`
- `qrcm.resistance_detected`
- `qrcm.support_lost`
- `qrcm.resistance_broken`
- `qrcm.range_context_changed`
- `qrcm.mobile_bias_changed`

Ejemplo de payload:

```json
{
  "event_type": "qrcm.structure_context_changed",
  "symbol": "BTCUSDT",
  "timeframe": "60",
  "bias": "Preferir LONG en soporte",
  "regime": "Pullback alcista",
  "zone": "Soporte",
  "plan": "Long soporte con confirmación; SL bajo zona",
  "tf_15m": "Rebote alcista",
  "tf_1h": "Bajista hacia soporte",
  "tf_4h": "Pullback alcista",
  "tf_1d": "Rango zona baja",
  "tf_macro": "Macro alcista en corrección",
  "support": 63600.0,
  "resistance": 67000.0,
  "support_touches": 2,
  "resistance_touches": 2,
  "breakout_confirmed": false
}
```

Por defecto las alertas se disparan solo al cierre de vela (`Solo cierre de vela`).

## No repaint

La versión V1.4 evita repaint operativo mediante:

- `request.security(..., lookahead = barmerge.lookahead_off)`;
- pivotes confirmados (`ta.pivothigh`, `ta.pivotlow`);
- rupturas basadas en cierre con buffer ATR;
- alertas al cierre por defecto;
- objetos S/R persistentes que se actualizan solo en la última barra.

## Bitácora de dominio

- Se añadió `QRCM Structure Range Context V1.4` como indicador nuevo, sin sobrescribir versiones anteriores.
- Se documentó el nuevo motor estructural, el S/R automático horizontal, el rango manual persistente, el panel móvil compacto y las alertas JSON.
