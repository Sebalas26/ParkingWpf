# Historial Oficial de Modificaciones y Control de Cambios
**Proyecto**: ParkFlow Desktop (WPF) & API Central  
**Fecha de Creación**: 2026-08-24  

---

## 📌 Protocolo Obligatorio de Registro de Cambios
A partir del **24 de Agosto de 2026**, cualquier agente de IA, desarrollador o mantenedor que realice cambios en el código fuente de la aplicación WPF o del API **DEBE** registrar su modificación en este documento antes de finalizar su turno o tarea, incluyendo:
1. **Fecha y Hora Exacta (ISO o Local)**.
2. **Autor / Agente Responsable**.
3. **Componentes / Módulos Modificados** (archivos afectados).
4. **Tipo de Cambio**: `[FIX]`, `[FEAT]`, `[UI/UX]`, `[REFACTOR]`, `[PERF]`, `[SECURITY]`.
5. **Descripción Detallada** del problema resuelto o característica incorporada.
6. **Resultado de la Verificación** (estado de compilación y pruebas).

---

## 📋 Registro Cronológico de Cambios

### [2026-08-25 21:45:00] - [FIX] [PERF] [SYNC] - Protección contra Solapamiento de Sincronización Rápida en Background (15s) y Reutilización de Conexiones
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **💬 Prompt Original del Usuario**:
  > *"excelente lo primero funciono perfecto lo de los turnos excelente, pero sabes que no funciono mira esto el tema de las conexión me preocupa eso por que veo que no estas cerrando conexiones estas dejando conexiones abiertas en el backend eso esta gravisimos cuando hace varias operaciones ojo con eso necesito que realices un analisis completo de eso de que sucede con las conexiones.*
  > *pero eso es solo mientras tenemos el plan gratuito cierto ? pues ya cuando pasemos a un nivel diferente pues tendremos mas conexiones cierto ? ese limite ya no sería necesario por que pasar de 15 a 60 enserio coloca lento el sistema"*
- **🤖 Resumen Técnico para la IA**:
  1. **Preservación de Sincronización Rápida (15 Segundos)**:
     - Se mantuvo el temporizador de background sync en **15 segundos** (`TimeSpan.FromSeconds(15)`) para garantizar una experiencia en tiempo casi real de la terminal POS sin demoras.
  2. **Protección Anti-Solapamiento en `BackgroundSyncScheduler.cs`**:
     - Se implementó un semáforo / bandera de control `_isSyncInProgress` para evitar que peticiones de sincronización lentas por latencia de red se solapen o encolen ráfagas simultáneas al API Central.
  3. **Reutilización Eficiente de Conexiones**:
     - Se coordinó con la optimización del Connection Pool de `ParkingApi` (`MaximumPoolSize=12`, `ConnectionIdleTimeout=5`), permitiendo que las sincronizaciones rápidas reutilicen conexiones calientes en milisegundos sin alcanzar la cuota `max_user_connections (20)` de MySQL.
- **📦 Componentes Modificados**:
  - `Parking/Services/Implementations/BackgroundSyncScheduler.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build Parking\Parking.csproj` -> **0 Errores** (Compilación Correcta).

### [2026-08-25 21:30:00] - [FEAT] [FIX] [MULTI-BRANCH] [UI/UX] - Independencia Total Multi-Sede de Turnos (WorkShifts), Corrección de Binding TwoWay en Cierre y Rediseño Compacto del TopBar
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **💬 Prompt Original del Usuario**:
  > *"mira como se ve de feo eso, segundo fui a cerrar turno en una sede yo como administrador y mira como salio el error y eso daño todo el sistema. analiza eso y revisa bien como funciona eso por que no esta funcionando completamente bien.*
  > *tengo otra duda, se supone que los turnos son igual independientes de sedes claro ? eso espero sea claro si ? un turno pertenece a una sede especifica."*
- **🤖 Resumen Técnico para la IA**:
  1. **Aislamiento Multi-Sede Estricto de Turnos de Trabajo (`WorkShift`)**:
     - Se clarificó e implementó la regla de negocio: cada turno pertenece exclusiva y aisladamente a una sede (`BranchId`). El dinero en gaveta, arqueo, tickets calculados y retiros de caja corresponden únicamente a la sede activa.
     - En `EfShiftService.cs` se inyectó `ISessionService` (`_sessionService.CurrentBranch?.Id`).
     - Al abrir turno (`OpenShiftAsync`) y en el relevo (`HandoverAndOpenNextShiftAsync`) se asigna de forma explícita el `BranchId` de la sede activa.
     - Se actualizaron las consultas locales de SQLite (`GetActiveShiftAsync`, `GetLastClosedShiftAsync`, `GetShiftHistoryAsync`) para filtrar obligatoriamente por `s.BranchId == currentBranchId`.
     - En `GetCurrentShiftSummaryAsync`, el cálculo de balance y desglose por método de pago filtra estrictamente los tiquetes por `t.BranchId == currentBranchId`.
     - En `ShiftClosureViewModel.cs` se agregó suscripción a `_sessionService.ActiveBranchChanged` para recargar automáticamente el arqueo y balance cuando el usuario cambia de sede activa.
  2. **Corrección de Excepción en Cierre de Turno (`ShiftClosureView.xaml`)**:
     - Se identificó que `<Run Text="{Binding LastClosedShift.EndTime, StringFormat='...'}" />` en WPF intentaba enlazar por defecto con `Mode=TwoWay` contra una propiedad calculada de solo lectura (`EndTime`), causando fallos de DataBinding. Se corrigió a `Mode=OneWay` explícito. Se revisaron y blindaron todos los `<Run>` del proyecto (`CheckInView.xaml`).
  3. **Refinamiento Estético del TopBar (`MainShellWindow.xaml`)**:
     - Se rediseñó el TopBar para que sea compacto (altura optimizada, padding 16,6), uniforme y sin duplicidades.
     - A la izquierda se presenta la Sede Activa con su icono y botón compacto para cambiar de sede si el usuario tiene múltiples sedes asignadas.
     - A la derecha se alinean ordenadamente las píldoras de telemetría: estado de sincronización con API Central, botón de sincronización manual, indicador de ocupación en tiempo real y reloj digital en vivo.
