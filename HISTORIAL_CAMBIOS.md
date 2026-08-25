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
