namespace Frelon.Mail;

/// <summary>
/// Sélectionne le parseur adapté au contenu réel d'une preuve de courrier électronique.
/// </summary>
public sealed class EmailEvidenceParser : IEmailParser
{
    private static ReadOnlySpan<byte> OutlookCompoundFileSignature =>
    [
        0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1
    ];

    private readonly EmailAnalysisLimits _limits;
    private readonly MimeKitEmailParser _emlParser;
    private readonly OutlookMsgEmailParser _msgParser;

    /// <summary>Crée le sélecteur avec les parseurs EML et MSG de référence.</summary>
    public EmailEvidenceParser()
        : this(EmailAnalysisLimits.Default)
    {
    }

    /// <summary>Crée le sélecteur avec des quotas explicites.</summary>
    public EmailEvidenceParser(EmailAnalysisLimits limits)
    {
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _limits.Validate();
        _emlParser = new MimeKitEmailParser(_limits);
        _msgParser = new OutlookMsgEmailParser(_limits);
    }

    /// <inheritdoc/>
    public async Task<ParsedEmail> ParseAsync(
        Stream emailStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emailStream);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceBytes = await EmailContentBuffer
            .ReadAsync(emailStream, _limits, cancellationToken)
            .ConfigureAwait(false);
        var bytes = sourceBytes.AsSpan();
        var isOutlookMessage = bytes.StartsWith(OutlookCompoundFileSignature);

        try
        {
            return isOutlookMessage
                ? await _msgParser.ParseBufferedAsync(sourceBytes, cancellationToken).ConfigureAwait(false)
                : await _emlParser.ParseBufferedAsync(sourceBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException
            and not EmailAnalysisLimitException
            and not OutOfMemoryException)
        {
            throw new InvalidDataException(
                "Le fichier ne contient pas un message EML ou MSG valide.",
                exception);
        }
    }
}
