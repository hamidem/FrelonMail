# Mission #006C — Générer des IOC Hash depuis les pièces jointes analysées

## Goal

Compléter la chaîne locale d'analyse des pièces jointes en transformant les SHA-256 déjà calculés dans `AttachmentIndicator` en `Ioc` de type `Hash`, puis intégrer ces IOC dans `FraudIncident.Iocs`.

Cette mission ne recalcule aucun hash. Elle consomme uniquement les `AttachmentIndicator` déjà produits par `IEmailAttachmentAnalyzer`.

Le résultat attendu est :

```text
pièce jointe MIME déclarée
→ ParsedEmailAttachment
→ AttachmentIndicator + SHA-256
→ IOC Hash
→ FraudIncident.Iocs
```

Les IOC URL et Domain existants doivent continuer à fonctionner sans régression.

---

## Scope

Modifier uniquement :

- `src/Frelon.Mail/`
- `tests/Frelon.Mail.Tests/`

Ne modifier aucun autre projet.

En particulier :

- ne pas modifier `Frelon.Core`;
- ne pas modifier `Frelon.Reports`;
- ne pas modifier `Frelon.Exporters`;
- ne pas modifier `Frelon.Cli`;
- ne pas ajouter de package NuGet.

---

## Security and behavior constraints

- Aucun appel réseau.
- Aucun envoi de hash vers un service externe.
- Aucun accès à VirusTotal ou service équivalent.
- Aucune écriture de pièce jointe sur disque.
- Aucune exécution de pièce jointe.
- Aucune ouverture de fichier avec une application externe.
- Aucun contenu de pièce jointe interprété.
- Aucun calcul de score.
- Aucune classification de fraude.
- Aucun changement de `IsSuspicious`.
- Aucun ajout dans `Reasons`.
- Aucun signalement automatique.
- Aucun enrichissement distant.

Cette mission transforme uniquement un SHA-256 local déjà calculé en IOC `Hash`.

---

## Current context

`BasicEmailAttachmentAnalyzer` produit déjà des `AttachmentIndicator` contenant notamment :

- `FileName`;
- `ContentType`;
- `SizeBytes`;
- `Sha256`;
- `IsSuspicious`;
- `Reasons`.

`BasicEmailIncidentAnalyzer` renseigne déjà `FraudIncident.Attachments`.

`BasicUrlIocExtractor` produit déjà les IOC `Url` et `Domain`.

`BasicEmailIncidentAnalyzer` renseigne déjà `FraudIncident.Iocs` avec les IOC produits depuis les URLs.

La mission #006B interdisait volontairement la création d'IOC Hash. Cette interdiction est levée uniquement pour la présente mission #006C.

---

## Required design

### A. Create `IAttachmentIocExtractor`

Créer :

`src/Frelon.Mail/IAttachmentIocExtractor.cs`

Contrat attendu :

```csharp
using Frelon.Core;

namespace Frelon.Mail;

public interface IAttachmentIocExtractor
{
    IReadOnlyList<Ioc> ExtractIocs(
        IReadOnlyList<AttachmentIndicator> attachments,
        DateTimeOffset observedAt);
}
```

L'interface ne doit dépendre d'aucun type MimeKit.

Elle reçoit uniquement des `AttachmentIndicator` déjà analysés.

---

### B. Create `BasicAttachmentIocExtractor`

Créer :

`src/Frelon.Mail/BasicAttachmentIocExtractor.cs`

Implémenter `IAttachmentIocExtractor`.

Définir les constantes publiques suivantes :

```csharp
public const double DefaultConfidence = 1.0;
public const string SourceName = "email-attachment";
```

#### Rules

Pour chaque `AttachmentIndicator` :

1. Lire uniquement `Sha256`.
2. Ne jamais recalculer le hash.
3. Si `Sha256` est `null`, vide ou composé uniquement d'espaces, ne produire aucun IOC.
4. Appliquer `Trim()`.
5. Normaliser la valeur en minuscules avec `ToLowerInvariant()`.
6. N'accepter comme SHA-256 canonique qu'une valeur :
   - de longueur exactement `64`;
   - composée uniquement de caractères hexadécimaux.
