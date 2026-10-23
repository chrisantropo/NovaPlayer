# 🎬 NovaPlayer

**NovaPlayer** es un reproductor multimedia de escritorio para Windows de alto rendimiento con diseño moderno estilo **Windows 11 Fluent / Acrylic**, impulsado por el motor nativo **LibVLC (VLC Core)** en **C# .NET 9**.

Diseñado como una evolución visual y funcional de reproductores clásicos como **VLC Media Player** y **Media Player Classic (MPC-BE)**, combinando una interfaz oscura elegante con todas las capacidades de reproducción, códecs, aceleración hardware, arrastrar y soltar y herramientas de subtítulos en línea.

---

## ✨ Características Principales

### 🎥 Soporte Total de Formatos y Códecs (Motor LibVLC)
- **Contenedores soportados**: MKV, MP4, AVI, WebM, MOV, FLV, WMV, TS, M2TS, VOB, OGV, 3GP y más.
- **Códecs de vídeo**: H.264 (AVC), H.265 (HEVC), AV1, VP8, VP9, MPEG-1/2/4, VC-1, ProRes, Theora.
- **Códecs de audio**: AAC, AC3 (Dolby Digital), E-AC3 (Dolby Digital Plus), DTS, DTS-HD, TrueHD, FLAC, MP3, Opus, Vorbis, PCM.
- **Aceleración por hardware**: D3D11 y DXVA2 integrados automáticamente para reproducción fluida en 4K y 8K.
- **Optimizado para archivos pesados (15 GB+)**: Búfer de caché ampliado a 3000 ms, decodificación por hardware de subprocesos automáticos (`--avcodec-threads=0`, `--avcodec-fast`) y sistema de salto temporal asíncrono no bloqueante para evitar congelamientos.
- **Control de relación de aspecto**: Original (Default), 16:9, 4:3, 21:9 (Ultrawide) y 1:1.
- **Avance fotograma a fotograma**: Inspección precisa de vídeo (`>|`).
- **Captura de pantalla (Snapshot)**: Guarda fotogramas en alta resolución en `Imágenes/NovaPlayer` con efecto visual de obturador y copia directa al portapapeles con un clic o pulsando `S`.

### 📂 Navegación de Vídeos por Carpeta y Arrastrar y Soltar
- **Navegación inteligente en la carpeta actual**:
  - Los botones de **Anterior** y **Siguiente** en la barra inferior buscan automáticamente todos los archivos de vídeo compatibles en la misma carpeta del vídeo actual.
  - **Ordenación natural de archivos**: Algoritmo `StrCmpLogicalW` (idéntico al del Explorador de Windows) para ordenar correctamente nombres numerados (ej. *Episodio 1*, *Episodio 2*, *Episodio 10*).
  - Notificaciones en pantalla indicando el vídeo cargado y la posición actual (ej. *Siguiente (3/12): S01E03.mkv*).
  - Avance automático continuo al siguiente vídeo de la carpeta al finalizar la reproducción.
  - Atajos dedicados: `Re Pág` / `Av Pág` o `Ctrl + ←` / `Ctrl + →`.
- **Arrastrar y Soltar (Drag & Drop)**:
  - Arrastra cualquier archivo o conjunto de archivos de vídeo desde el Explorador de Windows directamente a la ventana del reproductor para iniciar su reproducción al instante y agregarlos a la lista de reproducción.
  - Arrastra archivos de subtítulos (`.srt`, `.vtt`, `.ass`, etc.) para cargarlos y activarlos de inmediato.
  - Compatibilidad integral con **OLE Drag & Drop** y mensajes nativos Win32 de shell (`WM_DROPFILES`, `ChangeWindowMessageFilterEx`), garantizando funcionamiento incluso ante aislamiento de privilegios UIPI o reproduciendo sobre capas nativas de vídeo.

### 💬 Integración y Sincronización Avanzada de Subtítulos
- **Formatos de subtítulos**: SRT, VTT, ASS, SSA, SUB (tanto pistas incrustadas en MKV/MP4 como archivos externos).
- **Detección automática de subtítulos locales**: Detecta y asocia automáticamente archivos `.srt` con el mismo nombre en la carpeta del vídeo.
- **Sincronización milimétrica en tiempo real**:
  - Control de retraso/adelanto en microsegundos con botones de paso rápido (`-100ms`, `-25ms`, `+25ms`, `+100ms`) y botón de restablecer a `0 ms`.
  - Atajos de teclado dedicados: `G` para adelantar 50ms y `H` para retrasar 50ms.
