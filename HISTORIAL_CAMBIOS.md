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

### [2026-09-02 08:53:00] - [FIX] [MERGE] [BUILD] [WPF] - Resolución de Conflictos de Git Residuales y Lanzamiento de Terminal Desktop
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"ejecuta wpf"*
- **🤖 Resumen Técnico para la IA**:
  1. **Resolución de Conflictos de Combinación (Git Merge)**:
     - Se resolvieron marcadores residuales (`<<<<<<< HEAD`, `=======`, `>>>>>>>`) en 6 archivos generados por un merge commit previo.
     - `DbConnectionManager.cs`: Consolidación completa de migraciones DDL de SQLite para tickets (`ResolutionId`, `ResolutionName`, `InvoiceNumber`, `IsElectronicInvoice`, `OperatorEntryId`, `OperatorExitId`, `BayNumber`, `CreatedAtUtc`).
     - `TicketApiModels.cs`: Soporte de propiedades de facturación fiscal en `ProcessExitApiRequest`.
     - `SyncEngineService.cs` y `EfParkingTicketService.cs`: Preservación de campos de discriminación de cobro y resolución fiscal en la cola offline y checkout online.
     - `ReceiptPreviewViewModel.cs` y `ReceiptPreviewDialog.xaml`: Unificación de diseño térmico y soporte para factura electrónica de venta (FVM) con cálculo de CUFE y QR fiscal.
  2. **Verificación y Ejecución**:
     - Compilación limpia con `dotnet build` (**0 Errores**).
     - Lanzamiento del proceso de escritorio de WPF en segundo plano.
- **📦 Componentes Modificados**:
  - `Parking/Data/Factories/DbConnectionManager.cs`
  - `Parking/Models/ApiModels/TicketApiModels.cs`
  - `Parking/Services/Implementations/SyncEngineService.cs`
  - `Parking/Services/Implementations/EfParkingTicketService.cs`
  - `Parking/ViewModels/ReceiptPreviewViewModel.cs`
  - `Parking/Views/ReceiptPreviewDialog.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores, 4 Advertencias leves de nulabilidad**.
  - `dotnet run`: Proceso WPF en ejecución activa (`RUNNING`).


### [2026-08-31 23:08:00] - [FEAT] [RATES] [MULTI-BRANCH] [WPF] - Preservación Integral de Todos los Tipos de Vehículos Parametrizados por Sede Activa
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Pues ya me muestra el otro tipo de vehiculo , pero sigue faltandome los demas y que los muestre por sede logeada"*
- **🤖 Resumen Técnico para la IA**:
  1. **Eliminación de Sobreescritura por Enum (`EfPricingCalculatorService.cs`)**:
     - Se reemplazó el almacenamiento indexado por `ConcurrentDictionary<VehicleType, VehicleRate>` por una lista viva `List<VehicleRate> _activeBranchRates` que preserva el 100% de los registros parametrizados en la base de datos sin colisiones entre categorías.
  2. **Filtrado Estricto por Sede Logueada**:
     - `ReloadRatesAsync()` carga prioritariamente todas las tarifas asignadas a `r.BranchId == currentBranchId.Value`, ordenadas por nombre, y recurre a tarifas globales (`r.BranchId == null`) únicamente si la sede no tiene tarifas propias.
  3. **Ampliación Léxica de Tipos de Vehículos (`VehicleTypeHelper.cs`)**:
     - Cobertura completa para variantes comerciales como *motocarro, patineta, cuatrimoto, monopatín, bus, buseta, volqueta, remolque, camioneta, furgón, microbús, etc.*
- **📦 Componentes Modificados**:
  - `Parking/Services/Implementations/EfPricingCalculatorService.cs`
  - `Parking/Core/Helpers/VehicleTypeHelper.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores, 0 Advertencias**.
  - `dotnet run`: Terminal WPF en ejecución.

### [2026-08-31 23:00:00] - [FIX] [API] [SYNC] [RATES] [WPF] - Soporte Multinombre de Tarifas en Deserialización JSON y Reconciliación Integral por Sede
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Revisa desde el pwa, ya que desde el pwa veo y asigne mas tipos de vehiculo , pero en el wpf solo recibo moto, revisa que retorna la api y ajuste donde se encuentre el error"*
- **🤖 Resumen Técnico para la IA**:
  1. **Tolerancia y Deserialización Resiliente (`BootstrapSyncResponse.cs`)**:
     - Se refactorizó `ApiVehicleRateSyncDto` con mapeo de campos alternativos (`id`, `rateId`, `branch_id`, `sedeId`, `valorHora`, `hourlyRate`, `hour_rate`, `valorMinuto`, `minuteRate`, `maximoDia`, `fullDayRate`, etc.).
     - Soporte para identificadores numéricos o cadenas mediante generación de `Guid` determinístico (`GetRateId()`), eliminando fallos en la deserialización de `bootstrap.Rates`.
  2. **Reconciliación y Upsert en SQLite (`SyncEngineService.cs`)**:
     - Se actualizó el paso 5 de sincronización para buscar registros existentes tanto por `RateId` como por la clave lógica `(BranchId, VehicleType)`, evitando eliminaciones o sobreescrituras accidentales de categorías concurrentes (Carro, Moto, Bicicleta, etc.).
  3. **Consulta y Priorización por Sede (`EfPricingCalculatorService.cs`)**:
     - `ReloadRatesAsync()` ahora consulta las tarifas de la sede activa y globales (`r.BranchId == currentBranchId.Value || r.BranchId == null`), agrupando por `VehicleType` y priorizando la tarifa específica de la sede sobre la global.
- **📦 Componentes Modificados**:
  - `Parking/Models/ApiModels/BootstrapSyncResponse.cs`
  - `Parking/Services/Implementations/SyncEngineService.cs`
  - `Parking/Services/Implementations/EfPricingCalculatorService.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores, 0 Advertencias**.
  - `dotnet run`: Terminal WPF en ejecución.

### [2026-08-31 22:48:00] - [FEAT] [UI/UX] [MVVM] [WPF] - Implementación de Selector ComboBox de Tipos de Vehículo por Sede con Empty State y Reactividad en Tiempo Real
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Actúa como un desarrollador experto en C# y WPF utilizando el patrón de diseño MVVM... aplicalo... agrégale estas 3 consideraciones: 1. Manejo de Estados Vacíos (Empty State)... 2. Verificación del Conversor... 3. Escucha de eventos de cambio de Sede..."*
- **🤖 Resumen Técnico para la IA**:
  1. **Selector ComboBox Moderno (`CheckInView.xaml`)**:
     - Se integró el `ComboBox` con estilo `ModernComboBox`, enlazado bidireccionalmente a `SelectedRate` y a la colección filtrada por sede `AvailableRates`.
     - `ItemTemplate` enriquecido con ícono vectorial (`VehicleTypeToIconConverter`), nombre legible de la categoría y píldora con tarifa por hora (`$X / hora`).
  2. **Manejo de Estado Vacío (Empty State)**:
     - El `ComboBox` se deshabilita automáticamente (`IsEnabled="{Binding HasConfiguredRates}"`) cuando no existen tarifas para la sede activa.
     - Se renderiza un banner informativo y de advertencia institucional guiando al usuario si la sede no cuenta con parametrización.
  3. **Reactividad al Cambio de Sede en Tiempo Real (`CheckInViewModel.cs`)**:
     - Inyección de `ISessionService` y suscripción al evento `_sessionService.ActiveBranchChanged` para recargar y sincronizar inmediatamente las tarifas y selección activa sin recargar la vista.
- **📦 Componentes Modificados**:
  - `Parking/ViewModels/CheckInViewModel.cs`
  - `Parking/Views/CheckInView.xaml`
  - `Parking/Styles/Controls.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores, 0 Advertencias**.
  - `dotnet run`: Terminal WPF en ejecución.

### [2026-08-31 22:31:00] - [FEAT] [UI/UX] [RATES] [SYNC] [WPF] - Carga Completa y Ajuste Visual de Categorías de Vehículos por Sede
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Ajustame este componente, donde vea el nombre del tipo ed vehiculo, ademas validame que me carguen todos los tipos de vehiculo configurados para esa sede"*
- **🤖 Resumen Técnico para la IA**:
  1. **Helper Centralizado de Tipos de Vehículo (`VehicleTypeHelper.cs`)**:
     - Se implementó un parser resiliente multilingüe que mapea sinónimos en español e inglés (`"moto"`, `"carro"`, `"motocicleta"`, `"camioneta"`, `"suv"`, `"bicicleta"`, `"camión"`, etc.) e infiere el tipo a partir de `DisplayName` o `PlateNumber` en caso de discrepancias.
  2. **Resolución de Colisión de Tarifas en Sincronización y Caché**:
     - En `BootstrapSyncResponse.cs`, todos los métodos `GetVehicleType()` delegan a `VehicleTypeHelper.Parse()`, eliminando el error donde los tipos en español caían en `VehicleType.Car` (0) y sobrescribían las demás tarifas en `_ratesCache`.
     - En `EfPricingCalculatorService.cs`, se vinculó `_sessionService.ActiveBranchChanged` para recargar tarifas dinámicamente al cambiar de sede, filtrando por la sede activa (`BranchId == currentBranchId || BranchId == null`).
  3. **Íconos y Convertidores (`Icons.xaml`, `VehicleTypeToIconConverter.cs`, `VehicleTypeToStringConverter.cs`)**:
     - Se añadió la geometría vectorial `IconBicycle`.
     - Se extendieron los convertidores para soportar `Bicycle`, `Suv`, `Van`, `HeavyTruck`, `Motorcycle` y `Car` con fallbacks seguros.
  4. **Rediseño del Componente en XAML (`CheckInView.xaml` y `CheckInViewModel.cs`)**:
     - Se mejoraron las tarjetas de categoría (`CategoryOptionRadioButton`): íconos vectoriales ampliados a 22x22 con contenedores de 44x44, tipografía destacada a 15pt bold para el nombre de la categoría, tarifa legible `$X / hora`, badge de selección activa y distribución responsiva en cuadrícula de 2 columnas.
     - `DbConnectionManager.cs`: Normalización automática de datos existentes en SQLite.
- **📦 Componentes Modificados**:
  - `Parking/Core/Helpers/VehicleTypeHelper.cs` (Nuevo)
  - `Parking/Models/ApiModels/BootstrapSyncResponse.cs`
  - `Parking/Styles/Icons.xaml`
  - `Parking/Core/Converters/VehicleTypeToIconConverter.cs`
  - `Parking/Core/Converters/VehicleTypeToStringConverter.cs`
  - `Parking/Services/Implementations/EfPricingCalculatorService.cs`
  - `Parking/ViewModels/CheckInViewModel.cs`
  - `Parking/Views/CheckInView.xaml`
  - `Parking/Data/Factories/DbConnectionManager.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores, 0 Advertencias**.
  - `dotnet run`: Terminal WPF en ejecución.

### [2026-08-31 22:15:00] - [UI/UX] [DESIGN] [WPF] - Estandarización Global de Fondo Oscuro Translúcido (Backdrop Overlay) en Diálogos y Modales
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Mi wpf tiene varias alertas de este estilo , quiero que cuando kas muestre, la pantalla de detras me la dejes oscura, revisa toda mi wpf y ajustalo para todos"*
- **🤖 Resumen Técnico para la IA**:
  1. **Estandarización de XAML (`Background="#B3000000"` y Centrado Absoluto)**:
     - Se eliminaron los anchos fijos y `SizeToContent` de la etiqueta raíz `<Window>` en todos los diálogos y alertas.
     - Se configuró `Background="#B3000000"` (overlay negro al 70% de opacidad) en todas las ventanas modales.
     - Se estructuró el contenido dentro de un `<Grid Background="Transparent">` y `<Border>` centrado horizontal y verticalmente (`HorizontalAlignment="Center" VerticalAlignment="Center"`), manteniendo las dimensiones compactas y legibles de cada tarjeta.
  2. **Sincronización Dinámica con Ventana Padre (`Owner` / `MainWindow`)**:
     - En el evento `Loaded` de cada ventana modal, se evalúa el estado del `Owner` o `MainWindow`: si la ventana principal está maximizada, el modal se maximiza automáticamente para cubrir el 100% de la pantalla sin cortes ni bordes libres; si está en modo normal, hereda dinámicamente `Left`, `Top`, `Width` y `Height`.
  3. **Vistas y Diálogos Actualizados**:
     - `BranchSelectionDialog`: Selector de sede de trabajo en Login y Shell.
     - `ModernMessageDialog`: Diálogo global de alertas del sistema (Información, Éxito, Advertencia, Error) y confirmaciones.
     - `CashWithdrawalDialog`: Formulario modal de egreso y retiro parcial de efectivo.
     - `ShiftHandoverAuthDialog`: Ventana de autenticación y relevo de turno.
     - `SyncProgressDialog`: Barra de progreso y pasos de sincronización.
     - `SyncRequiredDialog`: Alerta interactiva de actualización obligatoria de SignalR.
     - `ReceiptPreviewDialog`: Vista previa de tiquetes térmicos.
     - `CheckOutDialog`: Modal de cobro y liquidación de vehículos.
- **📦 Componentes Modificados**:
  - `Parking/Views/BranchSelectionDialog.xaml`
  - `Parking/Views/BranchSelectionDialog.xaml.cs`
  - `Parking/Views/ModernMessageDialog.xaml`
  - `Parking/Views/ModernMessageDialog.xaml.cs`
  - `Parking/Views/CashWithdrawalDialog.xaml`
  - `Parking/Views/CashWithdrawalDialog.xaml.cs`
  - `Parking/Views/ShiftHandoverAuthDialog.xaml`
  - `Parking/Views/ShiftHandoverAuthDialog.xaml.cs`
  - `Parking/Views/SyncProgressDialog.xaml`
  - `Parking/Views/SyncProgressDialog.xaml.cs`
  - `Parking/Views/SyncRequiredDialog.xaml`
  - `Parking/Views/SyncRequiredDialog.xaml.cs`
  - `Parking/Views/ReceiptPreviewDialog.xaml`
  - `Parking/Views/ReceiptPreviewDialog.xaml.cs`
  - `Parking/Views/CheckOutDialog.xaml.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores, 0 Advertencias**.
  - `dotnet run`: Terminal WPF en ejecución.

### [2026-08-31 21:50:00] - [FEAT] [SYNC] [MULTI-BRANCH] [WPF] - Sincronización Automática al Cambiar de Sede y Validación Dinámica de Turno Operativo
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Ayudame que cuando hago cambio de sede en mi Wpf me haga la sincronizacion automatica"*
- **🤖 Resumen Técnico para la IA**:
  1. **Conversión de Comando Asíncrono (`MainShellViewModel.cs`)**:
     - Se convirtió `SwitchBranch` a `SwitchBranchAsync` bajo `[RelayCommand]` (`SwitchBranchCommand`).
     - Al seleccionar una nueva sede en `BranchSelectionDialog`, se evalúa si difiere de la sede activa actual.
     - Se invoca `_sessionService.SetActiveBranch(dialog.SelectedBranch)`.
  2. **Sincronización Automática con Servidor Central**:
     - Se dispara inmediatamente `ForceSyncAsync()` el cual abre el modal interactivo de sincronización (`ShowSyncProgressModalAsync`), descargando tarifas, convenios, métodos de pago, cupos, tiquetes activos y turnos correspondientes a la nueva sede.
     - Al completarse la sincronización, `SyncEngine` dispara `DataSynchronized`, refrescando los datos en cascada en todos los ViewModels de la aplicación.
  3. **Validación Dinámica de Turno y Re-inicialización de Vista Activa**:
     - Se consulta el estado del turno en la nueva sede (`_shiftService.GetActiveShiftAsync()`).
     - Si no hay turno abierto o está a nombre de otro operador y el usuario no es admin, se notifica y se redirige automáticamente a `ShiftClosureViewModel` para apertura/relevo.
     - Si la vista actual es operativa, se invoca `ActiveView.InitializeAsync()` para reflejar los datos de la nueva sede.
- **📦 Componentes Modificados**:
  - `Parking/ViewModels/MainShellViewModel.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores, 0 Advertencias**.
  - `dotnet run`: Terminal WPF en ejecución.
>>>>>>> dec9abebb249833f08c6ee6001f810e2bd23104f

### [2026-08-31 17:38:00] - [BUGFIX] [INTEGRITY] [WPF] - Validación Estricta de Placa Única Activa y Control de Duplicidad en Ingreso
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Me dejo pero ahora me quitaste la logica de que permite ingresar la placa mas de una vez, observando el admin ahi quedaron 2, el ingreso de la placa solo debe permitirse una vez por registro, es decir que si existe una placa ya ingresada, no permita mas veces, sin embargo que si le doy salida por el pwa o wpf ya se actualice el proceso y permita inhgresar de nuevo qeu fue algo que estaba fallando"*
- **🤖 Resumen Técnico para la IA**:
  1. **Restablecimiento de la Regla de Placa Única**:
     - Se eliminó el auto-cierre prematuro que existía dentro de `RegisterEntryAsync` en `EfParkingTicketService.cs`.
     - Se configuró la verificación estricta de placa activa en `RegisterEntryAsync` e `IsPlateCurrentlyParkedAsync`: Si la placa ya tiene un tiquete con `Status == TicketStatus.Active`, se bloquea el ingreso arrojando la excepción correspondiente.
  2. **Propagación Inmediata de Excepciones del Servidor**:
     - En `EfParkingTicketService.cs`, se capturó y re-lanzó de forma explícita `InvalidOperationException` al invocar `_apiClient.CheckInAsync(...)`, evitando que el cliente WPF encole tiquetes duplicados de forma offline cuando el servidor rechaza el ingreso.
  3. **Ciclo de Vida y Liberación de Placa**:
     - Al procesar la salida desde la PWA (o WPF), la reconciliación del motor de sincronización (`SyncEngineService.cs`) actualiza el estado del tiquete a `Completed`, liberando la placa para permitir su nuevo ingreso limpio.
- **📦 Componentes Modificados**:
  - `ParkingWpf/Parking/Services/Implementations/EfParkingTicketService.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Compilación `dotnet build` (**0 Errores**).

### [2026-08-31 17:18:00] - [BUGFIX] [SYNC] [WPF] - Reconciliación de Tiquetes Salidos desde PWA y Validación de Ingreso por Sede
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"estoy teniendo este problema al entrar un vehiculo , pero el vehiculo estaba ingresado en otra, ya le di salida desde el administrador pwa, pero en el wpf me sigue restringiendo el ingreso de esa placa"*
- **🤖 Resumen Técnico para la IA**:
  1. **Diagnóstico**: Al dar salida a un vehículo desde la PWA, el backend central lo removía de los activos. Sin embargo, en SQLite local el registro anterior quedaba en estado `Active` huérfano porque la sincronización solo iteraba los tiquetes presentes en el payload entrante.
  2. **Reconciliación Automática en `SyncEngineService.cs`**:
     - Se añadió un paso de reconciliación antes de guardar: si un tiquete figura como `Active` localmente pero ya no está en `bootstrap.ActiveTickets` del servidor, se marca automáticamente como `Completed` en SQLite.
  3. **Validación de Ingreso por Sede (`EfParkingTicketService.cs`)**:
     - Se ajustó la validación previa de ingreso para evaluar únicamente la sede activa (`t.BranchId == currentBranchId`).
     - Si existen registros huérfanos previos de la misma placa en SQLite, se liberan y marcan como `Completed` automáticamente permitiendo el registro e impresión normal de la entrada.
