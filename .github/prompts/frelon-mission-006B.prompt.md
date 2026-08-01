Mission Copilot #006B — Extraire les pièces jointes MIME et calculer leur SHA-256
Contexte

Les missions précédentes ont mis en place :

le modèle métier minimal dans Frelon.Core ;
IEmailParser et ParsedEmail ;
BasicEmailParser ;
MimeKitEmailParser ;
l’analyse des headers ;
la construction d’un FraudIncident ;
l’extraction des URLs ;
la génération d’IOC Url et Domain ;
les sorties incident.json, report.md et iocs.json.

MimeKitEmailParser permet maintenant de lire correctement la structure MIME d’un message.

Cette mission démarre le traitement défensif des pièces jointes.

Cette mission doit respecter :

.github/copilot-instructions.md
Objectif

Permettre au pipeline Frelon de :

représenter les pièces jointes décodées dans ParsedEmail ;
extraire ces pièces jointes avec MimeKitEmailParser ;
calculer localement leur SHA-256 ;
produire des AttachmentIndicator ;
renseigner FraudIncident.Attachments.

Cette mission ne doit pas encore créer d’IOC Hash.

Elle ne doit pas classifier ou scorer les pièces jointes.

Périmètre autorisé

Copilot peut modifier uniquement :

src/Frelon.Mail/
tests/Frelon.Mail.Tests/

Copilot ne doit pas modifier :

src/Frelon.Core/
src/Frelon.Reports/
src/Frelon.Exporters/
src/Frelon.Cli/
tests/Frelon.Core.Tests/
tests/Frelon.Reports.Tests/
Dépendances

Utiliser uniquement les dépendances déjà présentes.

MimeKit est déjà autorisé dans Frelon.Mail.

Ne pas ajouter d’autre package NuGet.

Utiliser les API cryptographiques du framework .NET pour SHA-256.

Contraintes de sécurité
Ne jamais exécuter une pièce jointe.
Ne jamais ouvrir une pièce jointe avec une application externe.
Ne jamais écrire une pièce jointe sur disque.
Ne jamais charger une pièce jointe dans un navigateur.
Ne jamais faire d’appel réseau.
Ne jamais soumettre le hash à un service distant.
Ne pas analyser dynamiquement le contenu.
Ne pas décompresser les archives.
Ne pas interpréter le HTML ou JavaScript d’une pièce jointe.
Toute opération doit rester locale et en mémoire.

Décoder un transfert MIME Base64 ou quoted-printable afin d’obtenir les octets réels de la pièce jointe est autorisé.

Décoder n’est pas exécuter.

Limite connue de cette mission

Cette mission traite uniquement les entités exposées comme pièces jointes par le mécanisme Attachments de MimeKit.

Les contenus MIME inline, les ressources multipart/related et les parties suspectes non déclarées comme pièces jointes sont hors périmètre.

Ne pas tenter de corriger cette limite dans cette mission.

Une future mission de durcissement MIME pourra analyser l’arbre MIME plus largement.

Travail demandé
1. Créer ParsedEmailAttachment

Créer :

src/Frelon.Mail/ParsedEmailAttachment.cs

Créer un type technique représentant une pièce jointe déjà décodée.

Forme suggérée :

namespace Frelon.Mail;

public sealed record ParsedEmailAttachment
{
    public string? FileName { get; init; }

    public string? ContentType { get; init; }

    public required ReadOnlyMemory<byte> Content { get; init; }
}

Adapter légèrement si nécessaire.

Ce type appartient uniquement à la représentation intermédiaire de Frelon.Mail.

Ne pas l’ajouter à Frelon.Core.

2. Étendre ParsedEmail

Ajouter une propriété :

public IReadOnlyList<ParsedEmailAttachment> Attachments { get; init; } = [];

Ne pas modifier les autres propriétés de ParsedEmail.

La collection doit être vide par défaut.

Grâce à cette valeur par défaut, BasicEmailParser peut continuer à produire un ParsedEmail sans pièces jointes.

Ne pas transformer BasicEmailParser en parser MIME.

3. Étendre MimeKitEmailParser

Après chargement du MimeMessage, extraire les pièces jointes exposées par MimeKit.

Construire une collection de ParsedEmailAttachment.

Conserver leur ordre d’énumération.

4. Traiter les MimePart

Lorsqu’une pièce jointe est un MimePart :

obtenir le nom de fichier disponible ;
obtenir le MIME type ;
décoder le contenu en mémoire ;
respecter le CancellationToken ;
conserver les octets décodés dans ParsedEmailAttachment.Content.

Utiliser un MemoryStream.

Utiliser l’API asynchrone de décodage de MimeKit.

Ne pas écrire sur disque.

Ne pas modifier volontairement le nom de fichier déclaré dans le message.