7. Une valeur non valide doit être ignorée sans exception.
8. Produire un `Ioc` avec :
   - `Type = IocType.Hash`;
   - `Value = sha256 normalisé en minuscules`;
   - `Confidence = DefaultConfidence`;
   - `Source = SourceName`;
   - `FirstSeen = observedAt`.
9. Dédupliquer les hash identiques.
10. La déduplication doit considérer deux hash ne différant que par la casse comme identiques.
11. Préserver l'ordre de première apparition.
12. Ne produire aucun IOC URL ou Domain.
13. Ne modifier aucun `AttachmentIndicator`.

Le paramètre `attachments` doit être vérifié avec `ArgumentNullException.ThrowIfNull`.

#### Important semantic point

`Confidence = 1.0` signifie ici que la valeur de hash provient directement du SHA-256 local déjà calculé sur les octets décodés de la pièce jointe.

Cela ne signifie pas que la pièce jointe est malveillante.

Ne pas introduire de logique de réputation ou de dangerosité.

---

### C. Integrate `IAttachmentIocExtractor` into `BasicEmailIncidentAnalyzer`

Modifier `BasicEmailIncidentAnalyzer`.

Le constructeur doit devenir :

```csharp
public BasicEmailIncidentAnalyzer(
    IEmailParser parser,
    IEmailHeaderAnalyzer headerAnalyzer,
    IEmailUrlExtractor urlExtractor,
    IUrlIocExtractor urlIocExtractor,
    IEmailAttachmentAnalyzer attachmentAnalyzer,
    IAttachmentIocExtractor attachmentIocExtractor)
```

Ajouter le champ privé correspondant.

Vérifier `attachmentIocExtractor` avec `ArgumentNullException.ThrowIfNull`.

#### AnalyzeAsync order

Conserver la logique actuelle et utiliser l'ordre suivant :

```text
parse email
→ extract identity
→ extract authentication
→ extract Received chain
→ extract URLs
→ analyze attachments
→ capture one logical now
→ extract URL IOC
→ extract attachment Hash IOC
→ merge IOC collections
→ build FraudIncident
```

Utiliser le même `now` pour :

- `FraudIncident.CreatedAt`;
- `EvidenceSource.ImportedAt`;
- `Ioc.FirstSeen` des IOC URL;
- `Ioc.FirstSeen` des IOC Domain;
- `Ioc.FirstSeen` des IOC Hash.

#### IOC merge order

`FraudIncident.Iocs` doit contenir :

1. les IOC produits par `IUrlIocExtractor`, dans leur ordre actuel;
2. puis les IOC produits par `IAttachmentIocExtractor`, dans leur ordre actuel.

Ne pas introduire de nouvelle abstraction générique d'agrégation IOC dans cette mission.

Ne pas déplacer la logique des extracteurs existants.

Ne pas dédupliquer globalement des IOC de types différents.

---

## Tests

### D. Create `BasicAttachmentIocExtractorTests`

Créer :

`tests/Frelon.Mail.Tests/BasicAttachmentIocExtractorTests.cs`

Couvrir au minimum les scénarios suivants :

1. une liste vide produit une liste vide;
2. une pièce jointe avec SHA-256 valide produit un IOC;
3. l'IOC produit est de type `IocType.Hash`;
4. la valeur de l'IOC correspond au SHA-256 attendu;
5. la valeur produite est normalisée en minuscules;
6. les espaces en début et fin de SHA-256 sont supprimés;
7. `Confidence` vaut exactement `1.0`;
8. `Source` vaut exactement `"email-attachment"`;
9. `FirstSeen` correspond exactement à `observedAt`;
10. un `Sha256` null est ignoré;
11. un `Sha256` vide ou composé d'espaces est ignoré;
12. une valeur de longueur différente de 64 est ignorée;
13. une valeur de 64 caractères contenant un caractère non hexadécimal est ignorée;
14. deux pièces jointes avec le même SHA-256 ne produisent qu'un seul IOC Hash;
15. deux SHA-256 ne différant que par la casse ne produisent qu'un seul IOC Hash;
16. deux SHA-256 différents produisent deux IOC Hash;
17. l'ordre de première apparition des hash distincts est préservé;
18. une liste `attachments` null lève `ArgumentNullException`.

