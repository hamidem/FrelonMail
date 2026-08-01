# Mission Copilot #006A — Introduire un parser email basé sur MimeKit

## Contexte

Les missions précédentes ont mis en place :

* le modèle métier minimal dans `Frelon.Core` ;
* `IEmailParser` et `ParsedEmail` dans `Frelon.Mail` ;
* un `BasicEmailParser` volontairement minimal ;
* l'analyse des headers email ;
* la construction d'un `FraudIncident` ;
* l'extraction des URLs ;
* la génération d'IOC `Url` et `Domain` ;
* la génération de `incident.json`, `report.md` et `iocs.json`.

`BasicEmailParser` a rempli son rôle de parser minimal pour les premières étapes du projet.

Frelon doit maintenant préparer le futur traitement MIME et des pièces jointes sans développer un parser MIME propriétaire.

Cette mission doit respecter :

```text
.github/copilot-instructions.md
```

## Objectif

Ajouter une nouvelle implémentation de `IEmailParser` utilisant MimeKit.

La nouvelle classe doit transformer un `MimeMessage` en `ParsedEmail` sans modifier le contrat public `IEmailParser`.

Cette mission ne doit pas encore extraire ou hasher les pièces jointes.

`BasicEmailParser` doit être conservé.

## Périmètre autorisé

Copilot peut modifier uniquement :

```text
src/Frelon.Mail/
tests/Frelon.Mail.Tests/
```

Copilot ne doit pas modifier :

```text
src/Frelon.Core/
src/Frelon.Reports/
src/Frelon.Exporters/
src/Frelon.Cli/
tests/Frelon.Core.Tests/
tests/Frelon.Reports.Tests/
```

## Dépendance explicitement autorisée

Exception contrôlée aux règles générales du projet :

Ajouter le package NuGet officiel :

```text
MimeKit
```

uniquement au projet :

```text
src/Frelon.Mail/
```

Ne pas ajouter `MailKit`.

Ne pas ajouter d'autre package NuGet.

Ne pas modifier les versions d'autres packages.

## Contraintes

* Ne pas modifier `IEmailParser`.
* Ne pas modifier les modèles de `Frelon.Core`.
* Ne pas supprimer `BasicEmailParser`.
* Ne pas modifier `BasicEmailIncidentAnalyzer`.
* Ne pas modifier `BasicEmailHeaderAnalyzer`.
* Ne pas modifier `BasicEmailUrlExtractor`.
* Ne pas modifier `BasicUrlIocExtractor`.
* Ne pas faire d'appel réseau.
* Ne jamais ouvrir une URL.
* Ne jamais faire de requête HTTP.
* Ne jamais résoudre un domaine en DNS.
* Ne pas envoyer d'email.
* Ne pas utiliser IMAP.
* Ne pas exécuter de pièce jointe.
* Ne pas écrire de pièce jointe sur disque.
* Ne pas calculer encore de hash de pièce jointe.
* Ne pas créer de base de données.
* Ne pas créer de CLI.
* Garder l'adaptation MimeKit isolée dans `Frelon.Mail`.

## Travail demandé

### 1. Créer `MimeKitEmailParser`

Créer :

```text
src/Frelon.Mail/MimeKitEmailParser.cs
```

La classe doit implémenter :

```csharp
IEmailParser
```

Signature publique attendue :

```csharp
public sealed class MimeKitEmailParser : IEmailParser
{
    public Task<ParsedEmail> ParseAsync(
        Stream emlStream,
        CancellationToken cancellationToken = default);
}
```

Adapter uniquement les détails nécessaires à l'API réelle de MimeKit.

### 2. Validation du flux

`ParseAsync` doit :

* lever `ArgumentNullException` si `emlStream` est null ;
* respecter `CancellationToken` ;
* ne pas fermer un flux appartenant à l'appelant ;
* fonctionner avec un flux seekable ou non seekable si MimeKit le permet naturellement.

Ne pas créer de logique complexe de buffering prématurée.

### 3. Charger le message MIME

Utiliser l'API MimeKit appropriée pour charger un `MimeMessage` depuis le `Stream`.

Ne pas utiliser une API réseau.

Ne pas utiliser MailKit.

### 4. Construire `ParsedEmail`

Transformer le message MimeKit en `ParsedEmail`.

Renseigner :

```text
RawContent
Headers
BodyText
BodyHtml
```

### 5. RawContent

`RawContent` doit conserver le contenu brut du message sous forme de chaîne dans la mesure permise par le contrat actuel de `ParsedEmail`.

Cette mission ne doit pas modifier `ParsedEmail`.

La préservation exacte byte-for-byte n'est pas exigée par cette mission.

Ne pas normaliser volontairement le message.

Ne pas reconstruire `RawContent` depuis les propriétés du `MimeMessage` si le contenu source peut être conservé simplement.

Choisir l'implémentation la plus simple et la plus sûre.

### 6. Headers

Copier les headers du `MimeMessage` dans :

```text
IReadOnlyList<ParsedEmailHeader>
```

Pour chaque header, conserver :

