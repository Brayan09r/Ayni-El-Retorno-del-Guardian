namespace Ayni.Combat
{
    /// <summary>Altura de un ataque: decide qué esquiva lo evita (Duck evita los altos, Jump evita los bajos).</summary>
    public enum AttackHeight
    {
        High,   // Puñetazos y patadas altas: se esquivan agachándose (Guardia + S / Espacio)
        Low     // Barridos de pierna: se esquivan saltando (Guardia + W)
    }

    /// <summary>Resultado de un ataque una vez resuelto contra el defensor.</summary>
    public enum AttackResult
    {
        Missed,   // Fuera de alcance o defensor invulnerable
        Dodged,   // Esquiva correcta
        Parried,  // Desvío perfecto
        Blocked,  // Bloqueado con la guardia
        Hit       // Golpe directo
    }

    /// <summary>Cómo reacciona quien recibe un golpe, según el golpe que le dan.</summary>
    public enum HitReaction
    {
        Head,      // Sacudida de cabeza (directos, ganchos)
        Body,      // Se dobla por el estómago
        Heavy,     // Golpe fuerte: retrocede tambaleándose
        Knockdown  // Cae al suelo y tiene que levantarse
    }
}
