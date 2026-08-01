# Vague de travail #009 — plan de parallélisation

## Point de départ vérifié

- branche `master` propre et alignée sur `origin/master` au commit `516b965`;
- six projets applicatifs et quatre projets de tests;
- 252 tests verts : 49 Core, 112 Mail, 43 Reports, 48 Storage;
- `Frelon.Cli` et `Frelon.Exporters` ne contiennent encore aucune implémentation;
- scoring, pièces jointes, IOC Hash, rapports et premier stockage SQLite sont présents;
- le README et les instructions Copilot sont en retard sur cet état.

## Gate obligatoire

Exécuter et fusionner d'abord :

```text
#008B — Stabiliser le contrat d'identité des incidents
```

Cette mission corrige le défaut `Guid N` / `Guid D` et donne un contrat `Guid` commun aux branches suivantes.

## Vague parallèle

Créer les trois branches depuis le même commit contenant #008B :

| Mission | Propriétaire de fichiers | Dépendances | Valeur livrée |
|---|---|---|---|
| #009A | `Frelon.Mail` + tests Mail | #008B | intégrité SHA-256 de la preuve `.eml` |
| #009B | `Frelon.Storage` + tests Storage | #008B | liste locale bornée des incidents |
| #009C | `Frelon.Cli`, tests CLI, `Frelon.slnx` | #008B | commande MVP et trois sorties |

Les ensembles de fichiers sont disjoints. Aucun cherry-pick croisé n'est requis pendant l'exécution.

## Ordre de fusion conseillé

```text
#008B
  -> lancer #009A, #009B, #009C en parallèle
  -> fusionner #009A
  -> fusionner #009B
  -> fusionner #009C
  -> test complet Release
  -> synchroniser README et instructions Copilot avec l'état réellement livré
```

#009C ne doit pas anticiper les API de #009A ou #009B. Après fusion, elle bénéficiera automatiquement du SHA-256 via le pipeline Mail. L'exposition de `ListRecentAsync` dans une future commande `list` fera l'objet d'une mission séparée.

## Zones volontairement différées

- `Frelon.Exporters` : préparer ensuite un export défensif CSV ou des règles locales, avec son propre projet de tests;
- classification automatique : différée tant que les signaux ne permettent pas une sémantique prudente;
- corrélation de campagnes : différée après consultation SQLite et définition de clés de corrélation;
- persistance depuis la commande `analyze` : différée pour garder #009C atomique et réversible;
- mise à jour documentaire : à faire après la vague pour décrire uniquement des fonctions effectivement fusionnées.

## Validation finale de vague

Exécuter :

```text
dotnet restore Frelon.slnx
dotnet build Frelon.slnx -c Release --no-restore
dotnet test Frelon.slnx -c Release --no-build
```

Puis effectuer un test manuel local de la commande `analyze` sur un `.eml` de fixture non sensible.
