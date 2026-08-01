namespace Frelon.Mail;

/// <summary>Signale le refus sûr d'une preuve qui dépasse un quota d'analyse.</summary>
public sealed class EmailAnalysisLimitException : IOException
{
    internal EmailAnalysisLimitException(string message)
        : base(message)
    {
    }
}
