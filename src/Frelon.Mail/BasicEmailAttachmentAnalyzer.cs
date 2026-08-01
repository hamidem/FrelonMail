using System.Security.Cryptography;
using Frelon.Core;

namespace Frelon.Mail;

/// <summary>Analyse locale et déterministe des pièces jointes d'un email parsé.</summary>
public sealed class BasicEmailAttachmentAnalyzer : IEmailAttachmentAnalyzer
{
    /// <summary>Raison associée à une extension exécutable ou de script.</summary>
    public const string ExecutableExtensionReason = "L'extension peut exécuter du code ou un script";

    /// <summary>Raison associée à un format pouvant embarquer du contenu actif.</summary>
    public const string ActiveContentReason = "Le format peut contenir du contenu actif";

    /// <summary>Raison associée à une double extension trompeuse.</summary>
    public const string MisleadingDoubleExtensionReason = "Le nom utilise une double extension potentiellement trompeuse";

    /// <summary>Raison associée à un type MIME exécutable explicite.</summary>
    public const string ExecutableContentTypeReason = "Le type MIME déclare un contenu exécutable";

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".scr", ".com", ".bat", ".cmd", ".ps1", ".js", ".jse",
        ".vbs", ".vbe", ".wsf", ".wsh", ".hta", ".msi", ".lnk", ".jar"
    };

    private static readonly HashSet<string> ActiveContentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm", ".svg", ".docm", ".xlsm", ".pptm"
    };

    private static readonly HashSet<string> DecoyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".jpg", ".jpeg", ".png", ".gif", ".txt"
    };

    private static readonly HashSet<string> ExecutableContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/x-msdownload",
        "application/x-msdos-program",
        "application/x-executable",
        "application/vnd.microsoft.portable-executable"
    };

    /// <inheritdoc/>
    public IReadOnlyList<AttachmentIndicator> AnalyzeAttachments(ParsedEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (email.Attachments.Count == 0)
        {
            return [];
        }

        var results = new List<AttachmentIndicator>(email.Attachments.Count);

        foreach (var attachment in email.Attachments)
        {
            var sha256 = Convert
                .ToHexString(SHA256.HashData(attachment.Content.Span))
                .ToLowerInvariant();
            var fileName = string.IsNullOrWhiteSpace(attachment.FileName)
                ? "unnamed-attachment"
                : attachment.FileName;
            var reasons = EvaluateReasons(fileName, attachment.ContentType);

            results.Add(new AttachmentIndicator
            {
                FileName = fileName,
                ContentType = attachment.ContentType,
                SizeBytes = attachment.Content.Length,
                Sha256 = sha256,
                IsSuspicious = reasons.Count != 0,
                Reasons = reasons
            });
        }

        return results;
    }

    private static IReadOnlyList<string> EvaluateReasons(string fileName, string? contentType)
    {
        var reasons = new List<string>(3);
        var extension = GetExtension(fileName);

        if (ExecutableExtensions.Contains(extension))
        {
            reasons.Add(ExecutableExtensionReason);
        }

        if (ActiveContentExtensions.Contains(extension))
        {
            reasons.Add(ActiveContentReason);
        }

        var stem = fileName[..^extension.Length];
        var stemExtension = GetExtension(stem);
        if (ExecutableExtensions.Contains(extension) && DecoyExtensions.Contains(stemExtension))
        {
            reasons.Add(MisleadingDoubleExtensionReason);
        }

        var normalizedContentType = contentType?.Split(';', 2)[0].Trim();
        if (!string.IsNullOrEmpty(normalizedContentType) && ExecutableContentTypes.Contains(normalizedContentType))
        {
            reasons.Add(ExecutableContentTypeReason);
        }

        return reasons;
    }

    private static string GetExtension(string fileName)
    {
        var lastSeparator = Math.Max(fileName.LastIndexOf('/'), fileName.LastIndexOf('\\'));
        var lastDot = fileName.LastIndexOf('.');
        return lastDot > lastSeparator ? fileName[lastDot..] : string.Empty;
    }
}
