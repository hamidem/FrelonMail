namespace Frelon.Web;

/// <summary>Centralise le contrat des fichiers de message acceptés par l'interface locale.</summary>
public static class EmailEvidenceFilePolicy
{
    /// <summary>Valide et normalise le nom encodé transmis par le navigateur.</summary>
    public static EmailEvidenceFileValidation ValidateEncodedFileName(string? encodedFileName)
    {
        if (string.IsNullOrWhiteSpace(encodedFileName))
        {
            return EmailEvidenceFileValidation.Rejected(
                EmailEvidenceFileRejection.Missing,
                "Sélectionnez le fichier du message suspect à analyser.");
        }

        string decodedFileName;
        try
        {
            decodedFileName = Uri.UnescapeDataString(encodedFileName);
        }
        catch (UriFormatException)
        {
            return EmailEvidenceFileValidation.Rejected(
                EmailEvidenceFileRejection.InvalidName,
                "Le nom du fichier transmis est invalide.");
        }

        // Le nom vient d'un navigateur et doit avoir le même contrat sur Windows et Linux.
        // Path.GetFileName ne considère pas le séparateur de l'autre système comme spécial.
        var containsDirectorySeparator = decodedFileName.Contains('/') || decodedFileName.Contains('\\');
        var normalizedFileName = Path.GetFileName(decodedFileName);
        if (string.IsNullOrWhiteSpace(normalizedFileName)
            || containsDirectorySeparator
            || !string.Equals(normalizedFileName, decodedFileName, StringComparison.Ordinal)
            || normalizedFileName.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(normalizedFileName)))
        {
            return EmailEvidenceFileValidation.Rejected(
                EmailEvidenceFileRejection.InvalidName,
                "Le nom du fichier transmis est invalide.");
        }

        var extension = Path.GetExtension(normalizedFileName);
        if (string.Equals(extension, ".eml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".msg", StringComparison.OrdinalIgnoreCase))
        {
            return EmailEvidenceFileValidation.Accepted(normalizedFileName);
        }

        return EmailEvidenceFileValidation.Rejected(
            EmailEvidenceFileRejection.UnsupportedFormat,
            "Ce format n'est pas pris en charge. Frelon accepte les fichiers de message .eml et .msg.");
    }
}

/// <summary>Résultat explicite de la validation d'un fichier de message.</summary>
public sealed record EmailEvidenceFileValidation(
    bool IsAccepted,
    string? FileName,
    EmailEvidenceFileRejection Rejection,
    string? Message)
{
    internal static EmailEvidenceFileValidation Accepted(string fileName)
        => new(true, fileName, EmailEvidenceFileRejection.None, null);

    internal static EmailEvidenceFileValidation Rejected(
        EmailEvidenceFileRejection rejection,
        string message)
        => new(false, null, rejection, message);
}

/// <summary>Motif stable d'un refus avant analyse.</summary>
public enum EmailEvidenceFileRejection
{
    None,
    Missing,
    InvalidName,
    UnsupportedFormat
}
