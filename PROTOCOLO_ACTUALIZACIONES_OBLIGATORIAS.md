# 📦 Protocolo Oficial de Actualizaciones Obligatorias y Cero Pérdida de Datos (ParkFlow Desktop)

> **Estado**: Especificación Técnica Oficial y Regla de Oro Inmutable  
> **Fecha de Emisión**: 2026-09-27  
> **Versión**: 1.0.0  
> **Ámbito de Aplicación**: Terminales de Escritorio ParkFlow (WPF .NET 10) y Micro-Actualizador `ParkFlow.Updater`

---

## 1. Declaración de Principios de Integridad

El sistema ParkFlow opera en puntos de venta críticos donde se gestionan ingresos de vehículos, liquidaciones en efectivo y turnos operativos. Por tanto, las actualizaciones se rigen por tres mandatos inviolables:

1. **Cero Pérdida de Información (Garantía de Sincronización al 100%)**:
   - Jamás se descarga ni se aplica una actualización si existen transacciones locales en cola (`PendingItemsCount > 0`) que no hayan sido transmitidas y confirmadas en la nube.
   - Si una terminal no tiene conexión a internet para subir sus datos pendientes, la actualización se congela de inmediato, protegiendo las ventas y registros locales en SQLite.
2. **Obligatoriedad Inmediata (Prohibición de Posponer)**:
   - Toda actualización liberada por la administración es de cumplimiento obligatorio inmediato.
   - Se erradica por completo la opción de *"Posponer"*; el operador no puede dilatar la aplicación de parches críticos de seguridad, tarifas o resolución de contingencias.
3. **Confirmación Visual y Cierre Limpio**:
   - Todo reemplazo de binarios en caliente finaliza con el cierre ordenado de la sesión del operador y el relanzamiento hacia la pantalla de Login con el mensaje verde: *"Listo, sistema actualizado"*.

---

## 2. Diagrama de Flujos de Actualización

```mermaid
flowchart TD
    subgraph Flujo 1: En Pantalla de Login (Arranque de Terminal)
        A1[Apertura de la App / Clic en Iniciar Sesión] --> A2{¿Hay Actualización en Servidor?}
        A2 -->|No| A3[Permitir Login y Apertura de MainShellWindow]
        A2 -->|Sí| A4{¿Cola Local PendingItemsCount == 0?}
        A4 -->|Sí| A5[Abrir Diálogo Bloqueante: Actualización Obligatoria]
        A4 -->|No: Hay pendientes| A6[Autenticar Credenciales y Obtener Token JWT]
        A6 --> A7[Forzar Subida Total a la Nube: PerformFullSyncAsync]
        A7 --> A8{¿PendingItemsCount == 0?}
        A8 -->|No: Error de Red| A9[Bloquear Actualización y Alertar al Operador: Datos Protegidos]
        A8 -->|Sí: Garantizado| A5
    end

    subgraph Flujo 2: En Operación Activa (Terminal en Caja)
        B1[Notificación SignalR AppReleaseAvailable O Sondeo Periódico 1 Hora] --> B2{¿Nueva Versión?}
        B2 -->|Sí| B3[Desplegar Diálogo Obligatorio de Actualización Inmediata]
        B3 --> B4[Ejecutar Flush Forzado a la Nube: PerformFullSyncAsync]
        B4 --> B5{¿PendingItemsCount == 0?}
        B5 -->|No| B6[Suspender Actualización hasta Restablecer Conexión]
        B5 -->|Sí: Todo en Nube| B7[Generar Backup Preventivo de BD SQLite local]
        B7 --> B8[Cerrar Sesión Activa del Operador: LogoutAsync]
        B8 --> B9[Lanzar ParkFlow.Updater.exe con flag --updated]
        B9 --> B10[Cerrar Proceso Parking.exe]
    end

    subgraph Flujo 3: Post-Actualización
        B10 --> C1[ParkFlow.Updater Reemplaza Binarios]
        C1 --> C2[Updater Ejecuta: Parking.exe --updated]
        C2 --> C3[Parking.exe detecta argumento --updated en OnStartup]
        C3 --> C4[Abre LoginWindow desplegando: 'Listo, sistema actualizado']
        C4 --> C5[Operador inicia sesión con sistema al día y operativo]
    end
```

---

## 3. Especificación Técnica de los Componentes

### 3.1 Servicio Central de Actualizaciones (`AppUpdateService.cs`)
* **Sondeo Periódico de 1 Hora**: Implementa `StartHourlyPeriodicCheck()` mediante un temporizador en segundo plano que consulta `GET api/v1/app-update/check` cada 60 minutos.
* **Validación Previa Inflexible**:
  ```csharp
  if (_syncEngine.PendingItemsCount > 0)
  {
      await _syncEngine.PerformFullSyncAsync();
      if (_syncEngine.PendingItemsCount > 0)
      {
          // REGLA DE ORO: ABORTAR ACTUALIZACIÓN SI NO SE PUDO SUBIR TODO A LA NUBE
          progress?.Report(new UpdateProgressReport
          {
              IsError = true,
              ErrorMessage = "No se puede actualizar en este momento: existen transacciones locales pendientes que no pudieron subirse a la nube. Verifique su conexión."
          });
          return false;
      }
  }
  ```

### 3.2 Micro-Actualizador (`ParkFlow.Updater.exe`)
* Al completar la extracción de binarios, relanza el ejecutable principal con el parámetro de confirmación:
  ```csharp
  Process.Start(new ProcessStartInfo
  {
      FileName = exePath,
      Arguments = "--updated",
      WorkingDirectory = _targetDir,
      UseShellExecute = true
  });
  ```

### 3.3 Experiencia de Inicio de Sesión (`LoginViewModel.cs` / `LoginWindow.xaml`)
* Captura de la bandera `--updated`: Despliega un banner de éxito estilizado en verde (`BrushSuccess`) con el mensaje *"Listo, sistema actualizado"*.
* Validación en `LoginAsync`: Antes de conceder el acceso a `MainShellWindow`, valida si existe una actualización pendiente. Si existe, sube el 100% de datos a la nube y exige la actualización antes de operar.

---

## 4. Comandos Oficiales de Generación (Instalador Setup .EXE + Actualización .ZIP)

### 4.1 Comando Único Unificado (Recomendado)
El script oficial detecta automáticamente Inno Setup 6 y genera **ambos artefactos** en una sola ejecución:
```powershell
.\Scripts\publish-release.ps1 -Version "1.1.0" -ReleaseNotes "Descripción de cambios y mejoras"
```

* **Artefactos generados en `Releases\v1.1.0\`**:
  1. **`ParkFlow_Setup_v1.1.0.exe`**: Asistente de instalación oficial de Windows (Setup Wizard para nuevos PCs, accesos directos de escritorio, menú inicio y registro en Windows).
  2. **`ParkFlow_v1.1.0.zip`**: Paquete comprimido para auto-actualizaciones en caliente vía nube/PWA.
  3. **`release_manifest.json`**: Manifiesto criptográfico con Checksum SHA-256 inmutable y metadatos listos para el API.

---
*Fin del documento oficial de especificación técnica.*
