using Frelon.Core;

namespace Frelon.Mail;

/// <summary>Analyse les pièces jointes déjà extraites d'un email parsé.</summary>
public interface IEmailAttachmentAnalyzer
{
    /// <summary>Produit les indicateurs de pièces jointes à partir d'un email parsé.</summary>
    /// <param name="email">Email parsé contenant les pièces jointes en mémoire.</param>
    /// <returns>Liste d'indicateurs de pièces jointes.</returns>
    IReadOnlyList<AttachmentIndicator> AnalyzeAttachments(ParsedEmail email);
}