- **📦 Componentes Modificados**:
  - `Parking/Services/Contracts/IApiClientService.cs`
  - `Parking/Services/Implementations/ParkingApiClient.cs`
  - `Parking/Services/Implementations/EfShiftService.cs`
  - `Parking/Models/ApiModels/ShiftApiModels.cs`
  - `Parking/ViewModels/ShiftClosureViewModel.cs`
  - `Parking/Views/ShiftClosureView.xaml`
  - `Parking/Views/CheckInView.xaml`
  - `Parking/Views/MainShellWindow.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build Parking\Parking.csproj` -> **0 Errores** (Compilación Correcta).

### [2026-08-25 21:00:00] - [FEAT] [UI/UX] [BRANDING] - TitleBar Moderno Personalizado, Icono de Aplicación (.ICO) PARK POINT y Soporte de Logo de Sede (Base64)
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Mira en la primera imagen se ve supremamente mal el tema del diseño de la parte de arriba sigue siendo wpf pero sin diseño sin nada eso se ve mal si me explico.*
  > *en la seguna imagen no tiene logo si revisas el codigo de la PWA ves que cuando crea las sedes debe subir el logo que deberia tener entonces usar un logo sii que sea configurable o que tengamos un logo en el sistema o no se si puedas usar un ico o algo dime que se puede hacer hay pues lo digo por que cada sede tiene un logo.*
  > *y si ves la 3 imagen no tiene esa columna para el logo entonces eso como se va a subir donde se esta guardando eso deberia guardarse en base 64 comprimido para que se pueda leer desde la bd y sin generar tanto consumo de espacio si analiza eso recuerda que como regla de oro si no esta en el agent deberia estar no vas a tocar el pwa si mi autorizacion."*
- **🤖 Resumen Técnico para la IA**:
  1. **Barra de Título Moderna Personalizada (Custom TitleBar con WindowChrome)**:
     - En `MainShellWindow.xaml` se configuró `WindowChrome` con `CaptionHeight="38"`, `UseAeroCaptionButtons="False"`, eliminando el marco blanco/gris nativo de Windows.
     - Se creó una barra de título integrada en color Grafito Carbón (`#152024` / `#1E2A2F`) que luce el isotipo PARK POINT en Verde Esmeralda (`#00867A`), el título del sistema (*"PARK POINT • Terminal POS de Control de Acceso y Caja"*) y botones estilizados de control de ventana (Minimizar `—`, Maximizar/Restaurar `▢` y Cerrar `✕` con efecto hover rojo `#DC2626`).
     - Se mantuvo el soporte para arrastre suave de ventana (`DragMove`) y doble clic para maximizar/restaurar.
  2. **Icono Oficial de la Aplicación (.ICO Multi-Resolución)**:
     - Se generó el archivo de icono vectorial multi-resolución `parkpoint.ico` (16x16, 32x32, 48x48, 64x64, 128x128, 256x256) con la insignia oficial PARK POINT y se configuró como `<ApplicationIcon>` en `Parking.csproj`.
     - Se asignó `Icon="/Resources/parkpoint.ico"` en `MainShellWindow.xaml` y `LoginWindow.xaml` (usando la ruta absoluta de recurso BAML para evitar resolución relativa a la carpeta `Views/`) asegurando presencia de marca en la barra de tareas y el marco de ventanas.
  3. **Soporte de Logo por Sede (Base64) y Convertidor de Imagen**:
     - Se creó `Base64ToImageConverter.cs` en `Parking/Core/Converters/` y se registró como recurso global `Base64ToImageConv` en `App.xaml` para decodificar fluidamente cadenas Base64 a `BitmapImage` con caché en memoria `OnLoad` y `Freeze()`.
     - Se agregó la propiedad `LogoBase64` en las entidades `Branch` (SQLite), `BranchModel`, `ApiBranchSyncDto` y en `SyncEngineService.cs` para persistir el logo de cada sede durante el bootstrap.
     - En `MainShellWindow.xaml` (Sidebar Header) y en `BranchSelectionDialog.xaml` (Tarjetas de Sedes), se configuró la visualización dinámica del logo personalizado en Base64 con fallback elegante al icono vectorial por defecto si es nulo.
  4. **Preservación de la PWA**: De acuerdo a la directiva estricta del usuario, no se tocó ningún archivo de `ParkingPwa`.
