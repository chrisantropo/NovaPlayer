namespace NovaPlayer.Models;

public enum PlaybackEndAction
{
    /// <summary>
    /// Detener el vídeo y no hacer nada al terminar (comportamiento por defecto).
    /// </summary>
    Stop = 0,

    /// <summary>
    /// Volver a reproducir el mismo vídeo desde el inicio en bucle continuo.
    /// </summary>
    RepeatCurrent = 1,

    /// <summary>
    /// Reproducir automáticamente el siguiente vídeo de la lista de reproducción o de la carpeta.
    /// </summary>
    PlayNext = 2
}
