# Mission #007A — Score de risque local et explicable

## Goal

Remplacer le `RiskScore` neutre actuellement construit par `BasicEmailIncidentAnalyzer`
par un score local, déterministe et explicable, calculé à partir de signaux déjà présents
dans `FraudIncident`.

Cette mission introduit une première politique de scoring volontairement simple.

Le résultat attendu est :

```text
signaux déjà extraits
→ BasicIncidentRiskScorer
→ RiskScore(Value, Level, Reasons)
→ FraudIncident.RiskScore
```

Le score ne doit pas déterminer la classification de fraude et ne doit déclencher aucune action.

---

## Scope

Modifier uniquement :

- `src/Frelon.Core/`
- `tests/Frelon.Core.Tests/`
- `src/Frelon.Mail/`
- `tests/Frelon.Mail.Tests/`

Ne modifier aucun autre projet.

En particulier :

- ne pas modifier `Frelon.Reports`;
- ne pas modifier `Frelon.Exporters`;
- ne pas modifier `Frelon.Cli`;
- ne pas créer de projet `Frelon.Scoring`;
- ne pas ajouter de package NuGet;
- ne pas modifier les modèles métier existants de `Frelon.Core`.

La mission doit utiliser `RiskScore`, `RiskLevel`, `FraudIncident`,
`AuthenticationAssessment`, `UrlIndicator` et `AttachmentIndicator` tels qu'ils existent.

---

## Security and behavior constraints

- Aucun appel réseau.
- Aucun enrichissement distant.
- Aucun appel à VirusTotal ou service équivalent.
- Aucune ouverture d'URL.
- Aucune exécution de pièce jointe.
- Aucune écriture de pièce jointe sur disque.
- Aucun calcul de réputation.
- Aucun moteur antispam externe.
- Aucun modèle IA ou ML.
- Aucune classification automatique de fraude.
- Aucune action recommandée ajoutée.
- Aucun signalement automatique.
- Aucun changement de `IsSuspicious`.
- Aucun changement des IOC existants.

Cette mission calcule uniquement un score local à partir de signaux déjà produits.

---

## Important semantic rules

### Observation is not suspicion

La présence d'une URL, d'un domaine, d'un hash ou d'une pièce jointe ne doit pas,
à elle seule, augmenter le score.

En particulier :

```text
IocType.Url présent
≠ URL suspecte

IocType.Domain présent
≠ domaine malveillant

IocType.Hash présent
≠ pièce jointe malveillante

AttachmentIndicator présent
≠ pièce jointe suspecte
```

Une URL ne contribue au score que si `UrlIndicator.IsSuspicious == true`.

Une pièce jointe ne contribue au score que si
`AttachmentIndicator.IsSuspicious == true`.

Ne jamais utiliser `Ioc.Confidence` comme mesure de dangerosité.

### Score is a policy value, not a probability

`RiskScore.Value` représente le résultat de la politique locale de scoring Frelon.

Un score de `75` ne signifie pas « 75 % de probabilité de fraude ».

Ne pas introduire de vocabulaire probabiliste dans les commentaires ou les tests.

### Absence of signal is not low risk

Un score de `0` doit produire :

```csharp
RiskLevel.Unknown
```

et non `RiskLevel.Low`.

L'absence de signal actuellement reconnu ne constitue pas une preuve de faible risque.

---

## Required design

### A. Create `IIncidentRiskScorer`

Créer :

`src/Frelon.Core/IIncidentRiskScorer.cs`

Contrat attendu :

```csharp
namespace Frelon.Core;

public interface IIncidentRiskScorer
{
    RiskScore Score(FraudIncident incident);
}
```

Cette interface reste dans `Frelon.Core`.

Elle ne dépend d'aucune infrastructure, de MimeKit, du système de fichiers ou du réseau.

---

### B. Create `BasicIncidentRiskScorer`

Créer :

`src/Frelon.Core/BasicIncidentRiskScorer.cs`

Implémenter `IIncidentRiskScorer`.

Définir les constantes publiques suivantes :

```csharp
public const double SpfFailWeight = 15.0;
public const double DkimFailWeight = 15.0;
public const double DmarcFailWeight = 30.0;
public const double SuspiciousUrlWeight = 20.0;
public const double SuspiciousAttachmentWeight = 30.0;
public const double MaxScore = 100.0;
```