- **Búsqueda y descarga online integrada**:
  - Panel lateral deslizante para buscar subtítulos en tiempo real en **OpenSubtitles.com** y **Subdl**.
  - **Cálculo de Hash de vídeo**: Algoritmo OpenSubtitles de 64 bits para coincidencia exacta con el archivo específico.
  - Filtro por idiomas: 🇪🇸 Español, 🇺🇸 English, 🇫🇷 Français, 🇩🇪 Deutsch o Todos.
  - Visualización de puntuación (★), recuento de descargas e indicador para personas con discapacidad auditiva.
  - Botón **"Descargar y Aplicar"**: Descarga, descomprime (.zip / .gz) y aplica el subtítulo directamente a la reproducción sin reiniciar el vídeo.

### 🔊 Audio y Control de Sonido
- Selector dinámico de pistas de audio (cambio fluido entre doblajes e idiomas originales).
- **Control de volumen con precisión milimétrica**:
  - Incremento o decremento de **1%** por pulsación mediante flechas de teclado `↑` y `↓`.
  - Incremento o decremento de **5%** mediante scroll de ratón en cualquier punto de la ventana.
- **Audio Boost**: Amplificación de volumen hasta el **200%** para vídeos con pistas grabadas a bajo volumen.
- Atajo rápido de silencio (`M`).

### ⚡ Estética, Rendimiento y Robustez
- **Arranque Ultrarrápido e Inmediato**: Inicialización asíncrona de LibVLC en segundo plano y precarga optimizada sin congelar el hilo principal UI; la ventana abre y se dibuja al instante.
- **Resistencia total contra cierres inesperados**:
  - Ciclo de vida extendido de punteros nativos `Media`, evitando liberaciones prematuras que provocaban fallos aleatorios.
  - Desacoplamiento seguro de callbacks nativos C de LibVLC (`EndReached`, `Playing`, `Paused`, `Stopped`, etc.) mediante despachos asíncronos para eliminar bloqueos mutuos (deadlocks).
  - Capturador global de excepciones no controladas con registro de diagnóstico detallado en `%APPDATA%\NovaPlayer\crash_log.txt`.
- **Interfaz Dark Fluent**: Paleta oscura obsidiana con acentos en cian neón y tipografía moderna Google **Audiowide**.
- **Barra de menú superior y menú contextual emergente**:
  - Menú superior completo con accesos a Medio, Reproducción, Audio, Subtítulos, Vídeo y Barras.
  - Menú contextual desplegable al hacer clic derecho sobre la ventana de vídeo.
  - Sistema de menús emergentes con renderizado optimizado y sin solapamiento de ventanas.
- **Controles flotantes translúcidos (HUD)** con opción de fijarse siempre visibles o desvanecerse automáticamente tras 5 segundos de inactividad del cursor.
- **Desplazamiento de ventana (Window Dragging)**: Mueve la ventana libremente arrastrando desde el área superior (logo de la app, nombre o título del vídeo).
- **Pantalla completa inmersiva**: Cobertura total de la pantalla ocultando por completo la barra de tareas de Windows.
- **Modo Mini-Reproductor (PiP / Picture-in-Picture)**: Ventana compacta flotante siempre visible en la esquina inferior derecha.
- **Fijar en primer plano (Always on top)**: Mantén el reproductor por encima de otras ventanas.
- **Historial de archivos recientes**: Lista de reproducción rápida con los últimos archivos abiertos y opción de borrado.
- **Lista de reproducción (Playlist)** con soporte de ordenación, modos de repetición (Todo, Uno, Desactivado) y reproducción aleatoria (Shuffle).
- **Comportamiento configurable al finalizar el vídeo**:
  - **Detener el vídeo y no hacer nada (por defecto)**: Detiene la reproducción al llegar al final y mantiene el reproductor listo para iniciar de nuevo.
  - **Volver a reproducir el mismo vídeo desde el inicio**: Bucle continuo del vídeo actual (ideal para clips, tutoriales o bucles visuales).
  - **Reproducir el siguiente vídeo de la lista o carpeta**: Modo de reproducción continua automática que avanza al siguiente archivo de la lista o al siguiente vídeo ordenado de la carpeta.
  - Acceso desde la barra inferior (con botón interactivo y clic derecho), menú superior (`Reproducción > Al terminar el vídeo`) y menú contextual emergente, guardando la preferencia automáticamente entre sesiones en `%APPDATA%\NovaPlayer\settings.json`.
