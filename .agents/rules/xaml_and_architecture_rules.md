# Reglas de Recursos XAML, Multi-Sede y Arquitectura

## 1. Verificación Estricta de Recursos XAML
- NUNCA referenciar `{StaticResource Icon...}` o `{StaticResource ...Button}` sin comprobar previamente en `Parking/Styles/Icons.xaml`, `Parking/Styles/Controls.xaml` o `Parking/Styles/Brushes.xaml` que el recurso existe con esa clave exacta.
- Si se necesita un ícono vectorial nuevo, agregarlo a `Parking/Styles/Icons.xaml` primero.
- No inventar sufijos como `*Style` en botones (usar `ModernButton`, `SecondaryButton`, `OutlineButton`, `DangerButton`, `SuccessButton`).

## 2. Planificación Obligatoria
- Nunca modificar código sin antes crear `implementation_plan.md` y recibir aprobación explícita del usuario.

## 3. Registro Histórico
- Cada cambio debe quedar asentado en `HISTORIAL_CAMBIOS.md` y compilar con 0 errores.
