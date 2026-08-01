namespace Frelon.Mail;

/// <summary>Signale l'arrêt défensif d'une analyse qui dépasse son quota de temps.</summary>
public sealed class EmailAnalysisTimeoutException : IOException
{
    internal EmailAnalysisTimeoutException()
        : base("L'analyse a dépassé le temps maximal autorisé.")
    {
    }
}