Le nom est une donnée observée et doit rester fidèle à la source.

5. Traiter les MessagePart

Lorsqu’une pièce jointe est un MessagePart représentant un message attaché :

sérialiser le message attaché vers un MemoryStream ;
respecter le CancellationToken ;
utiliser les octets sérialisés comme Content.

Pour le nom de fichier, utiliser les informations MIME disponibles.

Si aucun nom n’est disponible, utiliser :

attached-message.eml

Renseigner le type MIME disponible sur l’entité.

Ne pas analyser récursivement le message attaché dans cette mission.

6. Types d’entités non pris en charge

Si une entité retournée comme pièce jointe n’est ni un MimePart ni un MessagePart :

ne pas lever d’exception uniquement pour cette raison ;
ignorer cette entité.

Ne pas inventer un traitement générique complexe.

7. Créer IEmailAttachmentAnalyzer

Créer :

src/Frelon.Mail/IEmailAttachmentAnalyzer.cs

Signature suggérée :

using Frelon.Core;

namespace Frelon.Mail;

public interface IEmailAttachmentAnalyzer
{
    IReadOnlyList<AttachmentIndicator> AnalyzeAttachments(
        ParsedEmail email);
}

Cette interface travaille sur un ParsedEmail déjà construit.

Elle ne reçoit pas le flux .eml.

Elle ne connaît pas MimeKit.

8. Créer BasicEmailAttachmentAnalyzer

Créer :

src/Frelon.Mail/BasicEmailAttachmentAnalyzer.cs

La classe doit :

vérifier que ParsedEmail n’est pas null ;
parcourir ParsedEmail.Attachments ;
produire un AttachmentIndicator par pièce jointe ;
calculer le SHA-256 sur les octets décodés ;
ne pas modifier ParsedEmail.

Pour chaque AttachmentIndicator, renseigner :

FileName
ContentType
SizeBytes
Sha256
IsSuspicious
Reasons
9. FileName

Si ParsedEmailAttachment.FileName est null, vide ou blanc, utiliser :

unnamed-attachment

Ne pas tenter de créer un chemin disque.

Ne pas utiliser le nom de fichier pour ouvrir quoi que ce soit.

10. ContentType

Reporter la valeur disponible dans ParsedEmailAttachment.ContentType.

Ne pas tenter de détecter le vrai type de fichier dans cette mission.

Ne pas comparer encore extension et MIME type.

11. SizeBytes

Renseigner :

Content.Length

Le résultat doit représenter le nombre d’octets décodés conservés en mémoire.

12. SHA-256

Calculer le SHA-256 sur les octets décodés de la pièce jointe.

Utiliser les API du framework .NET.

Produire une chaîne hexadécimale en minuscules.

Exemple de forme :

566a194e17b9cced887226f71d117300e8e51314531d6cae8cd0c9a82ac588f8

Pour les octets UTF-8 de :

contenu factice

le test peut utiliser cette valeur fixe comme résultat attendu.

Ne pas calculer le résultat attendu du test en appelant la même fonction SHA-256 que le code testé.

13. Suspicion

Pour cette mission :

IsSuspicious = false

et :

Reasons = []

La présence d’une pièce jointe ne suffit pas à conclure qu’elle est frauduleuse.

Ne pas introduire de scoring.

Ne pas ajouter de règles sur les extensions.

Ne pas comparer encore extension et MIME type.

14. Intégrer IEmailAttachmentAnalyzer dans BasicEmailIncidentAnalyzer

Ajouter une dépendance :

IEmailAttachmentAnalyzer

Le constructeur doit désormais recevoir :

IEmailParser parser,
IEmailHeaderAnalyzer headerAnalyzer,
IEmailUrlExtractor urlExtractor,
IUrlIocExtractor urlIocExtractor,
IEmailAttachmentAnalyzer attachmentAnalyzer

Vérifier chaque dépendance avec :

ArgumentNullException.ThrowIfNull(...)

Dans AnalyzeAsync :

parser le message ;
extraire l’identité ;
extraire l’authentification ;
extraire la chaîne Received ;
extraire les URLs ;
analyser les pièces jointes du ParsedEmail ;
capturer ou réutiliser l’instant logique approprié ;
générer les IOC d’URL ;
construire le FraudIncident.

Renseigner :

Urls
Attachments
Iocs

Ne pas générer d’IOC Hash depuis les pièces jointes dans cette mission.

15. Ne pas coupler l’orchestrateur à MimeKit

BasicEmailIncidentAnalyzer doit continuer à dépendre uniquement de :

IEmailParser
IEmailHeaderAnalyzer
IEmailUrlExtractor
IUrlIocExtractor
IEmailAttachmentAnalyzer

Il ne doit pas référencer :

MimeMessage
MimePart
MessagePart
MimeKit

