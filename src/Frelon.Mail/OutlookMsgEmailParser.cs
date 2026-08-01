using System.Security.Cryptography;
using System.Text;
using MimeKit;
using MsgReader.Outlook;

namespace Frelon.Mail;

/// <summary>Parseur local des courriels Outlook au format MSG.</summary>
public sealed class OutlookMsgEmailParser : IEmailParser
{
    private readonly EmailAnalysisLimits _limits;

    /// <summary>Crée le parseur avec les quotas de référence.</summary>
    public OutlookMsgEmailParser()
        : this(EmailAnalysisLimits.Default)
    {
    }

    /// <summary>Crée le parseur avec des quotas explicites.</summary>
    public OutlookMsgEmailParser(EmailAnalysisLimits limits)
    {
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _limits.Validate();
    }

    /// <inheritdoc/>
    public async Task<ParsedEmail> ParseAsync(
        Stream msgStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(msgStream);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceBytes = await EmailContentBuffer
            .ReadAsync(msgStream, _limits, cancellationToken)
            .ConfigureAwait(false);
        return ParseBuffered(sourceBytes, cancellationToken);
    }

    internal Task<ParsedEmail> ParseBufferedAsync(
        byte[] sourceBytes,
        CancellationToken cancellationToken)
        => Task.FromResult(ParseBuffered(sourceBytes, cancellationToken));

    private ParsedEmail ParseBuffered(
        byte[] sourceBytes,
        CancellationToken cancellationToken)
    {
        var sourceSha256 = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();
        var rawContent = Encoding.Latin1.GetString(sourceBytes);

        using var memoryStream = new MemoryStream(sourceBytes, writable: false);
        using var message = new Storage.Message(memoryStream, FileAccess.Read, leaveStreamOpen: true);

        if (!message.Type.ToString().StartsWith("Email", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Le fichier Outlook contient un élément de type '{message.Type}', pas un courrier électronique.");
        }

        var headers = ParseHeaders(message.TransportMessageHeaders);
        AddHeaderWhenMissing(headers, "From", FormatMailbox(message.Sender?.DisplayName, message.Sender?.Email));
        AddHeaderWhenMissing(headers, "Subject", message.Subject);
        AddHeaderWhenMissing(headers, "Message-ID", message.Id);

        var attachments = new List<ParsedEmailAttachment>();
        var totalAttachmentBytes = 0;
        foreach (var item in message.Attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (attachments.Count >= _limits.MaxAttachmentCount)
            {
                throw new EmailAnalysisLimitException(
                    "Le message contient trop de pièces jointes pour être analysé en sécurité.");
            }

            switch (item)
            {
                case Storage.Attachment attachment:
                    var content = attachment.Data ?? [];
                    EnsureAttachmentFits(content.Length, totalAttachmentBytes);
                    attachments.Add(new ParsedEmailAttachment
                    {
                        FileName = attachment.FileName,
                        ContentType = attachment.MimeType,
                        Content = content
                    });
                    totalAttachmentBytes += content.Length;
                    break;

                case Storage.Message attachedMessage:
                    var remainingAttachmentBytes =
                        _limits.MaxTotalAttachmentBytes - totalAttachmentBytes;
                    var attachmentQuota = Math.Min(
                        _limits.MaxAttachmentBytes,
                        remainingAttachmentBytes);
                    using (var attachmentStream = new BoundedMemoryStream(attachmentQuota))
                    {
                        attachedMessage.Save(attachmentStream);
                        attachments.Add(new ParsedEmailAttachment
                        {
                            FileName = EnsureMsgExtension(attachedMessage.FileName),
                            ContentType = "application/vnd.ms-outlook",
                            Content = attachmentStream.ToArray()
                        });
                        totalAttachmentBytes += checked((int)attachmentStream.Length);
                    }
                    break;
            }
        }

        var parsedEmail = new ParsedEmail
        {
            RawContent = rawContent,
            SourceSha256 = sourceSha256,
            Headers = headers,
            BodyText = NullWhenEmpty(message.BodyText),
            BodyHtml = NullWhenEmpty(message.BodyHtml),
            Attachments = attachments
        };
        ParsedEmailLimitGuard.Validate(parsedEmail, _limits);
        return parsedEmail;
    }

    private void EnsureAttachmentFits(int attachmentBytes, int totalAttachmentBytes)
    {
        if (attachmentBytes > _limits.MaxAttachmentBytes)
        {
            throw new EmailAnalysisLimitException(
                "Une pièce jointe dépasse la limite de sécurité.");
        }

        if (attachmentBytes > _limits.MaxTotalAttachmentBytes - totalAttachmentBytes)
        {
            throw new EmailAnalysisLimitException(
                "Le volume cumulé des pièces jointes dépasse la limite de sécurité.");
        }
    }

    private static List<ParsedEmailHeader> ParseHeaders(string? rawHeaders)
    {
        if (string.IsNullOrWhiteSpace(rawHeaders))
        {
            return [];
        }

        var normalizedHeaders = rawHeaders.TrimEnd('\r', '\n') + "\r\n\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(normalizedHeaders));
        var headerList = HeaderList.Load(stream);

        return headerList.Select(header => new ParsedEmailHeader
        {
            Name = header.Field,
            Value = header.Value
        }).ToList();
    }

    private static void AddHeaderWhenMissing(
        ICollection<ParsedEmailHeader> headers,
        string name,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || headers.Any(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        headers.Add(new ParsedEmailHeader
        {
            Name = name,
            Value = value
        });
    }

    private static string? FormatMailbox(string? displayName, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return NullWhenEmpty(displayName);
        }

        return string.IsNullOrWhiteSpace(displayName)
            ? email
            : $"{displayName} <{email}>";
    }

    private static string EnsureMsgExtension(string? fileName)
    {
        var normalized = string.IsNullOrWhiteSpace(fileName) ? "attached-message" : fileName;
        return string.Equals(Path.GetExtension(normalized), ".msg", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : normalized + ".msg";
    }

    private static string? NullWhenEmpty(string? value)
        => string.IsNullOrEmpty(value) ? null : value;
}
