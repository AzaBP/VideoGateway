UI testing apps for VideoGateway
--------------------------------

Se han añadido dos aplicaciones WinForms para pruebas locales:

- VideoGateway.PublisherUI: lista ficheros de la carpeta Samples y puede publicar un fichero seleccionado hacia un servidor RTSP usando ffmpeg.
- VideoGateway.SubscriberUI: permite introducir la URL RTSP y abrirla en VLC o ffplay.

Proyectos:
- VideoGateway.Testing.Common (classlib) - utilidades compartidas (ProcessRunner, MediaInfo)
- VideoGateway.PublisherUI (WinForms) - publisher UI
- VideoGateway.SubscriberUI (WinForms) - subscriber UI

Pasos para usar:
1) Añade vídeos en la carpeta Samples/ del repositorio.
2) Compila la solución.
3) Ejecuta PublisherUI, selecciona la carpeta Samples y publica un fichero.
4) Ejecuta SubscriberUI y abre la URL RTSP (por defecto rtsp://127.0.0.1:8554/live) con VLC o ffplay.

Notas:
- ffmpeg, ffplay y vlc deben estar instalados y en PATH para que las apps lancen los binarios externos.
- Este diseño separa frontend (UI projects) del backend/testing (Testing.Common).