Définir également les raisons exactes suivantes :

```csharp
public const string SpfFailReason = "Échec d'authentification SPF";
public const string DkimFailReason = "Échec d'authentification DKIM";
public const string DmarcFailReason = "Échec d'authentification DMARC";
public const string SuspiciousUrlReason = "URL suspecte détectée";
public const string SuspiciousAttachmentReason = "Pièce jointe suspecte détectée";
```

Le paramètre `incident` doit être vérifié avec :

```csharp
ArgumentNullException.ThrowIfNull(incident);
```

---

## Scoring rules

Évaluer les règles dans cet ordre exact :

1. SPF fail;
2. DKIM fail;
3. DMARC fail;
4. présence d'au moins une URL explicitement suspecte;
5. présence d'au moins une pièce jointe explicitement suspecte.

### Authentication failures

Une valeur d'authentification est considérée comme `fail` uniquement si, après `Trim()` :

```csharp
string.Equals(value, "fail", StringComparison.OrdinalIgnoreCase)
```

est vraie.

Règles :

- `SpfResult == fail` → `+15`;
- `DkimResult == fail` → `+15`;
- `DmarcResult == fail` → `+30`.

Les valeurs suivantes ne doivent pas ajouter de points dans cette mission :

- `null`;
- chaîne vide;
- espaces;
- `pass`;
- `none`;
- `neutral`;
- `softfail`;
- `temperror`;
- `permerror`;
- toute autre valeur.

Ne pas interpréter davantage les valeurs d'authentification dans cette mission.

### Suspicious URLs

Si au moins un élément de `incident.Urls` vérifie :

```csharp
url.IsSuspicious
```

ajouter exactement :

```text
+20
```

et ajouter une seule fois :

```text
URL suspecte détectée
```

Dix URLs suspectes ne doivent pas produire `10 × 20`.

### Suspicious attachments

Si au moins un élément de `incident.Attachments` vérifie :

```csharp
attachment.IsSuspicious
```

ajouter exactement :

```text
+30
```

et ajouter une seule fois :

```text
Pièce jointe suspecte détectée
```

Dix pièces jointes suspectes ne doivent pas produire `10 × 30`.

### Score cap

Après évaluation de toutes les règles :

```csharp
score = Math.Min(score, MaxScore);
```

Toutes les raisons correspondant aux règles déclenchées doivent être conservées,
même lorsque le score brut dépasse `MaxScore`.

---

## RiskLevel mapping

Mapper le score final avec les règles exactes suivantes :

```text
score == 0          → Unknown
0 < score < 25      → Low
25 <= score < 50    → Medium
50 <= score < 75    → High
75 <= score <= 100  → Critical
```

Ne pas modifier `RiskLevel`.

Ne pas introduire d'autre niveau.

---

## RiskScore result

Retourner un nouveau `RiskScore` :

```csharp
new RiskScore
{
    Value = score,
    Level = level,
    Reasons = reasons,
}
```

L'ordre des raisons doit correspondre à l'ordre fixe d'évaluation des règles :

```text
SPF
DKIM
DMARC
URL
Attachment
```

Le scorer ne doit pas modifier `FraudIncident`.

Le scorer doit ignorer totalement :

- `incident.RiskScore`;
- `incident.Classification`;
- `incident.Iocs`;
- `incident.RecommendedActions`;
- `Ioc.Confidence`.

Le score doit dépendre uniquement des règles explicitement décrites dans cette mission.

---

## C. Integrate `IIncidentRiskScorer` into `BasicEmailIncidentAnalyzer`

Modifier `BasicEmailIncidentAnalyzer`.

Le constructeur doit devenir :

```csharp
public BasicEmailIncidentAnalyzer(
    IEmailParser parser,
    IEmailHeaderAnalyzer headerAnalyzer,
    IEmailUrlExtractor urlExtractor,
    IUrlIocExtractor urlIocExtractor,
    IEmailAttachmentAnalyzer attachmentAnalyzer,
    IAttachmentIocExtractor attachmentIocExtractor,
    IIncidentRiskScorer riskScorer)
```

Ajouter le champ privé correspondant.

Vérifier `riskScorer` avec :

```csharp
ArgumentNullException.ThrowIfNull(riskScorer);
```

