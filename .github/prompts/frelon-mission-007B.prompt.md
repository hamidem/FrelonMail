# Mission #007B — Exposer l’explication du score dans les rapports

## Goal

Rendre le score de risque introduit par #007A réellement explicable dans les sorties de rapport Frelon.

Le score, le niveau et les raisons existent déjà dans `FraudIncident.RiskScore`.

Cette mission doit :

1. afficher les raisons du score dans `report.md`;
2. verrouiller explicitement leur présence et leur ordre dans `incident.json`.

Le résultat attendu est :

```text
RiskScore
├── Value
├── Level
└── Reasons
        ↓
report.md lisible par un humain
incident.json contractuellement vérifié
```

Cette mission ne calcule aucun score et ne modifie aucune règle de scoring.

---

## Scope

Modifier uniquement :

- `src/Frelon.Reports/`
- `tests/Frelon.Reports.Tests/`

Ne modifier aucun autre projet.

En particulier :

- ne pas modifier `Frelon.Core`;
- ne pas modifier `Frelon.Mail`;
- ne pas modifier `Frelon.Exporters`;
- ne pas modifier `Frelon.Cli`;
- ne pas ajouter de package NuGet.

---

## Security and behavior constraints

- Aucun appel réseau.
- Aucun accès au système de fichiers ajouté.
- Aucun enrichissement distant.
- Aucun recalcul du score.
- Aucune interprétation des raisons.
- Aucune classification automatique.
- Aucune action recommandée ajoutée.
- Aucun signalement automatique.
- Aucun changement des IOC.
- Aucun changement des modèles métier.

`Frelon.Reports` doit uniquement raconter l’état du `FraudIncident` reçu.

---

## Important semantic rule

Les raisons présentes dans :

```csharp
incident.RiskScore.Reasons
```

sont la trace explicative du score déjà calculé.

Le writer ne doit pas :

- recalculer les raisons;
- les déduire depuis `Authentication`;
- les déduire depuis `Urls`;
- les déduire depuis `Attachments`;
- les comparer aux constantes de `BasicIncidentRiskScorer`;
- les dédupliquer;
- les trier;
- les reformuler.

Le writer doit préserver strictement :

```text
valeurs reçues
+
ordre reçu
```

Le rapport expose la décision du scorer ; il ne rejoue pas la politique de scoring.

---

## A. Update `BasicIncidentMarkdownReportWriter`

Modifier :

`src/Frelon.Reports/BasicIncidentMarkdownReportWriter.cs`

### Add a dedicated risk explanation section

Ajouter une section Markdown appelée exactement :

```text
## Explication du score de risque
```

Cette section doit être produite immédiatement après :

```text
## Résumé
```

et avant :

```text
## Preuve source
```

Le pipeline d’écriture doit donc commencer par :

```text
Résumé
→ Explication du score de risque
→ Preuve source
→ Identité déclarée
→ Authentification
→ Chaîne Received
→ URLs
→ Pièces jointes
→ IOC
→ Actions recommandées
```

Créer une méthode privée dédiée, par exemple :

```csharp
private static void AjouterExplicationScore(
    StringBuilder sb,
    RiskScore riskScore)
```

Le nom exact de la méthode privée peut rester cohérent avec le style existant.

### Risk reasons present

Si `riskScore.Reasons` contient des éléments, produire une ligne Markdown par raison :

```text
- Échec d'authentification SPF
- Échec d'authentification DKIM
- URL suspecte détectée
```

Préserver exactement l’ordre de `riskScore.Reasons`.

Ne pas ajouter de numéro.

Ne pas préfixer chaque raison avec son poids.

Ne pas afficher de pourcentage.

Ne pas transformer les chaînes.

### No risk reason

Si `riskScore.Reasons` est vide, afficher exactement :

```text
Aucune raison de risque identifiée.
```

Ne pas utiliser ici :

```text
Aucun élément détecté.
```

Le message doit rester spécifique à l’explication du score.

Créer une constante privée dédiée :

```csharp
private const string AucuneRaisonRisque =
    "Aucune raison de risque identifiée.";
```

### Existing summary

Conserver dans `## Résumé` :

```text
Score de risque
Niveau de risque
```

Ne pas déplacer ces deux lignes.

La nouvelle section complète le résumé ; elle ne le remplace pas.

### Markdown escaping

Ne pas introduire de mécanisme général d’échappement Markdown dans cette mission.

La dette existante concernant les valeurs non fiables dans le Markdown reste hors périmètre de #007B.

---

## B. Update Markdown report tests

Modifier :

`tests/Frelon.Reports.Tests/BasicIncidentMarkdownReportWriterTests.cs`

### Align the main fixture with the current scoring semantics

La fixture actuelle contient :

```text
spf=pass
dkim=fail
dmarc=none
```

Mettre son `RiskScore` en cohérence avec le comportement actuel de #007A :

```csharp
RiskScore = new RiskScore
{
    Value = 15,
    Level = RiskLevel.Low,
    Reasons = ["Échec d'authentification DKIM"],
}
```

Le writer ne calcule toujours rien.

La fixture représente simplement un incident déjà scoré de manière cohérente.

### Tests to add or adapt

Couvrir au minimum les scénarios suivants :