- **📦 Componentes Modificados**:
  - `ParkingWpf/Parking/Services/Implementations/SyncEngineService.cs`
  - `ParkingWpf/Parking/Services/Implementations/EfParkingTicketService.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Compilación `dotnet build` (**0 Errores**).

### [2026-08-31 16:57:00] - [FEATURE] [SYNC] [WPF] - Sincronización y Selector de Resoluciones de Facturación DIAN por Sede en Salida de Vehículos
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Ayudame a que esto quede un poco mas para la izquierda y me dejes un espacio para incluir la resoluciones que me envia el pwa por la sincro y estan almacenadas en BD, ya quedepende de ello se relaciona a una factura pos o por factura, sin embargo conectala a las que me retorna ya la BD por sede elegida y logeada en el wpf"*
- **🤖 Resumen Técnico para la IA**:
  1. **Sincronización Backend / API**:
     - Se integró `Resolutions` (`BillingResolution`) en `BootstrapSyncDto` (`ParkingApi.Domain.Dtos.Sync.SyncDtos.cs`) y se inyectó `IBillingResolutionRepository` en `SyncService.cs` para entregar las resoluciones activas asociadas a la sede y empresa.
  2. **Persistencia Local y Capa de Servicio (WPF)**:
     - Se creó la entidad `BillingResolution.cs` en `Parking/Entities/` y se registró `DbSet<BillingResolution> BillingResolutions` en `ParkFlowDbContext.cs`.
     - Se extendió `BootstrapSyncResponse.cs` con `ApiBillingResolutionSyncDto` y `SyncEngineService.cs` para el upsert local en SQLite.
     - Se crearon `IBillingResolutionService.cs` y `BillingResolutionService.cs`, registrados como Singleton en `App.xaml.cs`.
  3. **UI / UX en `CheckOutView.xaml`**:
     - Se rediseñó el encabezado de `VEHÍCULOS ACTIVOS ADENTRO` a 3 columnas:
       - **Izquierda**: Icono + Título "VEHÍCULOS ACTIVOS ADENTRO".
       - **Centro-Izquierda**: Selector/Badge de Resolución DIAN Activa de la sede (`SelectedResolution`) indicando tipo de documento (`Factura POS`), prefijo y consecutivo actual (`#CurrentNumber`).
       - **Derecha**: Badge de conteo de vehículos en patio (`{0} en Patio`).
- **📦 Componentes Modificados**:
  - `ParkingApi/ParkingApi.Domain/Dtos/Sync/SyncDtos.cs`
  - `ParkingApi/ParkingApi.Core/Services/Sync/SyncService.cs`
  - `ParkingWpf/Parking/Entities/BillingResolution.cs` (Nuevo)
  - `ParkingWpf/Parking/Data/ParkFlowDbContext.cs`
  - `ParkingWpf/Parking/Models/ApiModels/BootstrapSyncResponse.cs`
  - `ParkingWpf/Parking/Services/Implementations/SyncEngineService.cs`
  - `ParkingWpf/Parking/Services/Contracts/IBillingResolutionService.cs` (Nuevo)
  - `ParkingWpf/Parking/Services/Implementations/BillingResolutionService.cs` (Nuevo)
  - `ParkingWpf/Parking/Styles/Icons.xaml`
  - `ParkingWpf/Parking/App.xaml.cs`
  - `ParkingWpf/Parking/ViewModels/CheckOutViewModel.cs`
  - `ParkingWpf/Parking/Views/CheckOutView.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Compilación limpia `dotnet build` (**0 Errores**).

### [2026-08-31 16:07:00] - [UI/UX] [FEATURE] [WPF] - Botones Interactivos de Convenios por Logo con Icono de Ojo y Pop-up Flotante de 6 Segundos
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"ahi me cargo la imagen del convenio, pero quiero que esa imagen sea el boton que el usuario seleccione para hacer descuento del covenio, adicional que tenga en una esquina del boton un icono de ojo para ver toda la descripcion del convenio, ahi puedes traer toda la info del convenio , eso muestralo como un pop up que se abra y se cierre en 6 segundos , que no sea tan grande para que no sea tan invasivo"*
- **🤖 Resumen Técnico para la IA**:
  1. **Logos de Convenio como Botones Interactivos (`CheckOutDialog.xaml`)**:
     - Se transformó la galería de convenios para que cada logo sea un botón interactivo (`ToggleSelectAgreementCommand`).
     - Al hacer click sobre el logo, se aplica / deselecciona directamente el convenio, recalculando en tiempo real el descuento y el total neto a pagar.
     - Se agregó un indicador visual de selección activa con check (`IconCheck`) y borde de resaltado.
  2. **Icono de Ojo en la Esquina Superior Derecha**:
     - Cada botón de convenio incluye un botón circular con la geometría `IconEye` en su esquina.
  3. **Pop-up Informativo con Autocierre en 6 Segundos**:
     - Al presionar el ojo, se abre un pop-up modal compacto y no invasivo que detalla el nombre del convenio, comercio asociado y reglas de descuento.
     - Se controla mediante un `DispatcherTimer` de 6 segundos en `CheckOutViewModel.cs` para su cierre automático, permitiendo además el cierre manual mediante el botón 'X'.
- **📦 Componentes Modificados**:
  - `Parking/ViewModels/CheckOutViewModel.cs`
  - `Parking/Views/CheckOutDialog.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Compilación limpia con 0 errores.

### [2026-08-31 15:42:00] - [FIX] [SYNC] [WPF] - Corrección de Cobro por Tiempo/Tarifa y Sincronización de Convenios con Imágenes de la PWA
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Bien, dejamelo asi, ahora ayudame a saber porque no me esta cobran el valor de acuerdo al tiempo y tarifa, asi mismo quisiera que me cargaras los convenios que se crean desde la pwa y se almacenan en la bd, carga la imagen con la que quedan almacenadas"*
- **🤖 Resumen Técnico para la IA**:
  1. **Corrección de Cálculo de Tarifa ($0.00 -> Valor Real)**:
     - En `CheckOutViewModel.cs`, se removió la condición `value.HourlyRate == 0m` en `IsMonthlyTicket`. Anteriormente, cualquier tiquete con tarifa por minuto o sin tarifa horaria fija al momento de check-in era catalogado erróneamente como mensualidad gratis, forzando `CalculatedFee = 0m`.
     - Ahora la liquidación en vivo evalúa y aplica fielmente los minutos/horas transcurridos multiplicados por la tarifa activa configurada (`_pricingCalculator.CalculateFee`).
  2. **Sincronización y Renderizado de Convenios e Imágenes (PWA -> WPF)**:
     - Se añadió `ImageUrl` a `ApiCommercialAgreementSyncDto` en `BootstrapSyncResponse.cs`.
     - Se actualizó `SyncEngineService.cs` para persistir `ImageUrl` en SQLite local (`CommercialAgreements`).
     - Se potenció `Base64ToImageConverter.cs` con soporte híbrido para data URIs en Base64 y URLs HTTP/HTTPS/pack.
     - Se integró la tarjeta de convenios comerciales en `CheckOutDialog.xaml` mostrando los logos/fotos subidos desde la PWA.
- **📦 Componentes Modificados**:
  - `Parking/Models/ApiModels/BootstrapSyncResponse.cs`
  - `Parking/Services/Implementations/SyncEngineService.cs`
  - `Parking/Core/Converters/Base64ToImageConverter.cs`
  - `Parking/ViewModels/CheckOutViewModel.cs`
  - `Parking/Views/CheckOutDialog.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Compilación limpia con 0 errores.

### [2026-08-31 14:43:00] - [UI/UX] [WPF] - Ampliación de Ancho y Limpieza Visual en Diálogo de Cobro (CheckOutDialog)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"quisiera que me ancharas mas esta pantalla dialog para que quepa mas informacion, adicionalo lo que esta en verde eliminalo"*
- **🤖 Resumen Técnico para la IA**:
  1. **Ampliación de Ancho (`CheckOutDialog.xaml`)**:
     - Se incrementó el ancho de la tarjeta modal a `Width="800"` (anteriormente 550px), dando máxima amplitud a los botones de métodos de pago y campos numéricos de caja.
  2. **Eliminación de Elementos Redundantes / No Deseados**:
     - Se removió el banner de mensualidad activa (`IsMonthlyTicket`).
     - Se removió la sección completa de convenios de comercio aliado.
     - Se ajustaron los botones de billetes rápidos en 4 columnas (`Exacto`, `$5K`, `$10K`, `$50K`), eliminando `$20K` y `$100K`.
- **📦 Componentes Modificados**:
  - `Parking/Views/CheckOutDialog.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Compilación limpia con 0 errores.

### [2026-08-31 14:15:00] - [UI/UX] [PRINT] [WPF] - Reemplazo de URL por Texto 'PARKING - FLOW' en Fuente Raleway
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"en la impresion quiero que esto me reemplace por la palabra PARKING - FLOW en negrilla y en fuente de raleway y tenga 2 lineas de espacion entre consulte su estado y la palabra que te pedi"*
- **🤖 Resumen Técnico para la IA**:
  1. **Ajuste en `ReceiptPreviewDialog.xaml`**:
     - Se reemplazó el texto estático de la URL por `PARKING - FLOW` en negrilla (`FontWeight="Bold"`), con tipografía `Raleway` y un margen superior de 2 líneas (`Margin="0,16,0,12"`).
- **📦 Componentes Modificados**:
  - `Parking/Views/ReceiptPreviewDialog.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Compilación limpia con 0 errores.

### [2026-08-31 13:01:00] - [FEAT] [PRINT] [WPF] - Parametrización Dinámica de Datos de Sede, Tarifa y QR de Consulta en Tiquete Térmico
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"en la impresion de la etiqueta quisiera que en el ciruclo amarillo me reemplace 1- nombre de la sede que se encuentra seleccionada en el wpf , el nit y la direccion, valida porque eso me lo retorna la BD , en lo azul pon el tipo de medio seleccionado cuadno ingreso el vehiculo , en lo rojo pon la tarifa del tipo de vehiculo elegido, y en lo verde las 3 filas reemplazalas por un QR que me lleve a esta url https://www.parking-flow.com/mockup-consulta"*
- **🤖 Resumen Técnico para la IA**:
  1. **Encabezado Dinámico de Sede (`ReceiptPreviewViewModel.cs`, `ReceiptPreviewDialog.xaml`)**:
     - Se integró `ISessionService` para proyectar `BranchName`, `BranchNit` y `BranchAddress` de la sede activa.
  2. **Tipo de Vehículo y Tarifa**:
     - Se enlazó el tipo de vehículo seleccionado en negrita y la tarifa horaria calculada (`FormattedRateText`, ej. `TARIFA: $3.500 / HORA`).
  3. **Código QR de Consulta Web**:
     - Se sustituyeron las 3 líneas estáticas de póliza por un código QR generado dinámicamente apuntando a `https://www.parking-flow.com/mockup-consulta` con subtítulo informativo.
- **📦 Componentes Modificados**:
  - `Parking/ViewModels/ReceiptPreviewViewModel.cs`
  - `Parking/Views/ReceiptPreviewDialog.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Compilación limpia con 0 errores.

### [2026-08-31 12:31:00] - [UI/UX] [WPF] - Altura Compacta y 100% Adaptativa al Contenido en Tarjetas de Patio
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Bien pero la altura de los componentes es mucho, dejamelo adaptitivos al text"*
- **🤖 Resumen Técnico para la IA**:
  1. **Ajuste en `CheckOutView.xaml`**:
     - Se configuró `VerticalAlignment="Top"` en `ItemsControl`, `UniformGrid` y en cada tarjeta `Border`, eliminando el estiramiento vertical innecesario.
     - Se compactó el padding interno a `14,10` y los márgenes verticales entre filas a `8px`, logrando tarjetas esbeltas y ceñidas al texto.
- **📦 Componentes Modificados**:
  - `Parking/Views/CheckOutView.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Compilación limpia con 0 errores.

### [2026-08-31 12:18:00] - [UI/UX] [WPF] - Distribución Uniforme en 2 Columnas de Tarjetas de Vehículos Activos
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"bien, ahora ayudame a organizar esos componentes , por que ahi se pueden mostrar por fila de a 2"*
- **🤖 Resumen Técnico para la IA**:
  1. **Distribución en `CheckOutView.xaml`**:
     - Se reemplazó el `WrapPanel` por `<UniformGrid Columns="2"/>` en el listado de vehículos activos en patio.
     - Se sustituyó el ancho rígido `Width="460"` por `HorizontalAlignment="Stretch"` y `Margin="0,0,12,12"`, garantizando que cada fila contenga exactamente 2 tarjetas distribuidas al 50% del ancho disponible sin espacios desaprovechados.
- **📦 Componentes Modificados**:
  - `Parking/Views/CheckOutView.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Compilación limpia con 0 errores.

### [2026-08-31 12:01:00] - [FEAT] [PRINT] [WPF] - Sustitución de Código QR por Código de Barras Code 128 con Placa en Tiquete Térmico
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"en la impresion de la etiqueta reemplazame el codigo qr por uno de barras code 128, el cual contenga la placa ingresada al ingresar vehiculo, la cual es la misam que esta debajo de la impresion"*
- **🤖 Resumen Técnico para la IA**:
  1. **Servicio `BarcodeGeneratorService.cs`**:
     - Se creó un generador de códigos de barras estándar **Code 128** usando `ZXing.Net` (`BarcodeWriterPixelData`), generando `BitmapSource` de alta nitidez en escala de grises / monocromático para impresión térmica de 58/80mm y pantalla.
  2. **Integración en ViewModel y Vista (`ReceiptPreviewViewModel.cs`, `ReceiptPreviewDialog.xaml`)**:
     - Se reemplazó el binding del QR por `BarcodeImage`, alimentado directamente por la placa ingresada (`ticket.PlateNumber`).
     - Se ajustó el visor del tiquete con dimensiones rectangulares óptimas (`280x85px`) con escalado `NearestNeighbor`.
- **📦 Componentes Modificados**:
  - `Parking/Services/Implementations/BarcodeGeneratorService.cs` (Nuevo)
  - `Parking/ViewModels/ReceiptPreviewViewModel.cs`
  - `Parking/Views/ReceiptPreviewDialog.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Compilación limpia con 0 errores y renderizado validado.

### [2026-08-31 11:46:00] - [FEAT] [UI/UX] [WPF] - Acceso con Tecla Enter al Digitar Placa y Limpieza de Encabezado de Ocupación
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Quiero que en esta pantalla cuando la persona digite la placa, permita el acceso dando enter y por el bton de registrar e imprimir entrada . adicional eliminame lo que te señale en verde que es informacion innecesaria"*
- **🤖 Resumen Técnico para la IA**:
  1. **Acceso con Enter al Digitar Placa (`CheckInView.xaml`, `CheckInView.xaml.cs`)**:
     - Se configuraron `InputBindings` (`KeyBinding Key="Return"`, `KeyBinding Key="Enter"`) vinculados a `RegisterAndPrintCommand`.
     - Se implementó el manejador `PlateTextBox_KeyDown` para disparar el comando de registro e impresión al pulsar `Enter` de manera instantánea.
  2. **Limpieza del Encabezado de Ocupación (`CheckInView.xaml`)**:
     - Se removió el texto resumen redundante `OccupancySummary` (*"34 disponibles / 3 ocupados"*) del encabezado, evitando el recorte de texto del título (*"Ocupación de Parqueadero"*) y manteniendo las píldoras inferiores de disponibles y ocupados limpias y claras.
- **📦 Componentes Modificados**:
  - `Parking/Views/CheckInView.xaml`
  - `Parking/Views/CheckInView.xaml.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - XAML y C# validados limpiamente con 0 errores.

### [2026-08-31 11:34:00] - [UI/UX] [WPF] - Cambio de Campo 'Operador Responsable' a Texto Plano en Apertura de Turno
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Cuando este en la pantalla para abriri caja, este cuadro no deberia de verse como un cuadro seleccionable si no debe ser un text plano , dejalo como si no un boton"*
- **🤖 Resumen Técnico para la IA**:
  1. **Ajuste en `ShiftClosureView.xaml`**:
     - Se reemplazó el control interactivo `<TextBox IsReadOnly="True" Style="{StaticResource ModernTextBox}" .../>` por un `<TextBlock>` de texto plano informativo (`FontSize="15"`, `FontWeight="SemiBold"`, `Foreground="{DynamicResource BrushTextPrimary}"`).
     - Se eliminó el aspecto de recuadro/botón editable y seleccionable en el flujo de apertura de caja.
- **📦 Componentes Modificados**:
  - `Parking/Views/ShiftClosureView.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - XAML y C# validados limpiamente con 0 errores.

### [2026-08-31 10:33:00] - [UI/UX] [WPF] - Rediseño y Ajuste Proporcional del Cuadro de Captura de Placa en Salida y Liquidación
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Ayudame que en mi wpf me ajuste esta pantalla , donde el cuadro de donde se ingresa la placa quede mas angosta y alta, algo como deje el cuadro rojo"*
- **🤖 Resumen Técnico para la IA**:
  1. **Distribución Integral y Solución de Recorte (`CheckOutView.xaml`)**:
     - Se configuró la caja de búsqueda para abarcar de forma fluida el ancho completo de la tarjeta superior (`Grid.Column="0"` con `Width="*"`, `Height="100"` y tipografía `48px Black Monospace`), evitando márgenes vacíos antiestéticos.
     - Se ajustó el botón `"Buscar / Cobrar"` con `MinWidth="210"`, `Height="100"` y `Padding="24,0"`, eliminando por completo el recorte de texto observado (*"Buscar / Cobra"*).
     - **Rediseño Adaptativo de Tarjetas de Patio**: Se rediseñó el `DataTemplate` de las tarjetas de vehículos activos a `Width="460"`, `CornerRadius="16"`, `Padding="18,16"` y una arquitectura interna en 2 niveles (Fila 1: Icono + Placa 22px y Categoría; Fila 2: Hora de entrada y tiempo transcurrido en pastilla destacada sin colisiones, junto con el botón Liquidar).
  2. **Actualización de Estilo Visual (`Controls.xaml` -> `CheckoutSearchTextBox`)**:
     - Altura establecida en `100px` con `FontSize="48"` y alineación centrada.
     - Radio de borde `CornerRadius="16"` con sombra institucional suave.
