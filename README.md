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

### 📂 Arrastrar y Soltar (Drag & Drop)
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

### ⚡ Estética y Experiencia de Usuario Moderna
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
- **Notificaciones Toast flotantes**: Indicadores visuales instantáneos al cambiar volumen, velocidad, retraso de subtítulos o guardar capturas.

---

## ⌨️ Atajos de Teclado y Ratón (Shortcuts)

| Control | Acción |
| :--- | :--- |
| `Espacio` / `K` | Reproducir / Pausar |
| `Clic izquierdo` | Reproducir / Pausar (en área de vídeo) |
| `←` / `→` | Retroceder / Avanzar **10 segundos** desde el punto actual |
| `↑` / `↓` | Subir / Bajar volumen en **1%** |
| `Rueda del ratón (Scroll)` | Subir / Bajar volumen en **5%** en cualquier parte de la ventana |
| `M` | Silenciar / Activar sonido |
| `F` / `F11` | Alternar pantalla completa total |
| `Doble clic` | Alternar pantalla completa total |
| `Clic derecho` | Abrir menú emergente de opciones |
| `Esc` | Salir de pantalla completa o cerrar paneles/cajones |
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

### Ejecutar:
```powershell
dotnet run --project NovaPlayer.csproj
```
o abrir directamente el ejecutable compilado:
`NovaPlayer\bin\Release\net9.0-windows\NovaPlayer.exe`
