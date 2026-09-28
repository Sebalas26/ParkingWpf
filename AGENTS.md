# 📜 REGLAS ESTRICTAS DE DESARROLLO Y ARQUITECTURA PARA LA IA

Este documento define las **Reglas de Oro y Estándares Obligatorios** para cualquier asistente de IA o desarrollador trabajando en este proyecto. **Estas reglas son inquebrantables.**

---

## 🛑 1. REGLA DE ORO: PLANIFICACIÓN CERRADA Y EJECUCIÓN DIRECTA (CERO DOBLE ANÁLISIS / CERO DESVIACIÓN)
1. **Fase de Planificación (`implementation_plan.md`) - Análisis Agotado al 100%**:
   - Todo diagnóstico de causa raíz, inspección de dependencias y evaluación de impacto DEBE quedar resuelto y cerrado durante la elaboración del plan.
   - El plan debe listar con precisión quirúrgica:
     a) Archivos exactos que se van a modificar, crear o eliminar.
     b) Métodos, líneas, modelos y validaciones exactas a implementar.
     c) Pruebas automatizadas y comandos de verificación final.
   - Queda prohibido dejar puntos abiertos o aplazar el análisis técnico para la fase de implementación.
2. **Aprobación del Usuario ("Proceder", "Adelante", "Aprobado") = Orden Estricta de Ejecución Directa**:
   - Al recibir la confirmación del usuario, la IA tiene **ESTRICTAMENTE PROHIBIDO volver a hacer análisis preliminares, re-diagnosticar, buscar archivos de nuevo para repensar la solución o entrar en bucles de doble procesamiento**.
   - La IA debe iniciar **INMEDIATAMENTE la ejecución física de las modificaciones pactadas en el plan**, aplicando los cambios archivo por archivo sin vacilar.
3. **Cero Tolerancia a Desviaciones o Cambios de Alcance**:
   - Durante la ejecución, la IA debe limitarse estrictamente a lo aprobado en el plan.
   - Queda prohibido inventar cambios no acordados, omitir pasos planificados o alterar la solución técnica aprobada por el usuario.

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

---

## 🧪 6. REGLA DE ORO: EJECUCIÓN OBLIGATORIA DEL 100% DE PRUEBAS UNITARIAS EN CADA CAMBIO O COMMIT
> [!CAUTION]
> **EJECUCIÓN TOTAL OBLIGATORIA (CERO OMISIONES)**: Ante CUALQUIER modificación, refactorización, corrección de bug o nueva funcionalidad en el repositorio (incluso si solo se modificó una línea, un método, un modelo, un ViewModel o un convertidor), es **ESTRICTAMENTE OBLIGATORIO ejecutar TODAS las pruebas unitarias de la solución completa** (`dotnet test ParkingWpf.slnx`).

1. **Prohibición de Pruebas Parciales o Selectivas**:
   - Está terminantemente prohibido ejecutar únicamente una clase o suite de pruebas por comodidad o asumir que el cambio no tuvo efectos colaterales. Se deben ejecutar **TODAS** las pruebas unitarias del proyecto sin excepción, sin importar el tiempo de ejecución.