- **📦 Componentes Modificados**:
  - `Parking/Styles/Controls.xaml`
  - `Parking/Views/CheckOutView.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - XAML y C# validados y estructurados correctamente con 0 errores de sintaxis/diseño.

### [2026-08-30 23:59:00] - [UI/UX] [WPF] - Corrección de Cobertura de Fondo Oscuro (Backdrop) en Modal de Cobro y Liquidación
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Pero la pantallaoscura no se ve ajustada"*
- **🤖 Resumen Técnico para la IA**:
  1. **Diagnóstico**: CheckOutDialog.xaml tenía propiedades quemadas Height="800" Width="1000" con fondo translúcido #B3000000. Al abrirse sobre una ventana maximizada o de mayor resolución, el telón/overlay oscuro solo cubría ese rectángulo central de 1000x800 px, dejando los costados sin oscurecer.
  2. **Remoción de Restricciones Fijas (CheckOutDialog.xaml)**: Se eliminaron los atributos Height="800" y Width="1000" del elemento raíz <Window>.
  3. **Ajuste y Sincronización Dinámica con Ventana Principal (CheckOutDialog.xaml.cs)**:
     - En el evento Loaded, se evalúa el estado del Owner (MainWindow).
     - Si el Owner está maximizado, la ventana modal se maximiza (WindowState = WindowState.Maximized) para cubrir el 100% de la pantalla de forma uniforme.
     - Si está en modo normal, hereda dinámicamente Left, Top, Width y Height del Owner.
     - El card de cobro (Border Width="550") permanece centrado en el medio de la pantalla con su comportamiento de cierre al hacer clic en el backdrop oscuro.
- **📦 Componentes Modificados**:
  - Parking/Views/CheckOutDialog.xaml
  - Parking/Views/CheckOutDialog.xaml.cs
  - HISTORIAL_CAMBIOS.md
- **✅ Verificación y Compilación**:
  - dotnet build: **0 Errores, 0 Advertencias**.

### [2026-08-30 20:30:00] - [SECURITY] [RBAC] [SYNC] - Estandarización Canónica de Permisos RBAC, Motor de Alias Resiliente y Sincronización Offline en Terminal WPF
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"AUDITORÍA TÉCNICA EXHAUSTIVA: SISTEMA DE PERMISOS (PWA/API/WPF) Y MULTI-TENANCY SaaS. Diagnóstico del flujo de permisos (PWA -> API -> WPF), blindaje de aislamiento multi-tenant SaaS (Organizaciones y Sedes), cero errores de compilación y registro estricto en HISTORIAL_CAMBIOS.md."*
- **🤖 Resumen Técnico para la IA**:
  1. **Motor de Permisos Canónico y Aliases Bidireccionales (`PermissionService.cs`)**:
     - Se implementó una matriz de resolución de permisos que soporta coincidencia exacta, comodines globales (`*`, `all`), comodines a nivel de módulo (`shifts.*`, `monitoring.*`, `analytics.*`) y mapeo bidireccional de alias entre slugs canónicos de backend/PWA (`shifts.view_current`, `monitoring.view_occupancy`, `analytics.view_dashboard`) y nombres de vista de WPF (`shift.view`, `recent_entries.view`, `analytics.view`).
  2. **Estandarización en XAML y ViewModels**:
     - `MainShellWindow.xaml`: Se actualizaron los botones del sidebar a los slugs canónicos (`checkin.create_ticket`, `checkout.process_payment`, `subscriptions.view_list`, `monitoring.view_occupancy`, `shifts.view_current`, `analytics.view_dashboard`).
     - `MainShellViewModel.cs`: Comandos de navegación actualizados a slugs canónicos.
     - `ShiftClosureViewModel.cs`, `RecentEntriesViewModel.cs`, `AnalyticsViewModel.cs`: Decoradores `[RequirePermission]` y comprobaciones de permisos actualizados a slugs canónicos.
  3. **Persistencia Dinámica de Roles y Permisos en SQLite (`SyncEngineService.cs`, `BootstrapSyncResponse.cs`)**:
     - Se crearon los DTOs `ApiUserRoleSyncDto` y `ApiRoleActionSyncDto` en `BootstrapSyncResponse.cs`.
     - `SyncEngineService.cs` ahora sincroniza dinámicamente los `UserRoles` recibidos en el payload bootstrap con la tabla `db.Roles` y crea/actualiza `db.AppPermissions` y `db.RolePermissions` en SQLite local.
     - `AuthService.cs`: Se actualizaron las listas de fallback offline para incluir slugs canónicos y evitar bloqueos si SQLite es nuevo.
- **📦 Componentes Modificados**:
  - `Parking/Services/Implementations/PermissionService.cs`
  - `Parking/Views/MainShellWindow.xaml`
  - `Parking/ViewModels/MainShellViewModel.cs`
  - `Parking/ViewModels/ShiftClosureViewModel.cs`
  - `Parking/ViewModels/RecentEntriesViewModel.cs`
  - `Parking/ViewModels/AnalyticsViewModel.cs`
  - `Parking/Models/ApiModels/BootstrapSyncResponse.cs`
  - `Parking/Services/Implementations/SyncEngineService.cs`
  - `Parking/Services/Implementations/AuthService.cs`
- **✅ Verificación y Compilación**:
  - `dotnet build` ejecutado en `c:\Users\migue\source\repos\ParkingWpf` con resultado exitoso (**0 Errores**).

---

### [2026-08-30 23:15:00] - [Funcionalidad/UI] [WPF] - Visualización de Logos de Convenios Activos
- **Autor**: Antigravity AI Assistant
- **💬 Prompt Original del Usuario**:
  > *"Quisiera que en esta pantalla me cargue los logos de los convenios que se encuentran registrados para esa sede"*
- **🤖 Resumen Técnico para la IA**:
  1. En `CheckOutViewModel.cs` se creó la colección `BranchAgreements`.
  2. Al ejecutar `LoadStoresAsync()` (que trae las tiendas activas de la sede), se invocó `_agreementService.GetAgreementsByStoreAsync` para cada una de ellas con el fin de recolectar todos los convenios y poblar `BranchAgreements`.
  3. En `CheckOutView.xaml` se incrustó un `ItemsControl` horizontal a la derecha del `CheckBox` *"Aplicar Convenio"*.
  4. Este `ItemsControl` renderiza una previsualización pequeña (tarjeta de imagen con tooltip) por cada convenio disponible usando su `ImageUrl` (con fallback dinámico al logo principal en caso de ausencia de imagen).
- **📦 Componentes Modificados**:
  - `Parking/ViewModels/CheckOutViewModel.cs`
  - `Parking/Views/CheckOutView.xaml`
  - `HISTORIAL_CAMBIOS.md`

### [2026-08-30 22:54:00] - [UI/UX] [WPF] - Rediseño Profesional de Tiquete Térmico
- **Autor**: Antigravity AI Assistant
- **💬 Prompt Original del Usuario**:
  > *"la impresion que me genera, quisiera que me generara una mas pro (basate en la 2da imagen), quisiera que la generaras en blanco negro, ademas que tuviese el logo que tiene el login en la parte superior"*
- **🤖 Resumen Técnico para la IA**:
  1. Se reestructuró por completo el contenedor principal en `ReceiptPreviewDialog.xaml`.
  2. Se adoptó una estética monocromática `blanco/negro` típica de las impresoras térmicas (fondos blancos sólidos, textos `#000000`).
  3. Se incluyó el logo (`logo.jpeg`) en la parte superior y se movió el Código QR también hacia arriba, imitando la foto de referencia.
  4. Se reemplazaron las líneas sólidas por cadenas de asteriscos `***` como divisores para mayor fidelidad a los tiquetes físicos reales.
  5. Se implementó la placa invertida (fondo negro y letras blancas) para la rápida visualización del operario.
  6. Se usaron tipografías monoespaciadas (`FontFamilyMonospace`) para darle el efecto de impresión matricial/térmica profesional.
- **📦 Componentes Modificados**:
  - `Parking/Views/ReceiptPreviewDialog.xaml`
  - `HISTORIAL_CAMBIOS.md`

### [2026-08-30 22:06:00] - [UI/UX] [WPF] - Optimización de Espacio y Placa Gigante
- **Autor**: Antigravity AI Assistant
- **💬 Prompt Original del Usuario**:
  > *"Elimina esto y aprovecha esos texta para aumentar el cuadro donde se digita la placa"*
- **🤖 Resumen Técnico para la IA**:
  1. Se eliminó por completo el bloque `<Grid>` del "Header del Módulo" (que contenía los textos "Registro de Entrada de Vehículo" y el badge de "Terminal Activa") en `CheckInView.xaml` para liberar espacio vertical en la columna izquierda.
  2. Se aplicaron los tamaños masivos directamente al `PlateTextBox` (`Height="160"` y `FontSize="90"`) ocupando el espacio liberado por el header, sin romper el diseño de 2 columnas ni provocar scroll horizontal.
- **📦 Componentes Modificados**:
  - `Parking/Views/CheckInView.xaml`
  - `HISTORIAL_CAMBIOS.md`

### [2026-08-30 21:55:00] - [UI/UX] [WPF] - Toggle de Contraseña en Inicio de Sesión
- **Autor**: Antigravity AI Assistant
- **💬 Prompt Original del Usuario**:
  > *"ayudame con el login que tenga un icono para ver la clave"*
- **🤖 Resumen Técnico para la IA**:
  1. Se agregaron las geometrías de íconos `IconEye` y `IconEyeOff` a `Icons.xaml` para mantener la estandarización de recursos vectoriales.
  2. En `LoginWindow.xaml`, se reemplazó el `PasswordBox` solitario por un `Grid` superpuesto que contiene el `PasswordBox`, un `TextBox` en modo `Collapsed` y un `Button` transparente de alternancia alineado a la derecha.
  3. En `LoginWindow.xaml.cs`, se implementó la lógica en Code-Behind para alternar la visibilidad entre el `TextBox` y el `PasswordBox`, copiando el texto entre ellos al cambiar, y se actualizaron los manejadores del Enter Key para leer de la caja visible actual.
- **📦 Componentes Modificados**:
  - `Parking/Styles/Icons.xaml`
  - `Parking/Views/LoginWindow.xaml`
  - `Parking/Views/LoginWindow.xaml.cs`
  - `HISTORIAL_CAMBIOS.md`

### [2026-08-30 21:47:00] - [UI/UX] [WPF] - Ajuste de Tamaño Masivo en Caja de Placa
- **Autor**: Antigravity AI Assistant
- **💬 Prompt Original del Usuario**:
  > *"quisiera que donde se ingresa la placa tenga este tamaño, sin que las otras cards se corten, dejalas responsive tambien"*
- **🤖 Resumen Técnico para la IA**:
  1. **Ajuste XAML (`CheckInView.xaml`)**: Se incrementó drásticamente el alto (`Height="160"`) y el tamaño de la fuente (`FontSize="90"`) del `PlateTextBox`.
  2. **Responsividad**: Dado que la fila inferior del Grid (`Row 1`) posee un `Height="*"`, absorbe dinámicamente el espacio restante. Los contenedores de las columnas inferiores ya cuentan con `ScrollViewer` (`VerticalScrollBarVisibility="Auto"`), lo que garantiza que las tarjetas (Cards) nunca se corten irreparablemente; simplemente habilitarán el scroll vertical si la pantalla es muy pequeña.
- **📦 Componentes Modificados**:
  - `Parking/Views/CheckInView.xaml`
  - `HISTORIAL_CAMBIOS.md`

### [2026-08-30 21:20:00] - [UI/UX] [WPF] - Reorganización Panorámica de Pantalla (Eliminación de Título y Expansión de Placa)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Quiero que me reorganices esta pantalla , donde lo rojo quiero que me quede en lo que te encerre en azul y lo de verde eliminalo, dejame el tamaño del rojocon mas 4 de size"*
- **🤖 Resumen Técnico para la IA**:
  1. **Reestructuración de XAML (`CheckInView.xaml`)**:
     - Se añadió `RowDefinitions` al Grid principal para dividir la vista horizontalmente (Top/Bottom).
     - Se extrajo todo el bloque de la "Caja Panorámica de Placa" (junto con la alerta de feedback) hacia una fila superior (`Grid.Row="0" Grid.ColumnSpan="2"`) logrando que abarque todo el ancho de la pantalla sobre ambas columnas inferiores.
     - Se eliminó por completo el "Header del Módulo" (Texto de Registro y Terminal Activa).
  2. **Ajuste de Fuentes**:
     - Se incrementó nuevamente en +4 el tamaño de todos los textos dentro de la caja de placa (`FontSize="56"`, rótulos superiores a `20` y `19`).
- **📦 Componentes Modificados**:
  - `Parking/Views/CheckInView.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores**.
  - XAML compilado sin problemas y aplicativo reiniciado.

### [2026-08-30 20:49:00] - [UI/UX] [WPF] - Ajuste de Tamaño en Cuadro de Texto de Placa
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"ayudame que este cuadro se vea un poco mas ancho y con 4 mas de size en el text"*
- **🤖 Resumen Técnico para la IA**:
  1. **Ajuste de Dimensiones (`CheckInView.xaml`)**:
     - Se incrementó el `Height` de `PlateTextBox` de 84 a 100 para que se vea más amplio (ancho/alto) en la interfaz.
     - Se incrementó su `FontSize` en +4 (pasando de 48 a 52) para garantizar máxima legibilidad durante la captura de placas a distancia o con pantallas táctiles.
- **📦 Componentes Modificados**:
  - `Parking/Views/CheckInView.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores**.

### [2026-08-30 20:47:00] - [UI/UX] [WPF] - Validación Visual del Botón de Ingreso (Botón Gris)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"ayudame con que este boton se muestre en gris y quede inhabiitado hasta que el campo de la placa tenga min 1 letra, cuando ya detecte , ahi si se pnga en verde osea en el color que ya esta"*
- **🤖 Resumen Técnico para la IA**:
  1. **Validación MVVM (`CheckInViewModel.cs`)**:
     - Se añadió lógica de evaluación `CanExecute` (`CanRegisterAndPrint()`) al comando `RegisterAndPrintCommand` evaluando que `PlateNumber` no esté vacío.
     - Se decoró la propiedad `_plateNumber` con `[NotifyCanExecuteChangedFor(nameof(RegisterAndPrintCommand))]` para revaluar en cada pulsación de tecla.
  2. **Estilo Visual (`CheckInView.xaml`)**:
     - Se añadió un `Trigger` en `IsEnabled="False"` para forzar explícitamente el cambio de color a gris (`Background="#B0BEC5"`, `BorderBrush="#B0BEC5"`) cuando el comando no está disponible.
- **📦 Componentes Modificados**:
  - `Parking/ViewModels/CheckInViewModel.cs`
  - `Parking/Views/CheckInView.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores**.
  - Reinicio del aplicativo ejecutado correctamente.

### [2026-08-30 20:39:00] - [UI/UX] [WPF] - Incremento General de Tamaño de Fuente en Registro de Entrada (Check-In)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"podrias subirle 2 mas al fontsize de esta pantalla"*
- **🤖 Resumen Técnico para la IA**:
  1. **Aumento de Tamaño de Fuente (`CheckInView.xaml`)**:
     - Se realizó un incremento global de +2 puntos en todos los atributos `FontSize` definidos en la vista de Ingreso de Vehículos.
     - Esto mejora la legibilidad general de toda la pantalla (títulos, campos, notas, botones y paneles laterales) sin comprometer la estructura del layout.
- **📦 Componentes Modificados**:
  - `Parking/Views/CheckInView.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores**.
  - Reinicio del aplicativo ejecutado correctamente.

### [2026-08-30 20:25:00] - [UI/UX] [WPF] - Configuración Global de la Tipografía (Inter)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"perfecto, ahora ayudame a que todo el wpf tenga como fuente de texto (inter)"*
- **🤖 Resumen Técnico para la IA**:
  1. **Actualización de FontFamilyPrimary (`Typography.xaml`)**:
     - Se añadió `Inter` como la primera prioridad en la pila de fuentes de la aplicación para `<FontFamily x:Key="FontFamilyPrimary">`.
  2. **Aplicación Global y Herencia en toda la Aplicación (`App.xaml`)**:
     - Se introdujeron Estilos Base implícitos (`TargetType="Window"` y `TargetType="TextBlock"`) dentro de los recursos globales de la aplicación.
     - Esto asegura que cualquier texto (`TextElement.FontFamily`, `TextBlock` y el `Window` por defecto) que no especifique explícitamente una fuente herede y aplique `Inter` instantáneamente.
- **📦 Componentes Modificados**:
  - `Parking/Styles/Typography.xaml`
  - `Parking/App.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Se espera un reinicio del aplicativo para aplicar los cambios a nivel de `App.xaml`.

### [2026-08-30 20:16:00] - [UI/UX] [WPF] - Opacidad y Deshabilitación Visual de Interfaz al no tener Turno Activo
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Ayduame con algo, cuando un usuario ingresa a mi wpf y no he abierto caja, el actualmente obliga a que la persona abra caja para permitirle avanzar, sin embargo quisiera que todo lo que te señale en rjo se vea opaco haciendo la alucion de que se encuentra inactivo"*
- **🤖 Resumen Técnico para la IA**:
  1. **Exposición del Estado del Turno (`MainShellViewModel.cs`)**:
     - Se añadió la propiedad `HasActiveShift` (`[ObservableProperty] private bool _hasActiveShift;`).
     - Se actualiza su valor suscribiéndose al evento `_shiftService.ShiftStateChanged` y durante la inicialización `InitializeAsync()`.
  2. **Bloqueo Visual de la Barra Lateral (`MainShellWindow.xaml`)**:
     - Se agregó un `Style` con `DataTrigger` al `StackPanel` que contiene los botones de navegación (`CheckIn`, `CheckOut`, etc.). Si `HasActiveShift` es `False`, se aplica `Opacity="0.3"` y `IsEnabled="False"`.
  3. **Bloqueo Visual de la Vista de Turnos (`ShiftClosureView.xaml`)**:
     - Se añadieron triggers similares (`Opacity="0.3"` y `IsEnabled="False"`) al `StackPanel` que agrupa las tarjetas KPI superiores (Efectivo, Tarjetas, etc.) y al `Border` inferior que contiene el historial (`DataGrid`).
     - Esto centra la atención del usuario de manera forzosa en la tarjeta central "Apertura de Turno Operativo".
- **📦 Componentes Modificados**:
  - `Parking/ViewModels/MainShellViewModel.cs`
  - `Parking/Views/MainShellWindow.xaml`
  - `Parking/Views/ShiftClosureView.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores**. (Compilación reiniciada exitosamente).

### [2026-08-28 08:00:00] - [SECURITY] [RBAC] [REFACTOR] - Erradicación Total de Contraseñas Maestras y Desacoplamiento de Roles Quemados en Terminal WPF
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Listo sucede que el superadmin accede y super bien accede al perfil de eso pero cree un administrador y tambien accede al portal del superadmin y eso no deberia ser así creo que esta algo quemado en codigo que sea administrador aparte necesito que revises todo el codigo de todos los 3 proyectos que no tenga cosas quemadas que no deberian estar . analiza completamente todo el desarrollo"*
- **🤖 Resumen Técnico para la IA**:
  1. **Eliminación de Contraseñas Maestras Quemadas / Backdoors (`AuthService.cs`)**:
     - Se eliminaron completamente las validaciones de bypass por contraseña fija (`"Admin2026*"` y `"9988"`) tanto en autenticación local offline como en autorizaciones administrativas en caliente (`ValidateAdminAuthorizationAsync`). Toda autenticación se verifica exclusivamente contra el hash BCrypt del usuario.
  2. **Modelo de Sesión Basado en Propiedades y Claims (`UserSessionModel.cs`, `TicketApiModels.cs`)**:
     - `UserSessionModel`: Se convirtieron `IsAdmin` e `IsSuperAdmin` en propiedades asignables desde la respuesta del servidor o del contexto offline, eliminando la comparación estática `RoleName.Equals("Administrador", ...)`.
     - `LoginApiResponse`: Incorporadas las propiedades `IsSuperAdmin`, `CompanyId` y `CompanyName`.
  3. **Desacoplamiento de Roles en Navegación y Turnos (`MainShellViewModel.cs`, `ShiftClosureViewModel.cs`, `PermissionService.cs`)**:
     - En `MainShellViewModel.cs`, `ValidateShiftAccess` e inicialización de turno usan directamente `CurrentUser.IsAdmin`.
     - En `PermissionService.cs`, la carga de permisos evalúa dinámicamente `user.IsAdmin` y carga la matriz de permisos otorgada.
     - En `ShiftClosureViewModel.cs`, se removieron los filtros basados en cadenas de texto (`roleName.Contains("operador")`, `roleName.Contains("cajero")`, etc.). La entrega y recepción de turno opera para cualquier usuario activo asignado a la sede.
- **📦 Componentes Modificados**:
  - `Parking/Models/UserSessionModel.cs`
  - `Parking/Models/ApiModels/TicketApiModels.cs`
  - `Parking/Services/Implementations/AuthService.cs`
  - `Parking/Services/Implementations/PermissionService.cs`
  - `Parking/ViewModels/MainShellViewModel.cs`
  - `Parking/ViewModels/ShiftClosureViewModel.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build ParkingWpf.slnx`: **0 Errores**.

### [2026-08-27 13:10:00] - [ARCHITECTURE] [SAAS] [MULTI-TENANT] - Transición a Arquitectura Multi-Tenant SaaS Centralizada y Compatibilidad
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Tengo una consulta, se penso que el sistema es para venderlo pero es un saas completo entonces necesitamos un super admin que nosotros creemos entremos creemos un administrador y le demos ese usuario al man y que le ingrese cree su parqueadero y sus sedes y si le vendemos el producto a otras personas e igual se les cree su usuario administrador y que ingrese registre su parqueadero y sus sedes si me explico como se quiere manejar antes eso si lo entiendes encesito que revises toda la BD si la logica que tenemos si nos da para eso o que tanto se deberia cambiar ? necesito que revises eso y has un analisis completo y el plan completo que se deberia tomar."*
- **🤖 Resumen Técnico para la IA**:
  1. **Aislamiento Multi-Tenant Centralizado en Backend API**:
     - Introducción de la entidad `Company` y discriminadores `CompanyId` en todas las entidades de negocio.
     - Aprovisionamiento de tenants desde API y PWA sin romper la compatibilidad con terminales de escritorio WPF.
  2. **Compatibilidad Terminal WPF (`Parking`)**:
     - Las terminales de escritorio continúan autenticando usuarios y sincronizando catálogos y transacciones filtrados automáticamente por el contexto de la sede (`BranchId`) y la empresa del operador autenticado.
     - Cero rupturas en modelos locales SQLite y sincronización bidireccional continua.
- **📦 Componentes Verificados**:
  - `Parking/Services/Implementations/SyncEngineService.cs`
  - `Parking/Services/Implementations/SessionService.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build Parking.sln`: **0 Errores**.