- **📦 Componentes Modificados**:
  - `Parking/Parking.csproj`
  - `Parking/Resources/parkpoint.ico`
  - `Parking/Core/Converters/Base64ToImageConverter.cs`
  - `Parking/App.xaml`
  - `Parking/Entities/Branch.cs`
  - `Parking/Models/BranchModel.cs`
  - `Parking/Models/ApiModels/BootstrapSyncResponse.cs`
  - `Parking/Services/Implementations/SyncEngineService.cs`
  - `Parking/Views/MainShellWindow.xaml`
  - `Parking/Views/LoginWindow.xaml`
  - `Parking/Views/BranchSelectionDialog.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build ParkingWpf.slnx` -> **0 Errores**.
  - `dotnet build ParkingApi.slnx` -> **0 Errores**.

### [2026-08-25 20:45:00] - [FEAT] [UI/UX] [MULTI-BRANCH] - Capacidad Real de Sede, Escalado Global de Tipografía (+2px), Remoción de Botón X y Banner Amarillo Informativo
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **💬 Prompt Original del Usuario**:
  > *"mira que si esta la capacidad del parqueadero pero veo que el wpf no la trae dice sin configurar esas cosas no deberian salir así. aparte toda la letra del sistema necesito que me le subas 2 px mas a cada letra si alguna tiene 8 pues queda en 10 y la de 10 en 12 si me hago entender , este boton no deberia estar toca quitarlo, este mensaje no deberia ser así de ese color por que no es error es algo informativo deberia ser amarillo. ya con eso procede a crear el plan"*
- **🤖 Resumen Técnico para la IA**:
  1. **Capacidad de Parqueadero Multi-Sede y Ocupación en Tiempo Real**:
     - En `EfParkingTicketService.cs` se inyectó `ISessionService` y se implementó la obtención de la capacidad real desde la sede activa (`_sessionService.CurrentBranch?.TotalCapacity`) o la tabla local `Branches`, calculando los cupos disponibles (`TotalSpots - OccupiedSpots`).
     - Se añadieron en `OccupancyStats.cs` las propiedades puente `AvailableSlots => AvailableSpots` y `OccupiedSlots => OccupiedSpots` para resolver los enlaces de datos (bindings) en `CheckInView.xaml`, `MainShellWindow.xaml` y demás módulos.
     - Se asignó `ticket.BranchId = activeBranchId` en `RegisterEntryAsync` y se filtraron los vehículos activos de la sede en `GetOccupancyStatsAsync()`.
     - En `TicketApiModels.cs` se agregaron los campos `BranchId`, `HourlyRate` y alias `CustomerPhone` para alineación multi-sede contra la API central.
  2. **Escalado Global de Tipografía (+2px en todo el sistema)**:
     - En `Parking/Styles/Typography.xaml`: Se incrementaron todas las escalas base en +2px (`TextHeaderLarge` a 26, `TextHeaderMedium` a 20, `TextHeaderSmall` a 17, `TextBodyDefault` a 15, `TextBodySecondary` a 14, `TextCaption` a 13, `TextStatNumber` a 30, `TextPlateDisplay` a 22, `TextBadge` a 13).
     - En `Parking/Styles/Controls.xaml`: Se subieron +2px a los tamaños de control (`ModernTextBox` 15, `SearchPillTextBox` 14, `PlateInputTextBox` 60, `CheckoutSearchTextBox` 30, `ModernPasswordBox` 16, `ModernButton` 15, `SidebarNavButton` 15, `FilterChipRadioButton` 14, `ModernComboBox` 15, `ModernDataGrid` 15, `DataGridColumnHeader` 13).
     - En las vistas XAML (`CheckInView.xaml`, `CheckOutView.xaml`, `MainShellWindow.xaml`, `BranchSelectionDialog.xaml`): Se incrementaron todos los `FontSize` inline en +2px (8->10, 9->11, 10->12, 11->13, 12->14, 13->15, 14->16, 16->18, 18->20, 20->22, 24->26, 26->28, 32->34, 44->46).
  3. **Eliminación del Botón ✕ de Búsqueda en Caja/Salida**:
     - En `CheckOutView.xaml`, se removió el botón `ClearSearchCommand` con texto `✕`, reajustando la cuadrícula del buscador a dos columnas (`*` para el campo de búsqueda masivo y `Auto` para el botón de búsqueda con lupa).
  4. **Banner Informativo / Advertencia en Amarillo Institucional**:
     - En `CheckInView.xaml`, se modificaron los triggers de retroalimentación para que ante mensajes no exitosos (`IsSuccessFeedback == false`) se utilicen los recursos institucionales `BrushWarningBg` (`#FFF8E1`), `BrushWarning` (`#FFC107`) y `BrushWarningText` (`#8A6D00`) en lugar de colores rojos de error destructivo.
