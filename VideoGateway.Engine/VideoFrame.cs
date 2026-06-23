using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoGateway.Engine
{
    /// <summary>
    /// Representa un paquete o frame de vídeo transmitido a través del sistema.
    /// Esta entidad actúa como modelo de transferencia de datos (DTO) entre la capa DDS y la capa de Streaming.
    /// </summary>
    public class VideoFrame
    {
        /// <summary>
        /// Número de secuencia para garantizar el orden o control temporal de los frames recibidos.
        /// </summary>
        public long SequenceNumber { get; set; }

        /// <summary>
        /// Formato del frame codificado (e.g., "mjpeg", "h264", "hevc", "raw").
        /// Es utilizado por el streamer para saber cómo tratar o decodificar el payload.
        /// </summary>
        public string Format { get; set; } = string.Empty; // Ejemplo: "mjpeg", "h264", "raw"

        /// <summary>
        /// Anchura del vídeo en píxeles. Necesario si el formato no lleva esta información implícita (ej: "raw").
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Altura del vídeo en píxeles. Necesario si el formato no lleva esta información implícita (ej: "raw").
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Carga útil (payload) binaria que contiene los datos del vídeo de este frame.
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>(); // Los bytes del vídeo
    }
}
