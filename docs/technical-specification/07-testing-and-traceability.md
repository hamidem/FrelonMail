# 07 — Tests et traçabilité

## 1. État de vérification de cette rétroconception

Le 31 juillet 2026, la solution complète a été compilée en Release et testée localement :

```powershell
dotnet test Frelon.slnx --no-restore --configuration Release --verbosity minimal
```

Résultat : **725 tests réussis, 0 échec, 0 ignoré**.

| Projet de tests | Tests réussis |
|---|---:|
| `Frelon.Application.Tests` | 35 |
| `Frelon.Cli.Tests` | 54 |
| `Frelon.Core.Tests` | 113 |
| `Frelon.Exporters.Tests` | 41 |
| `Frelon.Mail.Tests` | 184 |
| `Frelon.Reports.Tests` | 73 |
| `Frelon.Storage.Tests` | 143 |
| `Frelon.Web.Tests` | 82 |
| **Total** | **725** |

La machine de vérification a utilisé `10.0.400-preview.0.26322.102`, autorisé par le roll-forward et `allowPrerelease` de `global.json`. Le SDK a émis `NETSDK1057` pour signaler la préversion. La baseline CI déclarée et recommandée reste `10.0.301`.

## 2. Stratégie de test reconstruite

### 2.1 Core

Les tests du domaine verrouillent :

- les seuils et raisons du score ;
- l'ordre de priorité du classifieur prudent ;
- les invariants des décisions humaines ;
- les poids, normalisations et seuils de corrélation ;
- la construction, l'empreinte et la comparaison exacte des campagnes.

### 2.2 Mail

Couverture observée :

- parseur de base, MimeKit et MSG ;
- SHA-256 de source ;
- limites de taille/profondeur/en-têtes/corps/pièces jointes ;
- URL, en-têtes, pièces jointes et IOC ;
- processus d'analyse isolé ;
- corpus hostile et corpus externe attribué ;
- campagne de mutations déterministe.

Les tests MSG génèrent aussi des entrées synthétiques via `MsgKit`, distinct de `MsgReader` utilisé en production.

### 2.3 Storage

Les tests SQLite couvrent initialisation, insertion, doublons, relecture, liste récente, revues, ordre stable, données invalides, corrélation et consultation/revue de campagne.

### 2.4 Reports et Exporters

Sont vérifiés : JSON incident/IOC, Markdown automatique/validé, takedown packs, CSV et protection contre les formules, export IOC minimisé, séparation audit/paquet.

### 2.5 Application

Les tests utilisent des stores substituables pour contrôler le rechargement des dernières décisions, les incohérences, les préconditions de validation et le passage exact au writer/exporter.

### 2.6 Web et CLI

Les tests Web portent sur les services de présentation, l'export, les workspaces, la politique de fichier, l'identité, l'instance unique, le port et la politique HTTP locale. Les tests CLI couvrent parsing des commandes, conflits de chemin, publication atomique au mieux, persistance, consultation et processus isolé.

Aucun projet dédié de test JavaScript, test navigateur ou test d'accessibilité automatisé n'est présent. L'UI statique est donc principalement couverte indirectement par les contrats backend et doit faire l'objet d'une recette visuelle/manuelle.

## 3. Fuzzing et corpus

Le workflow `Parser fuzzing` s'exécute :

- à chaque push sur `master` ou `dev` touchant le moteur ;
- quotidiennement à `02:37 UTC` ;
- manuellement.

Il utilise une graine fixe `1179796812`, 2 000 cas sur push et 5 000 autrement. Cette campagne est reproductible, mais reste une mutation déterministe et non un fuzzing guidé par couverture.

Le corpus externe sous `tests/Frelon.Mail.Tests/Corpus/External` possède des notices d'origine. Le modèle de menace identifie encore l'élargissement autorisé du corpus et la minimisation automatique comme travaux futurs.

## 4. CI et contrôles de chaîne d'approvisionnement