- **📦 Componentes Modificados**:
  - `Parking/Models/OccupancyStats.cs`
  - `Parking/Services/Implementations/EfParkingTicketService.cs`
  - `Parking/Models/ApiModels/TicketApiModels.cs`
  - `Parking/Styles/Typography.xaml`
  - `Parking/Styles/Controls.xaml`
  - `Parking/Views/CheckOutView.xaml`
  - `Parking/Views/CheckInView.xaml`
  - `Parking/Views/MainShellWindow.xaml`
  - `Parking/Views/BranchSelectionDialog.xaml`
- **✅ Verificación y Compilación**:
  - `dotnet build ParkingWpf.slnx` -> **0 Errores**.
  - `dotnet build ParkingApi.slnx` -> **0 Errores**.

### [2026-08-25 20:15:00] - [FIX] [SYNC] [MULTI-PC] - Corrección de Fallo de Sincronización Bootstrap y Establecimiento de Protocolo de Contexto Multi-PC
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **💬 Prompt Original del Usuario**:
  > *"oye por que sale que no se tiene el servidor no respondio, pues si arria dice, eso deberia ya estar claro osea que si esta conectada la api osea que paso ? eso lo probe y estaba funcionando ahorita pero ahora no funciona, que suecede sabes que otra 0cosa pasa es que como estoy trabajando en dos lugares entonces creo que se esta perdiendo el contexto y eso esta terrible no sirve necesito que se cree un archivo en ese agent que se creo que son reglas donde diga que cada cambio nuevo o realizado debe crear en un archivo de registros con el promp que se hizo o el resumen que la IA entienda y cuando yo me encuentre en otro pc pues le diga que lo lea y tenga todo entendido lo ultimo que realizamos eso aplica tanto para el wpf y el api si me explico, ya con esto crea un plan completo y detallado."*
- **🤖 Resumen Técnico para la IA**:
  1. **Causa del Fallo de Sincronización**:
     - El indicador superior se mostraba *"API Central Online • Sincronizado"* porque `PingAsync()` contra `/api/health` respondía `200 OK`.
     - Sin embargo, la sincronización fallaba en el Paso 3 (`/api/sync/bootstrap`) debido a una excepción de deserialización JSON en WPF: la API serializaba `WorkShift.Status` como string (`"Open"`/`"Closed"`) mientras WPF lo esperaba como `int`, y enums como `PaymentMethod` tenían valores dispares (`Transfer` vs `DigitalTransfer`).
     - `ParkingApiClient.GetBootstrapAsync()` capturaba silenciosamente la excepción devolviendo `null`, activando el mensaje *"Respuesta incompleta / El servidor no entregó los paquetes de sincronización requeridos"*.
  2. **Arquitectura y Solución Aplicada**:
     - Se crearon DTOs dedicados y desacoplados en `BootstrapSyncResponse.cs` (`ApiBranchSyncDto`, `ApiUserSyncDto`, `ApiPaymentMethodSyncDto`, `ApiVehicleRateSyncDto`, `ApiStoreSyncDto`, `ApiCommercialAgreementSyncDto`, `ApiWorkShiftSyncDto`, `ApiMonthlySubscriptionSyncDto`, `ApiParkingTicketSyncDto`) con métodos normalizadores tolerantes a números, cadenas, nulos y conversiones de enum.
     - Se configuró `JsonSerializerOptions` con `JsonNumberHandling.AllowReadingFromString` y logging de excepciones de diagnóstico en `ParkingApiClient.cs`.
     - Se enriqueció `SyncEngineService.cs` para sincronizar sedes (`Branches`), medidores, tarifas, comercios, convenios, suscripciones, turnos y tiquetes de manera 100% segura en SQLite.
  3. **Protocolo Multi-PC**:
     - Se actualizaron las reglas en `AGENTS.md` de ambos repositorios (`ParkingWpf` y `ParkingApi`) estipulando la directiva mandatoria de registrar en cada tarea el prompt original + resumen técnico para la IA, garantizando que al cambiar de PC y solicitar "Lee el historial de cambios / contexto", la IA reconstruya todo el contexto sin lagunas.
- **📦 Módulos Modificados**:
  - `Parking/Models/ApiModels/BootstrapSyncResponse.cs`: DTOs resilientes y desacoplados.
  - `Parking/Services/Implementations/ParkingApiClient.cs`: Robustez en JSON parser y logging.
  - `Parking/Services/Implementations/SyncEngineService.cs`: Mapeo normalizado y soporte para sincronización de sedes.
  - `AGENTS.md`: Protocolo estricto de preservación de contexto entre computadores.
  - `HISTORIAL_CAMBIOS.md`: Registro oficial con formato prompt + AI summary.
- **✅ Verificación y Compilación**:
  - `dotnet build ParkingWpf.slnx` -> **0 Errores**.
  - `dotnet build ParkingApi.slnx` -> **0 Errores**.

### [2026-08-25 17:53:00] - [FIX] [WPF] [UI] - Corrección de XamlParseException en BranchSelectionDialog
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **Módulos Afectados**:
  - `Parking/Styles/Icons.xaml`: Inclusión del recurso vectorial `IconChevronRight`.
  - `Parking/Views/BranchSelectionDialog.xaml`: Corrección del recurso de botón (`SecondaryButton` en vez de `OutlineButtonStyle`) e iconos de sede a `IconBuilding`.
