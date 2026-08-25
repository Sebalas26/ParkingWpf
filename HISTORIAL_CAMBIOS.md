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
