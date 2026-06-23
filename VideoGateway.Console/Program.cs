using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VideoGateway.Engine;
using VideoGateway.Streaming;

namespace VideoGateway.ConsoleApp;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("       GATEWAY UNIVERSAL DDS -> RTSP (C#)        ");
        Console.WriteLine("=================================================");

        // 1. Inicializar el transformador a RTSP
        var rtspStreamer = new FFmpegRtspStreamer("rtsp://localhost:8554/live");
        rtspStreamer.Start();

        // 2. Configurar la herramienta oficial de RTI en segundo plano para escuchar la red
        // La ruta de rtiddsspy puede venir por: argumento de línea (--rtiddsspy=PATH o --rtiddsspy PATH),
        // luego por la variable de entorno RTIDDSSPY_PATH, y por último la ruta por defecto.
        // Por defecto usar el nombre simple para que la búsqueda en PATH lo encuentre
        string defaultSpyPath = "rtiddsspy";
        string? spyPath = null;

        // Buscar argumento de línea
        if (args != null)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a.StartsWith("--rtiddsspy=", StringComparison.OrdinalIgnoreCase))
                {
                    spyPath = a.Substring(a.IndexOf('=') + 1).Trim('"');
                    break;
                }
                if (a.Equals("--rtiddsspy", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    spyPath = args[i + 1].Trim('"');
                    break;
                }
            }
        }

        // Si no viene por args, intentar variable de entorno
        if (string.IsNullOrWhiteSpace(spyPath))
        {
            try { spyPath = Environment.GetEnvironmentVariable("RTIDDSSPY_PATH"); } catch { spyPath = null; }
        }

        if (string.IsNullOrWhiteSpace(spyPath)) spyPath = defaultSpyPath;

        // Obtener argumentos para rtiddsspy: prioridad args (--rtiddsspy-args or --rtiddsspy-args="..") -> env RTIDDSSPY_ARGS -> valor por defecto
        string? spyArgs = null;
        if (args != null)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a.StartsWith("--rtiddsspy-args=", StringComparison.OrdinalIgnoreCase))
                {
                    spyArgs = a.Substring(a.IndexOf('=') + 1).Trim('"');
                    break;
                }
                if (a.Equals("--rtiddsspy-args", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    spyArgs = args[i + 1].Trim('"');
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(spyArgs))
        {
            try { spyArgs = Environment.GetEnvironmentVariable("RTIDDSSPY_ARGS"); } catch { spyArgs = null; }
        }

        if (string.IsNullOrWhiteSpace(spyArgs)) spyArgs = "-domainId 0 -printSample";

        var spyInfo = new ProcessStartInfo
        {
            FileName = spyPath,
            Arguments = spyArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Resolver si la ruta proporcionada es un directorio, ruta completa o sólo el nombre del ejecutable
        string spyExeName = "rtiddsspy.exe";
        string? resolvedSpyPath = null;

        if (Directory.Exists(spyPath))
        {
            resolvedSpyPath = Path.Combine(spyPath, spyExeName);
        }
        else if (spyPath.IndexOfAny(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0 || Path.IsPathRooted(spyPath))
        {
            resolvedSpyPath = spyPath;
            if (string.IsNullOrEmpty(Path.GetExtension(resolvedSpyPath))) resolvedSpyPath += ".exe";
        }
        else
        {
            // Buscar en PATH si sólo se pasó el nombre
            var candidates = new[] { spyPath, spyPath + ".exe" };
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var cand in candidates)
                {
                    try
                    {
                        var p = Path.Combine(dir, cand);
                        if (File.Exists(p))
                        {
                            resolvedSpyPath = p;
                            break;
                        }
                    }
                    catch { }
                }
                if (resolvedSpyPath != null) break;
            }
        }

        if (!string.IsNullOrWhiteSpace(resolvedSpyPath))
        {
            spyInfo.FileName = resolvedSpyPath;
        }

        Console.WriteLine("[DDS] Levantando puente nativo estable con rtiddsspy...");
        Console.WriteLine($"[DDS] Ruta usada para rtiddsspy: {spyInfo.FileName}");

        Process? spyProcess = null;

        // Si la ruta indicada no existe, intentar buscar rtiddsspy.exe en PATH
        string candidatePath = spyInfo.FileName ?? string.Empty;
        if (!File.Exists(candidatePath))
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            Console.WriteLine("[DEBUG] PATH del proceso:\n" + pathEnv);
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var p = Path.Combine(dir, "rtiddsspy.exe");
                    bool exists = File.Exists(p);
                    Console.WriteLine($"[DEBUG] Comprobando: {p} -> {(exists ? "FOUND" : "not found")}");
                    if (exists)
                    {
                        candidatePath = p;
                        Console.WriteLine($"[DDS] rtiddsspy encontrado en PATH: {candidatePath}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Error comprobando {dir}: {ex.Message}");
                }
            }
        }

        if (!File.Exists(candidatePath))
        {
            Console.WriteLine($"[DDS] [CRITICAL] rtiddsspy no encontrado: {spyInfo.FileName}");
            Console.WriteLine("[DDS] El gateway seguirá ejecutándose sin puente nativo DDS.");
        }
        else
        {
            // Si es un .bat/.cmd debemos invocarlo a través de cmd.exe /c para permitir la ejecución
            var ext = Path.GetExtension(candidatePath).ToLowerInvariant();
            if (ext == ".bat" || ext == ".cmd")
            {
                var cmdInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{candidatePath}\" {spyArgs}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                try
                {
                    spyProcess = Process.Start(cmdInfo);
                    if (spyProcess == null)
                    {
                        Console.WriteLine($"[DDS] No se pudo iniciar rtiddsspy (.bat) con: {candidatePath}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DDS] Error al arrancar rtiddsspy (.bat): {ex.Message}");
                    spyProcess = null;
                }
            }
            else
            {
                spyInfo.FileName = candidatePath;
                spyInfo.Arguments = spyArgs;
                try
                {
                    spyProcess = Process.Start(spyInfo);
                    if (spyProcess == null)
                    {
                        Console.WriteLine($"[DDS] No se pudo iniciar rtiddsspy con: {spyInfo.FileName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DDS] Error al arrancar rtiddsspy: {ex.Message}");
                    spyProcess = null;
                }
            }
        }

        Console.WriteLine("[OK] El Gateway está corriendo.");
        Console.WriteLine("Presiona CTRL+C, cierra la ventana o Enter para apagar.");

        // Creamos el contenedor de frames universal
        var frame = new VideoFrame
        {
            Format = "mjpeg" // FFmpeg procesará el flujo continuo
        };

        // Modo de prueba: permitir forzar empuje de frames si no hay muestras
        bool testMode = false;
        if (args != null)
        {
            foreach (var a in args)
            {
                if (a.Equals("--rtiddsspy-test", StringComparison.OrdinalIgnoreCase) || a.Equals("--test-mode", StringComparison.OrdinalIgnoreCase))
                {
                    testMode = true;
                    break;
                }
            }
        }
        if (!testMode)
        {
            try { testMode = string.Equals(Environment.GetEnvironmentVariable("RTIDDSSPY_TEST_MODE"), "1", StringComparison.OrdinalIgnoreCase) || string.Equals(Environment.GetEnvironmentVariable("RTIDDSSPY_TEST_MODE"), "true", StringComparison.OrdinalIgnoreCase); } catch { }
        }

        // Monitor para detectar ausencia de samples y empujar frames de mantenimiento en modo prueba
        var cts = new CancellationTokenSource();
        DateTime lastSample = DateTime.UtcNow;
        TimeSpan sampleTimeout = TimeSpan.FromSeconds(5);
        Task? monitorTask = null;

        if (spyProcess != null)
        {
            // Registrar PID y redirigir stderr si es posible
            try
            {
                Console.WriteLine($"[DDS] rtiddsspy PID: {spyProcess.Id}");
            }
            catch { }

            // Leer stdout y stderr en background
            var stdout = spyProcess.StandardOutput;
            var stderr = spyProcess.StandardError;

            Task.Run(() => {
                try
                {
                    while (!stdout.EndOfStream)
                    {
                        var line = stdout.ReadLine();
                        if (line == null) break;
                        Console.WriteLine(line);
                        if (line.Contains("frame:") || line.Contains("sample"))
                        {
                            Console.WriteLine("[Gateway] Muestra detectada en bus DDS -> Enviando a RTSP...");
                            rtspStreamer.PushFrame(frame);
                            lastSample = DateTime.UtcNow;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Error leyendo stdout de rtiddsspy: {ex.Message}");
                }
            });

            Task.Run(() => {
                try
                {
                    while (!stderr.EndOfStream)
                    {
                        var line = stderr.ReadLine();
                        if (line == null) break;
                        Console.WriteLine($"[rtiddsspy-ERR] {line}");
                    }
                }
                catch { }
            });

            if (testMode)
            {
                monitorTask = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (DateTime.UtcNow - lastSample > sampleTimeout)
                        {
                            Console.WriteLine("[TEST-MODE] No samples detectados: empujando frame de mantenimiento.");
                            try { rtspStreamer.PushFrame(frame); } catch { }
                            lastSample = DateTime.UtcNow;
                        }
                        await Task.Delay(1000, cts.Token).ContinueWith(_ => { });
                    }
                }, cts.Token);
            }

            // Menú interactivo para controlar funciones de diagnóstico y un publisher de prueba
            Process? testPublisher = null;

            void StartTestPublisher()
            {
                if (testPublisher != null) { Console.WriteLine("[TEST] Publisher de prueba ya está en ejecución."); return; }
                var ffArgs = "-f lavfi -re -i testsrc=size=640x480:rate=25 -c:v libx264 -preset ultrafast -tune zerolatency -f rtsp rtsp://127.0.0.1:8554/live -rtsp_transport tcp";
                var psiPub = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = ffArgs,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                try
                {
                    testPublisher = Process.Start(psiPub);
                    if (testPublisher != null)
                    {
                        Console.WriteLine($"[TEST] Publisher iniciado (PID: {testPublisher.Id}).");
                        Task.Run(() => {
                            try
                            {
                                var se = testPublisher.StandardError;
                                while (!se.EndOfStream)
                                {
                                    var l = se.ReadLine();
                                    if (l == null) break;
                                    Console.WriteLine($"[test-ffmpeg] {l}");
                                }
                            }
                            catch { }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TEST] No se pudo iniciar ffmpeg: {ex.Message}");
                    testPublisher = null;
                }
            }

            void StopTestPublisher()
            {
                if (testPublisher == null) { Console.WriteLine("[TEST] No hay publisher de prueba en ejecución."); return; }
                try
                {
                    if (!testPublisher.HasExited) testPublisher.Kill();
                }
                catch { }
                try { testPublisher.Dispose(); } catch { }
                testPublisher = null;
                Console.WriteLine("[TEST] Publisher de prueba detenido.");
            }

            bool exitRequested = false;
            while (!exitRequested)
            {
                Console.WriteLine();
                Console.WriteLine("--- GATEWAY - MENÚ ---");
                Console.WriteLine("1) Alternar publisher de prueba (start/stop)");
                Console.WriteLine("2) Mostrar estado");
                Console.WriteLine("3) Parar gateway y salir");
                Console.Write("Elige una opción: ");
                var sel = Console.ReadLine();
                switch (sel)
                {
                    case "1":
                        if (testPublisher == null) StartTestPublisher(); else StopTestPublisher();
                        break;
                    case "2":
                        Console.WriteLine($"rtiddsspy: {(spyProcess != null ? (spyProcess.HasExited ? "exited" : $"running (PID {spyProcess.Id})") : "not running")}");
                        Console.WriteLine($"Publisher de prueba: {(testPublisher != null ? (testPublisher.HasExited ? "exited" : $"running (PID {testPublisher.Id})") : "not running")}");
                        Console.WriteLine($"Modo test interno: {testMode}");
                        Console.WriteLine($"Última muestra recibida: {lastSample:O}");
                        break;
                    case "3":
                        exitRequested = true;
                        break;
                    default:
                        Console.WriteLine("Opción no válida.");
                        break;
                }
            }

            // Salida ordenada: cancelar monitor, detener publisher y limpiar procesos
            cts.Cancel();
            try { monitorTask?.Wait(1000); } catch { }
            StopTestPublisher();
            try { if (spyProcess != null && !spyProcess.HasExited) spyProcess.Kill(); } catch { }
            rtspStreamer.Stop();
        }
        else
        {
            // No se pudo iniciar rtiddsspy: si testMode activo empujar frames periódicamente
            if (testMode)
            {
                Console.WriteLine("[INFO] rtiddsspy no disponible. Modo prueba activo: empujando frames periódicamente.");
                monitorTask = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        Console.WriteLine("[TEST-MODE] Empujando frame de prueba...");
                        try { rtspStreamer.PushFrame(frame); } catch { }
                        await Task.Delay(1000, cts.Token).ContinueWith(_ => { });
                    }
                }, cts.Token);

                Console.WriteLine("Presiona Enter para parar el modo prueba.");
                Console.ReadLine();
                cts.Cancel();
                try { monitorTask?.Wait(1000); } catch { }
                rtspStreamer.Stop();
            }
            else
            {
                Console.WriteLine("[INFO] rtiddsspy no está disponible. El streamer sigue activo.");
                Console.ReadLine();
                rtspStreamer.Stop();
            }
        }
    }
}