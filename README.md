# operaciones-en-rango

Indicadores de TradingView (Pine Script) para operativa de **rango** en futuros.

## Indicadores

- **QRCM Range MTF Risk Board V1.0** — `indicators/qrcm_range_mtf_risk_board_v1.pine`
  Indicador de contexto, régimen, riesgo y gestión emocional para operar rangos con
  visión multi-temporalidad (15m / 1H / 4H / 1D / 1M), multiplicador de riesgo,
  lectura del runner 10% y alertas JSON.
  Guía de uso: [`docs/qrcm_range_mtf_risk_board_v1.md`](docs/qrcm_range_mtf_risk_board_v1.md).

> Inspirado en QRCE-Lite V1.1, pero **independiente**: no sustituye ni modifica ese indicador.

## Estructura

```
indicators/                         Indicadores Pine Script
docs/                               Guías de uso
doc/logs/backend_modulos/           Bitácoras por dominio (motores/lógica)
```

## Cómo usar

1. Abre TradingView → Pine Editor.
2. Pega el contenido del `.pine` correspondiente y pulsa "Add to chart".
3. Configura los inputs (timeframes, modo de panel, riesgo, alertas).
4. Para alertas JSON: crea una alerta con la condición **"Any alert() function call"**.

> ⚠️ No son estrategias automáticas ni señales infalibles. Son herramientas de criterio.
