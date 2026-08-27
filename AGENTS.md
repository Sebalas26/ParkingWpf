# 📜 REGLAS ESTRICTAS DE DESARROLLO Y ARQUITECTURA PARA LA IA

Este documento define las **Reglas de Oro y Estándares Obligatorios** para cualquier asistente de IA o desarrollador trabajando en este proyecto. **Estas reglas son inquebrantables.**

---

## 🛑 1. REGLA DE ORO: PLANIFICACIÓN PREVIA OBLIGATORIA
1. **Nunca modificar ni crear código directamente** ante una nueva solicitud o cambio de comportamiento sin antes elaborar un **Plan de Arquitectura e Implementación** detallado (`implementation_plan.md`).
2. **Esperar siempre la aprobación explícita del usuario** antes de ejecutar cualquier edición en los archivos del proyecto.

---

## 🎨 2. INTEGRIDAD ABSOLUTA DE RECURSOS XAML (ÍCONOS, ESTILOS, PINCELES)
> [!CAUTION]
> **PROHIBICIÓN ESTRICTA**: Jamás asumir o inventar nombres de recursos `{StaticResource ...}` o `{DynamicResource ...}` en archivos XAML sin verificar primero su existencia exacta.

### Reglas para Vistas, Diálogos y Controles XAML:
1. **Verificación Previa Obligatoria**: Antes de usar cualquier `Path Data="{StaticResource Icon...}"` o `Style="{StaticResource ...}"`, se debe consultar el archivo correspondiente:
   - **Íconos y Geometrías**: [`Parking/Styles/Icons.xaml`](file:///c:/Users/miguelagutierrezg/Documents/Parking/Parking/Styles/Icons.xaml)
   - **Pinceles y Colores**: [`Parking/Styles/Brushes.xaml`](file:///c:/Users/miguelagutierrezg/Documents/Parking/Parking/Styles/Brushes.xaml)
   - **Botones, Tarjetas e Inputs**: [`Parking/Styles/Controls.xaml`](file:///c:/Users/miguelagutierrezg/Documents/Parking/Parking/Styles/Controls.xaml)
2. **Declaración Anticipada de Nuevos Íconos**:
   - Si se requiere un ícono nuevo (ej: `IconChevronRight`, `IconBuilding`, `IconCashRegister`), se **DEBE agregar primero su geometría vectorial `<Geometry x:Key="...">` en `Icons.xaml` ANTES** de enlazarlo en cualquier XAML.
3. **Nombres Exactos de Estilos de Botón**:
   - Los estilos base oficiales son:
     - `ModernButton` (Botón principal con degradado primario)
     - `SecondaryButton` / `OutlineButton` (Botón secundario / contorno)
     - `DangerButton` (Botón rojo de acción destructiva)
     - `SuccessButton` (Botón verde de acción positiva)
   - **Está prohibido** inventar nombres como `OutlineButtonStyle`, `PrimaryButtonStyle`, etc.

---

## 🏢 3. ESTÁNDARES MULTI-SEDE Y SEGURIDAD
1. **Filtrado por Sede Activa**:
   - Todo módulo operativo (Relevo de turnos, arqueo, tickets, mensualidades) debe estar estrictamente filtrado por la sede activa (`_sessionService.CurrentBranch.Id`).
2. **Acceso Global de Administradores**:
   - Los usuarios con rol Administrador tienen acceso a todas las sedes activas (`_branchRepository.GetActiveAsync()`).
   - Con 2 o más sedes, se debe presentar el selector `BranchSelectionDialog`.
3. **Sincronización Dinámica de Sesión**:
   - En cualquier relevo o cambio de operador, invocar siempre `_sessionService.SetSession(...)` y cargar la matriz de permisos en `_permissionService.LoadPermissions(...)` para evitar excepciones de *Acceso Denegado*.

---

## 🔒 4. PROHIBICIÓN ESTRICTA DE ROLES O PERMISOS QUEMADOS (HARDCODED)
> [!CAUTION]
> **PROHIBICIÓN ESTRICTA**: Jamás asumir, validar o asignar permisos mediante comparación de texto de nombres de rol (ej: `roleName.Contains("operador")`, `roleName.Contains("cajero")`, listas estáticas `OperatorPermissions`).

1. **RBAC 100% Basado en Datos**:
   - La evaluación de permisos en el sistema debe provenir exclusivamente de la matriz relacional de la base de datos (`RoleActions` / `RolePermissions` / `Action.Slug`).
   - El administrador del sistema tiene libertad absoluta de crear roles con cualquier nombre (*"Cajera Noche"*, *"Auxiliar Patio"*, *"Operario Caja"*, etc.). El código debe evaluar únicamente los slugs de permisos asignados a ese `RoleId`, sin importar el nombre del rol.

---

## 📝 5. PROTOCOLO ESTRICTO DE REGISTRO Y CONTEXTO MULTI-PC
> [!IMPORTANT]
> **PRESERVACIÓN DE CONTEXTO ENTRE COMPUTADORES**: Como el desarrollo se realiza alternando entre diferentes estaciones de trabajo (PCs), este protocolo garantiza que la IA nunca pierda el hilo técnico ni el contexto acumulado.

1. **Registro Obligatorio en Cada Modificación**:
   - Toda modificación, corrección de bug o nueva funcionalidad debe registrarse de inmediato en [`HISTORIAL_CAMBIOS.md`](file:///c:/Users/migue/source/repos/ParkingWpf/HISTORIAL_CAMBIOS.md) antes de finalizar el turno.
2. **Estructura Requerida para Cada Entrada**:
   - **`💬 Prompt Original del Usuario`**: Transcripción exacta o requerimiento solicitado por el usuario.
   - **`🤖 Resumen Técnico para la IA`**: Explicación técnica de arquitectura, contratos de datos modificados, DTOs, entidades, decisiones tomadas, estado del sistema y advertencias relevantes.
   - **`📦 Componentes Modificados`**: Lista precisa de rutas de archivos modificados, creados o eliminados.
   - **`✅ Verificación y Compilación`**: Resultado de compilación `dotnet build` (**0 Errores**) y pruebas funcionales.
3. **Directiva de Reanudación de Sesión (Nuevo PC / Nueva Conversación)**:
   - Cuando el usuario inicie en otro computador o abra un nuevo chat e indique *"Lee el historial de cambios / contexto"* o similar, la IA **DEBE LEER OBLIGATORIAMENTE `HISTORIAL_CAMBIOS.md`** como primer paso antes de elaborar planes o tocar código.
4. **Cero Errores de Compilación**:
   - Todo cambio debe compilar limpiamente con `dotnet build` (**0 Errores**) antes de dar por finalizada la tarea.
