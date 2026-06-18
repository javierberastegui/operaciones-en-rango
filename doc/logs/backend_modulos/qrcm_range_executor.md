# Log de dominio — QRCM Range Executor

Bitácora del módulo de indicador `indicators/qrcm_range_executor_v1_1.pine`.
(Log por dominio "backend_modulos" = motores/lógica del indicador. No usar `historico.md` como bitácora principal.)

---

## 2026-06-18 — V1.2 Context MTF (alta independiente)

**Tipo:** feature
**Autor:** equipo / Codex
**Estado:** entregado, pendiente de validación en TradingView por el usuario.

### Motivación
V1.1 resolvía la ejecución de rango, pero seguía estando demasiado ligada a la validez del rango S/R.
V1.2 nace como indicador independiente centrado en contexto MTF para rangos dibujados o configurados
manualmente por el trader.

### Qué se ha creado
- `indicators/qrcm_range_context_mtf_v1_2.pine` (Pine v5, independiente del V1.1 y V1.0).
- `docs/qrcm_range_context_mtf_v1_2.md` (guía de uso).

### Cambios clave respecto a V1.1
- El rango manual S/R pasa a ser opcional.
- La tabla no queda bloqueada si el rango manual no está configurado o es inválido.
- El modo Simple prioriza `SESGO RANGO`, `RIESGO LONG`, `RIESGO SHORT`, 15m, 1H, 4H, 1D, macro, `LECTURA` y `PLAN`.
- 4H actúa como timeframe principal de sesgo, 1D confirma o contradice, 15m queda como timing y 1W/1M como contexto macro no bloqueante.
- Se mantienen Compacto y Debug para métricas internas sin llenar el gráfico de etiquetas.

### Pendiente / próximos pasos
- Validar compilación y lectura visual en TradingView con rango manual desactivado, manual válido y manual inválido.

## 2026-06-18 — V1.1 (alta inicial)

**Tipo:** feature
**Autor:** equipo / Claude
**Estado:** entregado, pendiente de validación en TradingView por el usuario.

### Motivación
QRCM Range MTF Risk Board V1.0 resultó demasiado complejo y visualmente cargado.
V1.1 es una versión "ejecutor de rango": tabla brutalmente clara que responde ¿ENTRO O NO ENTRO?
No sustituye al V1.0 (ambos conviven en `indicators/`).

### Qué se ha creado
- `indicators/qrcm_range_executor_v1_1.pine` (Pine v5, independiente del V1.0).
- `docs/qrcm_range_executor_v1_1.md` (guía de uso).

### Cambios clave respecto a V1.0
- **3 modos de panel**: Simple (defecto, 7 filas), Compacto (13), Debug (28, oculto por defecto).
- **Rango Manual** con `input.price` (`confirm=true`) además de Auto (Donchian + pivotes). Manual por defecto.
- **Tabla ejecutor**: DECISIÓN / ACCIÓN / RANGO / ZONA / TENDENCIA / RUNNER 10% (textos humanos).
- **Sin spam de etiquetas**: `showSignalLabels=false` por defecto; si se activa, una sola etiqueta
  (última vela) o solo al cambiar la decisión. `max_labels_count=20`.
- **Runner 10% corregido**: input `positionContext` (Sin posición / Long desde soporte / Short desde
  resistencia). En soporte sin posición ya NO dice "cerrar en soporte"; muestra "NO APLICA". El runner
  solo se evalúa al llegar al extremo contrario gestionando una posición.
- **MTF simplificado**: pesos 15m 10 / 1H 20 / **4H 35** / **1D 25** / 1M 10. El panel simple solo
  muestra el resumen TENDENCIA; el detalle por TF queda en Compacto/Debug.
- **Riesgo** 0/0.25/0.5/1/1.5/2x con reglas a favor/contra 4H-1D y aviso de macro 1M.
- **Zona media = NO OPERAR** (cierra la decisión y se sombrea en gris en el gráfico).
- **Score** explicable 0–100 (solo número en header en modo simple).
- **Alertas JSON** reducidas a cambios importantes: `qrcm.decision_changed`, `qrcm.zone_changed`,
  `qrcm.risk_changed`, `qrcm.runner_changed`, `qrcm.range_broken`, `qrcm.range_recovered`.

### Decisiones técnicas
- No repaint: `request.security` lookahead_off + barra HTF cerrada opcional, pivotes confirmados,
  alertas al cierre de vela y solo en cambios de estado.
- Reutiliza el motor de tendencia `f_trendPack` del V1.0 (EMAs, pendiente/ATR, estructura, ADX/DI, KER, RSI).
- Sin `strategy.*`.

### Pendiente / próximos pasos
- Validar compilación y comportamiento en TradingView (BTC/ETH 1H, FX, activo de bajo volumen).
- Posible V1.2: persistencia de niveles manuales por símbolo, divergencias, ajuste de umbrales por activo.

### Limitaciones conocidas
- S/R automáticos son aproximaciones (prioriza Manual). Volumen poco fiable → `n/a`.
- El indicador no predice ni gestiona riesgo por sí mismo.