- **Descripción**:
  1. Se corrigió la excepción `System.Windows.Markup.XamlParseException` que ocurría en `InitializeComponent()` de `BranchSelectionDialog` al abrir el modal de selección de sedes.
- **Verificación**: `dotnet build ParkingWpf.slnx` -> Compilación con **0 errores**.

### [2026-08-25 17:34:00] - [FEAT] [MULTI-BRANCH] [AUTH] - Retorno Global de Sedes para Administradores y Filtrado de Operadores por Sede Activa
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **Módulos Afectados**:
  - `ParkingApi.Core`: `AuthService.cs`, `BranchService.cs`
  - `ParkingApi.Domain`: `IBranchRepository.cs`, `IBranchService.cs`
  - `ParkingApi.Infrastructure`: `BranchRepository.cs`
  - `ParkingApi`: `BranchesController.cs`
  - `Parking (WPF)`: `IApiClientService.cs`, `ParkingApiClient.cs`, `ShiftClosureViewModel.cs`
- **Descripción**:
  1. **Acceso Global para Administradores**: Se actualizó el endpoint de login en el backend para que los usuarios con rol Administrador reciban siempre el 100% de las sedes activas (`_branchRepository.GetActiveAsync()`). Con 2 o más sedes activas, la terminal WPF despliega de forma automática el modal emergente `BranchSelectionDialog` en el login.
  2. **Endpoint de Operadores por Sede**: Se implementó el endpoint `GET /api/branches/{id}/users` para consultar los operadores asignados a cada sede en `UserBranches` (junto con los administradores globales).
  3. **Filtrado Dinámico en Relevos**: En la pantalla de **Control de Turnos** de la terminal, la lista de operadores disponibles para entrega de caja ahora se filtra estrictamente por la sede activa (`CurrentBranch.Id`), impidiendo transferir turnos a operarios de otras sedes.
- **Verificación**:
  - `dotnet build ParkingApi.slnx` -> Compilación con **0 errores**.
  - `dotnet build ParkingWpf.slnx` -> Compilación con **0 errores**.

### [2026-08-25 17:19:00] - [FEAT] [AUTH] [SECURITY] - Sincronización Completa de Sesión en Relevo de Turno y Validación Estricta de Permisos de Operadores
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **Módulos Afectados**:
  - `Parking/Services/Implementations/AuthService.cs`: Sincronización total de sesión en `SwitchCurrentUser` y asignación de matriz de permisos operativos para el rol de Operador.
  - `Parking/ViewModels/ShiftClosureViewModel.cs`: Filtrado de operadores con roles operativos en `AvailableUsers`, validación de permisos del receptor previo al relevo y recarga de balance/historial.
  - `Parking/ViewModels/MainShellViewModel.cs`: Validación de titularidad de caja activa para impedir que un operador ajeno facture sobre un turno que no le pertenece sin previo relevo.
- **Descripción**:
  1. **Actualización en Caliente de la UI**: Al relevar el turno, la sesión se sincroniza con `_sessionService`, actualizando de inmediato el Avatar, Nombre y Rol en el pie de página.
  2. **Resolución de Acceso Denegado**: Se cargan los slugs operativos de terminal para operadores (`checkin.*`, `checkout.*`, `subscriptions.*`, `shift.*`, etc.), permitiendo que el operador entrante continúe facturando sin excepciones de autorización.
  3. **Protección de Gaveta y Caja**: Solo usuarios activos con roles operativos pueden recibir turnos. Se bloquea la operación cruzada entre operadores si no se ha realizado la entrega de turno formal.
- **Verificación**: `dotnet build ParkingWpf.slnx` -> Compilación con **0 errores**.

### [2026-08-25 16:03:00] - [FEAT] [WPF] [SECURITY] - Forzado Obligatorio de Apertura de Turno y Guardias de Navegación Operativa
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **Módulos Afectados**:
  - `Parking/ViewModels/MainShellViewModel.cs`: Validación estricta de turno en arranque (`InitializeAsync`) y guardias en `NavigateToCheckIn`, `NavigateToCheckOut`, `NavigateToMonthlySubscriptions`.
- **Descripción**:
  1. **Arranque Guiado**: Al iniciar sesión en la terminal sin turno abierto, el sistema redirige automáticamente a la pantalla de **Control de Turnos** y emite la alerta solicitando ingresar la base inicial de caja.
  2. **Bloqueo Estricto de Navegación Operativa**: Se bloquea el acceso a *Ingreso de Vehículos*, *Salida y Cobro* y *Mensualidades* si no hay un turno operativo abierto, manteniendo al operador en la pantalla de turnos hasta su apertura.
  3. **Flujo Fluido**: Tras abrir el turno en `ShiftClosureViewModel`, la terminal redirige automáticamente a *Ingreso de Vehículos* lista para la operación.
- **Verificación**: `dotnet build ParkingWpf.slnx` -> Compilación con **0 errores**.

### [2026-08-25 15:53:00] - [FIX] [WPF] [UI] - Inclusión de Recurso Vectorial IconBuilding en Icons.xaml
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **Módulos Afectados**:
  - `Parking/Styles/Icons.xaml`: Geometría vectorial para `IconBuilding`.
