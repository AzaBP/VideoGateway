# Documentación C++ (espacio reservado)

Propósito
- Documentar componentes nativos si existen (bindings, bibliotecas nativas que interactúan con DDS o FFmpeg).

Contenido sugerido (completar si hay código nativo)
- Estructura:
  - `native/` o `cpp/` — código C++.
  - `CMakeLists.txt` o instrucciones MSBuild.
- Build:
  - `mkdir build && cd build && cmake .. && cmake --build .`
  - O instrucciones con `msbuild`/`devenv`.
- Interoperabilidad con C#:
  - P/Invoke o C++/CLI: documentar firmas, ownership y gestión de memoria.
- Toolchain:
  - Versión MSVC / Visual Studio soportada.
  - Uso de `vcpkg` o submódulos para dependencias (RTI DDS libs).

Si detectas archivos nativos en el repo, puedo generar documentación completa y ejemplos de build.