2. **Cero Fallos Tolerados**:
   - La tarea **NUNCA** se dará por concluida si existe un solo fallo (`Failed > 0`) o error en los tests.
   - Todo cambio debe certificar **100% de Pruebas Superadas (0 Fallos)** y **0 Errores de Compilación** antes de responder al usuario y registrar en [`HISTORIAL_CAMBIOS.md`](file:///c:/Users/migue/source/repos/ParkingWpf/HISTORIAL_CAMBIOS.md).

---

## 🛑 7. REGLA DE ORO: PROHIBICIÓN ESTRICTA DE REGRESIONES Y ALTERACIÓN DE DISEÑOS FUNCIONALES (NO DAÑAR LO QUE YA FUNCIONA)
> [!CAUTION]
> **PROHIBICIÓN ESTRICTA DE ALTERAR ELEMENTOS PREVIAMENTE FUNCIONALES**:
> Está terminantemente prohibido modificar, "optimizar", reestructurar o cambiar estilos, contenedores, barras de navegación/pestañas, márgenes o componentes adyacentes que ya estén funcionando correctamente si el usuario no lo ha solicitado de forma expresa.

1. **Principio de Modificación Quirúrgica y Mínima**:
   - Todo cambio debe limitarse exclusivamente al componente o línea exacta requerida para cumplir la solicitud puntual del usuario.
   - Si el usuario solicita modificar un elemento específico, no se deben tocar cabeceras, pestañas, barras de herramientas ni la estructura circundante que ya se encuentre operativa.
2. **Cero Tolerancia a Regresiones Visuales**:
   - Si un componente, diálogo, vista XAML o control ya fue probado y aprobado por el usuario, **NO SE TOCA**.
   - Cada intervención debe garantizar que el comportamiento y la apariencia previa del resto de la pantalla se mantengan al 100% intactos.

---

## 🛑 8. REGLA DE ORO: PROHIBICIÓN ESTRICTA DE DATA QUEMADA (HARDCODED) Y CAMPOS PRE-LLENADOS EN FORMULARIOS
> [!CAUTION]
> **PROHIBICIÓN ESTRICTA DE QUEMAR DATA O PRE-LLENAR FORMULARIOS CON DATOS ARBITRARIOS**:
> Jamás asumir o imponer valores de negocio por defecto inventados por la IA en la base de datos o en los diálogos de captura.

1. **Formularios y Diálogos 100% Limpios (Uso Exclusivo de Placeholders)**:
   - Al crear una nueva entidad, los campos numéricos y de texto deben inicializarse estrictamente en `null` o vacíos (`''`).
   - Está terminantemente prohibido pre-llenar inputs con valores arbitrarios. Para orientar al usuario se debe utilizar **únicamente el atributo de placeholder o texto de guía**.
2. **Cero Data Quemada en Esquemas y Migraciones**:
   - Las columnas no deben imponer valores de negocio inventados en `DEFAULT` de SQL (ej: `DEFAULT 15`). Deben ser `NULL` o `DEFAULT 0` si son requeridas.
3. **Validaciones Claras de Obligatoriedad**:
   - Si un campo es obligatorio, debe validar activamente y notificar en pantalla si el usuario omite su ingreso, permitiendo ingresar `0` si la regla de negocio no aplica para esa sede.


---

## 🛑 9. REGLA DE ORO: ACTUALIZACIONES OBLIGATORIAS Y SINCRONIZACIÓN PREVIA AL 100% (CERO PÉRDIDA DE DATOS)
> [!CAUTION]
> **PROHIBICIÓN ESTRICTA DE ACTUALIZAR SIN SUBIR EL 100% DE LA INFORMACIÓN A LA NUBE**:
> Jamás aplicar una actualización de binarios si existen transacciones locales en cola (`PendingItemsCount > 0`) que no hayan sido confirmadas en el servidor central.

1. **Garantía Total de Información en la Nube (`PendingItemsCount == 0`)**:
   - Antes de iniciar la descarga y reemplazo de binarios, el sistema DEBE sincronizar forzosamente todas las transacciones locales. Si la sincronización falla por caída de red, la actualización se suspende de inmediato para proteger los datos operativos.
2. **Cero Posposición (Obligatoriedad Absoluta)**:
   - Las actualizaciones no son opcionales ni postergables (`CanCancel = false`). No existe el botón *"Posponer"*.
3. **Validación en el Login**:
   - Al abrir la aplicación o hacer clic en *"Iniciar Sesión"*, el sistema verifica si existe una nueva versión. Si hay actualización disponible, sincroniza todo a la nube y exige actualizar antes de permitir el acceso operativo a `MainShellWindow`.
4. **Sondeo Periódico en Caliente (1 Hora) y Notificación SignalR**:
   - Mientras la terminal esté en operación, el sistema sondea el API cada 1 hora y atiende eventos SignalR `AppReleaseAvailable`.
   - Al detectar una nueva versión en caliente: sincroniza al 100% las ventas/turnos, respalda la BD local, cierra formalmente la sesión del operador y lanza el actualizador, retornando a la pantalla de Login.
5. **Confirmación Visual de Sistema Actualizado**:
   - Tras el reinicio con la nueva versión, el micro-actualizador invoca `Parking.exe --updated`, desplegando en la pantalla de Login el mensaje verde: *"Listo, sistema actualizado"*.
   - Ver protocolo maestro en [`PROTOCOLO_ACTUALIZACIONES_OBLIGATORIAS.md`](file:///c:/Users/migue/source/repos/ParkingWpf/PROTOCOLO_ACTUALIZACIONES_OBLIGATORIAS.md).

---

## 🛑 REGLA DE ORO: PREVENCIÓN CRÍTICA DE FUGA DE DATOS (TENANCY LEAK)
> [!CAUTION]
> **PROHIBICIÓN ESTRICTA DE LISTADOS GLOBALES SIN FILTRO DE COMPAÑÍA O SEDE**:
> Jamás realizar consultas a la base de datos (locales o remotas) para obtener listas de usuarios, vehículos o transacciones sin aplicar explícitamente el filtro de la compañía (CompanyId) o sede (BranchId) actual.

1. **Filtrado Obligatorio en Origen**: Toda consulta LINQ o SQL que recupere colecciones (ej. db.Users.Where(...)) DEBE incluir obligatoriamente la validación de pertenencia al Tenant actual (ej. u.CompanyId == _sessionService.CurrentUser.CompanyId).
2. **Cero Suposiciones de Sincronización**: No asumas que la base de datos local SQLite solo contiene datos de una empresa. Si el usuario tiene múltiples accesos o sincroniza globales, los datos se mezclarán. El filtro debe ser a nivel de código de consulta siempre.