- **Descripción**:
  - Se corrigió la excepción `XamlParseException` en la línea 238 de `MainShellWindow.xaml` por falta del recurso `IconBuilding` (utilizado en la píldora informativa de Sede Activa en el TopBar).
- **Verificación**: `dotnet build ParkingWpf.slnx` -> Compilación con **0 errores**.

### [2026-08-25 15:50:00] - [FIX] [WPF] [UI] - Corrección de XamlParseException por Recursos de Iconos Faltantes (IconCalendar, IconCashRegister)
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **Módulos Afectados**:
  - `Parking/Styles/Icons.xaml`: Inclusión de las geometrías vectoriales `IconCalendar` e `IconCashRegister`.
- **Descripción**:
  - Se corrigió la excepción `System.Windows.Markup.XamlParseException` que se producía al abrir la ventana principal `MainShellWindow` luego del login.
  - La excepción ocurría en la línea 111 de `MainShellWindow.xaml` porque los botones de navegación de *Mensualidades* y *Control de Turnos* hacían referencia estática a `IconCalendar` e `IconCashRegister` que no estaban definidos en el diccionario de recursos vectoriales.
- **Verificación**: `dotnet build ParkingWpf.slnx` -> Compilación con **0 errores**.

### [2026-08-25 15:46:00] - [FEAT] [AUTH] [SECURITY] - Soporte de Autenticación Flexible Híbrida (Email o Username) y Unificación de DTOs
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **Módulos Afectados**:
  - `ParkingApi.Domain`: `IUserRepository.cs`
  - `ParkingApi.Infrastructure`: `UserRepository.cs`
  - `ParkingApi.Core`: `AuthService.cs`
  - `Parking (WPF)`: `TicketApiModels.cs`, `UserSessionModel.cs`, `AuthService.cs`
- **Descripción**:
  1. **Búsqueda Flexible en Backend**: Se implementó `GetByIdentifierAsync` en `UserRepository`, permitiendo que tanto la PWA (`login-mobile`) como el cliente WPF (`login`) acepten indistintamente el nombre de usuario (`admin`) o el correo electrónico (`admin@parkflow.local`).
  2. **Alineación de Tipos de Identificadores**: `LoginApiResponse` en WPF se sincronizó con el tipo `int UserId` de la base de datos MySQL, resolviendo las excepciones de deserialización JSON de forma limpia sin alterar el esquema relacional de la base de datos.
- **Verificación**:
  - `dotnet build ParkingApi.slnx` -> Compilación con **0 errores**.
  - `dotnet build ParkingWpf.slnx` -> Compilación con **0 errores**.

### [2026-08-25 14:38:00] - [FIX] [API] - Corrección de Error 500 en Swagger / OpenAPI (/openapi/v1.json)
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **Módulos Afectados**:
  - `ParkingApi/Program.cs`: Configuración de `AddSwaggerGen` con `ResolveConflictingActions`, `CustomSchemaIds` y mapeo de endpoint `/swagger/v1/swagger.json`.
- **Descripción**:
  - Se corrigió la excepción HTTP 500 que impedía cargar la definición de la API en Swagger UI. El error se originaba por la colisión de rutas múltiples y sobrecargas de endpoints en controladores heredados (`UsersController`, `PaymentMethodController`, `RoleActionsController`, etc.) en el generador básico de .NET.
  - Se configuró la resolución automática de conflictos de acciones y schemas en Swashbuckle, garantizando la renderización limpia de todos los endpoints de `BranchesController`, `AuthController`, etc.
- **Verificación**: `dotnet build ParkingApi.slnx` -> Compilación con **0 errores**.

### [2026-08-25 14:27:00] - [UI/UX] [BRANDING] [SECURITY] - Identidad Visual Oficial PARK POINT, Eliminación de Selector de Temas y Guardia de Login sin Sedes
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **Módulos Afectados**:
  - `Parking/Styles/Colors.xaml`: Reemplazo total con paleta oficial de PARK POINT (Verde `#00867A`, Grafito `#1E2A2F`, Gris Concreto `#B9B9B9`, Blanco `#FFFFFF`, Amarillo `#FFC107`, Fondo Neutro `#F4F6F7`).
  - `Parking/Services/Implementations/ThemeService.cs`: Fijación de la paleta institucional única y eliminación de temas alternos.
  - `Parking/ViewModels/LoginViewModel.cs`: Bloqueo total de acceso en login ante 0 sedes registradas o sin sedes asignadas con mensaje explicativo; remoción de selector de temas.
  - `Parking/ViewModels/MainShellViewModel.cs`: Limpieza de comandos y propiedades de temas; navegación con slugs unificados; soporte de cambio de sede interactivo.
  - `Parking/Views/LoginWindow.xaml`: Actualización a marca PARK POINT ("TU PUNTO DE LLEGADA"), remoción de botones de temas, modernización de banner.
  - `Parking/Views/MainShellWindow.xaml`: Actualización de títulos y banners a PARK POINT, remoción de combobox de temas, paleta en sidebar Grafito y acentos en Verde.
  - `Parking/Views/BranchSelectionDialog.xaml` y `ReceiptPreviewDialog.xaml`: Sombra Grafito y membrete oficial PARK POINT.
