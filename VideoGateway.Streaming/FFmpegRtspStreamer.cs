using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using VideoGateway.Engine;

namespace VideoGateway.Streaming
{
    public class FFmpegRtspStreamer : IVideoStreamer
    {
        private readonly string _rtspUrl;
        private Process? _ffmpegProcess;
        private BinaryWriter? _ffmpegWriter;
        private string _currentFormat = string.Empty;
        private bool _isStarted = false;

        public FFmpegRtspStreamer(string rtspUrl = "rtsp://localhost:8554/live")
        {
            _rtspUrl = rtspUrl;
        }

        public void Start()
        {
            // El proceso se iniciará dinámicamente cuando llegue el primer frame
            // para poder detectar su formato de origen automáticamente.
            _isStarted = true;
            Console.WriteLine($"[Streaming] Servidor de traducción preparado. Apuntando a: {_rtspUrl}");
        }

        public void PushFrame(VideoFrame frame)
        {
            if (!_isStarted) return;
            // Validaciones básicas
            if (frame == null || frame.Data == null || frame.Data.Length == 0)
            {
                Console.WriteLine("[Streaming] [WARN] Frame vacío recibido, se omite.");
                return;
            }

            // Si el formato o las dimensiones cambian, reiniciamos FFmpeg con la nueva configuración
            if (_ffmpegProcess == null || _currentFormat != frame.Format || (_currentFormat == "raw" && (_ffmpegProcess == null)))
            {
                RestartFFmpeg(frame);
            }

            // Inyectamos los bytes del frame DDS directamente en la tubería de FFmpeg
            try
            {
                if (_ffmpegWriter != null && _ffmpegProcess != null && !_ffmpegProcess.HasExited)
                {
                    _ffmpegWriter.Write(frame.Data);
                    _ffmpegWriter.Flush();
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[Streaming] [ERROR] Error al inyectar frame en la tubería: {ex.Message}");
            }
        }

        private void RestartFFmpeg(VideoFrame frame)
        {
            StopFFmpegInternal();
            var format = frame.Format ?? string.Empty;
            _currentFormat = format;

            // Configuración dinámica de argumentos según el formato origen de DDS
            string inputArgs = string.Empty;

            switch (format.ToLower())
            {
                case "mjpeg":
                case "jpeg":
                    inputArgs = "-f image2pipe -vcodec mjpeg";
                    break;
                case "h264":
                    inputArgs = "-f h264";
                    break;
                case "h265":
                case "hevc":
                    inputArgs = "-f hevc";
                    break;
                case "raw":
                    // RAW necesita resolución y pixel format desde el frame
                    var w = frame.Width > 0 ? frame.Width : 1920;
                    var h = frame.Height > 0 ? frame.Height : 1080;
                    // Asumimos BGR24 por defecto; ajusta si tus frames vienen en otro pix_fmt
                    inputArgs = $"-f rawvideo -pix_fmt bgr24 -s {w}x{h}";
                    break;
                default:
                    inputArgs = string.Empty; // dejar que FFmpeg intente autodetectar
                    break;
            }

            // Argumentos de salida universales: Convertir a H.264 ultra-rápido de baja latencia hacia el servidor RTSP
            string outputArgs = "-c:v libx264 -preset ultrafast -tune zerolatency -f rtsp";

            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg", // Lo tomará de la carpeta de salida gracias al paso 1
                Arguments = $"-y {inputArgs} -i pipe:0 {outputArgs} {_rtspUrl}",
                RedirectStandardInput = true,  // Habilita la tubería de entrada (pipe:0)
                RedirectStandardError = true,  // Para capturar logs de FFmpeg si fuera necesario
                UseShellExecute = false,
                CreateNoWindow = true          // Oculta la ventana negra de la consola de FFmpeg
            };

            try
            {
                var proc = Process.Start(startInfo);
                _ffmpegProcess = proc;
                if (_ffmpegProcess != null)
                {
                    _ffmpegWriter = new BinaryWriter(_ffmpegProcess.StandardInput.BaseStream);
                    Console.WriteLine($"[Streaming] FFmpeg iniciado con éxito para formato de entrada: '{format}'");

                    // Leer stderr de FFmpeg en background para diagnósticos
                    Task.Run(() =>
                    {
                        try
                        {
                            var reader = _ffmpegProcess!.StandardError;
                            while (!_ffmpegProcess.HasExited && !reader.EndOfStream)
                            {
                                var line = reader.ReadLine();
                                if (line != null) Console.WriteLine($"[FFmpeg] {line}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Streaming] Error leyendo stderr de FFmpeg: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Streaming] [ERROR] No se pudo iniciar FFmpeg: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isStarted = false;
            StopFFmpegInternal();
            Console.WriteLine("[Streaming] Componente de streaming detenido.");
        }

        private void StopFFmpegInternal()
        {
            try
            {
                if (_ffmpegProcess != null)
                {
                    try { _ffmpegWriter?.Close(); } catch { }
                    try
                    {
                        if (!_ffmpegProcess.HasExited)
                        {
                            _ffmpegProcess.Kill();
                        }
                    }
                    catch { }
                    try { _ffmpegProcess.Dispose(); } catch { }
                    _ffmpegProcess = null;
                    _ffmpegWriter = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Streaming] Error al cerrar FFmpeg: {ex.Message}");
            }
        }
    }
}
