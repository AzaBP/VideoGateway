# Documentación del Gateway de Video (DDS a RTSP)

Este documento detalla las partes fundamentales del proyecto encargadas de actuar como Gateway, recibiendo video en formato streaming a través de DDS y traduciéndolo para su retransmisión en un formato compatible con RTSP/VLC.

## Tecnologías Utilizadas

1. **RTI Connext DDS**: Se utiliza como el middleware de comunicación de alto rendimiento para recibir el flujo de video. El proyecto utiliza la librería `Rti.ConnextDds` (versión 7.7.0) para implementar el patrón Publicador/Suscriptor.
2. **FFmpeg**: Motor de procesamiento multimedia utilizado para transcodificar dinámicamente y emitir el video hacia un servidor RTSP.
3. **C# (.NET 8.0)**: Lenguaje principal en el que están desarrollados los componentes del Gateway.

## Componentes Principales del Gateway

La lógica central del Gateway se divide en dos módulos principales: la recepción de datos a través de DDS y la retransmisión a través de FFmpeg.

### 1. Módulo de Recepción (VideoGateway.Dds)

El archivo `DdsReceiver.cs` es el responsable de conectarse al bus de datos DDS y capturar los frames de video.

**Uso de RTI Connext:**
Sí, el sistema hace uso intensivo de la funcionalidad de **RTI Connext**. En lugar de usar tipos estáticos generados por código, emplea **Tipos Dinámicos (Dynamic Data)**, lo que permite una mayor flexibilidad sin necesidad de compilar archivos IDL previamente.

*   **Creación del Participante:** `DomainParticipantFactory.Instance.CreateParticipant(domainId)`
*   **Definición de Tipo Dinámico:** Utiliza `DynamicTypeFactory` para definir en tiempo de ejecución la estructura `VideoData::Frame` (secuencia, formato, ancho, alto y el array de bytes del frame).
*   **Lectura de Datos:** Emplea `ImplicitSubscriber.CreateDataReader` con un perfil QoS (`DataFlowLibrary::Reliable` o el por defecto) para suscribirse al topic. Un hilo en background (`ListenForFrames`) utiliza el método `Take()` para extraer continuamente muestras de datos, convirtiéndolas en objetos genéricos `VideoFrame`.

### 2. Módulo de Streaming (VideoGateway.Streaming)

El archivo `FFmpegRtspStreamer.cs` toma los `VideoFrame` capturados por DDS y los traduce para su emisión.

*   **Inyección a FFmpeg (Pipes):** En lugar de guardar archivos temporales, el componente arranca un proceso del sistema operativo (`ffmpeg.exe`) y escribe los bytes binarios del frame directamente en la entrada estándar del proceso (tubería o pipe) usando `BinaryWriter`.
*   **Detección Dinámica de Formato:** El método `RestartFFmpeg` lee la propiedad `Format` del primer frame (mjpeg, h264, h265, raw). Según el formato, configura los argumentos de entrada de FFmpeg. Por ejemplo:
    *   Si es H264: `-f h264`
    *   Si es RAW: `-f rawvideo -pix_fmt bgr24 -s {width}x{height}`
*   **Emisión RTSP:** Todos los formatos son transcodificados y enviados a un servidor RTSP de destino usando los argumentos: `-c:v libx264 -preset ultrafast -tune zerolatency -f rtsp {rtspUrl}`. Esto asegura la compatibilidad universal con clientes como VLC.

## Flujo Completo

1. `DdsReceiver` escucha en el dominio DDS.
2. Cuando llega una muestra dinámica de RTI Connext, extrae los bytes y propiedades del frame.
3. El `VideoFrame` se envía (Push) al `FFmpegRtspStreamer`.
4. El Streamer verifica si necesita inicializar o reiniciar `ffmpeg` basándose en el formato del frame.
5. Los bytes se envían a través de *standard input* al proceso `ffmpeg`, que los empaqueta y emite en el endpoint RTSP configurado, listo para ser consumido por VLC.