### [2026-08-27 11:47:00] - [FEATURE] [ARCHITECTURE] [RELATIONAL] [MULTI-BRANCH] [INCIDENTS] - Arquitectura Relacional Multi-Sede (VehicleIncidentBranches), Fix SQLite y Modal Informativo
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"estoy probando pero ese error no es diciente si me explico . por que primero que todo no es error es que el esa placa tiene una novedad y por eso no se puede registrar deberia mostrar la novedad en una modal explicando el por que no. tengo una duda esas novedades puede ser generales o pueden ser para una sede especifica cuando este null el branchid es que es general ? o como hacemoes eso explicame eso ? .. y si quiero que sea ejemplo tengo 5 sedes y solo aplique para 2 como funcionaria eso ? la bd esta contemplada apra eso ? analiza eso (...) pero esa es la mejor opcion seguro ? no es mejor una tabla relacionada o que ? por que esas cosas de metes un json en una columna no se no me cuadra. analiza eso por que si es necesario hacer eso para 1 para algunas o para todas. realiza el plan completo e incluyendo lo que dijiste de como se muestra esto que se ve horrible si me explico."*
- **🤖 Resumen Técnico para la IA**:
  1. **Arquitectura Relacional Canónica Multi-Sede (3NF) (`ParkingApi` & MySQL)**:
     - `VehicleIncidentBranch.cs`: Entidad relacional con clave compuesta `(IncidentId, BranchId)` y relaciones de integridad referencial hacia `VehicleIncident` y `Branch`.
     - `VehicleIncident.cs`: Agregado `bool IsGlobal` e `ICollection<VehicleIncidentBranch> IncidentBranches`.
     - `DataContext.cs` y `EntityConfigurations.cs`: Registro de `DbSet<VehicleIncidentBranch>` y mapeo Fluent API.
     - `SaveVehicleIncidentDto.cs` y `VehicleIncidentDto.cs`: Contratos con `IsGlobal`, `List<int> BranchIds` y `List<string> BranchNames`.
     - `VehicleIncidentRepository.cs`: Actualizado para filtrar y evaluar `i.IsGlobal || i.BranchId == branchId || i.IncidentBranches.Any(ib => ib.BranchId == branchId.Value)`.
     - `SyncService.cs`: Sincronización multi-sede de novedades incluyendo las sedes relacionadas.
  2. **Interfaz de Gestión Multi-Sede en Web PWA (`ParkingPwa`)**:
     - `NovedadesContracts.ts`: Contratos sincronizados con `isGlobal`, `branchIds` y `branchNames`.
     - `Novedades.tsx`: Nuevo selector de alcance interactivo con Radio buttons (**🌐 Todas las Sedes (Global)** vs **🏢 Sedes Específicas**) y grilla de checkboxes dinámicos para marcar 1, 2 o más sedes concurrentemente.
     - Visualización de badges por sede en tabla y modal de detalle.
  3. **Persistencia Local SQLite y Fix 'no such table' (`Parking` WPF)**:
     - `DbConnectionManager.cs`: Inclusión de sentencias automáticas `CREATE TABLE IF NOT EXISTS "VehicleIncidents"` y `CREATE TABLE IF NOT EXISTS "VehicleIncidentBranches"`, resolviendo de forma permanente el error de SQLite.
     - `VehicleIncidentBranch.cs`, `VehicleIncidentBranchConfiguration.cs` y `ParkFlowDbContext.cs`: Soporte SQLite local de la relación N:M.
     - `SyncEngineService.cs`: Sincronización completa de la entidad e inserción de `VehicleIncidentBranches`.
     - `EfParkingTicketService.cs`: Validación offline local de bloqueo evaluando `IsGlobal` y `IncidentBranches`.
  4. **Experiencia de Usuario Profesional y Modal Informativo (`CheckInViewModel.cs`)**:
     - Eliminación de errores técnicos en banners amarillos para bloqueos de novedades.
     - En `CheckInViewModel.cs`, si la placa tiene novedad activa, se muestra la tarjeta de advertencia en vivo y al intentar emitir tiquete se despliega la ventana modal informativa detallando la placa, el tipo de novedad, motivo y la instrucción clara de gestión administrativa desde la PWA.
- **📦 Componentes Modificados**:
  - `ParkingApi/ParkingApi.Domain/Models/VehicleIncidentBranch.cs`
  - `ParkingApi/ParkingApi.Domain/Models/VehicleIncident.cs`
  - `ParkingApi/ParkingApi.Domain/Dtos/Incidents/SaveVehicleIncidentDto.cs`
  - `ParkingApi/ParkingApi.Domain/Dtos/Incidents/VehicleIncidentDto.cs`
  - `ParkingApi/ParkingApi.Infrastructure/Data/DataContext.cs`
  - `ParkingApi/ParkingApi.Infrastructure/Data/Configurations/EntityConfigurations.cs`
  - `ParkingApi/ParkingApi.Infrastructure/Data/Repositories/Incidents/VehicleIncidentRepository.cs`
  - `ParkingApi/ParkingApi.Core/Services/Incidents/VehicleIncidentService.cs`
  - `ParkingApi/ParkingApi.Core/Services/Sync/SyncService.cs`
  - `ParkingPwa/src/features/novedades/model/NovedadesContracts.ts`
  - `ParkingPwa/src/features/novedades/ui/Novedades.tsx`
  - `Parking/Entities/VehicleIncidentBranch.cs`
  - `Parking/Entities/VehicleIncident.cs`
  - `Parking/Data/Configurations/VehicleIncidentBranchConfiguration.cs`
  - `Parking/Data/ParkFlowDbContext.cs`
  - `Parking/Data/Factories/DbConnectionManager.cs`
  - `Parking/Models/ApiModels/BootstrapSyncResponse.cs`
  - `Parking/Services/Implementations/SyncEngineService.cs`
  - `Parking/Services/Implementations/EfParkingTicketService.cs`
  - `Parking/ViewModels/CheckInViewModel.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `ParkingApi`: `dotnet build` (**0 Errores**).
  - `ParkingPwa`: `npm run build` (**0 Errores**).
  - `Parking` (WPF): `dotnet build` (**0 Errores**, 5.82s).

### [2026-08-27 11:06:00] - [FEATURE] [SECURITY] [INCIDENTS] [WPF & API] - Integración Completa de Novedades y Bloqueo de Placas (Lista Negra) en Terminal WPF y Sincronización
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Listo por hay me baje cambios de la PWA y me dijo mi compañero que le agrego algo de placas restingidas osea bloquedas para que no se les permita el ingreso me puedes decir si eso esta analiza y dime . (...) si realiza el plan para integrarlo completo"*
- **🤖 Resumen Técnico para la IA**:
  1. **Soporte de Sincronización de Novedades en Backend (`ParkingApi`)**:
     - `SyncDtos.cs`: Se agregó `public List<VehicleIncident> Incidents { get; set; } = new();` a `BootstrapSyncDto`.
     - `SyncService.cs`: En `GetBootstrapDataAsync`, se inyectó `IVehicleIncidentRepository` y se consultan las novedades activas (`i.Status == "Activa" && (i.BranchId == branchId || i.BranchId == null)`) entregándolas en el payload inicial.
  2. **Persistencia Local Offline en SQLite (`Parking` WPF)**:
     - `VehicleIncident.cs` y `VehicleIncidentConfiguration.cs`: Se modeló la entidad e índices compuestos en SQLite local.
     - `ParkFlowDbContext.cs`: Se agregó el DbSet `VehicleIncidents`.
     - `SyncEngineService.cs`: Se integró el paso de sincronización de incidencias en SQLite (`Paso 8.5`).
  3. **Interceptación y Validación en el API Client (`ParkingApiClient.cs`)**:
     - En `CheckInAsync`: Si el servidor responde `HTTP 400 BadRequest` (debido al bloqueo activo de la placa), se deserializa el mensaje JSON y se propaga como `InvalidOperationException(message)` evitando que sea interpretado como error de red.
     - Se agregó el método `CheckPlateAsync(string plate, int? branchId)`.
  4. **Protección Local y Alerta Visual en Tiempo Real (`CheckInViewModel.cs` y `CheckInView.xaml`)**:
     - En `CheckInViewModel.cs`: Al ingresar la placa en `OnPlateNumberChanged`, se evalúa de inmediato contra `db.VehicleIncidents` si tiene un bloqueo activo, activando `IsPlateBlocked = true` y capturando el tipo de novedad y motivo.
     - En `CheckInView.xaml`: Se implementó un banner destacado en rojo (`⛔ VEHÍCULO BLOQUEADO (INGRESO RESTRINGIDO)`) con ícono `IconLock`, tipo de novedad y motivo del reporte.
     - El botón de **"Registrar e Imprimir Entrada"** se inhabilita de inmediato (`IsEnabled = false`, `Opacity = 0.45`) si la placa está bloqueada, impidiendo la emisión física o digital del tiquete.
- **📦 Componentes Modificados**:
  - `ParkingApi/ParkingApi.Domain/Dtos/Sync/SyncDtos.cs`
  - `ParkingApi/ParkingApi.Core/Services/Sync/SyncService.cs`
  - `Parking/Entities/VehicleIncident.cs`
  - `Parking/Data/Configurations/VehicleIncidentConfiguration.cs`
  - `Parking/Data/ParkFlowDbContext.cs`
  - `Parking/Models/ApiModels/BootstrapSyncResponse.cs`
  - `Parking/Services/Contracts/IApiClientService.cs`
  - `Parking/Services/Contracts/IParkingTicketService.cs`
  - `Parking/Services/Implementations/ParkingApiClient.cs`
  - `Parking/Services/Implementations/SyncEngineService.cs`
  - `Parking/Services/Implementations/EfParkingTicketService.cs`
  - `Parking/ViewModels/CheckInViewModel.cs`
  - `Parking/Views/CheckInView.xaml`
  - `Parking/Styles/Icons.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `ParkingApi`: `dotnet build` (**0 Errores**).
  - `Parking` (WPF): `dotnet build` (**0 Errores**, 7.66s).

### [2026-08-27 10:44:00] - [FIX] [MULTI-BRANCH] [SYNC] [OFFLINE] [WPF & API] - Persistencia Estricta de BranchId en Tiquetes y Encolado de Ingresos Offline
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"que pasa en la sincronización debe sincronizar todo lo de la sede siempre filtrando por las sedes si me explico ? no general solo lo que se tienen en la sede recuerda que todas las tablas tienen eso del branch, otra cosa es cuando se agregue un vehiculo no esta guardando el brancid entonces eso es lo que genera error tambien en la sincronización si no trae información revisa eso por que eso debe ser error del api o error del wpf que no guarda el idbranch osea si o si deberia guardar siempre por que todo cambia de ingreso y salida va en una sede especifica me explico. ?"*
- **🤖 Resumen Técnico para la IA**:
  1. **Persistencia Estricta de `BranchId` en Backend (`ParkingApi`)**:
     - `CheckInRequestDto.cs` y `CheckOutRequestDto.cs`: Se agregó la propiedad `public int? BranchId { get; set; }`.
     - `ParkingTicketService.cs`: En `CheckInAsync` se asigna `ticket.BranchId = dto.BranchId`. En `CheckOutAsync`, si el tiquete tenía `BranchId == null`, se actualiza con `dto.BranchId.Value`.
  2. **Encolado de Ingresos Offline y Sincronización Multi-Sede (`Parking` WPF)**:
     - `EfParkingTicketService.cs`: En `RegisterEntryAsync`, cuando `ticket.IsSynchronized` es falso (sin conexión a internet o falla en endpoint), se encola inmediatamente la transacción mediante `_syncEngine.EnqueueOfflineCheckInAsync(ticket)`.
     - `SyncEngineService.cs`: Se incluyó `BranchId = ticket.BranchId` tanto en `EnqueueOfflineCheckInAsync` como en `EnqueueOfflineCheckOutAsync`. En el Paso 8 de sincronización de tiquetes, se asigna `existing.BranchId = targetBranchId` y `newTicket.BranchId = targetBranchId`.
- **📦 Componentes Modificados**:
  - `ParkingApi/ParkingApi.Domain/Dtos/Tickets/CheckOutRequestDto.cs`
  - `ParkingApi/ParkingApi.Core/Services/Tickets/ParkingTicketService.cs`
  - `Parking/Services/Implementations/EfParkingTicketService.cs`
  - `Parking/Services/Implementations/SyncEngineService.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `ParkingApi`: `dotnet build` (**0 Errores**).
  - `Parking` (WPF): `dotnet build` (**0 Errores**, 7.27s).

### [2026-08-27 10:01:00] - [FIX] [SECURITY] [SYNC] [WPF] - Aislamiento Estricto de Sesiones Concurrentes por Usuario y Sincronización Inmutable de RateId
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"tengo un problema yo inicie sesión solo con el usuario miguel123 en el wpf, y el pwa inicie sesion con el admin osea son diferentes por que me cerro la sesión de todo así inicie con uno de una cierra todos eso esta super mal solo debe cerrar sesión de los que estan logueados con el mismos usuario si me explico. eso es un error garrafal. aparte tengo este problema y muestra offline activo no se y falla la sincronización osea no tienen sentido eso que esta pasando. ? analiza por favor esos cambios. me urge el de seguridad por usuario ."*
- **🤖 Resumen Técnico para la IA**:
  1. **Aislamiento de Terminación de Sesión por Usuario (`MainShellViewModel.cs`)**:
     - Se blindó `HandleRealtimeNotificationAsync` para que al recibir el evento SignalR `UserSessionTerminated`, se valide si `currentUser.ServerUserId == notification.UserId.Value`.
     - Si el evento pertenece a **otro usuario** (ej: `admin` logueándose mientras `miguel123` opera la terminal), el evento se **descarta de inmediato mediante `return;`**, evitando que se abra el diálogo modal de advertencia y protegiendo la sesión del usuario actual.
  2. **Corrección de Clave Primaria en Sincronización de Tarifas (`SyncEngineService.cs`)**:
     - Se corrigió la consulta de tarifas vehiculares para realizar el matching directamente por clave primaria `r.RateId == rate.RateId`.
     - Se eliminó la reasignación prohibida `existing.RateId = rate.RateId;`, previniendo la excepción de EF Core (`The property 'VehicleRate.RateId' is part of a key and so cannot be modified`).
     - Se garantiza la actualización de valores y la eliminación de registros obsoletos de forma segura.
- **📦 Componentes Modificados**:
  - `Parking/ViewModels/MainShellViewModel.cs`
  - `Parking/Services/Implementations/SyncEngineService.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores**, 6 Warnings conocidas (6.16s).

### [2026-08-27 09:48:00] - [UI/UX] [SYNC] [OFFLINE] [WPF] - Indicador de Conexión en LoginWindow y Sincronización Visual Paso a Paso en Transición de Acceso
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"no entiendo por que no se pudo conectar, segundo esta mal osea a ver como me explico que es lo que quiero que hagas aparece el login bien le doy click me logueo y de una vez lo primeor que hace es realizar la sincronización osea deberia aparecer algo que muestre que esta sincronizando si me explico ya despues de sincronizar si entra al sistema si me explico ? si no tiene conexion a internet apenas estemos en el login no se donde pero coloca algo parecio pero mas pequeño hay te pase la segunda imagen donde uno sepa a si esta conectado a la red o si sale modo offline de una vez sabemos que esta offline sii me explico lo que se quiere. analiza eso."*
- **🤖 Resumen Técnico para la IA**:
  1. **Píldora de Estado de Red en Pantalla de Login (`LoginWindow.xaml` & `LoginViewModel.cs`)**:
     - Se integró un badge moderno en la barra superior de `LoginWindow.xaml` con indicador circular de color:
       - 🟢 Verde (`#10B981`): `API Central Online`
       - 🔵/🟠 Cian/Ámbar (`#06B6D4` / `#F59E0B`): `Modo Offline (Sin Conexión)`
     - Al instanciar `LoginViewModel`, se invoca `_apiClient.PingAsync()` para determinar de inmediato la disponibilidad del servidor central antes de que el cajero intente iniciar sesión.
  2. **Transición con Barra de Progreso de Sincronización Visual (0% - 100%)**:
     - Al validar credenciales y seleccionar la sede, la pantalla de Login muestra una barra de progreso interactiva (`ProgressBar` con `SyncProgressPercentage`) y el detalle exacto de la operación (`SyncStepDescription`: *"Sincronizando Usuarios y Permisos..."*, *"Sincronizando Tarifas y Reglas..."*, *"Sincronizando Comercios y Convenios..."*, etc.).
     - Al culminar la sincronización exitosamente (o determinar el modo offline seguro sin excepciones), se abre la ventana principal `MainShellWindow` lista para operar.
  3. **Eliminación de Alertas de Error Duplicadas en `MainShellViewModel.cs`**:
     - Se eliminó el diálogo de advertencia intrusivo en `MainShellViewModel.InitializeAsync()`, dejando que la terminal refleje el estado dinámico en su barra de estado de manera limpia y natural.
- **📦 Componentes Modificados**:
  - `Parking/ViewModels/LoginViewModel.cs`
  - `Parking/Views/LoginWindow.xaml`
  - `Parking/ViewModels/MainShellViewModel.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores**, 6 Warnings conocidas (6.20s).

### [2026-08-27 09:07:00] - [FIX] [SYNC] [OFFLINE] [UI/UX] - Corrección de Sincronización de Tarifas, Auto-Sync al Login con Modo Offline Seguro y Homologación Tipográfica en Cierre de Turno
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"fallo la sincronización, que sucede, sabes otra cosa que si o si deberia pasar apenas se loguee se haga la sincronización con el api se que demora mas entrar pero es lo mejor así garantizamos que el sistema esta full y conectado, dado que no tenga internet y no sea posible por que no esta conectado a internet deberia salir que se iniciara en modo offline por que la wpf debe funcionar por completo en modo offline eso no se ha probador pero revisar para que eso este completo. otra cosa el tamaño de esa letra que dice Fecha Apertura, Fecha Cierre se le aplique a la ultima imagen si ves analiza todo eso."*
- **🤖 Resumen Técnico para la IA**:
  1. **Corrección de Conflicto en Sincronización de Tarifas (`VehicleRateConfiguration.cs` & `SyncEngineService.cs`)**:
     - `VehicleRateConfiguration.cs`: Se corrigió el índice de `VehicleRates` reemplazando el índice único global por un índice compuesto por `(BranchId, VehicleType)`.
     - `SyncEngineService.cs`: Se refactorizó el paso de sincronización de tarifas (78%) a un patrón de **Upsert en memoria**, actualizando propiedades en sitio sin realizar ciclos de borrado/inserción simultáneos que entraban en colisión en SQLite (`DbUpdateException`).
     - Se añadió un bloque `try/catch` global en `PerformFullSyncWithProgressAsync` con extracción del `InnerException` para diagnóstico transparente.
  2. **Auto-Sincronización Obligatoria Post-Login con Modo Offline Tolerante (`MainShellViewModel.cs`)**:
     - En `InitializeAsync()`, se dispara la sincronización integral con el API de forma automática tras autenticarse y cargar la sede.
     - En caso de indisponibilidad de red, timeout o API apagada, el sistema entra en **Modo Offline Seguro**, notificando al usuario mediante un diálogo informativo no bloqueante y permitiendo la operación total sobre la base de datos local SQLite (`ParkFlowDbContext`).
  3. **Homologación Tipográfica en Cierre de Turno (`ShiftClosureView.xaml`)**:
     - Se actualizaron los títulos de las 4 tarjetas KPI (`EFECTIVO COBRADO`, `TARJETAS DÉBITO / CRÉDITO`, `TRANSFERENCIAS / QR`, `DESCUENTOS POR CONVENIOS`) y el banner de volumen (`TIQUETES LIQUIDADOS`, `VEHÍCULOS INGRESADOS`) a `FontSize="13"` y `FontWeight="SemiBold"`, igualando exactamente la tipografía de `FECHA APERTURA` / `FECHA CIERRE`.
- **📦 Componentes Modificados**:
  - `Parking/Data/Configurations/VehicleRateConfiguration.cs`
  - `Parking/Services/Implementations/SyncEngineService.cs`
  - `Parking/ViewModels/MainShellViewModel.cs`
  - `Parking/Views/ShiftClosureView.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores**, 6 Warnings conocidas (7.30s).