- **Descripción**:
  1. **Identidad Visual Corporativa**: Adoptada la marca oficial **PARK POINT** con el lema *"TU PUNTO DE LLEGADA"* y la paleta de materiales exacta.
  2. **Eliminación de Cambio de Temas**: Se eliminaron los selectores de temas en Login y MainShell para mantener una estética consistente y profesional.
  3. **Guardia de Login sin Sedes**: Al intentar loguearse sin sedes registradas, la terminal bloquea el acceso e instruye al usuario a crear la primera sede desde la PWA.
- **Verificación**:
  - `dotnet build ParkingWpf.slnx` -> Compilación con **0 errores**.
  - `dotnet build ParkingApi.slnx` -> Compilación con **0 errores**.

### [2026-08-25 12:35:00] - [FEAT] [ARCH] [SECURITY] - Arquitectura Multi-Sede (Parqueaderos), Catálogo Maestro RBAC Real y Autorización Declarativa en WPF
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **Módulos Afectados**:
  - `ParkingApi.Domain`: `Branch.cs`, `UserBranch.cs`, `BranchPaymentMethod.cs`, `VehicleRate.cs`, `ParkingTicket.cs`, `WorkShift.cs`, `Store.cs`, `MonthlySubscription.cs`, `User.cs`
  - `ParkingApi.Infrastructure`: `DataContext.cs`, `EntityConfigurations.cs`, `BranchRepository.cs`, `RepositoryExtensions.cs`
  - `ParkingApi.Core`: `BranchService.cs`, `AuthService.cs`, `SyncService.cs`, `ServiceExtensions.cs`
  - `ParkingApi`: `BranchesController.cs`
  - `ParkingApi/Scripts`: `01_Reset_Database_DDL.sql`, `02_Create_MultiBranch_Tables.sql`, `03_Seed_RBAC_Full_Catalog.sql`
  - `Parking (WPF)`: `Branch.cs`, `UserBranch.cs`, `BranchPaymentMethodEntity.cs`, `ParkFlowDbContext.cs`, `BranchModel.cs`, `LoginResultModel.cs`, `ISessionService.cs`, `SessionService.cs`, `IPermissionService.cs`, `PermissionService.cs`, `Authorize.cs` (Attached Property), `BranchSelectionDialog.xaml/.cs`, `LoginViewModel.cs`, `MainShellViewModel.cs`, `MainShellWindow.xaml`, `App.xaml.cs`
- **Descripción**:
  1. **Modelo Multi-Sede y Parametrización por Parqueadero**:
     - Creadas entidades de dominio y SQLite `Branch`, `UserBranch` (relación N:N usuario-sede) y `BranchPaymentMethod` (activación de medios de pago por sede).
     - Incorporado `BranchId` en `VehicleRates`, `ParkingTickets`, `WorkShifts`, `Stores`, `MonthlySubscriptions` con Fluent API limpio y seguro (cero `HasData`).
  2. **Flujo de Autenticación y Contexto de Sesión Multi-Sede**:
     - `LoginViewModel` maneja los 3 casos de acceso: 0 sedes (bloqueo informativo), 1 sede (login directo automático) y >1 sedes (modal interactivo `BranchSelectionDialog` con diseño premium de tarjetas).
     - Implementado `ISessionService` para mantener la sede activa en memoria y permitir cambio de sede en caliente.
  3. **Scripts SQL DDL y Catálogo RBAC Puro e Idempotente**:
     - `01_Clean_All_Tables.sql`: Limpieza segura con `FOREIGN_KEY_CHECKS = 0` para reseteo completo de tablas.
     - `02_Create_MultiBranch_Tables.sql`: DDL completo para creación directa en MySQL de tablas Multi-Sede.
     - `02_Init_RBAC_Seed.sql`: Script SQL **único, oficial y completo** de inicialización RBAC (WPF & PWA) con 13 módulos, 7 operaciones, 48 acciones reales y asignación Full Access (100%) al Administrador, bajo la premisa de **Zero-Data Bootstrap** (sin precarga de sedes, medios de pago ni tarifas).
  4. **Infraestructura de Autorización y Guardias en WPF**:
     - `IPermissionService` y `PermissionService` para evaluación en memoria de permisos.
     - Attached Property `security:Authorize.Permission="Modulo.Accion"` para ocultar/deshabilitar controles en XAML de forma reactiva.
     - Guardias de navegación en `MainShellViewModel` que previenen el ingreso no autorizado a módulos restringidos.
- **Verificación**:
  - `dotnet build ParkingApi.slnx` -> Compilación exitosa con **0 errores**.
  - `dotnet build ParkingWpf.slnx` -> Compilación exitosa con **0 errores**.

