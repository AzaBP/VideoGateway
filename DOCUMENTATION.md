# DDS Video Gateway System — Documentación Completa

> **Versión:** 1.0  
> **Plataforma:** Windows (.NET 8, WinForms)  
> **Última actualización:** Junio 2026

---

## Tabla de Contenidos

1. [Descripción General del Proyecto](#1-descripción-general-del-proyecto)
2. [Arquitectura del Sistema](#2-arquitectura-del-sistema)
3. [Tecnologías Utilizadas](#3-tecnologías-utilizadas)
4. [Estructura de Proyectos (Solución)](#4-estructura-de-proyectos-solución)
5. [Cómo Funciona el Proceso Completo](#5-cómo-funciona-el-proceso-completo)
6. [Requisitos e Instalación](#6-requisitos-e-instalación)
7. [Manual de Usuario — Publisher UI](#7-manual-de-usuario--publisher-ui)
8. [Manual de Usuario — Subscriber UI](#8-manual-de-usuario--subscriber-ui)
9. [Pruebas y Verificación](#9-pruebas-y-verificación)
10. [Resolución de Problemas](#10-resolución-de-problemas)
11. [Referencia de Formatos de Vídeo Soportados](#11-referencia-de-formatos-de-vídeo-soportados)

---

## 1. Descripción General del Proyecto

**DDS Video Gateway System** es una pasarela de vídeo que integra la tecnología de middleware **DDS (Data Distribution Service)** con streaming de vídeo en tiempo real a través del protocolo **RTSP (Real-Time Streaming Protocol)**.

El sistema permite:

- **Publicar** archivos de vídeo (o streams de cámara) desde una aplicación Windows hacia un bus de datos DDS.
- **Recibir** esos datos de vídeo desde DDS y retransmitirlos como un stream RTSP accesible por cualquier reproductor compatible (VLC, FFplay, navegadores, etc.).
- **Transcodificar automáticamente** cualquier formato de vídeo de origen (H.264, H.265, MJPEG, RAW, MKV, AVI, etc.) al formato estándar **H.264 / AAC**, que es universalmente compatible con RTSP y la mayoría de reproductores.

### Caso de uso principal

```
[Archivo de vídeo / Cámara]
         │
         ▼ (Publisher UI / ffmpeg)
   [Bus de datos DDS]
         │
         ▼ (DdsReceiver → FFmpegRtspStreamer)
   [Servidor RTSP MediaMTX]
         │
         ▼ (Subscriber UI / VLC / FFplay)
   [Reproductor de vídeo]
```

---

## 2. Arquitectura del Sistema

El sistema está dividido en **capas independientes** que se comunican a través de interfaces bien definidas:

```
┌─────────────────────────────────────────────────────────┐
│                   Capa de Presentación                   │
│  VideoGateway.PublisherUI  │  VideoGateway.SubscriberUI  │
└───────────────┬────────────┴──────────────┬─────────────┘
                │                           │
                ▼                           ▼
┌───────────────────────────┐   ┌───────────────────────────┐
│  VideoGateway.Streaming   │   │    VideoGateway.Dds        │
│  (FFmpegRtspStreamer)      │   │    (DdsReceiver)           │
└───────────────┬───────────┘   └───────────────────────────┘
                │
                ▼
┌───────────────────────────────────────────────────────────┐
│                   VideoGateway.Engine                      │
│         (VideoFrame, IVideoStreamer, interfaces)           │
└───────────────────────────────────────────────────────────┘
                │
                ▼
┌───────────────────────────────────────────────────────────┐
│              VideoGateway.Testing.Common                   │
│         (ProcessRunner, MediaInfo, utilidades)             │
└───────────────────────────────────────────────────────────┘
```

### Flujo de datos detallado

```
Publisher UI
    │  Selecciona archivo de vídeo (.mp4, .mkv, .avi, …)
    │  
    ├─► ffmpeg (proceso externo)
    │       └─ Transcodifica a H.264 + AAC
    │           └─ Publica al endpoint RTSP: rtsp://127.0.0.1:8554/live
    │                    │
    │           [MediaMTX recibe el stream]
    │                    │
    │           DdsReceiver (VideoGateway.Dds)
    │               └─ Topic DDS: "VideoData::Frame"
    │                   └─ VideoFrame {sequence_number, format, width, height, data[]}
    │
    └─► FFmpegRtspStreamer (VideoGateway.Streaming)
            ├─ Detecta formato del frame (MJPEG, H264, H265, RAW)
            ├─ Inyecta bytes vía pipe (stdin de ffmpeg)
            └─ ffmpeg transcodifica → H.264 ultrafast → RTSP destino

Subscriber UI
    └─ LibVLCSharp (embebido) o VLC/FFplay externos
        └─ Se conecta a rtsp://127.0.0.1:8554/live
            └─ Reproduce el stream en tiempo real
```

---

## 3. Tecnologías Utilizadas

### 3.1 RTI Connext DDS (`Rti.ConnextDds`)

**¿Qué es?** DDS (Data Distribution Service) es un estándar de middleware de comunicación publicación-suscripción diseñado para sistemas de tiempo real y distribuidos.

**Versión utilizada:** RTI Connext DDS C# API (moderna, basada en tipos genéricos)

**Uso en el proyecto:**
- `DomainParticipant`: punto de entrada al bus DDS; representa un nodo en la red.
- `DynamicTypeFactory`: define en tiempo de ejecución el tipo de datos `VideoData::Frame` (sin necesidad de IDL precompilado).
- `DataReader<DynamicData>`: suscriptor que recibe los frames de vídeo del bus.
- `QosProvider`: carga políticas de calidad de servicio desde `USER_QOS_PROFILES.xml`.

**Rol en el sistema:** Actúa como el canal de transporte entre el emisor de vídeo y el receptor/transcodificador. Permite comunicación distribuida, tolerante a fallos y con control de calidad de servicio.

### 3.2 FFmpeg

**¿Qué es?** Suite de herramientas de código abierto para procesamiento de audio y vídeo, incluyendo transcodificación, streaming, filtros y análisis.

**Herramientas usadas:**
| Herramienta | Uso en el proyecto |
|-------------|-------------------|
| `ffmpeg`    | Transcodificación de vídeo a H.264/AAC; publicación RTSP; inyección de frames vía pipe |
| `ffplay`    | Reproducción de vídeo local como alternativa a VLC |
| `ffprobe`   | Análisis de metadatos de archivos (formato, codec, resolución, bitrate, duración) |

**Argumentos clave de publicación:**
```bash
ffmpeg -re -i "archivo.mp4" \
       -c:v libx264 -preset ultrafast -tune zerolatency \
       -c:a aac \
       -f rtsp "rtsp://127.0.0.1:8554/live" \
       -rtsp_transport tcp
```
- `-re`: lee el archivo a velocidad de reproducción real (1x).
- `-preset ultrafast -tune zerolatency`: mínima latencia de codificación.
- `-f rtsp`: formato de salida RTSP.
- `-rtsp_transport tcp`: usa TCP en lugar de UDP para evitar pérdida de paquetes.

### 3.3 MediaMTX (antes rtsp-simple-server)

**¿Qué es?** Servidor RTSP/WebRTC/SRT/HLS/RTMP de código abierto y alto rendimiento. Actúa como punto de encuentro (relay) entre el publicador y el suscriptor.

**Configuración por defecto:**
- Puerto RTSP: `8554`
- URL de stream: `rtsp://127.0.0.1:8554/live`
- No requiere autenticación en configuración por defecto.

**Rol en el sistema:** Recibe el stream de ffmpeg/Publisher y lo redistribuye a todos los suscriptores conectados (1 → N).

### 3.4 LibVLCSharp

**¿Qué es?** Binding oficial de .NET para libVLC, la biblioteca multimedia del proyecto VLC (VideoLAN).

**Paquetes NuGet:**
- `LibVLCSharp` — biblioteca base
- `LibVLCSharp.WinForms` — control `VideoView` para WinForms
- `VideoLAN.LibVLC.Windows` — binarios nativos de libVLC para Windows (x64)

**Uso en el proyecto:**
- `VideoView`: control WinForms que renderiza vídeo en un área de la ventana.
- `MediaPlayer`: gestiona reproducción, volumen, posición, eventos.
- `Media`: representa una fuente de medios (URL RTSP, archivo local).

**Uso en Publisher UI:** Preview local de archivos antes de publicarlos.  
**Uso en Subscriber UI:** Reproducción del stream RTSP directamente en la ventana.

### 3.5 .NET 8 / WinForms

**¿Qué es?** Framework de aplicaciones de escritorio para Windows con componentes visuales (Forms, Controls, etc.).

**Uso en el proyecto:** Interfaces gráficas de usuario para Publisher y Subscriber, con diseño de tema oscuro moderno ("Catppuccin Mocha").

### 3.6 Herramientas adicionales

| Herramienta | Descripción |
|-------------|-------------|
| `ProcessRunner` (clase interna) | Wrapper para lanzar procesos externos (ffmpeg, ffplay, vlc) capturando stdout/stderr |
| `MediaInfo` (clase interna) | Wrapper de ffprobe para detectar formato y metadatos de archivos de vídeo |

---

## 4. Estructura de Proyectos (Solución)

```
DdsVideoGateWaySystem/
│
├── DdsVideoGateWaySystem.slnx          ← Archivo de solución
│
├── VideoGateway.Engine/                 ← Modelos y contratos (sin dependencias externas)
│   ├── VideoFrame.cs                   ← Entidad: frame de vídeo (bytes + metadatos)
│   └── IVideoStreamer.cs               ← Interfaz: Start(), PushFrame(), Stop()
│
├── VideoGateway.Dds/                    ← Capa de transporte DDS
│   └── DdsReceiver.cs                  ← Suscriptor DDS → convierte DynamicData a VideoFrame
│
├── VideoGateway.Streaming/             ← Capa de streaming RTSP
│   └── FFmpegRtspStreamer.cs           ← Implementa IVideoStreamer usando ffmpeg por pipe
│
├── VideoGateway.Testing.Common/        ← Utilidades compartidas
│   ├── ProcessRunner.cs                ← Lanza procesos externos con captura de logs
│   └── MediaInfo.cs                   ← Detecta formato y metadatos con ffprobe
│
├── VideoGateway.PublisherUI/           ← Aplicación de escritorio: Publisher
│   ├── PublisherForm.cs                ← UI principal (lista archivos, publica, preview)
│   ├── SettingsForm.cs                 ← Diálogo de configuración
│   └── Program.cs                     ← Punto de entrada
│
├── VideoGateway.SubscriberUI/          ← Aplicación de escritorio: Subscriber
│   ├── SubscriberForm.cs               ← UI principal (conecta RTSP, reproduce, logs)
│   ├── SettingsForm.cs                 ← Diálogo de configuración
│   └── Program.cs                     ← Punto de entrada
│
├── VideoGateway.Console/               ← Aplicación de consola (integración DDS+Streaming)
│
└── Samples/                            ← Carpeta de archivos de vídeo de prueba
```

### Dependencias entre proyectos

```
PublisherUI   ──► Testing.Common
SubscriberUI  ──► Testing.Common
Console       ──► Dds ──► Engine
              ──► Streaming ──► Engine
```

---

## 5. Cómo Funciona el Proceso Completo

### Paso 1 — Inicio del servidor RTSP (MediaMTX)

MediaMTX debe estar ejecutándose antes de publicar o suscribirse. Actúa como hub central.

```bash
# Descargar de: https://github.com/bluenviron/mediamtx/releases
mediamtx.exe
# Escucha en rtsp://0.0.0.0:8554
```

### Paso 2 — Publicación de vídeo (Publisher UI)

1. El usuario abre **Publisher UI** y selecciona un archivo de vídeo de la lista.
2. Al pulsar **"▶ Publicar"**, la aplicación lanza `ffmpeg` como proceso hijo con los argumentos de transcodificación.
3. `ffmpeg` lee el archivo origen, decodifica cualquier codec de entrada y **recodifica a H.264 + AAC** en tiempo real.
4. El stream codificado se envía al servidor MediaMTX mediante el protocolo RTSP sobre TCP.
5. El log de conversión de `ffmpeg` (stderr) aparece en tiempo real en la pestaña **"Logs FFmpeg"** de la UI.

### Paso 3 — Distribución por DDS (opcional, modo consola)

En el modo avanzado con la aplicación de consola:
1. `DdsReceiver` se une al dominio DDS y se suscribe al topic `VideoData::Frame`.
2. Cada muestra recibida se deserializa a un objeto `VideoFrame` con: `format`, `width`, `height`, y los bytes del frame (`data[]`).
3. `FFmpegRtspStreamer` recibe el `VideoFrame` y lo inyecta por pipe (`stdin`) a una instancia de `ffmpeg`.
4. `ffmpeg` detecta el formato del stream de entrada dinámicamente y transcodifica a H.264 → RTSP.
5. Si el formato de los frames cambia (ej. de MJPEG a H.264), `FFmpegRtspStreamer` reinicia `ffmpeg` automáticamente con la configuración correcta.

### Paso 4 — Recepción y reproducción (Subscriber UI)

1. El usuario abre **Subscriber UI** e introduce la URL RTSP (`rtsp://127.0.0.1:8554/live`).
2. Al pulsar **"▶ Conectar"**, `LibVLCSharp` establece conexión con el servidor RTSP.
3. El vídeo se renderiza en el área negra de la ventana en tiempo real.
4. Los eventos de reproducción (iniciado, detenido, error) aparecen en el **panel de log** inferior.
5. El usuario puede ajustar el volumen con el slider o detener la reproducción con **"⏹ Detener"**.

### Paso 5 — Transcodificación dinámica por formato

| Formato de origen | Argumentos de entrada ffmpeg | Salida |
|-------------------|------------------------------|--------|
| MJPEG / JPEG | `-f image2pipe -vcodec mjpeg` | H.264 |
| H.264 | `-f h264` | H.264 (remux/transcode) |
| H.265 / HEVC | `-f hevc` | H.264 |
| RAW (BGRs) | `-f rawvideo -pix_fmt bgr24 -s WxH` | H.264 |
| Otros | Autodetección ffmpeg | H.264 |

---

## 6. Requisitos e Instalación

### 6.1 Requisitos del sistema

| Componente | Versión mínima | Descarga |
|-----------|---------------|---------|
| Windows | 10 / 11 (64-bit) | — |
| .NET Runtime | 8.0 | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) |
| FFmpeg | 6.0+ (incluye ffmpeg, ffplay, ffprobe) | [ffmpeg.org/download](https://ffmpeg.org/download.html) |
| MediaMTX | 1.x | [github.com/bluenviron/mediamtx](https://github.com/bluenviron/mediamtx/releases) |
| VLC (opcional) | 3.x | [videolan.org/vlc](https://www.videolan.org/vlc/) |
| RTI Connext DDS | 7.x (para modo DDS) | [rti.com](https://www.rti.com/free-trial) |

### 6.2 Configuración de PATH

Añade los ejecutables de FFmpeg y MediaMTX al PATH del sistema:

```powershell
# Verifica que ffmpeg esté disponible:
ffmpeg -version

# Verifica ffprobe:
ffprobe -version

# Verifica ffplay:
ffplay -version
```

### 6.3 Compilar desde el código fuente

```powershell
# Clonar/abrir la carpeta del proyecto
cd C:\...\DdsVideoGateWaySystem

# Restaurar dependencias
dotnet restore

# Compilar en modo Debug
dotnet build

# Compilar en modo Release
dotnet build -c Release
```

### 6.4 Ejecutar las aplicaciones

```powershell
# Publisher UI
dotnet run --project VideoGateway.PublisherUI

# Subscriber UI
dotnet run --project VideoGateway.SubscriberUI

# Aplicación de consola (modo DDS)
dotnet run --project VideoGateway.Console
```

---

## 7. Manual de Usuario — Publisher UI

### 7.1 Pantalla principal

La ventana del Publisher tiene dos áreas principales:

**Panel izquierdo (Archivos/Samples):**
- Lista todos los archivos de vídeo encontrados en la carpeta `Samples/`.
- Haz clic en cualquier archivo para seleccionarlo y ver sus metadatos.
- El botón **"Examinar…"** permite navegar a cualquier carpeta del sistema.
- El botón **"⚙ Config"** abre el diálogo de configuración.

**Panel derecho (Espacio de trabajo):**
- Barra superior con la URL RTSP destino y botones de acción.
- Área de preview de vídeo (negro cuando no hay reproducción).
- Barra de controles de reproducción (Play/Pause/Stop, volumen, posición).
- Pestañas inferiores: **Info (ffprobe)** y **Logs FFmpeg**.

### 7.2 Publicar un vídeo

1. **Selecciona** un archivo de la lista izquierda. Los metadatos aparecen automáticamente en la pestaña **"ℹ Info (ffprobe)"**.
2. **Verifica** que la URL RTSP en el campo superior sea correcta (`rtsp://127.0.0.1:8554/live` por defecto).
3. **Asegúrate** de que MediaMTX esté ejecutándose (ver sección [Requisitos](#6-requisitos-e-instalación)).
4. Pulsa **"▶ Publicar"**. La publicación comienza inmediatamente.
5. Observa los logs de ffmpeg en tiempo real en la pestaña **"📋 Logs FFmpeg"**.
6. Para detener la publicación, pulsa **"■ Detener"**.

> **Nota:** El botón "▶ Publicar" se desactiva durante la publicación para evitar procesos duplicados.

### 7.3 Hacer preview de un archivo

1. Selecciona un archivo de la lista.
2. Pulsa **"👁 Preview"** o el botón **"▶"** de la barra de reproducción.
3. Si LibVLC está disponible, el vídeo se reproduce en el área negra embebida.
4. Si LibVLC no está disponible, se abre `ffplay` en una ventana externa.
5. Controles disponibles:
   - **⏸ Pausa / ▶ Reanudar**: pausa/reanuda la reproducción embebida.
   - **⏹ Stop**: detiene la reproducción.
   - **Slider Volumen**: ajusta el volumen del reproductor embebido.
   - **Slider Posición**: navega a un punto del vídeo (arrastrar y soltar).

### 7.4 Ver metadatos de un archivo (ffprobe)

Al seleccionar cualquier archivo, la pestaña **"ℹ Info (ffprobe)"** muestra automáticamente:
- Nombre del archivo y ruta completa.
- **Formato contenedor** (ej. `mov,mp4,m4a,3gp,3g2,mj2`).
- **Duración** del vídeo.
- **Bitrate** global.
- Para cada stream: **codec**, **resolución**, **framerate**, **canales de audio**, **sample rate**.

Ejemplo de salida:
```
Archivo: video_prueba.mp4
Formato: mov,mp4,m4a,3gp,3g2,mj2
Duración: 00:02:34
Bitrate global: 4,250 kb/s

[Stream #0:0] Video
  Codec: h264 (H.264 / AVC)
  Resolución: 1920x1080
  Framerate: 30 fps
  Bitrate: 4,000 kb/s

[Stream #0:1] Audio
  Codec: aac (AAC)
  Sample Rate: 48000 Hz
  Canales: 2 (stereo)
  Bitrate: 192 kb/s
```

---

## 8. Manual de Usuario — Subscriber UI

### 8.1 Pantalla principal

La ventana del Subscriber tiene:

**Cabecera superior:**
- Título de la aplicación.
- Campo de URL RTSP.
- Botones: **▶ Conectar**, **⏹ Detener**, **🔗 VLC ext.**, **⚙ Config**.
- Slider de volumen.

**Área principal (SplitContainer):**
- **Panel superior (negro):** área de vídeo embebido (LibVLC).
- **Panel inferior:** log de conexión en tiempo real.
- La barra divisoria se puede arrastrar para redimensionar las áreas.

**Barras inferiores:**
- Información sobre formato recomendado.
- Estado actual de la conexión.

### 8.2 Reproducir un stream RTSP

1. **Verifica** que el Publisher esté activo y MediaMTX esté ejecutándose.
2. **Introduce** la URL RTSP en el campo (por defecto: `rtsp://127.0.0.1:8554/live`).
3. Pulsa **"▶ Conectar"**.
4. El estado cambiará a "Conectando…" (amarillo) y después a "Reproduciendo streaming." (verde).
5. El vídeo aparecerá en el área negra.
6. El panel de log mostrará los eventos de conexión.

### 8.3 Opciones de reproducción

| Acción | Descripción |
|--------|-------------|
| **▶ Conectar** | Conecta al stream RTSP con el reproductor LibVLC embebido |
| **⏹ Detener** | Para la reproducción (desconecta del servidor RTSP) |
| **🔗 VLC ext.** | Abre la URL en VLC externo o ffplay (sin afectar el reproductor embebido) |
| **Slider Vol.** | Ajusta el volumen del reproductor embebido (0–100%) |
| **Limpiar** (en log) | Borra el historial de logs |

### 8.4 Panel de log

El panel de log inferior registra automáticamente:
- Hora de cada evento (`[HH:mm:ss]`).
- Intento de conexión al servidor.
- Confirmación de stream activo (`✔ Stream activo — reproduciendo.`).
- Errores de conexión con sugerencias de solución.
- Eventos de detención.

---

## 9. Pruebas y Verificación

### 9.1 Prueba básica de extremo a extremo

**Objetivo:** Verificar que el sistema completo funciona con un vídeo MP4 estándar.

```
1. Iniciar MediaMTX
2. Abrir Publisher UI → seleccionar un .mp4 → pulsar Publicar
3. Abrir Subscriber UI → pulsar Conectar
4. Verificar: vídeo reproducible y sin artefactos
```

### 9.2 Prueba de transcodificación por formato

**Objetivo:** Verificar que todos los formatos se convierten correctamente a H.264.

| Archivo de prueba | Formato origen | Resultado esperado |
|------------------|----------------|-------------------|
| `video.mp4` | H.264/AAC | Stream RTSP directo, mínima latencia |
| `video.mkv` | H.265/HEVC | Transcodificación a H.264 (tarda más) |
| `video.avi` | MPEG-4 / MP3 | Transcodificación completa a H.264/AAC |
| `video.mov` | ProRes / AAC | Transcodificación a H.264 |
| `imagen.jpg` | MJPEG | Publicación como stream de imagen estática |

**Cómo verificar el formato de salida con ffprobe:**

```powershell
# Analiza el stream RTSP recibido por MediaMTX
ffprobe -v quiet -print_format json -show_streams rtsp://127.0.0.1:8554/live
```

El resultado debe siempre mostrar:
```json
{
  "codec_name": "h264",
  "codec_type": "video"
}
```

### 9.3 Prueba de latencia

**Objetivo:** Medir el retardo entre la publicación y la visualización.

1. Reproduce un vídeo con un reloj o contador visible.
2. Publica con Publisher UI.
3. Compara la hora visible en Publisher UI preview vs. Subscriber UI.
4. La latencia típica con `-preset ultrafast -tune zerolatency` es de **0.5–2 segundos**.

### 9.4 Verificación de log de conversión

En Publisher UI, pestaña **"📋 Logs FFmpeg"**, busca estas líneas indicativas:

```
Output #0, rtsp
  Stream #0:0: Video: h264 (libx264), yuv420p, 1920x1080, ...
  Stream #0:1: Audio: aac, 48000 Hz, stereo, ...
frame=  120 fps= 30 q=23.0 size=    1024kB time=00:00:04.00 ...
```

- **`Video: h264`** → transcodificación correcta.
- **`Audio: aac`** → audio correctamente codificado.
- **`frame=`** → frames procesados; debe incrementar continuamente.

### 9.5 Prueba con VLC externo

```powershell
# Verifica reproducción directa del stream RTSP con VLC CLI
vlc rtsp://127.0.0.1:8554/live

# O con ffplay
ffplay rtsp://127.0.0.1:8554/live
```

---

## 10. Resolución de Problemas

### El Publisher no puede iniciar ffmpeg

**Síntoma:** Aparece el mensaje "Error al iniciar ffmpeg. ¿Está en PATH?"

**Soluciones:**
1. Verifica que `ffmpeg.exe` esté en el PATH del sistema.
2. Abre PowerShell y ejecuta `ffmpeg -version`.
3. Si no está en el PATH, añade la carpeta `bin/` de FFmpeg a las variables de entorno del sistema.

### El Subscriber no puede conectarse al stream

**Síntoma:** El estado cambia a "Error en el stream."

**Soluciones:**
1. Verifica que MediaMTX esté en ejecución (`mediamtx.exe`).
2. Verifica que el Publisher esté activo y publicando.
3. Confirma la URL: `rtsp://127.0.0.1:8554/live` (sin espacios).
4. Comprueba que el firewall no bloquea el puerto 8554.

### El vídeo embebido no se muestra (pantalla negra permanente)

**Síntoma:** El log muestra "✔ Stream activo" pero el área de vídeo permanece negra.

**Soluciones:**
1. Verifica que los binarios nativos de libVLC están presentes (`libvlc.dll`, `libvlccore.dll` en la carpeta de salida).
2. El paquete `VideoLAN.LibVLC.Windows` debe estar referenciado en el proyecto `.csproj`.
3. Usa **"🔗 VLC ext."** como alternativa para abrir en VLC externo.

### LibVLC falla al inicializar

**Síntoma:** El área de vídeo muestra "Reproductor embebido no disponible (LibVLC no cargado)."

**Causa:** Los binarios nativos de libVLC no se encuentran en el directorio de ejecución.

**Solución:** Asegúrate de que en el `.csproj` existe:
```xml
<PackageReference Include="VideoLAN.LibVLC.Windows" Version="3.*" />
```
y que el proyecto se ha compilado y restaurado correctamente (`dotnet restore`).

### Error "NO_DATA" en la consola DDS

**Comportamiento normal.** El receptor DDS imprime este mensaje cuando el topic no tiene datos disponibles en el buffer de lectura. Se suprime automáticamente en el código para no saturar los logs.

### La latencia es muy alta (>5 segundos)

**Soluciones:**
1. Usa `-preset ultrafast` en los argumentos de ffmpeg (ya incluido por defecto).
2. Cambia de UDP a TCP: `-rtsp_transport tcp` (ya incluido por defecto).
3. Comprueba que no hay otros procesos consumiendo mucha CPU.
4. Prueba con archivos de menor resolución/bitrate.

---

## 11. Referencia de Formatos de Vídeo Soportados

| Extensión | Formato contenedor | Codecs frecuentes | Soporte |
|-----------|-------------------|-------------------|---------|
| `.mp4` | MP4 / ISO Base Media | H.264, H.265, AAC | ✅ Nativo |
| `.mkv` | Matroska | H.264, H.265, VP9, AAC, FLAC | ✅ Transcodificado |
| `.avi` | AVI | MPEG-4, XVID, MP3 | ✅ Transcodificado |
| `.mov` | QuickTime | ProRes, H.264, AAC | ✅ Transcodificado |
| `.webm` | WebM | VP8, VP9, Opus | ✅ Transcodificado |
| `.ts` | MPEG-TS | H.264, MPEG-2 | ✅ Transcodificado |
| `.flv` | Flash Video | H.264, AAC, Sorenson | ✅ Transcodificado |
| `.jpg / .jpeg` | JPEG | MJPEG | ✅ Imagen estática como stream |

> **Formato de salida siempre:** H.264 (libx264) + AAC — compatible con VLC, FFplay, MediaMTX, navegadores modernos y cualquier dispositivo con soporte RTSP.

---

*Documentación generada para DDS Video Gateway System — Proyecto de integración DDS + RTSP.*
