# Mission #009A — Empreinte SHA-256 exacte de la preuve `.eml`

## Goal

Calculer localement l'empreinte SHA-256 des octets exacts du fichier `.eml` analysé et la placer dans `FraudIncident.Evidence.Sha256`.

Cette mission démarre après fusion de #008B et peut être exécutée en parallèle de #009B et #009C.

## Ownership exclusif

Modifier uniquement :

- `src/Frelon.Mail/`;
- `tests/Frelon.Mail.Tests/`.

Ne modifier aucun autre projet, aucun fichier projet et aucun fichier de solution.

## Required design

1. Étendre `ParsedEmail` avec une propriété requise explicite représentant le SHA-256 du flux source, par exemple :

```csharp
public required string SourceSha256 { get; init; }
```

2. `BasicEmailParser` et `MimeKitEmailParser` doivent calculer l'empreinte sur les octets exacts lus depuis le flux, avant toute conversion de texte, normalisation de fin de ligne ou interprétation MIME.

3. Utiliser uniquement `System.Security.Cryptography.SHA256`.

4. Représenter l'empreinte par 64 caractères hexadécimaux minuscules, comme les empreintes de pièces jointes existantes.

5. `BasicEmailIncidentAnalyzer` doit recopier cette valeur dans :

```csharp
Evidence.Sha256
```

6. Ne pas relire le fichier depuis le système de fichiers. Le flux fourni reste l'unique source.

## Stream and safety rules

- Propager le `CancellationToken` pendant la lecture.
- Ne pas fermer le flux fourni par l'appelant.
- Supporter un flux non seekable.
- Ne pas dépendre de `Stream.Position` initial égal à zéro : analyser à partir de la position courante.
- Aucun fichier temporaire.
- Aucun appel réseau.
- Ne jamais écrire ou exécuter les pièces jointes.
- Ne pas calculer l'empreinte à partir de `RawContent`, car cette chaîne est déjà une interprétation des octets.

Une seule copie mémoire du `.eml` peut servir à la fois au hash, au contenu brut et à MimeKit. Ne pas multiplier inutilement les lectures complètes.

## Tests

Couvrir au minimum pour les deux parseurs :

1. SHA-256 connu d'un `.eml` ASCII;
2. conservation exacte des octets avec CRLF;
3. contenu comportant des octets non ASCII;
4. flux non seekable;
5. lecture depuis une position courante non nulle;
6. token déjà annulé;
7. flux appelant laissé ouvert;
8. même entrée -> même empreinte pour les deux parseurs;
9. changement d'un seul octet -> empreinte différente;
10. l'incident final expose l'empreinte dans `Evidence.Sha256`.

Les valeurs attendues doivent être calculées dans le test directement sur les octets de la fixture, indépendamment du parseur testé.

## Completion criteria

- l'empreinte porte sur la preuve source exacte;
- les deux parseurs alimentent le même contrat;
- le rapport et le JSON existants l'exposent automatiquement via `EvidenceSource` sans modification de `Frelon.Reports`;
- tous les tests Mail et la solution complète passent.

