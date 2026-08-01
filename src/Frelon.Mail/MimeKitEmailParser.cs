using System.Security.Cryptography;
using System.Text;
using MimeKit;

namespace Frelon.Mail;

/// <summary>Parseur d'email basé sur MimeKit.</summary>
public sealed class MimeKitEmailParser : IEmailParser
{
    private readonly EmailAnalysisLimits _limits;

    /// <summary>Crée le parseur avec les quotas de référence.</summary>
    public MimeKitEmailParser()
        : this(EmailAnalysisLimits.Default)
    {
    }

    /// <summary>Crée le parseur avec des quotas explicites.</summary>
    public MimeKitEmailParser(EmailAnalysisLimits limits)
    {
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _limits.Validate();
    }

    /// <inheritdoc/>
    public async Task<ParsedEmail> ParseAsync(Stream emlStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emlStream);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceBytes = await EmailContentBuffer
            .ReadAsync(emlStream, _limits, cancellationToken)
            .ConfigureAwait(false);
        return await ParseBufferedAsync(sourceBytes, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<ParsedEmail> ParseBufferedAsync(
        byte[] sourceBytes,
        CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream(sourceBytes, writable: false);

        var sourceSha256 = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();
        var rawContent = Encoding.Latin1.GetString(sourceBytes);
        var parserOptions = new ParserOptions
        {
            MaxAddressGroupDepth = _limits.MaxAddressGroupDepth,
            MaxMimeDepth = _limits.MaxMimeDepth
        };

        var mimeMessage = await MimeMessage
            .LoadAsync(parserOptions, memoryStream, cancellationToken)
            .ConfigureAwait(false);

        var attachments = new List<ParsedEmailAttachment>();
        var totalAttachmentBytes = 0;

        foreach (var attachment in mimeMessage.Attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (attachments.Count >= _limits.MaxAttachmentCount)
            {
                throw new EmailAnalysisLimitException(
                    "Le message contient trop de pièces jointes pour être analysé en sécurité.");
            }

            var remainingAttachmentBytes =
                _limits.MaxTotalAttachmentBytes - totalAttachmentBytes;
            var attachmentQuota = Math.Min(
                _limits.MaxAttachmentBytes,
                remainingAttachmentBytes);
            if (attachmentQuota <= 0)
            {
                throw new EmailAnalysisLimitException(
                    "Le volume cumulé des pièces jointes dépasse la limite de sécurité.");
            }

            switch (attachment)
            {
                case MimePart mimePart:
                {
                    using var attachmentStream = new BoundedMemoryStream(attachmentQuota);
                    if (mimePart.Content is { } content)
                    {
                        await content
                            .DecodeToAsync(attachmentStream, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    attachments.Add(new ParsedEmailAttachment
                    {
                        FileName = mimePart.FileName,
                        ContentType = mimePart.ContentType?.MimeType,
                        Content = attachmentStream.ToArray()
                    });
                    totalAttachmentBytes += checked((int)attachmentStream.Length);

                    break;
                }

                case MessagePart messagePart:
                {
                    using var attachmentStream = new BoundedMemoryStream(attachmentQuota);
                    if (messagePart.Message is { } attachedMessage)
                    {
                        await attachedMessage
                            .WriteToAsync(attachmentStream, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    attachments.Add(new ParsedEmailAttachment
                    {
                        FileName = GetMessagePartFileName(messagePart),
                        ContentType = messagePart.ContentType?.MimeType,
                        Content = attachmentStream.ToArray()
                    });
                    totalAttachmentBytes += checked((int)attachmentStream.Length);

                    break;
                }
            }
        }

        var parsedEmail = new ParsedEmail
        {
            RawContent = rawContent,
            SourceSha256 = sourceSha256,
            Headers = mimeMessage.Headers.Select(header => new ParsedEmailHeader
            {
                Name = header.Field,
                Value = header.Value
            }).ToList(),
            BodyText = NullWhenEmpty(mimeMessage.TextBody),
            BodyHtml = NullWhenEmpty(mimeMessage.HtmlBody),
            Attachments = attachments
        };
        ParsedEmailLimitGuard.Validate(parsedEmail, _limits);
        return parsedEmail;
    }

    private static string? GetMessagePartFileName(MessagePart messagePart)
    {
        if (!string.IsNullOrWhiteSpace(messagePart.ContentDisposition?.FileName))
        {
            return messagePart.ContentDisposition.FileName;
        }

        if (!string.IsNullOrWhiteSpace(messagePart.ContentType?.Name))
        {
            return messagePart.ContentType.Name;
        }

        return "attached-message.eml";
    }

    private static string? NullWhenEmpty(string? value)
        => string.IsNullOrEmpty(value) ? null : value;
}