- **Notificaciones Toast flotantes**: Indicadores visuales instantáneos al cambiar volumen, velocidad, retraso de subtítulos o guardar capturas.
- **Centro de Ayuda, Actualizaciones y Acerca de (`F1`)**:
  - Panel deslizante integrado con pestañas para información de la aplicación, guía de atajos y gestor de actualizaciones.
  - **Búsqueda y descarga automática de actualizaciones**: Consulta las últimas versiones publicadas en GitHub Releases, muestra notas de la versión y descarga/ejecuta el instalador con barra de progreso con un solo clic.
  - Enlaces rápidos al repositorio oficial y reporte de incidencias en GitHub.

---

## ⌨️ Atajos de Teclado y Ratón (Shortcuts)

| Control | Acción |
| :--- | :--- |
| `Espacio` / `K` | Reproducir / Pausar |
| `Clic izquierdo` | Reproducir / Pausar (en área de vídeo) |
| `←` / `→` | Retroceder / Avanzar **10 segundos** desde el punto actual |
| `Re Pág` / `Av Pág` | Vídeo **anterior** / **siguiente** en la carpeta actual |
| `Ctrl + ←` / `Ctrl + →` | Vídeo **anterior** / **siguiente** en la carpeta actual |
| `↑` / `↓` | Subir / Bajar volumen en **1%** |
| `Rueda del ratón (Scroll)` | Subir / Bajar volumen en **5%** en cualquier parte de la ventana |
| `M` | Silenciar / Activar sonido |
| `F` / `F11` | Alternar pantalla completa total |
| `Doble clic` | Alternar pantalla completa total |
| `Clic derecho` | Abrir menú emergente de opciones |
| `Esc` | Salir de pantalla completa o cerrar paneles/cajones |
| `F1` | Abrir centro de Ayuda, Acerca de y Actualizaciones |
| `G` | Adelantar subtítulos (+50 ms) |
| `H` | Retrasar subtítulos (-50 ms) |
| `[` / `]` | Disminuir / Aumentar velocidad de reproducción |
| `S` | Tomar captura de pantalla (guarda en `Imágenes/NovaPlayer` y copia al portapapeles) |
| `P` | Abrir / Cerrar panel de lista de reproducción |
| `Ctrl + O` | Abrir archivo de vídeo |
| `Ctrl + Shift + S` | Abrir panel de búsqueda online de subtítulos |
| `Arrastrar archivos` | Cargar y reproducir archivo de vídeo o subtítulo |

---

## 🚀 Compilación y Ejecución

### Requisitos:
- **Windows 10 / 11 (64-bit)**
- **.NET 9.0 SDK** (instalado en el sistema)

### Compilar:
```powershell
cd NovaPlayer
dotnet build -c Release
```

### Ejecutar en modo desarrollo:
```powershell
dotnet run --project NovaPlayer.csproj
```
o abrir directamente el ejecutable compilado:
`NovaPlayer\bin\Release\net9.0-windows\NovaPlayer.exe`

---

## 📦 Instalador de Windows (Setup)

Nova Player incluye un instalador completo para Windows desarrollado con **Inno Setup**:

- **Ubicación del instalador**: `InstallerOutput\NovaPlayer-Setup.exe`
- **Características de la instalación**:
  - **Empaquetado autónomo (Self-contained)**: Incluye todo el runtime de .NET 9 y el motor nativo LibVLC x64 (no requiere descargas externas).
  - **Accesos directos**: En el Menú Inicio y en el Escritorio.
  - **Anclaje a la barra de tareas**: Configurado con `AppUserModelID: "NovaPlayer.App"` para anclar el acceso directo y agrupar ventanas sin iconos duplicados.
  - **Reproductor por defecto**: Registra las capacidades multimedia en Windows 10 y Windows 11 (*Configuración > Aplicaciones > Aplicaciones predeterminadas*).
  - **Menú contextual de Windows**: Añade la opción **"Abrir con Nova Player"** al hacer clic derecho en cualquier archivo de vídeo o audio, y **"Reproducir con Nova Player"** en carpetas.
  - **Desinstalador limpio**: Desinstala la aplicación y elimina las entradas del registro de forma segura desde el panel de control.

### Generar el instalador desde código fuente:
```powershell
# 1. Publicar la aplicación en modo autónomo
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o bin\Publish

# 2. Compilar el instalador con Inno Setup 6
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" NovaPlayerInstaller.iss
```
El instalador resultante se generará en `InstallerOutput\NovaPlayer-Setup.exe`.