### [2026-08-27 08:16:00] - [UI/UX] [BRANDING] [WPF] - Rediseño de Hero Banner en LoginWindow con Logotipo a Gran Escala y Fondo Homogéneo
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"yo creo que el logo deberia estar en toda esa parte gris o verde como sea si me explico no así de pequeño se ve super mal si me explico. analiza eso por que no se ve supremamente bien. [Captura señalando con círculo rojo todo el panel izquierdo para que el logo ocupe el espacio principal]"*
- **🤖 Resumen Técnico para la IA**:
  1. **Armonización Cromática del Fondo (`#111822`)**:
     - Se ajustó el fondo del panel izquierdo de `LoginWindow.xaml` a `#111822` (valor hexadecimal exacto del fondo de `logo.jpeg`), eliminando cortes y contornos rígidos y fusionando el arte de forma ininterrumpida.
  2. **Exhibición del Imagotipo en Gran Formato (Hero Banner Centrado)**:
     - Se eliminó el recuadro diminuto de 52px, los textos comerciales extensos y las tres tarjetas de viñetas operativas que saturaban la pantalla.
     - Se ubicó el imagotipo oficial en el centro visual con `MaxWidth="380"`, `Stretch="Uniform"` y `RenderOptions.BitmapScalingMode="HighQuality"`.
  3. **Píldora de Identificación y Pie Minimalista**:
     - Se agregó una píldora estética estilizada: `● Terminal POS • Control de Acceso y Recaudación` (`#1A2332` con borde `#2A384C`) y copyright minimalista en el pie de página.
- **📦 Componentes Modificados**:
  - `Parking/Views/LoginWindow.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores**, 6 Warnings conocidas (6.62s).

### [2026-08-27 07:59:00] - [ASSETS] [BRANDING] [WPF] - Integración del Logotipo Oficial (.jpeg) en Login, Barra de Título, Sidebar y Recibos
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"ya agrege pero es .jpg la imagen no png entonces analiza pero ya esta en la route que me dijhiste"*
- **🤖 Resumen Técnico para la IA**:
  1. **Configuración de Recurso Embebido (`Parking/Parking.csproj`)**:
     - Se vinculó `Resources\logo.jpeg` con Build Action `<Resource Include="Resources\logo.jpeg" />` para que el compilador de .NET empaquete el imagotipo de alta resolución dentro del ensamblado ejecutable.
  2. **Integración en Pantalla de Acceso (`LoginWindow.xaml`)**:
     - Reemplazo del contenedor y glifo genérico de auto por el control `<Image Source="/Resources/logo.jpeg" Height="52" Stretch="Uniform" RenderOptions.BitmapScalingMode="HighQuality"/>`.
     - Actualización del título de la ventana a `"Parking Flow - Control de Acceso y Caja"` y ajuste de membretes de pie de página a `"PARKING FLOW • Tu punto de llegada"`.
  3. **Integración en Ventana Principal (`MainShellWindow.xaml`)**:
     - **TitleBar Superior**: Inserción del imagotipo escalado a 22px junto con el título `"PARKING FLOW"`.
     - **Sidebar Brand Header**: Visualización del imagotipo institucional con lógica de fallback automático (si la sede activa tiene un logotipo `LogoBase64` cargado en base de datos, este prevalece; en caso contrario, se renderiza `logo.jpeg` con escalado de alta fidelidad).
     - Actualización de `Title` a `"Parking Flow - Terminal POS de Control de Acceso y Caja"`.
  4. **Ajuste en Membrete de Impresión de Recibos (`ReceiptPreviewDialog.xaml`)**:
     - Actualización del encabezado por defecto del ticket a `"PARKING FLOW"`.
- **📦 Componentes Modificados**:
  - `Parking/Parking.csproj`
  - `Parking/Views/LoginWindow.xaml`
  - `Parking/Views/MainShellWindow.xaml`
  - `Parking/Views/ReceiptPreviewDialog.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build`: **0 Errores**, 6 Warnings conocidas de nulabilidad (6.56s).

### [2026-08-27 00:12:00] - [UI/UX] [PWA] [MOBILE] - Bloqueo de Desbordamiento Lateral (Anti-Horizontal Shift) y Ajuste de Encabezado de Métodos de Pago
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"cuando hago el gesto hacia la izquierda en la dashboard en la version mobile, me queda ese espacio que te señale, evitalo y corrigelo, adicional que recuadacion hoy en distribucion por meotodos por pago se ve muy juntos, separalos para version mobile [Captura mostrando espacio blanco a la derecha por desplazamiento horizontal y solapamiento del título de métodos de pago]"*
- **🤖 Resumen Técnico para la IA**:
  1. **Diagnóstico y Corrección del Desplazamiento Lateral (Espacio en Blanco a la Derecha)**:
     - Se identificó que en pantallas móviles pequeñas (< 400px), los elementos combinados de `.top-bar` (`mobile-brand` + `branch-selector-pill` + botón de refrescar) excedían el ancho del viewport (aprox. 404px de ancho), lo cual habilitaba el desplazamiento horizontal involuntario al hacer swipe a la izquierda.
     - `index.css`: Se añadió blindaje global con `overflow-x: hidden; width: 100%; max-width: 100%;` en `html`, `body` y `#root`.
     - `DashboardLayout.css`: Se configuró `.top-bar` con `max-width: 100%; overflow: hidden; gap: 6px;` e hijos con `min-width: 0` y `flex-shrink: 1`, y `.main-content` con `overflow-x: hidden;`.
  2. **Separación y Ajuste en "Distribución por Métodos de Pago" (`Dashboard.css`)**:
     - Se dotó a `.pie-card-header` de `flex-wrap: wrap; gap: 8px; justify-content: space-between; align-items: center;` con `min-width: 150px` y `flex: 1` para el título `<h3>`.
     - Esto asegura que el título *"Distribución por Métodos de Pago"* y la insignia *"Recaudación hoy"* mantengan una separación limpia, sin amontonamiento ni superposición en resoluciones móviles.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/index.css`
  - `ParkingPwa/src/shared/ui/DashboardLayout.css`
  - `ParkingPwa/src/features/dashboard/ui/Dashboard.css`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `npm run build`: **0 Errores** (1.38s).
  - `oxlint`: **0 Errores**.



### [2026-08-27 00:07:00] - [ASSETS] [BRANDING] [PWA] - Integración del Imagotipo Oficial PNG en Login, Menú Lateral y Barra Superior
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"en el login y en el menu lateral hay un icono de un carro quiero que los reemplaces por el png que te pase [Imagen oficial PNG con fondo transparente]"*
- **🤖 Resumen Técnico para la IA**:
  1. **Procesamiento del Logo Maestro (`ParkingPwa/public/logo.png`)**:
     - Se procesó y exportó el logotipo oficial de alta resolución con canal alfa transparente (`/logo.png`) y se actualizaron las densidades de icono PWA (`pwa-512x512.png`, `pwa-192x192.png`, `apple-touch-icon.png`, `favicon.png`).
  2. **Reemplazo en Pantalla de Login (`Login.tsx` & `Login.css`)**:
     - En la columna izquierda (hero institucional desktop): Se reemplazó el icono vectorial genérico por `<img src="/logo.png" alt="Parking Flow" className="brand-logo-img" />`.
     - En la cabecera compacta móvil: Se reemplazó por `<img src="/logo.png" alt="Parking Flow" className="mobile-logo-img" />`.
     - Se actualizaron los textos de marca a **PARKING FLOW - GESTIÓN INTELIGENTE DE PARQUEADEROS**.
  3. **Reemplazo en Menú Lateral y Header Móvil (`DashboardLayout.tsx` & `DashboardLayout.css`)**:
     - Sidebar Header: Se reemplazó el icono de auto por `<img src="/logo.png" alt="Parking Flow" className="sidebar-logo-img" />`.
     - Barra Superior Móvil: Se reemplazó por `<img src="/logo.png" alt="Parking Flow" className="mobile-header-logo-img" />`.
- **📦 Componentes Modificados**:
  - `ParkingPwa/public/logo.png`
  - `ParkingPwa/src/features/auth/ui/Login.tsx`
  - `ParkingPwa/src/features/auth/ui/Login.css`
  - `ParkingPwa/src/shared/ui/DashboardLayout.tsx`
  - `ParkingPwa/src/shared/ui/DashboardLayout.css`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `npm run build`: **0 Errores** (1.29s).
  - `oxlint`: **0 Errores**.



### [2026-08-27 00:00:00] - [UI/UX] [PWA] [MOBILE] - Unificación de Scroll y Cabecera Sticky para Evitar Cortes Superiores en Móvil
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"volvi a notar que en la version mobile, cuando me logeo e ingreso, esto no muestra completo la pagina desde los elementos superiores, ajustalos para que se vean bien [Captura mostrando banner verde de dashboard cortado y top-bar oculto]"*
- **🤖 Resumen Técnico para la IA**:
  1. **Diagnóstico del Corte Superior en Móvil**:
     - Se identificó la existencia de doble scroll anidado (`.main-content` con `overflow-y: auto` y `.dashboard-container` / `.caja-container` con `height: 100%; overflow-y: auto;`).
     - Al entrar al Dashboard en navegadores móviles (iOS Safari / Android Chrome), el contenedor exterior sufría un micro-desplazamiento vertical al renderizar, desplazando la barra superior fuera del viewport y dejando visible solo la mitad inferior del banner verde de KPIs.
  2. **Correcciones Realizadas**:
     - `DashboardLayout.css`:
       - Se fijó `.dashboard-layout` con `height: 100dvh; overflow: hidden;` en móviles.
       - Se convirtió `.top-bar` en cabecera fija/adhesiva (`position: sticky; top: 0; z-index: 100;`) con soporte para `padding-top: max(8px, env(safe-area-inset-top));` para evitar solapamientos con el notch o barras de estado.
       - Se unificó el scroll vertical exclusivamente en `.main-content` con `-webkit-overflow-scrolling: touch;`.
     - `Dashboard.css` y `Caja.tsx`:
       - Se eliminó el `overflow-y: auto` y `height: 100%` redundante de los contenedores internos, permitiendo un flujo de contenido elástico natural.
     - `DashboardLayout.tsx`:
       - Se agregó `mainContentRef` con reseteo forzado de scroll `(0, 0)` en cada cambio de ruta.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/shared/ui/DashboardLayout.tsx`
  - `ParkingPwa/src/shared/ui/DashboardLayout.css`
  - `ParkingPwa/src/features/dashboard/ui/Dashboard.css`
  - `ParkingPwa/src/features/caja/ui/Caja.tsx`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `npm run build`: **0 Errores** (1.10s).
  - `oxlint`: **0 Errores**.



### [2026-08-26 23:56:00] - [BUGFIX] [AUTH] [PWA] - Cierre de Sesión Inmediato en Un Solo Clic sin Rebote de Navegación
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Cuando quiero cerrar sesion, no me lo hace hasta que le oprima 2 veces"*
- **🤖 Resumen Técnico para la IA**:
  1. **Diagnóstico del Rebote de Navegación**:
     - Al invocar `authService.logout()`, la función realizaba una llamada asíncrona de red (`apiClient.post('/Auth/logout')`) y borraba `auth_token` solo en el bloque `finally`.
     - Simultáneamente, `DashboardLayout` ejecutaba `navigate('/')`. En la ruta `/`, el componente guardián `RootAuthHandler` comprobaba `authService.isAuthenticated()`, encontrando el token todavía presente en `localStorage` mientras la petición de red seguía en vuelo, provocando que redirigiera inmediatamente de vuelta al Dashboard (`<Navigate to="/dashboard" replace />`). En el segundo clic, como la petición anterior ya había culminado y purgado el storage, finalmente permitía salir al Login.
  2. **Corrección Implementada**:
     - `authService.ts`: Se reestructuró `logout` para remover `auth_token` y `auth_user` de `localStorage` de manera síncrona e inmediata **antes** de disparar la notificación de red a la API.
     - `DashboardLayout.tsx`: Se convirtió `handleLogout` en función asíncrona que espera la purga y navega a `/` con `{ replace: true }`.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/auth/data/authService.ts`
  - `ParkingPwa/src/shared/ui/DashboardLayout.tsx`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `npm run build`: **0 Errores** (1.37s).
  - `oxlint`: **0 Errores**.



### [2026-08-26 23:46:00] - [ASSETS] [BRANDING] [PWA] [WPF] - Integración del Nuevo Logotipo Oficial de Parking Flow en PWA y Aplicación de Escritorio WPF
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"requiero que este logo sea el que que quede cuando el pwa y el wpf se instalen en el pc y en el cel o cuando quede con el acceso directo [Imagen oficial adjunta con imagotipo 'P' estilizada con carretera y vehículo en paleta verde, blanco y naranja sobre fondo oscuro]"*
- **🤖 Resumen Técnico para la IA**:
  1. **Generación de Assets de Alta Densidad para la PWA (`ParkingPwa/public/`)**:
     - Se procesó el logotipo maestro de alta resolución generando:
       - `pwa-512x512.png`: Icono principal de instalación para Android, Windows y Chrome/Edge.
       - `pwa-192x192.png`: Icono de acceso directo y pantalla de inicio.
       - `maskable-icon-512x512.png`: Icono adaptable con zona segura para launchers móviles.
       - `apple-touch-icon.png` (180x180 px): Icono nativo para iOS / Safari.
       - `favicon.png` (64x64 px) y `favicon.ico` (multi-capa 64/32/16 px): Icono de pestaña web.
       - `index.html`: Vinculación de `favicon.png`, `favicon.ico` y `apple-touch-icon.png`.
  2. **Generación de Archivo Binario .ICO Multi-Resolución para WPF (`ParkingWpf/Parking/Resources/`)**:
     - Se construyó el archivo `parkpoint.ico` con 6 resoluciones embebidas (256x256, 128x128, 64x64, 48x48, 32x32 y 16x16 px) con codificación PNG/ARGB 32-bit de máxima nitidez.
     - Este archivo está enlazado como `<ApplicationIcon>` en `Parking.csproj`, lo que define el icono del ejecutable `.exe` y los accesos directos de Windows, además de las ventanas `MainShellWindow.xaml` y `LoginWindow.xaml`.
- **📦 Componentes Modificados**:
  - `ParkingPwa/public/pwa-512x512.png`
  - `ParkingPwa/public/pwa-192x192.png`
  - `ParkingPwa/public/maskable-icon-512x512.png`
  - `ParkingPwa/public/apple-touch-icon.png`
  - `ParkingPwa/public/favicon.png`
  - `ParkingPwa/public/favicon.ico`
  - `ParkingPwa/index.html`
  - `ParkingWpf/Parking/Resources/parkpoint.ico`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `npm run build` en ParkingPwa: **0 Errores** (1.40s).
  - `dotnet build` en ParkingWpf: **0 Errores** (compilación correcta).



### [2026-08-26 23:38:00] - [FEATURE] [UI/UX] [PWA] [CAJA] - Alineación de Estados, Cierre Dinámico de Cajas y Renombrado Oficial a Parking Flow
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"en el modulo de cajaas, los estanos no seven alineados con el titulo, adicional quiero que exista la opcion para cerrar cajas, otra cosa que requuiero y es que en vez de llamarse parkflow, llamalo parking flow, asi que reemplazalo en el pwa"*
- **🤖 Resumen Técnico para la IA**:
  1. **Alineación Visual de Estados en Módulo de Caja (`Caja.tsx`)**:
     - Se ajustó el encabezado `<th className="text-center">ESTADO</th>` y el cuerpo `<td className="text-center">` para que los badges `Abierto` / `Cerrado` queden perfectamente alineados y centrados con su título de columna tanto en *Turno de Caja Activo* como en *Historial Consolidado de Cajas*.
  2. **Cierre Dinámico de Cajas Abiertas (`Caja.tsx`)**:
     - Se añadió la columna `<th className="text-right">ACCIONES</th>`.
     - Para cualquier turno en estado "Abierto" (`!isClosed`), se incorporó el botón **"🔴 Cerrar Caja"** en el historial y en la tarjeta de turno activo.
     - Al interactuar, el modal de liquidación/arqueo se abre calculando la base, recaudación y efectivo esperado específico de dicho turno (`shiftToClose`), permitiendo registrar el arqueo físico y liquidar el turno con `cajaService.closeShift(...)` contra la API.
  3. **Renombrado Oficial a "Parking Flow"**:
     - Se reemplazó "ParkFlow" y "ParkControl" por **"Parking Flow"** en:
       - `DashboardLayout.tsx`: Sidebar (`app-name`) y Header móvil (`mobile-brand-name`).
       - `ZeroDataOnboardingWizard.tsx`: Mensaje de bienvenida.
       - `index.html`: Etiqueta `<title>Parking Flow - Sistema de Estacionamiento</title>`, `apple-mobile-web-app-title` y `application-name`.
       - `vite.config.ts`: Nombre y short name en el manifest de la PWA.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/caja/ui/Caja.tsx`
  - `ParkingPwa/src/shared/ui/DashboardLayout.tsx`
  - `ParkingPwa/src/features/auth/ui/ZeroDataOnboardingWizard.tsx`
  - `ParkingPwa/index.html`
  - `ParkingPwa/vite.config.ts`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `npm run build`: **0 Errores** (compilación de producción exitosa en 1.13s).
  - `oxlint`: **0 Errores**.



### [2026-08-26 23:31:00] - [UI/UX] [PWA] [MOBILE] - Scroll Horizontal Táctil (table-responsive) en Todos los Módulos y Tablas de Configuración
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"sigo viendo estos errores en la version mobile [Captura 1: Tabla de Roles y Matriz de Permisos cortada en el extremo derecho sin scroll; Captura 2: Tabla de Convenios Comerciales cortada a la derecha]"*
- **🤖 Resumen Técnico para la IA**:
  1. **Estandarización de Contenedores con Desplazamiento Horizontal (`table-responsive`)**:
     - Se envolvieron todas las tablas de datos que carecían de contenedor elástico con `<div className="table-responsive" style={{ width: '100%', overflowX: 'auto', WebkitOverflowScrolling: 'touch' }}>`.
     - Se asignó a cada tabla un ancho mínimo (`min-width: 480px` a `720px`) para impedir que los datos de las celdas se compacten o deformen.
  2. **Componentes y Módulos Adaptados**:
     - `RolesTab.tsx`: Tabla de roles con columnas completas (ID, Nombre, Permisos Asignados, Estado, Botón Configurar Permisos y Editar) desplazable en mobile.
     - `ConveniosTab.tsx`: Tabla de convenios con logo, descuento, compra mínima, horas máximas, estado y acciones totalmente accesibles.
     - `VehiculosConfigTab.tsx`: Tabla de catálogo de tipos de vehículos protegida contra desbordes.
     - `MediosPagoTab.tsx`: Tabla de medios de pago con íconos y acciones protegida con scroll táctil.
     - `ResolucionesTab.tsx`: Tabla de resoluciones DIAN/Facturación (10 columnas) con scroll suave de 720px.
     - `TarifasTab.tsx`: Tabla de tarifas horarias/diarias con scroll suave de 600px.
     - `Vehicles.css`: `.table-card` actualizado con `overflow-x: auto; -webkit-overflow-scrolling: touch;` beneficiando automáticamente a los módulos de `Vehicles`, `Reports`, `Novedades` y `Caja`.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/settings/ui/RolesTab.tsx`
  - `ParkingPwa/src/features/settings/ui/ConveniosTab.tsx`
  - `ParkingPwa/src/features/settings/ui/VehiculosConfigTab.tsx`
  - `ParkingPwa/src/features/settings/ui/MediosPagoTab.tsx`
  - `ParkingPwa/src/features/settings/ui/ResolucionesTab.tsx`
  - `ParkingPwa/src/features/settings/ui/TarifasTab.tsx`
  - `ParkingPwa/src/features/settings/ui/Settings.css`
  - `ParkingPwa/src/features/vehicles/ui/Vehicles.css`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `npm run build`: **0 Errores** (compilación de producción exitosa en 1.10s).
  - `oxlint`: **0 Errores**.



### [2026-08-26 23:17:00] - [UI/UX] [PWA] [MOBILE] - Adaptación de Grid de Tarifas 2x2 y Modal de Usuarios con Altura 100dvh
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Ajustame el diseño mobile, para que en el cel se vea bien en los flujos y modulos que te pase en las imagenes [Captura 1: Formulario Nueva Tarifa para esta Sede con campos comprimidos; Captura 2: Modal Crear Nuevo Usuario con botones fuera del viewport]"*
- **🤖 Resumen Técnico para la IA**:
  1. **Formulario de Tarifas Vehiculares (`ParqueaderosTab.tsx` & `Settings.css`)**:
     - Se reemplazó la disposición forzada de 4 columnas en línea por la clase `.form-grid-rates`.
     - En pantallas móviles (`max-width: 640px`), se transforma en un **grid 2x2 equilibrado**:
       - Fila 1: *Valor Hora ($)* y *Valor Minuto ($)*.
       - Fila 2: *Máximo Día ($)* y *Gracia (min)*.
     - Se eliminó el salto de línea en las etiquetas y los inputs cuentan con ancho suficiente para la digitación de montos monetarios.
  2. **Modal Crear / Editar Usuario (`UsuariosTab.tsx` & `Settings.css`)**:
     - Se configuró `.modal-content` con `max-height: calc(100dvh - 20px); display: flex; flex-direction: column; overflow: hidden;`.
     - El formulario y su cuerpo (`.modal-body`) tienen `flex: 1; overflow-y: auto; -webkit-overflow-scrolling: touch;`.
     - El pie de página (`.modal-footer`) quedó fijado como barra inferior estática (`flex-shrink: 0; background: #ffffff; border-top: 1px solid #e2e8f0;`), asegurando que los botones *"Cancelar"* y *"Crear Usuario / Guardar Cambios"* permanezcan 100% visibles y accesibles en cualquier tamaño de pantalla móvil.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/settings/ui/ParqueaderosTab.tsx`
  - `ParkingPwa/src/features/settings/ui/UsuariosTab.tsx`
  - `ParkingPwa/src/features/settings/ui/Settings.css`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `npm run build`: **0 Errores** (compilación de producción exitosa en 1.16s).
  - `oxlint`: **0 Errores**.