```text
Name
Value
```

Règles :

* conserver les headers dupliqués ;
* conserver leur ordre d'énumération ;
* ne pas convertir les noms de headers en minuscules ;
* ne pas fusionner plusieurs headers `Received` ;
* ne pas analyser leur signification dans cette classe.

`MimeKitEmailParser` parse.

`BasicEmailHeaderAnalyzer` analyse.

Ne pas mélanger ces responsabilités.

### 7. BodyText

Renseigner `BodyText` depuis le corps texte fourni par MimeKit.

Si aucun corps texte n'est disponible, utiliser la convention déjà présente dans `ParsedEmail`.

Ne pas convertir automatiquement le HTML en texte.

### 8. BodyHtml

Renseigner `BodyHtml` depuis le corps HTML fourni par MimeKit.

Si aucun corps HTML n'est disponible, utiliser la convention déjà présente dans `ParsedEmail`.

Ne pas interpréter le HTML.

Ne pas charger les ressources référencées.

Ne pas ouvrir les URLs contenues dans le HTML.

### 9. Pièces jointes

MimeKit peut exposer les pièces jointes du message.

Dans cette mission :

```text
NE PAS LES EXTRAIRE.
NE PAS LES DECODER.
NE PAS LES HASHER.
NE PAS LES ECRIRE SUR DISQUE.
```

Le traitement des pièces jointes fera l'objet de la Mission #006B.

### 10. BasicEmailParser

Conserver :

```text
BasicEmailParser
```

Ne pas le supprimer.

Ne pas le faire hériter de `MimeKitEmailParser`.

Ne pas transformer `BasicEmailParser` en wrapper MimeKit.

Les deux implémentations de `IEmailParser` doivent pouvoir coexister.

## Tests à créer

Créer :

```text
tests/Frelon.Mail.Tests/MimeKitEmailParserTests.cs
```

Ajouter des tests vérifiant que :

1. `ParseAsync` lit un email texte simple ;
2. `ParseAsync` conserve les headers `From`, `To` et `Subject` ;
3. `ParseAsync` conserve plusieurs headers `Received` ;
4. les headers `Received` conservent leur ordre ;
5. `BodyText` contient le corps `text/plain` ;
6. `BodyHtml` est renseigné pour un message `text/html` ;
7. un message `multipart/alternative` permet de récupérer le corps texte et le corps HTML ;
8. un sujet MIME encodé est exposé de manière exploitable dans les headers parsés ;
9. `ParseAsync` lève `ArgumentNullException` si le flux est null ;
10. un message contenant une pièce jointe peut être parsé sans exécuter ni écrire la pièce jointe ;
11. aucun appel réseau n'est nécessaire.

## Test multipart/alternative

Créer un email de test contenant :

```text
text/plain
+
text/html
```

Vérifier que :

```text
BodyText
BodyHtml
```

sont tous les deux renseignés.

Le test doit fonctionner entièrement en mémoire.

## Test de pièce jointe

Créer un message MIME de test contenant une petite pièce jointe factice.

Exemple conceptuel :

```text
filename = "facture.txt"
content = "contenu factice"
```

Le but du test est uniquement de vérifier que le message peut être parsé.

Ne pas créer encore d'`AttachmentIndicator`.

Ne pas calculer de SHA-256.

Ne pas écrire le contenu sur disque.

## Non-régression

Tous les tests existants encore pertinents doivent continuer de passer.

`BasicEmailParserTests` doivent rester valides.

Aucune classe existante ne doit être modifiée simplement pour faciliter les nouveaux tests.

## Critères d'acceptation

La mission est terminée si :

* `Frelon.Mail` compile ;
* `Frelon.Mail.Tests` compile ;
* le package `MimeKit` est ajouté uniquement à `Frelon.Mail` ;
* aucun autre package NuGet n'est ajouté ;
* `MimeKitEmailParser` implémente `IEmailParser` ;
* `BasicEmailParser` est conservé ;
* les headers dupliqués sont conservés ;
* l'ordre des headers `Received` est conservé ;
* `BodyText` est extrait ;
* `BodyHtml` est extrait ;
* tous les tests pertinents passent ;
* aucun fichier hors périmètre n'est modifié ;
* aucun modèle Core n'est modifié ;
* aucun appel réseau n'est ajouté ;
* aucune URL n'est ouverte ;
* aucune pièce jointe n'est exécutée ;
* aucune pièce jointe n'est écrite sur disque ;
* aucun hash de pièce jointe n'est encore calculé.

## Important

Ne pas intégrer encore `MimeKitEmailParser` dans `BasicEmailIncidentAnalyzer`.

Ne pas modifier le constructeur de `BasicEmailIncidentAnalyzer`.

Ne pas traiter les pièces jointes.

Ne pas créer d'`AttachmentIndicator`.

Ne pas créer d'IOC depuis les pièces jointes.

Ne pas introduire de scoring.

Ne pas créer de CLI.

Ne pas introduire de base de données.

Cette mission sert uniquement à introduire un véritable parser MIME derrière le contrat existant `IEmailParser`.