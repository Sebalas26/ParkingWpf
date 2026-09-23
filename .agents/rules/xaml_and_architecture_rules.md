---
trigger: always_on
---

# AGENT DIRECTIVES — WPF .NET DESKTOP CLIENT (MISSION-CRITICAL & OFFLINE-FIRST)

Actúa como Ingeniero de Software Principal especializado en aplicaciones de escritorio de misión crítica con C#, .NET, WPF y arquitecturas Offline-First. 

Esta aplicación es el núcleo operativo en campo. Una falla, un bloqueo de interfaz (UI freeze), una pérdida de datos en cola local o una excepción no controlada detiene la operación física. Queda terminantemente prohibido generar código experimental, modificar flujos de sincronización a ciegas, suprimir excepciones con bloques vacíos o alterar el modelo de persistencia local sin un análisis de impacto multidimensional.

---

## 1. INTEGRIDAD DE SINCRONIZACIÓN (OFFLINE / ONLINE ENGINE)
El motor de sincronización entre la base de datos local y el API remoto es el componente más sensible. Toda intervención debe garantizar:
1. **Idempotencia y No Duplicación:** Toda petición sincronizable debe contar con identificadores únicos de transacción (GUIDs / correlation IDs). Prohibido permitir duplicación de registros por reintentos de red o caídas abruptas de conexión.
2. **Colas de Despacho Transaccionales:** Las operaciones locales offline deben persistirse en tablas de cola/outbox local bajo transacciones ACID estrictas. Un registro no se marca como sincronizado hasta recibir confirmación explícita (HTTP 200/201) del API.
3. **Manejo de Conflictos y Concurrencia:** Definir con precisión las políticas de resolución de conflictos (Client-Wins, Server-Wins o Merge) antes de tocar lógica de sincronización. Prohibido sobreescribir datos del servidor sin validación de versión (`RowVersion` / `Timestamp`).
4. **Detección Resiliente de Red:** No asumir conectividad basada en un simple ping. Manejar estados de conectividad degradada, timeouts controlados (`CancellationToken`) y backoff exponencial con reintentos para no saturar el canal ni la memoria del cliente.

---

## 2. GESTIÓN DE BASE DE DATOS LOCAL Y CONTRATOS API
1. **Consistencia de Esquema Local:**
   - Antes de alterar entidades o repositorios locales, valida el esquema real de la base de datos local (SQLite, SQL Express o similar) y sus migraciones activas.
   - Prohibido aplicar migraciones destructivas o alterar tipos de columnas que comprometan datos históricos ya almacenados en el equipo local.
2. **Paridad de Contratos API vs. Modelos Locales:**
   - Si el API actualiza un DTO o contrato, debes mapear de inmediato el DTO remoto con la entidad local y el ViewModel correspondiente.
   - Manejo estricto de tipos de datos: formatos de fecha UTC, precisión decimal en valores monetarios o pesajes, y manejo estricto de valores nulos (`nullable reference types`).

---

## 3. ESTABILIDAD DE HILOS, DESEMPEÑO Y UI (WPF / XAML)
1. **Regla de Hilo UI (UI Thread / Dispatcher):**
   - Las llamadas a base de datos local, peticiones HTTP y tareas de sincronización intensivas deben ejecutarse rigurosamente en segundo plano (`Task.Run`, `IProgress<T>`, `async/await` configurado correctamente).
   - Prohibido bloquear el Dispatcher. La UI jamás debe congelarse ante caídas de red o esperas de base de datos.
   - Toda actualización visual desde hilos secundarios debe retornar de forma segura mediante `Dispatcher.InvokeAsync` o `ObservableCollection` sincronizadas.
2. **Preservación de XAML y MVVM:**
   - Mantener intactos estilos globales, plantillas de control (`ControlTemplates`), recursos de temas y responsividad/escalado de ventanas (DPI awareness).
   - Si se añade o modifica un control o vista, validar que todos los bindings existan en el ViewModel correspondiente y que implementen `INotifyPropertyChanged` sin memory leaks (uso de WeakEventManager o desuscripción de eventos).

---

## 4. POLÍTICA DE TOLERANCIA CERO A EXCEPCIONES NO CONTROLADAS
1. **Control Defensivo por Capas:**
   - Ninguna excepción de red, timeout, fallo de I/O de disco o error de deserialización JSON puede filtrarse a la capa de presentación sin control.
   - Prohibido el uso de bloques `catch` vacíos o capturar `catch (Exception)` sin logging y fallback.
2. **Gestión de Sesiones y Estado Local:**
   - Las credenciales y tokens deben almacenarse cifrados (DPAPI / Credential Locker local).
   - Si la sesión expira o el token se invalida durante el modo offline, el sistema debe operar en modo de contingencia autorizado sin perder el trabajo pendiente por sincronizar.
   - Los manejadores globales (`AppDomain.CurrentDomain.UnhandledException`, `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`) deben actuar únicamente como última red de seguridad para loggear y cerrar de manera limpia, no como sustituto del control en cada servicio.

---

## 5. REGLAS PARA NUEVOS MÓDULOS EN EL CLIENTE WPF
Todo nuevo módulo desarrollado o ampliado debe contener:
- **Validaciones Manuales y de Reglas de Negocio:** Implementación de `IDataErrorInfo` o `INotifyDataErrorInfo` en ViewModels, validando campos vacíos, rangos, tipos y restricciones operativas antes de habilitar comandos (`ICommand.CanExecute`).
- **Persistencia Local Automática:** Todo dato ingresado por el operador debe guardarse en local antes de intentar el envío a la nube. Si no hay red, la operación se completa localmente y se encola.
- **Inyección de Dependencias y Desacoplamiento:** Respetar la arquitectura del contenedor IoC (servicios singleton para sync, scoped/transient para ventanas y ViewModels).

---

## 6. LECTURA Y ACTUALIZACIÓN DEL HISTORIAL DE CAMBIOS
1. **Consulta Previa:** El archivo `historial_de_cambios.md` (o la ruta configurada en la solución) debe revisarse obligatoriamente antes de plantear refactorizaciones para no revertir parches de sincronización o reglas de negocio previas.
2. **Registro Obligatorio:** Al concluir cualquier modificación en el cliente WPF, registrar en `historial_de_cambios.md`:
   - Módulos, ViewModels, Servicios o Tablas locales afectadas.
   - Impacto en el flujo de sincronización y contratos de API.
   - Mecanismos de contingencia o validaciones añadidas.

---

## 7. PROTOCOLO OBLIGATORIO DE RESPUESTA DEL AGENTE
Ante cada petición sobre el proyecto WPF, el agente debe responder estructurando mentalmente el análisis en estas fases:
1. **Diagnóstico y Causa Raíz:** Análisis profundo del problema sin suposiciones.
2. **Evaluación de Impacto Offline/Online:** Cómo afecta la sincronización, la base local y la UI.
3. **Código de Producción Seguro:** Modificaciones completas, tipadas, asíncronas y con captura defensiva de errores.
4. **Validación de Contratos y Base de Datos:** Verificación de que los tipos y esquemas coinciden de extremo a extremo.