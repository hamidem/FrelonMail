# Mission Copilot #001B — Tests minimaux du modèle Frelon.Core

## Contexte

La Mission #001A a créé le modèle métier minimal dans `src/Frelon.Core/Models/`.

Le projet `Frelon.Core` contient notamment :

* `FraudIncident`
* `EvidenceSource`
* `MailIdentity`
* `AuthenticationAssessment`
* `ReceivedHop`
* `UrlIndicator`
* `AttachmentIndicator`
* `Ioc`
* `FraudClassification`
* `RiskScore`
* `RecommendedAction`
* enums auxiliaires éventuelles comme `IocType`, `RiskLevel`, `RecommendedActionType`

Cette mission doit respecter :

```text
.github/copilot-instructions.md
```

## Objectif

Créer des tests unitaires minimaux pour valider que le modèle métier de `Frelon.Core` est utilisable, instanciable et cohérent.

## Périmètre autorisé

Tu peux modifier uniquement :

```text
tests/Frelon.Core.Tests/
```

## Contraintes

* Ne pas modifier le modèle métier existant.
* Ne pas ajouter de package NuGet sans demande explicite.
* Ne pas créer de nouveau projet.
* Ne pas ajouter de code réseau.
* Ne pas ajouter de parsing `.eml`.
* Ne pas ajouter de logique de scoring réel.
* Ne pas anticiper les missions suivantes.
* Utiliser xUnit.
* Garder les tests simples et lisibles.

## Travail demandé

Créer un fichier de test, par exemple :

```text
tests/Frelon.Core.Tests/FraudIncidentTests.cs
```

Ajouter des tests vérifiant que :

1. un `FraudIncident` peut être instancié avec les valeurs minimales requises ;
2. les collections `ReceivedChain`, `Urls`, `Attachments`, `Iocs` et `RecommendedActions` sont vides par défaut si le modèle les initialise ainsi ;
3. un `RiskScore` peut représenter un niveau de risque simple ;
4. une `RecommendedAction` peut représenter une action nécessitant validation humaine ;
5. un `Ioc` peut représenter un domaine, une URL ou un hash selon les enums disponibles.

## Critères d’acceptation

La mission est terminée si :

* `Frelon.Core.Tests` compile ;
* les tests passent ;
* aucun fichier hors `tests/Frelon.Core.Tests/` n’a été modifié ;
* aucun package inutile n’a été ajouté ;
* aucun comportement hors modèle métier n’a été introduit.