### [2026-08-25 11:32:00] - [FEAT] [FIX] [DATA] - Sincronización Total Universal de Todas las Tablas (100%), Corrección de WorkShift.EndTime y Arquitectura Online-First
- **Autor**: Antigravity AI Assistant
- **Módulos Afectados**:
  - `ParkingApi.Domain/Dtos/Sync/SyncDtos.cs` (`BootstrapSyncDto`)
  - `ParkingApi.Core/Services/Sync/SyncService.cs` (`GetBootstrapDataAsync`)
  - `Parking/Models/ApiModels/BootstrapSyncResponse.cs` (`BootstrapSyncResponse`)
  - `Parking/Core/Enums/VehicleType.cs` (Adición de alias `Truck = 2`)
  - `Parking/Services/Contracts/ISyncEngineService.cs` (`DataSynchronized`, métricas ampliadas)
  - `Parking/Services/Implementations/SyncEngineService.cs` (Sincronización por fases de 100% de tablas: Roles, Usuarios, Medios de Pago, Tarifas, Comercios, Convenios, Suscripciones Mensuales, Turnos y Tiquetes)
  - `Parking/Services/Implementations/ParkingApiClient.cs` (Timeout robusto de 5s en Ping y 12s en Bootstrap)
  - `Parking/Services/Implementations/EfPricingCalculatorService.cs` (Recarga en caliente ante `DataSynchronized`)
  - `Parking/Views/ShiftClosureView.xaml`, `AnalyticsView.xaml`, `RecentEntriesView.xaml`, `MonthlySubscriptionsView.xaml` (`IsReadOnly="True"`, `Mode=OneWay` en propiedades calculadas `StartTime`, `EndTime`, `CustomerPhone`, `EntryTime`, `ExitTime`)
  - `Parking/ViewModels/CheckInViewModel.cs`, `CheckOutViewModel.cs`, `MonthlySubscriptionsViewModel.cs`, `ShiftClosureViewModel.cs` (Suscripción a recarga reactiva de datos en caliente)
- **Descripción**:
  1. **Regla de Sincronización Total (100% de Tablas)**: Se implementó la descarga y persistencia atómica de todas las entidades del sistema (Usuarios, Medios de Pago de PWA/API, Tarifas Vehiculares, Comercios, Convenios, Turnos de Trabajo, Mensualidades y Tiquetes de Acceso).
  2. **Recarga Reactiva en Caliente**: Al sincronizar, se notifica mediante `DataSynchronized` a todos los ViewModels y al servicio de tarifas para actualizar la interfaz de inmediato sin reiniciar la aplicación.
  3. **Corrección de Excepción TwoWay en DataGrids**: Se configuró `IsReadOnly="True"` y `Mode=OneWay` en todas las columnas que apuntaban a propiedades de solo lectura (`WorkShift.EndTime`, etc.), eliminando la `InvalidOperationException`.
  4. **Operación Online-First**: Operación prioritaria contra API Central con respaldo offline transparente sin bloqueos.
- **Verificación**:
  - `dotnet build ParkingApi.slnx` -> Compilación con **0 errores**.
  - `dotnet build ParkingWpf.slnx` -> Compilación con **0 errores**.

---
- **Autor**: Antigravity AI Assistant
- **Módulos Afectados**:
  - `Parking/App.xaml.cs` (`DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`, `AppDomain.UnhandledException`, `LogException`)
- **Descripción**:
  - Se implementó la arquitectura de contención global de errores para la aplicación WPF:
    1. **Contención en Hilo UI (`DispatcherUnhandledException`)**: Marca `e.Handled = true`, previniendo que cualquier excepción imprevista en vistas o enlaces XAML termine o cierre la aplicación de forma abrupta.
    2. **Contención en Tareas Asíncronas (`UnobservedTaskException`)**: Marca `e.SetObserved()` para evitar caídas del proceso originadas en hilos secundarios.
    3. **Registro Automático en Disco (`Logs/ErrorLog_yyyyMMdd.txt`)**: Toda excepción es registrada con fecha, hora exacta, tipo de error, mensaje, traza de pila (stack trace) e inner exception.
    4. **Notificación Visual No Bloqueante**: Se despliega una alerta al operador indicándole la novedad y permitiéndole continuar su flujo de trabajo normalmente.
- **Verificación**: Compilación con `dotnet build ParkingWpf.slnx` con **0 errores y 0 advertencias**.

---

### [2026-08-24 16:38:00] - [FIX] [UI/UX] - Corrección de XamlParseException en CheckInView (Línea 411 IconAlertTriangle)
- **Autor**: Antigravity AI Assistant
- **Módulos Afectados**:
  - `Parking/Views/CheckInView.xaml`
  - `Parking/Styles/Icons.xaml`
- **Descripción**:
  - Corrección de recurso de icono `IconWarning` y definición de alias `IconAlertTriangle`.
- **Verificación**: Compilación limpia con 0 errores.

---

### [2026-08-24 16:25:00] - [FIX] [PERF] [DATA] - Corrección de DbUpdateException en Sincronización Inicial (Transacciones Atómicas y Garantía de Roles)
- **Autor**: Antigravity AI Assistant
- **Módulos Afectados**:
  - `Parking/Services/Implementations/SyncEngineService.cs`
- **Descripción**:
  - Se aseguró la creación y persistencia previa de roles base antes de insertar usuarios sincronizados desde MySQL, eliminando fallos de foreign key en SQLite.
- **Verificación**: Compilación limpia con 0 errores.
