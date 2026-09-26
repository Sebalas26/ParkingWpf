using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;

namespace ParkFlow.Updater;

public partial class MainWindow : Window
{
    private int _pid;
    private string _zipPath = string.Empty;
    private string _targetDir = string.Empty;
    private string _exeName = "Parking.exe";
    private string _expectedSha256 = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ParseArguments();
        _ = Task.Run(ExecuteUpdateAsync);
    }

    private void ParseArguments()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].Equals("--pid", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                int.TryParse(args[++i], out _pid);
            }
            else if (args[i].Equals("--zip", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                _zipPath = args[++i];
            }
            else if (args[i].Equals("--target", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                _targetDir = args[++i];
            }
            else if (args[i].Equals("--exe", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                _exeName = args[++i];
            }
            else if (args[i].Equals("--sha256", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                _expectedSha256 = args[++i];
            }
        }
    }

    private async Task ExecuteUpdateAsync()
    {
        try
        {
            // 1. Esperar cierre del proceso padre para liberar locks de archivos
            if (_pid > 0)
            {
                UpdateStatus("Cerrando proceso anterior...");
                try
                {
                    var parentProcess = Process.GetProcessById(_pid);
                    parentProcess.WaitForExit(15000);
                }
                catch { }
            }

            await Task.Delay(1000); // Pausa defensiva para liberación de descriptores en Windows

            if (string.IsNullOrWhiteSpace(_zipPath) || !File.Exists(_zipPath) || string.IsNullOrWhiteSpace(_targetDir))
            {
                ShowErrorAndExit("Parámetros de actualización incompletos o archivo no encontrado.");
                return;
            }

            // 2. Validación de integridad SHA-256
            if (!string.IsNullOrWhiteSpace(_expectedSha256))
            {
                UpdateStatus("Verificando firma criptográfica del paquete...");
                using var sha = SHA256.Create();
                await using var fs = new FileStream(_zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var hashBytes = await sha.ComputeHashAsync(fs);
                var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

                if (!string.Equals(hashHex, _expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    fs.Close();
                    File.Delete(_zipPath);
                    ShowErrorAndExit("Falla de integridad: El paquete de actualización no coincide con la firma digital oficial.");
                    return;
                }
            }

            // 3. Extracción de binarios respetando la regla inviolable de exclusión de BD y licencias
            UpdateStatus("Instalando nueva versión de ParkFlow...");

            using (var archive = ZipFile.OpenRead(_zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    var entryName = entry.FullName.Replace('\\', '/');

                    // Regla de Exclusión Estricta (NUNCA sobreescribir ni tocar bases de datos locales ni credenciales)
                    if (entryName.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
                        entryName.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase) ||
                        entryName.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase) ||
                        entryName.EndsWith("license.dat", StringComparison.OrdinalIgnoreCase) ||
                        entryName.StartsWith("Data/", StringComparison.OrdinalIgnoreCase) ||
                        entryName.StartsWith("Backups/", StringComparison.OrdinalIgnoreCase) ||
                        entryName.StartsWith("Logs/", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var destinationPath = Path.GetFullPath(Path.Combine(_targetDir, entry.FullName));
                    if (!destinationPath.StartsWith(Path.GetFullPath(_targetDir), StringComparison.OrdinalIgnoreCase))
                    {
                        // Prevención de ataques de directory traversal en zip
                        continue;
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        // Es un directorio
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    // Si es un archivo de configuración de producción preexistente, preservarlo
                    if (File.Exists(destinationPath) && (entry.Name.Equals("appsettings.Production.json", StringComparison.OrdinalIgnoreCase) || entry.Name.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var parentDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    try
                    {
                        entry.ExtractToFile(destinationPath, overwrite: true);
                    }
                    catch (IOException) when (entry.Name.StartsWith("ParkFlow.Updater", StringComparison.OrdinalIgnoreCase))
                    {
                        // El micro-updater se encuentra actualmente en ejecución; se preserva el binario activo sin abortar la actualización del sistema principal.
                    }
                }
            }

            // 4. Relanzar aplicación principal
            UpdateStatus("Reiniciando ParkFlow...");
            var exePath = Path.Combine(_targetDir, _exeName);
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = _targetDir,
                    UseShellExecute = true
                });
            }

            // 5. Eliminar ZIP temporal
            try
            {
                File.Delete(_zipPath);
            }
            catch { }

            await Task.Delay(500);

            // 6. Cierre ordenado del Updater
            Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
        catch (Exception ex)
        {
            ShowErrorAndExit($"Error durante el reemplazo de binarios: {ex.Message}");
        }
    }

    private void UpdateStatus(string message)
    {
        Dispatcher.Invoke(() =>
        {
            TxtStatus.Text = message;
        });
    }

    private void ShowErrorAndExit(string error)
    {
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(error, "ParkFlow - Error de Actualización", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
        });
    }
}
