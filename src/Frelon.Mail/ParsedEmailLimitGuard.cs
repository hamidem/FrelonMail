namespace Frelon.Mail;

/// <summary>Vérifie les quotas qui ne sont connus qu'après décodage du message.</summary>
internal static class ParsedEmailLimitGuard
{
    public static void Validate(ParsedEmail email, EmailAnalysisLimits limits)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();

        if (email.Headers.Count > limits.MaxHeaderCount)
        {
            throw new EmailAnalysisLimitException(
                "Le message contient trop d'en-têtes pour être analysé en sécurité.");
        }

        long headerCharacters = 0;
        foreach (var header in email.Headers)
        {
            headerCharacters += header.Name.Length + header.Value.Length;
            if (headerCharacters > limits.MaxHeaderCharacters)
            {
                throw new EmailAnalysisLimitException(
                    "Les en-têtes du message dépassent la limite de sécurité.");
            }
        }

        if ((email.BodyText?.Length ?? 0) > limits.MaxBodyCharacters
            || (email.BodyHtml?.Length ?? 0) > limits.MaxBodyCharacters)
        {
            throw new EmailAnalysisLimitException(
                "Le corps du message dépasse la limite de sécurité.");
        }

        if (email.Attachments.Count > limits.MaxAttachmentCount)
        {
            throw new EmailAnalysisLimitException(
                "Le message contient trop de pièces jointes pour être analysé en sécurité.");
        }

        long totalAttachmentBytes = 0;
        foreach (var attachment in email.Attachments)
        {
            if (attachment.Content.Length > limits.MaxAttachmentBytes)
            {
                throw new EmailAnalysisLimitException(
                    "Une pièce jointe dépasse la limite de sécurité.");
            }

            totalAttachmentBytes += attachment.Content.Length;
            if (totalAttachmentBytes > limits.MaxTotalAttachmentBytes)
            {
                throw new EmailAnalysisLimitException(
                    "Le volume cumulé des pièces jointes dépasse la limite de sécurité.");
            }
        }
    }
}