Ne déplacer aucune logique existante d'extraction ou de génération d'IOC.

---

## Analyzer construction flow

Conserver le pipeline actuel :

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
```

Construire ensuite un `FraudIncident` provisoire contenant toutes les données extraites.

Comme `FraudIncident.RiskScore` est déjà requis par le modèle métier et ne doit pas être
modifié dans cette mission, initialiser provisoirement :

```csharp
RiskScore = new RiskScore
{
    Value = 0,
    Level = RiskLevel.Unknown,
}
```

Puis :

```csharp
var riskScore = _riskScorer.Score(incident);
```

et retourner :

```csharp
return incident with
{
    RiskScore = riskScore,
};
```

Ne pas reconstruire manuellement un second `FraudIncident`.

Ne pas appeler le scorer avant que les champs suivants soient présents dans l'incident provisoire :

- `Authentication`;
- `Urls`;
- `Attachments`;
- `Iocs`.

Même si les IOC ne contribuent pas au score dans cette mission, l'agrégat donné au scorer
doit représenter l'état complet courant de l'analyse.

### Important

`Classification` doit rester :

```csharp
FraudClassification.Unknown
```

Le scoring ne doit pas modifier la classification.

`RecommendedActions` doit rester inchangé.

---

## Tests

### D. Create `BasicIncidentRiskScorerTests`

Créer :

`tests/Frelon.Core.Tests/BasicIncidentRiskScorerTests.cs`

Construire un helper local permettant de créer un `FraudIncident` minimal avec :

- `AuthenticationAssessment`;
- `Urls`;
- `Attachments`;
- `Iocs`;
- éventuellement un `RiskScore` préexistant pour vérifier qu'il est ignoré.

Couvrir au minimum les scénarios suivants :

1. incident null → `ArgumentNullException`;
2. aucun signal reconnu → score `0`;
3. score `0` → `RiskLevel.Unknown`;
4. aucun signal reconnu → `Reasons` vide;
5. SPF fail → score `15`;
6. SPF fail → niveau `Low`;
7. SPF fail → raison exacte `Échec d'authentification SPF`;
8. DKIM fail → score `15`;
9. DKIM fail → raison exacte `Échec d'authentification DKIM`;
10. DMARC fail → score `30`;
11. DMARC fail → niveau `Medium`;
12. DMARC fail → raison exacte `Échec d'authentification DMARC`;
13. `fail` est reconnu sans sensibilité à la casse;
14. espaces autour de `fail` sont ignorés;
15. `pass`, `none`, `neutral`, `softfail`, `temperror` et `permerror` ne donnent aucun point;
16. une URL avec `IsSuspicious = true` → `+20`;
17. plusieurs URLs suspectes → une seule contribution de `20`;
18. plusieurs URLs suspectes → une seule raison URL;
19. une URL non suspecte → aucun point;
20. une pièce jointe avec `IsSuspicious = true` → `+30`;
21. plusieurs pièces jointes suspectes → une seule contribution de `30`;
22. plusieurs pièces jointes suspectes → une seule raison pièce jointe;
23. une pièce jointe non suspecte → aucun point;
24. SPF fail + DKIM fail + DMARC fail → score `60`;
25. score `60` → niveau `High`;
26. toutes les règles déclenchées → score brut `110` plafonné à `100`;
27. score `100` → niveau `Critical`;
28. le plafonnement à `100` conserve les cinq raisons;
29. l'ordre des raisons est exactement SPF, DKIM, DMARC, URL, pièce jointe;
30. la présence d'IOC URL/Domain/Hash sans signal `IsSuspicious` ne modifie pas le score;
31. un IOC Hash avec `Confidence = 1.0` ne modifie pas le score;
32. un `RiskScore` préexistant élevé dans l'incident est ignoré par le scorer;
33. la classification existante dans l'incident est ignorée par le scorer;
34. l'incident fourni n'est pas modifié.

Ne pas utiliser une boucle générant dynamiquement les résultats attendus du scoring
à partir des mêmes constantes que l'implémentation.

Les tests doivent contenir les valeurs attendues explicitement.

---

### E. Update `BasicEmailIncidentAnalyzerTests`

Adapter tous les constructeurs de `BasicEmailIncidentAnalyzer` pour fournir :

