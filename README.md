# operaciones-en-rango

Indicadores de TradingView (Pine Script) para operativa de **rango** en futuros.

## Indicadores

- **QRCM Range Executor V1.1** — `indicators/qrcm_range_executor_v1_1.pine` ⭐ *recomendado para operar*
  Versión simple y ejecutable: tabla brutalmente clara (¿ENTRO O NO ENTRO?), rango **manual** o auto,
  3 modos (Simple/Compacto/Debug), runner 10% corregido por contexto de posición y gráfico sin spam.
  Guía de uso: [`docs/qrcm_range_executor_v1_1.md`](docs/qrcm_range_executor_v1_1.md).

- **QRCM Range MTF Risk Board V1.0** — `indicators/qrcm_range_mtf_risk_board_v1.pine`
  Versión completa de contexto, régimen, riesgo y gestión emocional con visión multi-temporalidad
  (15m / 1H / 4H / 1D / 1M), multiplicador de riesgo, runner 10% y alertas JSON.
  Guía de uso: [`docs/qrcm_range_mtf_risk_board_v1.md`](docs/qrcm_range_mtf_risk_board_v1.md).

> El Executor V1.1 es independiente: **no sustituye** al Risk Board V1.0 (ambos conviven).
> Ambos están inspirados en QRCE-Lite V1.1.

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
