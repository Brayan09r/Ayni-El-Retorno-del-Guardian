namespace Ayni.Core
{
    /// <summary>
    /// Versión del juego (formato del equipo, ver COLLABORATORS.md): MAYOR.MENOR.PARCHE
    ///   MAYOR  hitos (Alpha, Beta, demo) · MENOR  mejoras de jugabilidad · PARCHE  arreglos y ajustes.
    /// Al subir de versión: cambiar <see cref="Number"/>, añadir la entrada en CHANGELOG.md y, cuando el cambio
    /// esté en main, crear el tag (git tag -a vX.Y.Z). El menú Ayni > Versión copia este número a los ajustes del proyecto.
    /// El HUD la muestra en la esquina, para saber siempre qué versión se está probando.
    /// </summary>
    public static class AyniVersion
    {
        public const string Number = "0.7.0";
        public const string Title = "Jefes en dos fases, menú de pausa y balanceo nuevo";

        public static string Label => "v" + Number;
    }
}