### [2026-08-26 23:09:00] - [FEATURE] [PWA] [AUTH] - Opción 'Recordar Usuario' en Inicio de Sesión
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Ayudame con agregarle al login una opcion para recordar el usuario"*
- **🤖 Resumen Técnico para la IA**:
  1. **Lógica de Persistencia y Estado (`Login.tsx`)**:
     - Se inicializan los estados `username` y `rememberUser` consultando de forma perezosa `localStorage.getItem('remembered_username')`.
     - Si el usuario existe guardado, el input se pre-completa automáticamente y la casilla se marca como activa.
     - En el método `handleLogin`, tras un inicio de sesión exitoso con la API, se guarda el `username.trim()` si `rememberUser` está activo, o se purga de `localStorage` si está desmarcado.
     - Por estrictos estándares de seguridad y OWASP, nunca se almacena la contraseña del usuario.
  2. **Diseño y Estilos (`Login.tsx` & `Login.css`)**:
     - Se añadió la fila `.login-options-row` con `.remember-user-label` y `.remember-user-checkbox` entre el campo de contraseña y el botón *"Ingresar"*.
     - Se aplicó la paleta institucional (#07665e), tipografía limpia y soporte táctil óptimo para smartphones y desktop.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/auth/ui/Login.tsx`
  - `ParkingPwa/src/features/auth/ui/Login.css`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `npm run build`: **0 Errores** (build PWA generado en 1.47s).
  - `oxlint`: **0 Errores**.



### [2026-08-26 22:52:00] - [UI/UX] [PWA] [MOBILE] - Optimización de Diseño y Vistas Responsive Mobile (Navbar, Dashboard Hero, Slicers, Gestión de Usuarios y Modales)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Ayudame ajustar en version mobile mi pwa en las imagenes que te anexe [5 capturas señalando: Top Navbar con selector de sede/refrescar, Dashboard Hero Banner & Slicers, Tabla de Usuarios con columnas cortadas, Sub-pestañas del Modal de Parametrización y Tabla de Tarifas Vehiculares en Modal]"*
- **🤖 Resumen Técnico para la IA**:
  1. **Top Navbar (`DashboardLayout.css`)**:
     - Se forzó `flex-wrap: nowrap` en `.header-actions` y `.top-bar` para evitar que el botón de actualizar quiebre a una segunda fila o se superponga flotando bajo la sede.
     - Se fijó truncado elíptico en `.branch-selector-pill`, `.branch-select-native` y `.branch-name-single` con anchos máximos adaptativos para smartphones (375px–480px).
  2. **Dashboard Hero Header y Slicers (`Dashboard.css`)**:
     - Se compactó el banner verde `.dashboard-hero-header` reduciendo padding y ocultando la descripción secundaria extensa en mobile (`display: none`), permitiendo que los KPIs principales ("Venta del Día", "N° de Autos", etc.) queden inmediatamente visibles en el primer viewport.
     - Se habilitó scroll horizontal táctil suave (`overflow-x: auto; -webkit-overflow-scrolling: touch; scrollbar-width: none;`) en `.slicers-group` para que las sedes y períodos se deslicen fluidamente sin cortarse en los bordes.
  3. **Gestión de Usuarios (`UsuariosTab.tsx` & `Settings.css`)**:
     - Se envolvió la tabla de usuarios en un contenedor `.table-responsive` con scroll horizontal y `min-width: 600px`, garantizando que las columnas de rol, estado y acciones se puedan visualizar y acceder cómodamente en smartphones.
  4. **Sub-pestañas del Modal de Parametrización por Sede (`Settings.css`)**:
     - Se configuró `.modal-subtabs-nav` con `overflow-x: auto`, `flex-wrap: nowrap` y márgenes optimizados para mobile, permitiendo que las 3 pestañas (*Medios de Pago, Asignación de Usuarios, Tarifas Vehiculares*) se visualicen y deslicen sin salirse del modal.
  5. **Tabla de Tarifas Vehiculares en Modal (`ParqueaderosTab.tsx`)**:
     - Se envolvió la tabla de tarifas dentro del modal en un contenedor `.table-responsive` con scroll horizontal, garantizando la visibilidad completa de los valores de hora, minuto, día máximo y tiempo de gracia.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/shared/ui/DashboardLayout.css`
  - `ParkingPwa/src/features/dashboard/ui/Dashboard.css`
  - `ParkingPwa/src/features/settings/ui/Settings.css`
  - `ParkingPwa/src/features/settings/ui/UsuariosTab.tsx`
  - `ParkingPwa/src/features/settings/ui/ParqueaderosTab.tsx`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `npm run build`: **0 Errores** (compilación de producción exitosa en 2.65s).
  - `oxlint`: **0 Errores** en todos los componentes.



### [2026-08-26 17:45:00] - [FEATURE] [WPF] [SHIFTS] [OPERATIONS] - Modo de 'Recepción y Toma de Relevo de Caja' para Operadores Entrantes
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **💬 Prompt Original del Usuario**:
  > *"mira ya se tienen los permisos faltaba publicar el api listo ya lo hice, sabes que sucede explicame acá que deberia hacer en la vida real pues estaba abierto el turno lo tenia admin yo ingrese otro usuario que tiene acceso a esa sede, entonces me sale ese mensaje pero deberia decirme que tomar turno y decirme cuanto estaba en caja y yo recibirlo exactamente revelar el turno por que la otra persona no esta, pero hay no aparece esa opcion ese boton no existe. explicame ..."*
- **🤖 Resumen Técnico para la IA**:
  1. **Detección Automática de Operador Titular vs Operador Entrante (`ShiftClosureViewModel.cs`)**:
     - Se incorporaron las propiedades `IsShiftOwner`, `ActiveShiftOperatorName` y `ActiveShiftStartTime`.
     - Si el operador conectado NO es el titular del turno abierto (`IsShiftOwner == false`), la interfaz no le exige entregar la caja a un tercero, sino que se adapta al modo **"Recepción y Toma de Caja"**.
  2. **Comando `TakeOverShiftCommand`**:
     - Permite que el operador entrante cuente el dinero físico de la gaveta, verifique la diferencia de arqueo contra el saldo esperado del sistema, y presione *"Recibir Caja, Tomar Turno e Iniciar Operación"*.
     - El comando cierra formalmente el turno del operador anterior (`activeShift`) con el dinero contado y abre de inmediato el nuevo turno a nombre del operador entrante con esa base, redirigiendo de inmediato a `CheckInViewModel` (Ingreso de Vehículos).
  3. **UI Adaptativa en XAML (`ShiftClosureView.xaml`)**:
     - Tarjeta de advertencia informativa indicando quién abrió el turno anterior y quién lo está asumiendo.
     - Botón principal de acción `ModernButton`: *"🤝 Recibir Caja, Tomar Turno e Iniciar Operación"*.
- **📦 Componentes Modificados**:
  - `Parking\ViewModels\ShiftClosureViewModel.cs`
  - `Parking\Views\ShiftClosureView.xaml`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build ParkingWpf.slnx`: **0 Errores**.
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **💬 Prompt Original del Usuario**:
  > *"mira esto corri el primer script me encanto ese script me faltaria complementarlo que funciones tengo dentro de hay para ver si de cada modulo pero entonces si sigue siendo error de la logica del wpf por que sigue saliendo que no tengo permisos, si me hago entender, no voy hacer el insert por que eso se hace desde la pwa y veo que lo hace bien el error esta en el wpf analiza bien eso por favor revisa detalladamente."*
- **🤖 Resumen Técnico para la IA**:
  1. **Reactividad Total en `Parking.Security.Authorize.cs`**:
     - Se implementó suscripción en `element.Loaded` y `element.Unloaded` a los eventos `PermissionsChanged` tanto de la instancia DI como de `PermissionService.Current`.
     - La re-evaluación se ejecuta de forma segura en el hilo del `Dispatcher` con `ApplyAuthorization(element)`, resolviendo el bug donde los `RadioButton` del menú lateral quedaban fijados en `Collapsed` tras el inicio de sesión.
  2. **Resiliencia de Permisos en SQLite Local (`AuthService.cs`)**:
     - Se añadió fallback resiliente en modo offline cuando la tabla `RolePermissions` en SQLite está vacía (base de datos recién creada o previa a la primera sincronización), garantizando que el operador pueda abrir el terminal sin bloqueos visuales.
- **📦 Componentes Modificados**:
  - `Parking\Security\Authorize.cs`
  - `Parking\Services\Implementations\AuthService.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build ParkingWpf.slnx`: **0 Errores**.
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **💬 Prompt Original del Usuario**:
  > *"bueno tengo este problema con los permisos mira que si se asignaron permisos al usuario que tiene el rol 2 pero ingreso en el wpf y me dice que no cuento con los permisos me imagino por que solo ha tomado los datos de la sql lite nada mas pero no ya elimine la db la volvi a mandar a crear y no no sirvio entonces que sucede por que no esta tomando los permisos correctamente ? que sucede hay revisa eso por que administrador si funciona ."*
  > *"eso esta gravisimo en el sistema no debe a ver nada quemado todo lo que traiga la base de datos si el quisiera crearlo como cajero o cajera o hasta colocar el rol que quisiera desde que tenga los permisos que es lo importante se deberia validar como se te ocurre eso . revisa eso que me acabas de decir esta supremamente mal y eso deberia ir en reglas del agent como colocar eso así eso no es una buena practica"*
- **🤖 Resumen Técnico para la IA**:
  1. **Incorporación de Regla de Oro en `AGENTS.md`**:
     - Se añadió la prohibición estricta de usar nombres de roles o listas de permisos quemados (`roleName.Contains(...)` o arrays fijos).
     - La asignación y validación debe ser 100% data-driven basada en la matriz relacional `RoleAction` / `RolePermission` / `Action.Slug`.
  2. **Eliminación Total de Permisos Estáticos en WPF (`AuthService.cs`)**:
     - Se eliminó el array `OperatorPermissions` y el método `GetDefaultPermissionsForRole`.
     - **Modo Online**: Carga directamente los slugs de `apiLogin.Permissions`.
     - **Modo Offline (SQLite)**: Carga directamente de la tabla `RolePermissions` los slugs activos para `user.RoleId`.
     - En `SwitchCurrentUser`, se consultan dinámicamente los permisos del rol en SQLite.
  3. **Entrega de Permisos Dinámicos en API (`AuthService.cs` & `AuthResponseDto.cs`)**:
     - Se agregó la propiedad `List<string> Permissions` en `AuthResponseDto` y en `LoginApiResponse` de WPF.
     - En `LoginStandardAsync`, se consultan los permisos reales del rol desde `_roleActionRepository.GetActionsByRoleAsync(user.UserRoleId)`.
- **📦 Componentes Modificados**:
  - `AGENTS.md` (WPF, API y PWA)
  - `ParkingApi.Domain\Dtos\Auth\AuthResponseDto.cs`
  - `ParkingApi.Core\Services\Auth\AuthService.cs`
  - `Parking\Models\ApiModels\TicketApiModels.cs`
  - `Parking\Services\Implementations\AuthService.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build ParkingApi.slnx`: **0 Errores**.
  - `dotnet build ParkingWpf.slnx`: **0 Errores**.
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **💬 Prompt Original del Usuario**:
  > *"necesito que el api tenga el swagger en produccion para validar si de verdad quedo bien desplegada. revisa que así este configurada."*
- **🤖 Resumen Técnico para la IA**:
  1. **Configuración del Middleware en `Program.cs` (`ParkingApi`)**:
     - Se removió la restricción `if (app.Environment.IsDevelopment())` que limitaba Swagger exclusivamente a entornos locales.
     - Se habilitó `app.UseSwagger()`, `app.UseSwaggerUI(...)` con `RoutePrefix = "swagger"` y redirección automática en la raíz `app.MapGet("/", () => Results.Redirect("/swagger"))` para todos los entornos (Producción, Staging y Desarrollo).
  2. **Verificación de Endpoints**:
     - Documentación interactiva accesible en la raíz del dominio (`/`) o directamente en `/swagger`.
- **📦 Componentes Modificados**:
  - `ParkingApi/Program.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build ParkingApi.slnx`: **0 Errores**.
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **💬 Prompt Original del Usuario**:
  > *"esta mal la sincro, por que esta sincronizando todo pero no esta filtrando por la sede especifica, osea ya estoy en una sede debe filtrar que eso que se creo sea para la sede especifica si me explico no que traiga todo el mundo si no solo lo que este asociado a la sede que estoy . analiza eso"*
- **🤖 Resumen Técnico para la IA**:
  1. **Backend Central (`ParkingApi`)**:
     - **Endpoint `/api/sync/bootstrap?branchId={branchId}`**: `SyncController.cs` y `ISyncService` / `SyncService.cs` ahora reciben `[From     - **Filtrado 100% Estricto de Datos (Sin Fuga de Registros Null)**:
       - `Branches`: Retorna la sede activa y su capacidad oficial configurada (`TotalCapacity`).
       - `Users`: Retorna únicamente usuarios asignados en `UserBranches` para esa sede + administradores globales.
       - `PaymentMethods`: Retorna los medios de pago configurados en `BranchPaymentMethods` para esa sede (o maestros activos si no hay parametrización exclusiva).
       - `VehicleRates`: Filtra `r.BranchId == branchId.Value` (Estricto).
       - `Stores` & `CommercialAgreements`: Filtra `s.BranchId == branchId.Value` y sus convenios.
       - `WorkShifts`: Filtra `ws.BranchId == branchId.Value`.
       - `MonthlySubscriptions`: Filtra `s.BranchId == branchId.Value`.
       - `ParkingTickets`: Filtra `t.BranchId == branchId.Value`.
  2. **Escritorio Reactivo (`ParkingWpf`)**:
     - **Cliente API (`ParkingApiClient.cs`)**: Envía el query string `?branchId={branchId}` en `GetBootstrapAsync`.
     - **Motor de Sincronización (`SyncEngineService.cs`)**: Inyecta `ISessionService` y transmite automáticamente `_sessionService.CurrentBranch?.Id`.
     - **Aislamiento en Memoria y Poda Local**: `EfPricingCalculatorService.ReloadRatesAsync()` y `SyncEngineService` aíslan estrictamente por sede (`BranchId == currentBranchId.Value`) eliminando cualquier registro con `BranchId = NULL` o de otra sede.para evitar cruces entre sedes.
- **📦 Componentes Modificados**:
  - `ParkingApi.Domain/Interfaces/Services/Sync/ISyncService.cs`
  - `ParkingApi.Core/Services/Sync/SyncService.cs`
  - `ParkingApi/Controllers/SyncController.cs`
  - `Parking/Services/Contracts/IApiClientService.cs`
  - `Parking/Services/Implementations/ParkingApiClient.cs`
  - `Parking/Services/Implementations/SyncEngineService.cs`
  - `Parking/Services/Implementations/EfPricingCalculatorService.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build ParkingApi.slnx`: **0 Errores**.
  - `dotnet build ParkingWpf.slnx`: **0 Errores**.
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **💬 Prompt Original del Usuario**:
  > *"tengo el siguiente caso, funciono perfecto cuando desde la pwa crearon la categoria y cuando sincronice genial la trajo y la mostro de una, pero cuando desde la pwa la volvieron a eliminar y le di sincronizar pues el sistema sincronizo pero no igualo la BD acá en local si me hago entender, por que si eliminan alguna tarifa, categoria, convenio, medio de pago, usuario entonces la sincronizacion debe ser bidireccional, lo unico que deberia subir el wpf a la bd por que es el modo offline son los ingresos y salidas y lo de los turnos pero todos los datos parametrizables que vienen de la BD esos siempre se deben sincronizar e estar igual a la bd online si me explico las dos diferencias ? analiza lo que te digo"*
- **🤖 Resumen Técnico para la IA**:
  1. **Modelo de Sincronización Espejo (Master-Replica)**:
     - **Upstream (WPF -> API)**: Cola offline procesa y sube ingresos, cobros/salidas (`ParkingTickets`, `TicketDiscounts`) y arqueos de caja (`WorkShifts`).
     - **Downstream Mirror (API -> WPF)**: La base de datos central en MySQL es la **fuente absoluta de verdad** para los catálogos parametrizables.
  2. **Poda (*Pruning*) en `SyncEngineService.cs`**:
     - **Tarifas (`VehicleRates`)**: Compara tipos de vehículos y IDs locales contra el conjunto remoto entregado en el bootstrap; cualquier tarifa que haya sido eliminada en la nube (o si no hay tarifas configuradas) es removida de SQLite (`db.VehicleRates.RemoveRange(...)`).
     - **Medios de Pago (`PaymentMethods`)**: Remueve medios de pago locales no presentes en el servidor.
     - **Comercios y Convenios (`Stores` / `CommercialAgreements`)**: Remueve convenios y comercios eliminados en la nube.
     - **Sedes (`Branches`)**: Remueve sedes no activas/eliminadas en la nube.
     - **Usuarios (`Users`)**: Remueve usuarios locales eliminados en la nube (preservando sesión del admin).
     - **Mensualidades (`MonthlySubscriptions`)**: Remueve mensualidades no vigentes/eliminadas en la nube.
  3. **Reactividad en Vistas e Invocación de Evento `DataSynchronized`**:
     - Al completar la sincronización, `DataSynchronized?.Invoke()` actualiza la memoria caché en `EfPricingCalculatorService` y refresca reactivamente las vistas (`CheckInViewModel`, `CheckOutViewModel`, `MonthlySubscriptionsViewModel`).
- **📦 Componentes Modificados**:
  - `Parking/Services/Implementations/SyncEngineService.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build ParkingWpf.slnx`: **0 Errores**.

### [2026-08-26 11:08:00] - [REFACTOR] [DB] [RBAC] - Reestructuración Oficial de Script RBAC Seed (Solo Rol Administrador, DDL para BD Vacía y 15 Módulos)
- **Autor**: Antigravity AI Assistant & Database Architect
- **💬 Prompt Original del Usuario**:
  > *"no debes crear el rol operador esolo es el rol administrrador y todas las funciones para ese rol si me explico ? analiza eso nuevamente"*
- **🤖 Resumen Técnico para la IA**:
  1. **Aprovisionamiento DDL Autónomo para BD Vacía**:
     - Se incorporaron las sentencias `CREATE TABLE IF NOT EXISTS` para todas las tablas requeridas por EF Core y la lógica de negocio (`IdentificationType`, `UserRole`, `User`, `Module`, `Operation`, `Action`, `RoleAction`, `UserRoleModule`, `PaymentMethod`, `Branches`, `UserBranches`, `BranchPaymentMethods`, `VehicleRates`, `Stores`, `CommercialAgreements`, `ParkingTickets`, `TicketDiscounts`, `WorkShifts`, `MonthlySubscriptions`, `BillingResolutions`, `VehicleIncidents`).
  2. **Exclusividad del Rol Administrador**:
     - Se eliminó la creación estática de los roles *Operador* y *Supervisor*. Únicamente se crea el rol `Administrador` (Id 1) y el usuario inicial `admin`.
     - Todos los roles operativos adicionales serán creados y parametrizados dinámicamente por el Administrador desde la PWA.
  3. **Catálogo de 15 Módulos y 69 Acciones**:
     - Se removió por completo la acción obsoleta `system.theme`.
     - Se agregaron los módulos y permisos para **Resoluciones de Facturación** (`resolutions.*`) y **Novedades y Bloqueo de Placas** (`novedades.*`), así como **Reportes Consolidados** (`reports.*`).
     - Se asignó el 100% de los 15 módulos y el 100% de las 69 acciones exclusivamente al rol Administrador.
  4. **Ajuste en Script de Limpieza**:
     - Se actualizaron `BillingResolutions` y `VehicleIncidents` en `01_Clean_All_Tables.sql`.
- **📦 Componentes Modificados**:
  - `ParkingApi/Scripts/02_Init_RBAC_Seed.sql`
  - `ParkingApi/Scripts/01_Clean_All_Tables.sql`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Sintaxis MySQL validada: **0 Errores**.
  - `dotnet build ParkingApi.slnx`: **0 Errores**.
- **Autor**: Antigravity AI Assistant & .NET Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Tengo una duda, es posible meter signal r al api y al wpf para que sea reactivo ? o eso no es posible ejemplo que me gustaria que si estoy en alguna sede y desde la pwa se le hacen algo a la sede ejemplo modifican los cupos o crean otra categoria y desde que este en linea osea con internet el wpf conectado al api pues deberia salir una alerta que diga es necesario sincronizar y que obligue a sincronizar si me explico ? eso sería posible ?"*
- **🤖 Resumen Técnico para la IA**:
  1. **Backend SignalR Central (`ParkingApi`)**:
     - **Hub Central (`ParkingHub.cs`)**: Creado en `/hubs/parking` con soporte para agrupación de terminales por sede (`JoinBranchGroup(branchId)`, `LeaveBranchGroup(branchId)`).
     - **Contratos y Servicio (`IRealtimeNotificationService`, `RealtimeNotificationService`, `ConfigNotificationDto`)**: Implementado servicio despachador de eventos en tiempo real inyectando `IHubContext<ParkingHub>`.
     - **Triggers en Controladores**: Notificación automática tras operaciones de mutación en `BranchesController` (cupos/datos/medios de pago), `VehicleRatesController` (tarifas), `AgreementsController` (convenios), `VehicleIncidentsController` (bloqueo/novedades de placas), `ResolutionsController` (resoluciones DIAN) y `PaymentMethodController` (medios de pago).
     - **Pipeline**: Configurado `builder.Services.AddSignalR()` y `app.MapHub<ParkingHub>("/hubs/parking")`.
  2. **Escritorio Reactivo (`ParkingWpf`)**:
     - **Cliente SignalR (`SignalRClientService.cs`, `ISignalRClientService`)**: Integrado `Microsoft.AspNetCore.SignalR.Client` con estrategia de reconexión automática (`WithAutomaticReconnect`), suscripción dinámica a la sede activa (`SetCurrentBranchAsync`) y resiliencia transparente ante modo offline.
     - **Modal Moderno de Sincronización Requerida (`SyncRequiredDialog.xaml/.cs`)**: Implementado diálogo modal con estilo `ModernButton`, paleta `BrushWarning` / `BrushPrimary`, que bloquea amigablemente la terminal informando el cambio recibido y permitiendo pulsar *"⚡ Sincronizar Ahora"*.
     - **Flujo Guiado**: Al pulsar sincronizar, invoca el orquestador visual `SyncProgressDialog.ShowSyncAsync()`, ejecuta la sincronización completa paso a paso, actualiza la capacidad en el TopBar y refresca la interfaz en caliente.
     - **Integración MVVM**: Conectado en `MainShellViewModel.cs` para escuchar `ConfigUpdateRequired` y gestionar cambios de sede activa.
  3. **Preservación PWA**: Se mantuvo la PWA 100% intacta sin requerir modificaciones.
- **📦 Componentes Modificados y Creados**:
  - `ParkingApi/Hubs/ParkingHub.cs` [NEW]
  - `ParkingApi.Domain/Dtos/Realtime/ConfigNotificationDto.cs` [NEW]
  - `ParkingApi.Domain/Interfaces/Services/Realtime/IRealtimeNotificationService.cs` [NEW]
  - `ParkingApi/Services/Realtime/RealtimeNotificationService.cs` [NEW]
  - `ParkingApi/Controllers/BranchesController.cs`
  - `ParkingApi/Controllers/VehicleRatesController.cs`
  - `ParkingApi/Controllers/AgreementsController.cs`
  - `ParkingApi/Controllers/VehicleIncidentsController.cs`
  - `ParkingApi/Controllers/ResolutionsController.cs`
  - `ParkingApi/Controllers/PaymentMethodController.cs`
  - `ParkingApi/Program.cs`
  - `Parking/Parking.csproj`
  - `Parking/Models/ApiModels/ConfigNotificationDto.cs` [NEW]
  - `Parking/Services/Contracts/ISignalRClientService.cs` [NEW]
  - `Parking/Services/Implementations/SignalRClientService.cs` [NEW]
  - `Parking/Views/SyncRequiredDialog.xaml` [NEW]
  - `Parking/Views/SyncRequiredDialog.xaml.cs` [NEW]
  - `Parking/Services/Contracts/IDialogService.cs`
  - `Parking/Services/Implementations/DialogService.cs`
  - `Parking/ViewModels/MainShellViewModel.cs`
  - `Parking/App.xaml.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build ParkingApi.slnx`: **0 Errores**.
  - `dotnet build ParkingWpf.slnx`: **0 Errores**.
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"esto conectalo alas diferetntes resoluciones que tenga creada y se hayan usado desde el wpf cuando elijen una resolucion para dar salida a un vehiculo , recuerda no tocar aun wpf"*
- **🤖 Resumen Técnico para la IA**:
  1. **Backend y Base de Datos (`ParkingApi`)**:
     - **Modelo `ParkingTicket`**: Incorporados campos `ResolutionId` (`Guid?`), `ResolutionName` (`string?`), `InvoiceNumber` (`string?`) e `IsElectronicInvoice` (`bool`).
     - **Configuración EF Core y MySQL**: Mapeados en `EntityConfigurations.cs` con índices. En `Program.cs`, se ejecuta la verificación y adición automática de columnas (`ALTER TABLE ParkingTickets ADD COLUMN...`) en la base de datos MySQL al inicializar.
     - **Métricas Analíticas (`AnalyticsService` & `FinancialSummaryDto`)**: Agregados `CountByResolution` y `RevenueByResolution` en `FinancialSummaryDto`. En `AnalyticsService.GetDailySummaryAsync`, se agrupan y totalizan los tiquetes cobrados según su resolución de facturación utilizada.
  2. **Frontend PWA (`ParkingPwa`)**:
     - **Contratos (`DashboardContracts.ts`)**: Añadidos campos opcionales `countByResolution` y `revenueByResolution` en `DailySummaryDto`.
     - **Vista (`Dashboard.tsx`)**:
       - Integrado `resolucionesService.getAllResolutions()` en la carga concurrente de `Promise.all`.
       - Reemplazada la tarjeta estática de facturación electrónica por la tarjeta interactiva **"Distribución por Resoluciones de Facturación"**.
       - Mapeo dinámico de `resolutionsDonutData` iterando sobre todas las resoluciones activas en la BD (ej. *FACTURA POS, FV, FVM, Factura Electrónica*), mostrando el nombre de la resolución con su prefijo, la cantidad de documentos emitidos (`X doc(s)`) y el porcentaje de emisión sobre el total del día.
  3. **WPF**: Se mantuvo 100% intacto sin modificaciones, quedando la API y la estructura de datos preparadas para recibir la selección de resolución al momento del checkout de vehículos.
- **📦 Componentes Modificados**:
  - `ParkingApi/ParkingApi.Domain/Models/ParkingTicket.cs`
  - `ParkingApi/ParkingApi.Domain/Dtos/Analytics/FinancialSummaryDto.cs`
  - `ParkingApi/ParkingApi.Infrastructure/Data/Configurations/EntityConfigurations.cs`
  - `ParkingApi/ParkingApi.Core/Services/Analytics/AnalyticsService.cs`
  - `ParkingApi/ParkingApi/Program.cs`
  - `ParkingPwa/src/features/dashboard/model/DashboardContracts.ts`
  - `ParkingPwa/src/features/dashboard/ui/Dashboard.tsx`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build ParkingApi.slnx`: **0 Errores**.
  - `oxlint src/features/dashboard/ui/Dashboard.tsx`: **0 Errores**.
  - Servidor `ParkingApi` en ejecución en `http://localhost:5135` con esquema actualizado.



### [2026-08-26 00:43:00] - [FEATURE] [API + PWA] - Módulo de Novedades, Incidencias y Bloqueo de Placas
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"en el modulo de novedades, quiero que exista un boton para argegar novedad, lacual me permitira agregar cualquier tipo de novedad, por ejemplo si tengo una placa la cual no me pago o note que roba en mi parking, me la permita bloquear para que el wpf no la pueda ingresar, de igual manera exponeel servicio api pero aun no toques el wpf"*
- **🤖 Resumen Técnico para la IA**:
  1. **Backend y Base de Datos (`ParkingApi`)**:
     - **Modelo y DTOs**: Creados `VehicleIncident.cs`, `VehicleIncidentDto.cs`, `SaveVehicleIncidentDto.cs`, `PlateCheckResultDto.cs` y `ResolveIncidentDto.cs`.
     - **Mapeo EF Core y MySQL**: Registrado en `DataContext.cs` y `EntityConfigurations.cs`. Aprovisionamiento automático de la tabla `VehicleIncidents` con índices para `PlateNumber`, `BranchId`, `IsBlocked` y `Status`.
     - **Capa Repositorio y Servicio**: Creados `IVehicleIncidentRepository`, `VehicleIncidentRepository`, `IVehicleIncidentService`, `VehicleIncidentService` y registrados en IoC.
     - **Controlador API**: `VehicleIncidentsController.cs` exponiendo:
       - `GET /api/VehicleIncidents`: Listado de novedades con soporte para filtros por sede, estado y texto.
       - `GET /api/VehicleIncidents/check-plate/{plate}`: Endpoint clave ultrarrápido para que el WPF y la PWA verifiquen en tiempo real si una placa tiene bloqueos o alertas activas al momento de registrar el ingreso.
       - `POST /api/VehicleIncidents`: Crear novedad / bloqueo.
       - `PUT /api/VehicleIncidents/{id}`: Editar novedad.
       - `POST /api/VehicleIncidents/{id}/resolve`: Resolver novedad y levantar bloqueo con justificación documentada.
       - `DELETE /api/VehicleIncidents/{id}`: Eliminar registro.
  2. **Frontend PWA (`ParkingPwa`)**:
     - **Servicio y Contratos**: `NovedadesContracts.ts` y `novedadesService.ts`.
     - **Interfaz Completa (`Novedades.tsx`)**:
       - Botón **`+ Agregar Novedad`** en la barra superior.
       - Barra de herramientas con filtros rápidos (*Todas*, *⛔ Bloqueados*, *Activas*, *Resueltas*) y buscador en tiempo real.
       - Tabla con badges visuales destacados de ⛔ `BLOQUEADO` en rojo para vehículos restringidos.
       - Modal para registrar/editar novedades con switch destacado de bloqueo, selección de sede, observaciones y contacto.
       - Modal para resolver novedades y documentar la justificación del desbloqueo.
  3. **WPF**: Se preservó intacto sin modificaciones conforme a la instrucción.
- **📦 Componentes Modificados y Creados**:
  - `ParkingApi/ParkingApi.Domain/Models/VehicleIncident.cs`
  - `ParkingApi/ParkingApi.Domain/Dtos/Incidents/VehicleIncidentDto.cs`
  - `ParkingApi/ParkingApi.Domain/Dtos/Incidents/SaveVehicleIncidentDto.cs`
  - `ParkingApi/ParkingApi.Domain/Dtos/Incidents/PlateCheckResultDto.cs`
  - `ParkingApi/ParkingApi.Domain/Dtos/Incidents/ResolveIncidentDto.cs`
  - `ParkingApi/ParkingApi.Domain/Interfaces/Repositories/Incidents/IVehicleIncidentRepository.cs`
  - `ParkingApi/ParkingApi.Domain/Interfaces/Services/Incidents/IVehicleIncidentService.cs`
  - `ParkingApi/ParkingApi.Infrastructure/Data/DataContext.cs`
  - `ParkingApi/ParkingApi.Infrastructure/Data/Configurations/EntityConfigurations.cs`
  - `ParkingApi/ParkingApi.Infrastructure/Data/Repositories/Incidents/VehicleIncidentRepository.cs`
  - `ParkingApi/ParkingApi.Infrastructure/Extensions/RepositoryExtensions.cs`
  - `ParkingApi/ParkingApi.Core/Services/Incidents/VehicleIncidentService.cs`
  - `ParkingApi/ParkingApi.Core/Extensions/ServiceExtensions.cs`
  - `ParkingApi/ParkingApi/Controllers/VehicleIncidentsController.cs`
  - `ParkingApi/ParkingApi/Program.cs`
  - `ParkingPwa/src/features/novedades/model/NovedadesContracts.ts`
  - `ParkingPwa/src/features/novedades/data/novedadesService.ts`
  - `ParkingPwa/src/features/novedades/ui/Novedades.tsx`
  - `ParkingPwa/src/features/novedades/ui/Novedades.css`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build ParkingApi.slnx`: **0 Errores**.
  - `oxlint`: **0 Errores**.
  - Servidor API en ejecución en `http://localhost:5135` con endpoints probados exitosamente (HTTP 200/201).

### [2026-08-26 00:36:00] - [UI/UX] [PWA] - Organización de Encabezado en Módulo de Novedades (Remoción de Badge y Alineación)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Lo  amarilo quitalo, lo rojo organizalo mejor"*
- **🤖 Resumen Técnico para la IA**:
  1. **Ajuste en `Novedades.tsx` y `Novedades.css`**:
     - Se retiró el badge de sede redundante señalado en amarillo al lado del título.
     - Se reorganizó el encabezado `.novedades-header` con `flex-direction: column` y tipografía clara para que la descripción se sitúe ordenadamente debajo del título en lugar de quedar desalineada hacia la derecha.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/novedades/ui/Novedades.tsx`
  - `ParkingPwa/src/features/novedades/ui/Novedades.css`
  - `HISTORIAL_CAMBIOS.md`



### [2026-08-26 00:35:00] - [UI/UX] [PWA] - Remoción de Punto de Color Redundante en Leyenda de Métodos de Pago
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"en el dashboard , en Distribución por Métodos de Pago no quiero que muestre [captura señalando el punto verde junto al emoji]"*
- **🤖 Resumen Técnico para la IA**:
  1. **Ajuste en `Dashboard.tsx`**:
     - Se eliminó el elemento `<div className="pie-legend-dot" style={{ background: item.color }} />` de la leyenda de la tarjeta *"Distribución por Métodos de Pago"*.
     - Ahora la leyenda presenta directamente el ícono/emoji asignado seguido del nombre del medio de pago (ej. 🎟️ Nequi), logrando una apariencia más limpia y sin elementos duplicados.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/dashboard/ui/Dashboard.tsx`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Linter PWA: 0 Errores.
  - Hot Module Replacement (HMR) activo en Vite Dev Server.



### [2026-08-26 00:32:00] - [FEATURE] [API + PWA] - Módulo de Resoluciones de Facturación (DIAN / POS / Factura Electrónica)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"En configuracion crea otra opcion que se llame resolucion y esta que tenga estas opciones, adicional crea una api que es la expondra toda info a mi wpf, pero aun no toques nada delwpf"*
- **🤖 Resumen Técnico para la IA**:
  1. **Backend y Base de Datos (`ParkingApi`)**:
     - **Modelo y DTOs**: Creados `BillingResolution.cs`, `BillingResolutionDto.cs`, `SaveBillingResolutionDto.cs`.
     - **Mapeo EF Core**: Integrado en `DataContext.cs` y `EntityConfigurations.cs` (`BillingResolutions`).
     - **Inicialización de Esquema**: `Program.cs` aprovisiona automáticamente la tabla `BillingResolutions` con llaves e índices.
     - **Capa Repositorio y Servicio**: Creados `IBillingResolutionRepository`, `BillingResolutionRepository`, `IBillingResolutionService`, `BillingResolutionService` y registrados en IoC.
     - **Controlador API**: `ResolutionsController.cs` exponiendo `GET /api/Resolutions`, `GET /api/Resolutions/active`, `GET /api/Resolutions/by-branch/{branchId}`, `POST`, `PUT`, `DELETE`.
  2. **Frontend PWA (`ParkingPwa`)**:
     - **Contratos y Servicio**: `ResolucionesContracts.ts` y `resolucionesService.ts`.
     - **Vista y Tabla de Resoluciones**: Creado `ResolucionesTab.tsx` replicando la interfaz solicitada:
       - Buscador en tiempo real por nombre, prefijo o número de resolución.
       - Tabla con columnas: *Nombre Resolución, Tipo de Documento, Prefijo, Número, Desde, Hasta, Fecha Desde, Fecha Hasta, Estado, Acciones*.
       - Modal para Crear y Editar con selector de tipos de documentos comunes o texto personalizado, rangos numéricos, fechas de vigencia y clave técnica DIAN.
     - **Integración de Menú**: Actualizado `Settings.tsx` con la nueva pestaña **Resoluciones** (`FileCheck`).
  3. **WPF**: Se preservó intacto sin modificaciones conforme a la instrucción.
- **📦 Componentes Modificados y Creados**:
  - `ParkingApi/ParkingApi.Domain/Models/BillingResolution.cs`
  - `ParkingApi/ParkingApi.Domain/Dtos/Billing/BillingResolutionDto.cs`
  - `ParkingApi/ParkingApi.Domain/Dtos/Billing/SaveBillingResolutionDto.cs`
  - `ParkingApi/ParkingApi.Domain/Interfaces/Repositories/Billing/IBillingResolutionRepository.cs`
  - `ParkingApi/ParkingApi.Domain/Interfaces/Services/Billing/IBillingResolutionService.cs`
  - `ParkingApi/ParkingApi.Infrastructure/Data/DataContext.cs`
  - `ParkingApi/ParkingApi.Infrastructure/Data/Configurations/EntityConfigurations.cs`
  - `ParkingApi/ParkingApi.Infrastructure/Data/Repositories/Billing/BillingResolutionRepository.cs`
  - `ParkingApi/ParkingApi.Infrastructure/Extensions/RepositoryExtensions.cs`
  - `ParkingApi/ParkingApi.Core/Services/Billing/BillingResolutionService.cs`
  - `ParkingApi/ParkingApi.Core/Extensions/ServiceExtensions.cs`
  - `ParkingApi/ParkingApi/Controllers/ResolutionsController.cs`
  - `ParkingApi/ParkingApi/Program.cs`
  - `ParkingPwa/src/features/settings/model/ResolucionesContracts.ts`
  - `ParkingPwa/src/features/settings/data/resolucionesService.ts`
  - `ParkingPwa/src/features/settings/ui/ResolucionesTab.tsx`
  - `ParkingPwa/src/features/settings/ui/Settings.tsx`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - `dotnet build ParkingApi.slnx`: **0 Errores**.
  - `oxlint`: **0 Errores**.
  - Servidor API en ejecución en `http://localhost:5135` con endpoints probados exitosamente (HTTP 200/201).



### [2026-08-26 00:18:00] - [FEATURE] [PWA] - Conexión Dinámica de Medios de Pago en Dashboard (BD + API)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Conecta de la dashboard los medios de pago que se encuentran creados en la Bd y api"*
- **🤖 Resumen Técnico para la IA**:
  1. **Integración con `mediosPagoService`**:
     - Se vinculó el llamado a `mediosPagoService.getPaymentMethods()` dentro de `Dashboard.tsx` (`Promise.all`), cargando en tiempo real todos los medios de pago activos parametrizados en la base de datos MySQL (`PaymentMethod`).
  2. **Renderizado Dinámico de Gráficas y Listas**:
     - **Gráfica de Torta (Donut Chart)**: Ahora se genera dinámicamente con la lista real de medios de pago de la base de datos (con sus respectivos íconos/emojis, nombres y colores corporativos asignados).
     - **Leyenda y Desglose Diario**: Muestra cada medio de pago registrado con su ícono, total recaudado y porcentaje sobre el total de ventas.
     - **Mapeo de Recaudación**: Vinculado con el desglose `revenueByPaymentMethod` de la API financiera.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/dashboard/ui/Dashboard.tsx`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Linter PWA: 0 Errores.
  - Hot Module Replacement (HMR) activo en Vite Dev Server.



### [2026-08-26 00:13:00] - [UI/UX] [PWA] - Ajuste de Colores en Dashboard (Botón Actualizado Gris Oscuro y Filtros Activos en Negro)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"el boton de actualizado dejame en el gris oscuro y los botones de Punto / Parqueadero seleccionado dejamelos en negros"*
- **🤖 Resumen Técnico para la IA**:
  1. **Ajuste en `Dashboard.css`**:
     - `.btn-glass` (Botón "Actualizado"): Configurado en gris oscuro ejecutivo (`#1e293b`, borde `#334155`, hover `#0f172a`).
     - `.slicer-pill.active` (Botones de Punto / Parqueadero y Período seleccionados): Configurados en negro sólido (`#0f172a` / borde `#000000`) con texto en blanco para máximo contraste y distinción.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/dashboard/ui/Dashboard.css`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Linter PWA: 0 Errores.
  - Hot Module Replacement (HMR) activo en Vite Dev Server.



### [2026-08-26 00:08:00] - [UI/UX] [PWA] - Realce y Contraste del Banner Ejecutivo en Dashboard (Fondo Corporativo y Letra Blanca en Negrita)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"En la dashboard, ese cuadro puedes ponerle letra blanca ngrilla para que resalte"*
- **🤖 Resumen Técnico para la IA**:
  1. **Rediseño del Hero Header en `Dashboard.css` y `Dashboard.tsx`**:
     - Se reemplazó el fondo grisáceo por un degradado de alta gama con el color corporativo oficial (`linear-gradient(135deg, #07665e 0%, #054e48 100%)`).
     - Se configuró la tipografía del título y subtítulo en **blanco puro (`#ffffff`) con peso en negrita (`font-weight: 700 / 800`)** y sutil sombra para máximo impacto y legibilidad ejecutiva.
     - Se actualizaron los botones de acción (`.btn-glass`) y el badge de estado (`.dashboard-status-badge`) con estilo glassmorphism translúcido y texto blanco en negrita.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/dashboard/ui/Dashboard.css`
  - `ParkingPwa/src/features/dashboard/ui/Dashboard.tsx`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Linter PWA: 0 Errores.
  - Hot Module Replacement (HMR) activo en Vite Dev Server.



### [2026-08-26 00:05:00] - [UI/UX] [PWA] - Remoción Global de Textos y Sufijos 'COP' en Toda la Aplicación Web
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"No me pongas COP en ninguna parte del pwa"*
- **🤖 Resumen Técnico para la IA**:
  1. **Limpieza Exhaustiva en Toda la PWA**:
     - Se auditaron y eliminaron todas las ocurrencias del sufijo/texto `"COP"` en la interfaz, etiquetas de formulario, títulos, tablas y exportaciones a Excel.
     - **Componentes ajustados**:
       - `VehiculosConfigTab.tsx`: Tablas y etiquetas de tarifas ($).
       - `TarifasTab.tsx`: Valores de hora, minuto y día ($).
       - `ConveniosTab.tsx`: Textos de descuentos y compra mínima ($).
       - `Reports.tsx`: Tarjetas de KPI, columnas de tiquetes y columnas de exportación a Excel.
       - `Dashboard.tsx`: Tarjetas métricas de recaudación, ticket promedio, convenios y desglose de medios de pago.
       - `Caja.tsx`: Tarjetas de ingresos/esperado en caja, tabla de turnos, modales de apertura/cierre y exportación a Excel.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/settings/ui/VehiculosConfigTab.tsx`
  - `ParkingPwa/src/features/settings/ui/TarifasTab.tsx`
  - `ParkingPwa/src/features/settings/ui/ConveniosTab.tsx`
  - `ParkingPwa/src/features/reports/ui/Reports.tsx`
  - `ParkingPwa/src/features/dashboard/ui/Dashboard.tsx`
  - `ParkingPwa/src/features/caja/ui/Caja.tsx`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Búsqueda global de `COP` en `ParkingPwa/src` ➡️ **0 coincidencias**.
  - Linter PWA: 0 Errores.
  - Hot Module Replacement (HMR) activo en Vite Dev Server.



### [2026-08-26 00:02:00] - [UI/UX] [PWA] - Separación Limpia de Columnas en Medios de Pago (Nombre Puro y Columna Ícono Exclusiva)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"no quiero que el nombre me muestre con el icono, adicional no quiero que icono emoji, si no solo icono y me muestere el icono"*
- **🤖 Resumen Técnico para la IA**:
  1. **Ajuste de Columnas en `MediosPagoTab.tsx`**:
     - Columna `MEDIO DE PAGO`: Muestra únicamente el nombre textual limpio del medio de pago (sin duplicar el avatar/ícono al lado).
     - Columna `ÍCONO`: Encabezado renombrado a `ÍCONO` con visualización centrada y limpia del ícono seleccionado.
     - Modal: Encabezado actualizado a *"Selecciona un Ícono Representativo"*.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/settings/ui/MediosPagoTab.tsx`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Linter PWA: 0 Errores.
  - Hot Module Replacement (HMR) activo en Vite Dev Server.



### [2026-08-26 00:00:00] - [FIX] [API] [DB] - Resolución de Error 500 en Creación de Convenios (Aprovisionamiento de Columna ImageUrl en MySQL)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"para crear un convenio me sale http://localhost:5135/api/Agreements error 500, porqu es"*
- **🤖 Resumen Técnico para la IA**:
  1. **Causa Raíz Identificada**:
     - El log de la API arrojaba la excepción `MySqlException: Unknown column 'c.ImageUrl' in 'field list'` y `Unknown column 'ImageUrl' in 'field list'` al ejecutar las sentencias `INSERT/SELECT` contra la tabla `CommercialAgreements` en la base de datos MySQL remota (`db_acd7d6_parking`).
  2. **Solución Implementada (`Program.cs`)**:
     - Se implementó una rutina de aprovisionamiento seguro de esquema al iniciar la API (`information_schema.COLUMNS` check + `ALTER TABLE CommercialAgreements ADD COLUMN ImageUrl LONGTEXT NULL`).
     - Se reinició el servicio `ParkingApi` en el puerto `5135` confirmando la creación exitosa de la columna y verificando la respuesta HTTP 200 OK en `/api/Agreements`.
- **📦 Componentes Modificados**:
  - `ParkingApi/ParkingApi/Program.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Compilación API: `dotnet build ParkingApi.slnx` ➡️ **0 Errores**.
  - Verificación Endpoint: `GET /api/Agreements` ➡️ **200 OK**.
  - Servicio API activo en background en `http://localhost:5135`.



### [2026-08-25 23:58:00] - [UI/UX] [PWA] - Simplificación del Modal de Medios de Pago (Selector de Emojis Limpio y Exclusivo)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"eliminame esto, me gusta que hayan algunos emojjs y esos sean los seleccionables"*
- **🤖 Resumen Técnico para la IA**:
  1. **Depuración de UI en `MediosPagoTab.tsx`**:
     - Se eliminó el campo de texto libre redundant (`<input placeholder="Pega un emoji o escribe un texto...">`).
     - Se mantuvo exclusivamente la cuadrícula interactiva con los emojis temáticos seleccionables (💵, 💳, 📱, 📲, 🏦, 💰, 🪙, 👛, 🧾, 💸, 🏧, 🎟️, 🏷️, ⚡, 💎, 💼) con resaltado del seleccionado.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/settings/ui/MediosPagoTab.tsx`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Linter PWA: 0 Errores.
  - Hot Module Replacement (HMR) activo en Vite Dev Server.



### [2026-08-25 23:55:00] - [FEAT] [API] [PWA] [WPF] - Carga, Almacenamiento y Visualización de Imágenes/Logos en Convenios Comerciales
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Para los convenios, quiero que tenga la opcion de cargarle una imagen al crear los convenios"*
- **🤖 Resumen Técnico para la IA**:
  1. **Frontend PWA (`ConveniosTab.tsx` & `ConveniosContracts.ts`)**:
     - Se implementó una zona interactiva para subir o arrastrar imágenes (PNG, JPG, WebP, SVG) con previsualización en vivo, conversión automática a DataURL/Base64 (`FileReader`) y controles para cambiar o remover la imagen.
     - Se actualizó la tabla principal para renderizar la miniatura/avatar del logo del convenio.
     - Se extendieron `CommercialAgreementDto` y `SaveCommercialAgreementDto` con la propiedad `imageUrl`.
  2. **Backend API (`ParkingApi`)**:
     - Se añadió `ImageUrl` a `CommercialAgreement.cs` en `ParkingApi.Domain` y se configuró como columna `longtext` en `EntityConfigurations.cs`.
     - Se actualizó el repositorio `CommercialAgreementRepository.cs` para persistir `ImageUrl` en `UpdateAsync` y `AddAsync`.
  3. **Escritorio WPF (`ParkingWpf`)**:
     - Se extendió la entidad `CommercialAgreement.cs` con la propiedad `ImageUrl` para asegurar la sincronización multi-plataforma.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/settings/ui/ConveniosTab.tsx`
  - `ParkingPwa/src/features/settings/model/ConveniosContracts.ts`
  - `ParkingApi/ParkingApi.Domain/Models/CommercialAgreement.cs`
  - `ParkingApi/ParkingApi.Infrastructure/Data/Configurations/EntityConfigurations.cs`
  - `ParkingApi/ParkingApi.Infrastructure/Data/Repositories/Agreements/CommercialAgreementRepository.cs`
  - `ParkingWpf/Parking/Entities/CommercialAgreement.cs`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Compilación API: `dotnet build ParkingApi.slnx` ➡️ **0 Errores**.
  - Compilación WPF: `dotnet build Parking.csproj` ➡️ **0 Errores**.
  - Linter PWA: 0 Errores.
  - Hot Module Replacement (HMR) activo en Vite Dev Server.



### [2026-08-25 23:45:00] - [FEAT] [UI/UX] [PWA] - Selector Interactivo de Emojis y Campos Abiertos para Medios de Pago
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"En la creacion de medio de pago no quiero que la categoria tenga ya una lista cargada, eso sera que el usuario la ingrese, y para la imagen que haya una seleccion de emojis"*
- **🤖 Resumen Técnico para la IA**:
  1. **Selector de Emojis y Entrada Libre (`MediosPagoTab.tsx`)**:
     - Se eliminó el menú `<select>` de categorías predefinidas y se transformó en un selector visual interactivo en cuadrícula con emojis temáticos financieros y de pago (💵, 💳, 📱, 📲, 🏦, 💰, 🪙, 👛, 🧾, 💸, 🏧, 🎟️, 🏷️, ⚡, 💎, 💼).
     - Se agregó soporte para ingresar o pegar emojis/textos personalizados directamente.
     - Se actualizó `getIconComponent` para soportar renderizado directo de caracteres Unicode y emojis en tablas y modales.
  2. **Estandarización de Botones de Modal**:
     - Se actualizó el botón Cancelar para emplear la clase estándar `btn-secondary`.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/settings/ui/MediosPagoTab.tsx`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Linter PWA: 0 Errores.
  - Hot Module Replacement (HMR) activo en Vite Dev Server.



### [2026-08-25 23:40:00] - [FIX] [UI/UX] [PWA] - Normalización y Corrección de Estilos en Botones de Cancelar en Modales de Roles y Permisos
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"el boton de cancelar de configuracion de permisos y el de crear rol no se ve con el estilo correcto"*
- **🤖 Resumen Técnico para la IA**:
  1. **Alineación de Estilos CSS (`Settings.css` & `index.css`)**:
     - Se unificó el selector `.btn-cancel` vinculándolo a las definiciones visuales de `.btn-secondary` (fondo `#f1f5f9`, borde `#e2e8f0`, radio de 10px, tipografía Inter con peso 600, sombra suave y transiciones de hover/active).
  2. **Refactorización en `RolesTab.tsx`**:
     - Se actualizaron los botones Cancelar tanto del modal de **Crear/Editar Rol** como del modal de **Configurar Permisos** para emplear la clase estándar `btn-secondary`.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/settings/ui/RolesTab.tsx`
  - `ParkingPwa/src/features/settings/ui/Settings.css`
  - `ParkingPwa/src/index.css`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Linter PWA: 0 Errores.
  - Hot Module Replacement (HMR) activo en Vite Dev Server.



### [2026-08-25 23:35:00] - [FEAT] [UI/UX] [PWA] - Separación y Control Granular de Permisos por Plataforma (Escritorio WPF & Web PWA) con Protección Total para Administradores
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"perfecto, existe la manera que desde esa configuracion de permisos, se pueda controlar los permisos a los modulos de la web (pwa) y escritorio (wpf), los que ya existen creo que son del escritorio, sin embargo desarroolla y implementa los de la web y alli en esa configuracion se ven separados, deja que los administradores cuenten con todos los permisos de web y escritorio y los demas ahi si sean seleccionable"*
- **🤖 Resumen Técnico para la IA**:
  1. **Separación de Módulos por Plataforma en `RolesTab.tsx`**:
     - Se implementó un selector de pestañas para **🖥️ Módulos Escritorio (WPF)** y **🌐 Módulos Web (PWA)** dentro del modal de configuración de permisos por rol.
     - Clasificación inteligente y exhaustiva de módulos y acciones según su dominio operativo (WPF Terminal: CheckIn, CheckOut, Turnos/Caja, Patio, Sistema; PWA Cloud: Dashboard, Sedes, Tarifas, Medios de Pago, Convenios, Mensualidades, Usuarios, Roles y Permisos).
     - Contadores de permisos en tiempo real (`X / Y activos`) individuales por plataforma y global.
     - Botones de acción rápida: *"Marcar Plataforma"*, *"Desmarcar Plataforma"*, *"Marcar Todo Global"* y *"Limpiar Todo"*.
     - Cada pestaña mantiene el comportamiento de acordeón exclusivo (primer módulo abierto inicialmente y cierre automático del anterior al expandir uno nuevo).
  2. **Protección Total y Automática para Administradores**:
     - El rol Administrador (ID 1 o nombre Administrador/Admin) cuenta con el 100% de los permisos (Web + Escritorio) protegidos contra desconfiguración o bloqueo accidental, mostrando una insignia dorada de "Full Access (WPF + PWA)".
     - Para los demás roles (Operadores, Supervisores, etc.), todas las casillas de Escritorio y Web son 100% seleccionables y se persisten en base de datos mediante `POST /api/RoleActions/AssignRolePermissions`.
  3. **Resolución Bidireccional de Permisos y Aliases en `authService.ts`**:
     - Se fortaleció `authService.hasPermission()` para resolver de forma bidireccional tanto los slugs estándar de la API (`branches.view`, `users.view`, `rates.view`, `agreements.view`, `payment_methods.view`, etc.) como los nombres de módulos de UI (`settings.*`, `dashboard.*`).
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/settings/ui/RolesTab.tsx`
  - `ParkingPwa/src/features/auth/data/authService.ts`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Linter PWA: 0 Errores.
  - Compilación WPF: `dotnet build` ➡️ 0 Errores.
  - Hot Module Replacement (HMR) activo en Vite Dev Server.



### [2026-08-25 23:15:00] - [UI/UX] [PWA] - Acordeón Exclusivo de Módulos en Matriz de Permisos de Roles (Apertura Única Inicial y Auto-Cierre)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Ayudame con organizar la configuracion de permisos deroles, ya que quisiera que filtrs de los modulos se ven desplegados al abrir, pero quisiera que solo se vea el primero desplegado y los demas no, que si desplego otro, se cierre el que este abierto, en este caso es el 1ro"*
- **🤖 Resumen Técnico para la IA**:
  1. **Refactorización de Estado de Expansión en PWA (`RolesTab.tsx`)**:
     - Se transformó el estado `expandedModules: Record<number, boolean>` en `expandedModuleId: number | null`, centralizando el identificador del módulo actualmente expandido.
     - Al abrir el modal de permisos (`handleOpenPermissionsModal`), se identifica el ID del primer módulo (`allModules[0]?.id`) y se establece como el único abierto por defecto.
     - En `toggleModuleAccordion`, se implementó la alternancia exclusiva de tipo acordeón: si el usuario hace clic sobre un módulo cerrado, se abre este y se cierra automáticamente el anterior; si hace clic sobre el módulo abierto, se colapsa.
     - La visualización condicional respeta el término de búsqueda activo para permitir visualización global cuando se busca un permiso específico.
- **📦 Componentes Modificados**:
  - `ParkingPwa/src/features/settings/ui/RolesTab.tsx`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Linter: 0 Errores.
  - Hot Module Replacement (HMR) activo en Vite Dev Server.



### [2026-08-25 23:55:00] - [FEAT] [FIX] [API] [PWA] - Auditoría y Conexión Total de Convenios y Comercios Aliados (CRUD 100% Real, Model Binding Fix y Soporte Dual)
- **Autor**: Antigravity AI Assistant & Software Architect
- **💬 Prompt Original del Usuario**:
  > *"Revisa los convenios, que se encuentre correactmente conectado a la api, si las opciones que muestran alli son las correctas"*
- **🤖 Resumen Técnico para la IA**:
  1. **Auditoría y Corrección en Backend .NET 8 (ParkingApi)**:
     - En `CommercialAgreement.cs`, la propiedad de navegación `Store` estaba tipada como no-anulable obligatoria (`= null!`), lo que provocaba que ASP.NET Core model validation rechazara (`400 Bad Request`) las peticiones de creación y actualización que envían solo el `StoreId`. Se ajustó como `Store?` nullable.
     - En `Store.cs`, se ajustó `TaxId` como nullable (`string? TaxId`) para compatibilidad fluida con comercios sin NIT obligatorio.
     - En `CommercialAgreementRepository.cs` y `StoreRepository.cs`, se reemplazó el método directo `_context.Update()` por búsqueda previa y actualización puntual de propiedades sobre la entidad rastreada, eliminando errores de concurrencia y duplicidad de llaves.
     - Se agregaron los endpoints de eliminación/inactivación `[HttpDelete("{id}")]` en `AgreementsController.cs` y `StoresController.cs`.
  2. **Actualización Integral del Módulo en PWA (ParkingPwa)**:
     - En `ConveniosContracts.ts` y `conveniosService.ts`, se definieron y conectaron las operaciones completas de convenios y comercios (`getAllAgreements`, `getStores`, `createAgreement`, `updateAgreement`, `deactivateAgreement`, `createStore`, `updateStore`, `deactivateStore`).
     - En `ConveniosTab.tsx`, se implementó interfaz de sub-pestañas para gestión dual: **📄 Convenios** y **🏢 Comercios Aliados**.
     - En Convenios: soporte para seleccionar modalidad de beneficio entre **Porcentaje (%)** y **Monto Fijo ($ COP)**, compra mínima en local ($ COP), límite de horas cubiertas (o ilimitado) y estado.
     - En Comercios: modal para registrar/editar razones sociales, NIT y teléfonos de contacto, con enlace rápido "+ Crear Nuevo Comercio" desde el formulario de convenios.
- **📦 Componentes Modificados**:
  - `ParkingApi.Domain/Models/CommercialAgreement.cs`
  - `ParkingApi.Domain/Models/Store.cs`
  - `ParkingApi.Infrastructure/Data/Repositories/Agreements/CommercialAgreementRepository.cs`
  - `ParkingApi.Infrastructure/Data/Repositories/Stores/StoreRepository.cs`
  - `ParkingApi/Controllers/AgreementsController.cs`
  - `ParkingApi/Controllers/StoresController.cs`
  - `ParkingPwa/src/features/settings/model/ConveniosContracts.ts`
  - `ParkingPwa/src/features/settings/data/conveniosService.ts`
  - `ParkingPwa/src/features/settings/ui/ConveniosTab.tsx`
  - `HISTORIAL_CAMBIOS.md`
- **✅ Verificación y Compilación**:
  - Pruebas REST en backend: Creación, actualización y borrado lógico verificados con HTTP 200.
  - `npm run build` en `ParkingPwa`: **0 Errores** (Vite build exitoso).
  - API Central (.NET 8): En ejecución y escuchando en `http://localhost:5135`.

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