Utiliser des valeurs SHA-256 fixes dans les tests.

Valeurs disponibles :

```text
566a194e17b9cced887226f71d117300e8e51314531d6cae8cd0c9a82ac588f8
e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
```

Ne pas calculer les valeurs attendues avec `SHA256.HashData` dans les tests de `BasicAttachmentIocExtractor`.

Le but est de tester la transformation `AttachmentIndicator → Ioc`, pas l'algorithme SHA-256 du framework.

---

### E. Update `BasicEmailIncidentAnalyzerTests`

Adapter tous les constructeurs de `BasicEmailIncidentAnalyzer` pour fournir :

```csharp
new BasicAttachmentIocExtractor()
```

Ajouter ou adapter les tests afin de vérifier au minimum :

1. une pièce jointe MIME analysée produit un IOC `Hash`;
2. la valeur de l'IOC Hash correspond exactement au SHA-256 de l'`AttachmentIndicator`;
3. l'IOC Hash a `Source == "email-attachment"`;
4. l'IOC Hash a `Confidence == 1.0`;
5. l'IOC Hash a `FirstSeen == incident.CreatedAt`;
6. un email sans pièce jointe ne produit aucun IOC Hash;
7. un email contenant une URL et une pièce jointe conserve les IOC `Url` et `Domain` existants et ajoute l'IOC `Hash`;
8. deux pièces jointes ayant exactement les mêmes octets restent deux `AttachmentIndicator`, mais ne produisent qu'un seul IOC Hash;
9. tous les IOC produits dans un incident utilisent le même instant logique `incident.CreatedAt` pour `FirstSeen`;
10. un `attachmentIocExtractor` null dans le constructeur lève `ArgumentNullException`.

Le test de #006B qui vérifiait volontairement l'absence d'IOC Hash pour une pièce jointe est désormais obsolète.

Ne pas le supprimer uniquement pour rendre la suite verte : l'adapter afin qu'il vérifie maintenant la présence du bon IOC Hash.

Les tests existants sur les IOC URL et Domain doivent rester pertinents et passer.

---

## Expected integration example

Pour une pièce jointe contenant les octets UTF-8 de :

```text
contenu factice
```

`AttachmentIndicator.Sha256` vaut :

```text
566a194e17b9cced887226f71d117300e8e51314531d6cae8cd0c9a82ac588f8
```

L'incident doit contenir un IOC équivalent à :

```csharp
new Ioc
{
    Type = IocType.Hash,
    Value = "566a194e17b9cced887226f71d117300e8e51314531d6cae8cd0c9a82ac588f8",
    Confidence = 1.0,
    Source = "email-attachment",
    FirstSeen = observedAt
}
```

---

## Explicitly forbidden

Ne pas ajouter dans cette mission :

- calcul SHA-256 dans `BasicAttachmentIocExtractor`;
- IOC de nom de fichier;
- IOC de type MIME;
- support MD5;
- support SHA-1;
- détection d'extension dangereuse;
- détection de double extension;
- comparaison extension / type MIME;
- détection de type réel de fichier;
- analyse de contenu;
- décompression d'archive;
- récursion dans les messages attachés;
- analyse des ressources `inline`;
- appel réseau;
- réputation distante de hash;
- VirusTotal;
- scoring;
- classification;
- actions recommandées;
- reporting spécifique;
- export spécifique;
- CLI;
- base de données;
- nouvelle abstraction générique d'agrégation d'IOC.

---

## Completion criteria

La mission est terminée lorsque :

- `IAttachmentIocExtractor` existe;
- `BasicAttachmentIocExtractor` existe;
- seuls des SHA-256 valides produisent des IOC Hash;
- les hash sont normalisés et dédupliqués correctement;
- `BasicEmailIncidentAnalyzer` fusionne les IOC URL/Domain et Hash;
- le même instant logique est utilisé pour tous les `FirstSeen`;
- aucun IOC existant ne régresse;
- aucun comportement hors périmètre n'est ajouté;
- tous les tests existants encore pertinents passent;
- les nouveaux tests passent;
- `Frelon.Mail` compile;
- `Frelon.Mail.Tests` passe intégralement.
