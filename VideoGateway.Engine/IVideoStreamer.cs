using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoGateway.Engine;

/// <summary>
/// Contrato que define el comportamiento de cualquier componente capaz de emitir vídeo hacia el exterior.
/// Abstrae la tecnología subyacente (RTSP, RTMP, WebRTC) del módulo que ingiere los datos (DDS).
/// </summary>
public interface IVideoStreamer
{
    /// <summary>
    /// Prepara el streamer para comenzar a recibir e inyectar datos (por ejemplo, arranca procesos FFmpeg).
    /// </summary>
    void Start();

    /// <summary>
    /// Envía un frame de vídeo individual al flujo activo.
    /// </summary>
    /// <param name="frame">Objeto VideoFrame con los metadatos de formato y carga binaria.</param>
    void PushFrame(VideoFrame frame);

    /// <summary>
    /// Detiene de manera segura la transmisión y libera los recursos del sistema (ej: pipes, handles, procesos hijos).
    /// </summary>
    void Stop();
}
