# operaciones-en-rango

Indicadores de TradingView (Pine Script) para operativa de **rango** en futuros.

## Indicadores

- **QRCM Range Context MTF V1.2** — `indicators/qrcm_range_context_mtf_v1_2.pine` ⭐ *recomendado*
  Contexto multi-temporalidad para rangos **dibujados a mano**: SESGO RANGO, RIESGO LONG/SHORT,
  tendencias 15m/1H/4H/1D/1W(1M), LECTURA y PLAN. El rango manual es **opcional y no bloquea** la tabla.
  Guía de uso: [`docs/qrcm_range_context_mtf_v1_2.md`](docs/qrcm_range_context_mtf_v1_2.md).

- **QRCM Range Executor V1.1** — `indicators/qrcm_range_executor_v1_1.pine`
  Versión simple y ejecutable: tabla clara (¿ENTRO O NO ENTRO?), rango manual o auto,
  3 modos (Simple/Compacto/Debug), runner 10% por contexto de posición y gráfico sin spam.
  Guía de uso: [`docs/qrcm_range_executor_v1_1.md`](docs/qrcm_range_executor_v1_1.md).

- **QRCM Range MTF Risk Board V1.0** — `indicators/qrcm_range_mtf_risk_board_v1.pine`
  Versión completa de contexto, régimen, riesgo y gestión emocional con visión multi-temporalidad
  (15m / 1H / 4H / 1D / 1M), multiplicador de riesgo, runner 10% y alertas JSON.
  Guía de uso: [`docs/qrcm_range_mtf_risk_board_v1.md`](docs/qrcm_range_mtf_risk_board_v1.md).

> Cada versión es **independiente** y no sustituye a las anteriores (las tres conviven).
> Todas están inspiradas en QRCE-Lite V1.1.

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
