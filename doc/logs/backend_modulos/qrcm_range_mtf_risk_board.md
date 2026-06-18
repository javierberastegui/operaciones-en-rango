# Log de dominio — QRCM Range MTF Risk Board

Bitácora del módulo de indicador `indicators/qrcm_range_mtf_risk_board_v1.pine`.
(Log por dominio "backend_modulos" = motores/lógica del indicador. No usar `historico.md` como bitácora principal.)

---

## 2026-06-18 — V1.0 (alta inicial)

**Tipo:** feature
**Autor:** equipo / Claude
**Estado:** entregado, pendiente de validación en TradingView por el usuario.

### Qué se ha creado
- Indicador nuevo **QRCM Range MTF Risk Board V1.0** (Pine Script v5), independiente de QRCE-Lite V1.1
  (no se toca ni se sobrescribe el indicador anterior).
- Estructura del repo: `indicators/`, `docs/`, `doc/logs/backend_modulos/`.

### Módulos implementados
1. Motor de tendencia MTF (`f_trendPack`): EMAs, pendiente normalizada por ATR,
   estructura HH/HL con pivotes confirmados, ADX/DI, KER, RSI de régimen → `biasScore` -100..+100 por TF.
2. Motor de rango: ADX + KER + Choppiness + anchura ATR + volatilidad → calidad de rango y estado.
3. Soporte/Resistencia: Donchian / Pivotes / Fusión (input `srMode`).
4. Motor de ubicación: soporte / zona baja / medio / zona alta / resistencia / ruptura.
5. Motor de fuerza/calidad (TQI 0..1).
6. Motor de volatilidad (ratio ATR + percentil) y volumen (con fallback `n/a`).
7. Motor de momentum (RSI, ADX/KER rising).
8. Motor de agotamiento (RSI extremo, distancia EMA en ATR, Bollinger, rachas, spike+rechazo).
9. Motor de riesgo (multiplicador 0/0.25/0.5/1/1.5/2 según alineación MTF y agotamiento).
10. Motor de gate (OPEN/CAUTION/CLOSED/WAIT) con texto humano.
11. Motor runner 10% (Permitido / Solo si rompe / Cerrar en extremo).
12. Score explicable 0..100 (35% rango, 25% ubicación, 25% MTF, 10% volatilidad, 5% agotamiento).
13. Panel visual (Full / Compact / Mini), estética oscura tipo QRCE-Lite.
14. Alertas estructuradas JSON vía `alert()` + `alertcondition` básicas.

### Decisiones técnicas
- **No repaint**: `request.security` con `barmerge.lookahead_off` en las 5 llamadas;
  input `HTF sin repaint` (offset 1 = última barra HTF cerrada). Pivotes confirmados.
  Alertas al cierre de vela (`barstate.isconfirmed`) y solo en cambios de estado.
- Pesos MTF por defecto: 15m 15%, 1H 25%, **4H 30%**, 1D 20%, 1M 10% (4H pesa más a propósito).
- Sin `strategy.*`: es indicador, no estrategia.
- El **medio del rango cierra el gate** (evita operar por aburrimiento).

### Pendiente / próximos pasos
- Validar compilación y comportamiento en TradingView (BTC, ETH, FX, activo de bajo volumen).
- Posible V1.1: divergencias RSI sin repaint, fusión S/R por clustering, ajuste fino de umbrales por activo.

### Limitaciones conocidas
- S/R automáticos son aproximaciones. Volumen poco fiable → módulo degradado a `n/a`.
- El indicador no predice ni gestiona riesgo por sí mismo.