1. le résumé contient exactement le score `15`;
2. le résumé contient le niveau `Low`;
3. le rapport contient la section exacte `## Explication du score de risque`;
4. la raison exacte `Échec d'authentification DKIM` est affichée;
5. les raisons sont affichées dans le même ordre que `RiskScore.Reasons`;
6. plusieurs raisons produisent une ligne Markdown chacune;
7. aucune raison ne produit aucun item de raison;
8. aucune raison affiche exactement `Aucune raison de risque identifiée.`;
9. la section `Explication du score de risque` apparaît après `Résumé`;
10. la section `Explication du score de risque` apparaît avant `Preuve source`;
11. le writer n’ajoute pas de raison depuis les données d’authentification lorsque `RiskScore.Reasons` est vide;
12. le writer n’interprète pas une URL `IsSuspicious == true` pour inventer une raison lorsque `RiskScore.Reasons` est vide;
13. le writer préserve une raison arbitraire reçue dans `RiskScore.Reasons` sans reformulation.

### Independent expected values

Dans les tests de rapport, utiliser les chaînes attendues littéralement.

Exemple :

```csharp
Assert.Contains(
    "Échec d'authentification DKIM",
    markdown);
```

Ne pas utiliser :

```csharp
BasicIncidentRiskScorer.DkimFailReason
```

Les tests de `Frelon.Reports` ne doivent pas demander au code de production du scorer de définir leur résultat attendu.

Ne pas ajouter de référence de projet de `Frelon.Reports.Tests` vers une implémentation de scoring uniquement pour récupérer ses constantes.

---

## C. Lock the `incident.json` risk explanation contract

La sérialisation actuelle de `FraudIncident` avec `System.Text.Json` doit déjà exposer `RiskScore.Reasons`.

Ne pas ajouter une seconde représentation JSON du score.

Ne pas créer de DTO JSON spécifique dans cette mission.

Ne pas modifier les options de sérialisation existantes sauf si un test démontre qu’elles empêchent le comportement demandé.

### Update `SystemTextJsonIncidentJsonWriterTests`

Modifier :

`tests/Frelon.Reports.Tests/SystemTextJsonIncidentJsonWriterTests.cs`

Ajouter une fixture ou adapter localement un incident avec :

```csharp
RiskScore = new RiskScore
{
    Value = 60,
    Level = RiskLevel.High,
    Reasons =
    [
        "Échec d'authentification SPF",
        "Échec d'authentification DKIM",
        "Échec d'authentification DMARC",
    ],
}
```

Ajouter des tests vérifiant au minimum :

1. `riskScore.value` vaut `60`;
2. `riskScore.level` représente `High` selon le comportement de sérialisation déjà établi;
3. `riskScore.reasons` existe;
4. `riskScore.reasons` est un tableau JSON;
5. le tableau contient exactement trois éléments;
6. les trois raisons ont exactement les valeurs littérales attendues;
7. l’ordre est exactement SPF, DKIM, DMARC;
8. un `RiskScore.Reasons` vide produit un tableau JSON vide et non `null`.

Utiliser de préférence `JsonDocument` pour inspecter le contrat JSON.

Ne pas valider le JSON par de simples `Assert.Contains` sur la chaîne lorsque la structure ou l’ordre du tableau est la propriété réellement testée.

### Independent expected values

Utiliser dans les assertions les chaînes littérales :

```text
Échec d'authentification SPF
Échec d'authentification DKIM
Échec d'authentification DMARC
```

Ne pas utiliser les constantes de `BasicIncidentRiskScorer`.

---

## D. Existing report behavior

Tous les tests existants encore pertinents doivent continuer à passer.

Conserver les sections existantes :

- Résumé;
- Preuve source;
- Identité déclarée;
- Authentification;
- Chaîne Received;
- URLs détectées;
- Pièces jointes détectées;
- IOC;
- Actions recommandées.

Ne pas modifier le contenu de ces sections en dehors de l’alignement de fixture nécessaire au nouveau score.

Le comportement :

```text
Aucun élément détecté.
```

pour les collections existantes reste inchangé.

---

## Explicitly forbidden

Ne pas ajouter dans cette mission :

- calcul de score;
- modification de `BasicIncidentRiskScorer`;
- modification de `IIncidentRiskScorer`;
- modification de `RiskScore`;
- modification de `RiskLevel`;
- modification de `FraudIncident`;
- modification de `BasicEmailIncidentAnalyzer`;
- classification de fraude;
- actions recommandées;
- interprétation des raisons;
- traduction automatique des raisons;
- poids affichés par raison;
- probabilité de fraude;
- pourcentage de risque;
- nouveau format de sortie;
- HTML;
- PDF;
- CLI;
- base de données;
- SQLite;
- nouveau package NuGet;
- échappement Markdown global;
- appel réseau.

---

## Completion criteria

La mission est terminée lorsque :

- `report.md` contient une section `## Explication du score de risque`;
- cette section suit immédiatement le résumé dans l’ordre des sections;
- chaque raison du `RiskScore` est affichée une fois et dans l’ordre reçu;
- aucune raison n’est recalculée ou inventée par le writer;
- un score sans raison affiche `Aucune raison de risque identifiée.`;
- `incident.json` expose explicitement `riskScore.reasons`;
- le tableau JSON conserve exactement les valeurs et l’ordre reçus;
- un tableau de raisons vide reste un tableau JSON vide;
- les tests utilisent des valeurs attendues indépendantes des constantes du scorer;
- aucun projet hors `Frelon.Reports` et `Frelon.Reports.Tests` n’est modifié;
- `Frelon.Reports` compile;
- `Frelon.Reports.Tests` passe intégralement.
