namespace Frelon.Mail;

/// <summary>Interrompt un décodage dès que sa sortie dépasse le quota accordé.</summary>
internal sealed class BoundedMemoryStream : MemoryStream
{
    private readonly int _maxBytes;

    public BoundedMemoryStream(int maxBytes)
    {
        if (maxBytes <= 0)
        {
            throw new EmailAnalysisLimitException(
                "Le volume cumulé des pièces jointes dépasse la limite de sécurité.");
        }

        _maxBytes = maxBytes;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureCapacityFor(count);
        base.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureCapacityFor(buffer.Length);
        base.Write(buffer);
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        EnsureCapacityFor(count);
        return base.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        EnsureCapacityFor(buffer.Length);
        return base.WriteAsync(buffer, cancellationToken);
    }

    public override void WriteByte(byte value)
    {
        EnsureCapacityFor(1);
        base.WriteByte(value);
    }

    public override void SetLength(long value)
    {
        if (value < 0 || value > _maxBytes)
        {
            throw new EmailAnalysisLimitException(
                "Le contenu décodé d'une pièce jointe dépasse la limite de sécurité.");
        }

        base.SetLength(value);
    }

    private void EnsureCapacityFor(int count)
    {
        if (count < 0 || Position > _maxBytes - (long)count)
        {
            throw new EmailAnalysisLimitException(
                "Le contenu décodé d'une pièce jointe dépasse la limite de sécurité.");
        }
    }
}
