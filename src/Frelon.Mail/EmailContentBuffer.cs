using System.Buffers;

namespace Frelon.Mail;

/// <summary>Lit une preuve sans jamais dépasser la taille maximale autorisée.</summary>
internal static class EmailContentBuffer
{
    private const int CopyBufferSize = 64 * 1024;

    public static async Task<byte[]> ReadAsync(
        Stream source,
        EmailAnalysisLimits limits,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        if (source.CanSeek)
        {
            var remainingLength = source.Length - source.Position;
            if (remainingLength <= 0)
            {
                throw new EmailAnalysisLimitException(
                    "Le fichier de message est vide.");
            }

            if (remainingLength > limits.MaxSourceBytes)
            {
                throw SourceTooLarge(limits);
            }
        }

        using var destination = new MemoryStream(
            capacity: source.CanSeek
                ? checked((int)(source.Length - source.Position))
                : Math.Min(CopyBufferSize, limits.MaxSourceBytes));
        var rented = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        try
        {
            var total = 0;
            while (true)
            {
                var remainingWithSentinel =
                    (long)limits.MaxSourceBytes - total + 1;
                var requested = checked((int)Math.Min(rented.Length, remainingWithSentinel));
                var read = await source
                    .ReadAsync(rented.AsMemory(0, requested), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > limits.MaxSourceBytes)
                {
                    throw SourceTooLarge(limits);
                }

                await destination
                    .WriteAsync(rented.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (total == 0)
            {
                throw new EmailAnalysisLimitException(
                    "Le fichier de message est vide.");
            }

            return destination.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    private static EmailAnalysisLimitException SourceTooLarge(EmailAnalysisLimits limits)
        => new(
            $"Le fichier dépasse la limite de {limits.MaxSourceBytes / (1024 * 1024)} Mo.");
}