| Workflow | Plateforme | Contrôles |
|---|---|---|
| `CI` | Ubuntu | restore verrouillé, build Release, 8 suites de tests, publication TRX |
| `Parser fuzzing` | Ubuntu | mutations déterministes et artefact TRX |
| `Package Windows` | Windows + Ubuntu | audit, isolation Windows, publish autonome, smoke test, ZIP/SHA-256, draft release |
| `CodeQL` | GitHub | activation conditionnelle documentée |

Les versions d'Actions sont épinglées sur des commits. Dependabot surveille la chaîne. Les `packages.lock.json` sont présents dans chaque projet.

## 5. Matrice de traçabilité

| Capacité | Implémentation principale | Tests principaux |
|---|---|---|
| modèle d'incident | `Core/Models/FraudIncident.cs` | `Core.Tests/FraudIncidentTests.cs` |
| score de risque | `Core/BasicIncidentRiskScorer.cs` | `Core.Tests/BasicIncidentRiskScorerTests.cs` |
| piste de classification | `Core/CautiousIncidentClassifier.cs` | `Core.Tests/CautiousIncidentClassifierTests.cs` |
| corrélation/campagnes | `Core/BasicIncidentCorrelator.cs`, `CampaignCandidate.cs` | `Core.Tests/BasicIncidentCorrelatorTests.cs`, `CampaignCandidateTests.cs` |
| parsing EML | `Mail/MimeKitEmailParser.cs` | `Mail.Tests/MimeKitEmailParserTests.cs`, corpus |
| parsing MSG | `Mail/OutlookMsgEmailParser.cs` | `Mail.Tests/OutlookMsgEmailParserTests.cs` |
| quotas | `Mail/EmailAnalysisLimits.cs`, `ParsedEmailLimitGuard.cs` | `Mail.Tests/EmailAnalysisLimitsTests.cs` |
| isolation | `Mail/IsolatedEmailAnalysis.cs`, `AnalysisWorkerProcess.cs` | `Mail.Tests/IsolatedEmailAnalysisTests.cs`, `Cli.Tests/IsolatedEmailAnalysisProcessTests.cs` |
| extraction URL | `Mail/BasicEmailUrlExtractor.cs`, `BasicUrlIocExtractor.cs` | tests homonymes sous `Mail.Tests` |
| pièces jointes | `Mail/BasicEmailAttachmentAnalyzer.cs`, `BasicAttachmentIocExtractor.cs` | tests homonymes sous `Mail.Tests` |
| SQLite incident | `Storage/SqliteIncidentStore.cs` | `Storage.Tests/SqliteIncidentStore*.cs` |
| revues incident | `IncidentReviewDecision.cs`, `SqliteIncidentStore.cs` | `Core.Tests/IncidentReviewDecisionTests.cs`, `Storage.Tests/SqliteIncidentReviewStoreTests.cs` |
| campagnes persistées | services `Storage/LocalCampaign*` | tests `LocalCampaign*` et `SqliteCampaignReviewStoreTests.cs` |
| rapports | writers sous `Reports` | cinq fichiers sous `Reports.Tests` |
| export IOC minimisé | `Exporters/BasicShareableIocExporter.cs` | `Exporters.Tests/BasicShareableIocExporterTests.cs` |
| cas d'usage exports | services sous `Application` | deux fichiers sous `Application.Tests` |
| CLI | `Cli/CliApplication.cs`, `IncidentConsultationRunner.cs` | `Cli.Tests/CliApplicationTests.cs`, `CliIncidentConsultationTests.cs` |
| API locale | `Web/Program.cs`, workspaces | tests unitaires sous `Web.Tests`; pas de serveur end-to-end dédié observé |
| sécurité HTTP | `Web/LocalHttpSecurityPolicy.cs` | `Web.Tests/LocalHttpSecurityPolicyTests.cs` |
| paquet Windows | profil publish + scripts/workflow | test catégorie `WindowsIsolation` + smoke script |

## 6. Conclusions confirmées, déduites et à valider