La connaissance de MimeKit reste dans MimeKitEmailParser.

Tests de MimeKitEmailParser

Compléter :

tests/Frelon.Mail.Tests/MimeKitEmailParserTests.cs

Ajouter des tests vérifiant que :

un message sans pièce jointe produit une collection Attachments vide ;
une pièce jointe MimePart est extraite ;
son nom de fichier est conservé ;
son type MIME est conservé ;
les octets décodés correspondent exactement au contenu original avant encodage MIME ;
une pièce jointe encodée en Base64 est correctement décodée ;
plusieurs pièces jointes conservent leur ordre ;
un MessagePart attaché est représenté comme une pièce jointe ;
aucun fichier n’est créé sur disque par les tests.

Tous les tests doivent fonctionner en mémoire.

Tests de BasicEmailAttachmentAnalyzer

Créer :

tests/Frelon.Mail.Tests/BasicEmailAttachmentAnalyzerTests.cs

Ajouter des tests vérifiant que :

un ParsedEmail sans pièce jointe produit une liste vide ;
une pièce jointe produit un AttachmentIndicator ;
le nom du fichier est reporté ;
le type MIME est reporté ;
SizeBytes correspond au nombre exact d’octets ;
le SHA-256 est calculé sur les octets décodés ;
le SHA-256 est une chaîne hexadécimale en minuscules ;
le contenu contenu factice produit exactement :
566a194e17b9cced887226f71d117300e8e51314531d6cae8cd0c9a82ac588f8
un nom absent utilise unnamed-attachment ;
IsSuspicious vaut false ;
Reasons est vide ;
AnalyzeAttachments lève ArgumentNullException si l’email est null.
Tests de BasicEmailIncidentAnalyzer

Adapter :

tests/Frelon.Mail.Tests/BasicEmailIncidentAnalyzerTests.cs

Mettre à jour les constructions de BasicEmailIncidentAnalyzer avec :

new BasicEmailAttachmentAnalyzer()

Ajouter des tests vérifiant que :

un message MIME avec pièce jointe renseigne FraudIncident.Attachments lorsque l’analyse utilise MimeKitEmailParser ;
le AttachmentIndicator contient le nom attendu ;
le AttachmentIndicator contient le SHA-256 attendu ;
un message sans pièce jointe laisse FraudIncident.Attachments vide ;
aucun IOC Hash n’est encore produit depuis la pièce jointe ;
le constructeur lève ArgumentNullException si attachmentAnalyzer est null.

Adapter les anciens tests dont les hypothèses deviennent obsolètes.

Ne pas supprimer un test uniquement pour faire passer la suite.

Tests existants

Tous les tests encore pertinents doivent continuer de passer.

BasicEmailParserTests doivent rester valides.

BasicEmailUrlExtractorTests doivent rester valides.

BasicUrlIocExtractorTests doivent rester valides.

Les tests de rapports ne doivent pas être modifiés.

Critères d’acceptation

La mission est terminée si :

Frelon.Mail compile ;
Frelon.Mail.Tests compile ;
tous les tests pertinents passent ;
aucun package NuGet supplémentaire n’est ajouté ;
aucun fichier hors périmètre n’est modifié ;
aucun modèle Frelon.Core n’est modifié ;
ParsedEmail expose ses pièces jointes ;
MimeKitEmailParser extrait les pièces jointes MIME déclarées comme telles ;
les pièces jointes restent uniquement en mémoire ;
aucune pièce jointe n’est exécutée ;
aucune pièce jointe n’est écrite sur disque ;
SHA-256 est calculé sur le contenu décodé ;
FraudIncident.Attachments est renseigné ;
aucun IOC Hash n’est encore créé ;
aucun scoring n’est introduit ;
aucun appel réseau n’est ajouté ;
aucune fonctionnalité hors mission n’est introduite.
Important

Ne pas créer d’IOC Hash.

Ne pas qualifier les extensions de fichier.

Ne pas comparer extension et type MIME.

Ne pas détecter de double extension.

Ne pas décompresser les archives.

Ne pas analyser le contenu des pièces jointes.

Ne pas traiter les ressources MIME inline.

Ne pas créer de règle antispam.

Ne pas créer de signalement.

Ne pas créer de CLI.

Ne pas introduire de base de données.

Cette mission sert uniquement à transformer les pièces jointes MIME déclarées comme telles en AttachmentIndicator locaux contenant des métadonnées et un SHA-256.

La séparation choisie ici suit bien le modèle MIME de MimeKit : le parser identifie et décode les entités de pièce jointe, tandis que le calcul du hash reste dans notre analyseur Frelon. MimeKit documente le MIME comme une arborescence plutôt qu’un simple « body + liste de fichiers », ce qui justifie aussi de réserver l’analyse plus large des parties inline ou ambiguës à une future étape de durcissement