```csharp
new BasicIncidentRiskScorer()
```

Ajouter ou adapter les tests afin de vérifier au minimum :

1. `MinimalEml`, qui contient actuellement `spf=pass`, `dkim=fail`, `dmarc=none`,
   produit un score `15`;
2. ce score produit `RiskLevel.Low`;
3. la raison exacte DKIM est présente;
4. un email contenant une URL extraite mais non marquée suspecte ne gagne aucun point
   uniquement à cause de cette URL;
5. une pièce jointe analysée avec `IsSuspicious == false` ne gagne aucun point
   uniquement à cause de la pièce jointe ou de son IOC Hash;
6. `Classification` reste `FraudClassification.Unknown` après scoring;
7. `RecommendedActions` reste inchangé et vide dans les fixtures actuelles;
8. un `riskScorer` null dans le constructeur lève `ArgumentNullException`.

### Injected scorer behavior test

Ajouter un petit double de test local implémentant `IIncidentRiskScorer`
et retournant un score fixe :

```csharp
new RiskScore
{
    Value = 42,
    Level = RiskLevel.Medium,
    Reasons = ["score fixe de test"],
}
```

Ajouter un test vérifiant que `BasicEmailIncidentAnalyzer` utilise bien le scorer injecté
et renseigne `FraudIncident.RiskScore` avec ce résultat.

Le but est de vérifier que l'orchestrateur délègue réellement le scoring et ne contient pas
la politique de score en dur.

Ne pas utiliser de framework de mocking supplémentaire.

---

## Existing tests

Tous les tests existants encore pertinents doivent continuer à passer.

Le test actuel qui vérifie :

```text
RiskScore.Value == 0
RiskLevel.Unknown
```

sur `MinimalEml` devient obsolète, car `MinimalEml` contient `dkim=fail`.

Ne pas supprimer ce test uniquement pour rendre la suite verte.

L'adapter pour vérifier le nouveau comportement attendu :

```text
Value == 15
Level == Low
```

et la raison DKIM.

Les tests existants sur :

- parsing;
- headers;
- URLs;
- IOC URL/Domain;
- attachments;
- IOC Hash;
- instant logique commun des IOC;

doivent rester pertinents et passer.

---

## Explicitly forbidden

Ne pas ajouter dans cette mission :

- modification de `RiskScore`;
- modification de `RiskLevel`;
- modification de `FraudIncident`;
- nouveau modèle métier;
- projet `Frelon.Scoring`;
- scoring basé sur le nombre d'IOC;
- scoring basé sur `Ioc.Confidence`;
- scoring basé sur la simple présence d'une URL;
- scoring basé sur la simple présence d'une pièce jointe;
- scoring basé sur la simple présence d'un hash;
- réputation de domaine;
- réputation d'URL;
- réputation de hash;
- comparaison extension / type MIME;
- détection de double extension;
- détection de type réel de fichier;
- analyse de contenu de pièce jointe;
- analyse du sujet ou du corps du mail;
- règles lexicales;
- ML ou IA;
- classification de fraude;
- actions recommandées;
- signalement;
- reporting spécifique;
- export spécifique;
- CLI;
- base de données;
- appel réseau;
- nouvelle dépendance NuGet.

---

## Completion criteria

La mission est terminée lorsque :

- `IIncidentRiskScorer` existe dans `Frelon.Core`;
- `BasicIncidentRiskScorer` existe dans `Frelon.Core`;
- le scoring est local et déterministe;
- les cinq règles exactes sont appliquées;
- le score est plafonné à `100`;
- les niveaux sont mappés selon les seuils définis;
- les raisons sont explicites et dans un ordre déterministe;
- l'absence de signal produit `Unknown`, pas `Low`;
- les IOC seuls ne modifient pas le score;
- `Ioc.Confidence` ne modifie pas le score;
- `BasicEmailIncidentAnalyzer` délègue le scoring à `IIncidentRiskScorer`;
- `Classification` reste `Unknown` dans le pipeline actuel;
- aucune action recommandée n'est ajoutée;
- aucune fonctionnalité hors périmètre n'est ajoutée;
- `Frelon.Core` compile;
- `Frelon.Mail` compile;
- `Frelon.Core.Tests` passe intégralement;
- `Frelon.Mail.Tests` passe intégralement.