### 6.1 Confirmé par le code

- aucune interaction réseau dans le pipeline métier ;
- analyse isolée et bornée ;
- snapshots SQLite et décisions append-only via les interfaces ;
- campagnes recalculées, non persistées comme entités courantes ;
- séparation stricte entre suggestion automatique et décision humaine ;
- exports avancés générés en mémoire ;
- Web et CLI ne câblent pas encore `Frelon.Application`.

### 6.2 Déductions techniques fortes

- le schéma de corrélation est `O(n²)` en nombre d'incidents de la fenêtre, avec chargement `1 + N` depuis SQLite ; la limite de 500 borne le coût mais ne l'annule pas ;
- la campagne historique est liée à une composition, pas à une identité métier durable au-delà de ses incidents ;
- les fichiers `Location` de création de revue sont informatifs, car aucune route GET unitaire ne les résout ;
- un endpoint inconnu peut tomber sur `index.html` à cause du fallback global ;
- l'intégrité référentielle SQLite dépend surtout des contrôles applicatifs, `PRAGMA foreign_keys` n'étant pas explicitement activé ;
- la consultation CLI est fonctionnellement en lecture seule, mais la connexion est construite en mode `ReadWriteCreate`, pas `ReadOnly`.

### 6.3 À valider avec le propriétaire produit

- politique de rétention, suppression et export RGPD ;
- niveau de confidentialité attendu pour la base locale et besoin de chiffrement ;
- stabilité publique souhaitée pour les formats JSON, notamment les enums numériques de `incident.json` ;
- stratégie de migration SQLite après la version de schéma 1 ;
- exposition Web/CLI des exports IOC minimisés et takedown packs ;
- signature de code, canal de mise à jour et support des plateformes hors Windows ;
- objectifs de performance avec 500 incidents et volume de base à long terme ;
- exigences d'accessibilité et navigateurs officiellement supportés.

## 7. Écarts et opportunités constatés

Ces points décrivent l'état, sans présumer qu'ils doivent tous être corrigés.

| Sujet | Observation |
|---|---|
| classification | `FraudIncident.Classification` reste `Unknown`; seule la piste est alimentée |
| actions | `RecommendedActions` n'est pas alimenté ; la guidance Web calcule ses propres actions |
| authentification | `IsSuspicious` n'est jamais activé par l'analyseur de référence |
| Received | seuls position et brut sont alimentés ; champs structurés inutilisés |
| normalisation URL | `NormalizedValue` reprend la valeur brute ; pas de retrait de tracking |
| sérialisation | enums numériques dans `incident.json`, textuels ailleurs |
| Markdown automatique | données hostiles interpolées sans neutralisation générale |
| stockage | pas de migration, chiffrement, backup, purge ni commande de réparation |
| HTTP | pas de spécification OpenAPI et fallback global sur `index.html` |
| UI | pas de tests navigateur/JS automatisés observés |
| performance | corrélation quadratique et chargement individuel des snapshots |
| observabilité | logs console/debug seulement, pas de journal persistant structuré |

## 8. Maintenance de la spécification

Cette documentation doit être révisée lorsqu'un changement touche :

- une route ou un format exporté ;
- un poids, seuil, quota ou ordre de règle ;
- un invariant de décision ;
- le schéma SQLite ou la sérialisation ;
- l'isolation du worker ou la politique HTTP ;
- les profils de publication et workflows de release.

Une vérification simple consiste à rechercher les constantes et routes :

```powershell
rg -n "Map(Get|Post)|public const|CurrentSchemaVersion|DefaultMax" src
dotnet test Frelon.slnx --configuration Release
```

## 9. Références

- projets de tests sous [`tests/`](../../tests/)
- [workflow CI](../../.github/workflows/ci.yml)
- [workflow fuzzing](../../.github/workflows/parser-fuzz.yml)
- [workflow paquet Windows](../../.github/workflows/package-windows.yml)
- [processus de release](../release-process.md)

