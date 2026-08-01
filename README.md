<p align="center">
  <img src="assets/mr-frelon.png" alt="Mr Frelon — emblème du projet Frelon" width="320" />
</p>

<h1 align="center">Frelon</h1>

<p align="center">
  <strong>Transformer un mail frauduleux en renseignement défensif actionnable.</strong>
</p>

<p align="center">
  <em>Frelon ne pique pas les machines. Il pique les campagnes.</em>
</p>

<p align="center">
  <sub>powered by Moralement.NET 😎</sub>
</p>

---

## Pourquoi Frelon ?

Un mail frauduleux ne devrait pas simplement finir dans une corbeille.

Frelon part d'un fichier `.eml` ou Outlook `.msg` local et cherche à le transformer en éléments exploitables :

- une preuve structurée ;
- des informations d'identité et d'authentification mail ;
- des URLs et indicateurs techniques ;
- des IOC (Indicators of Compromise) ;
- un rapport humain lisible ;
- des sorties JSON réutilisables par d'autres outils défensifs.

L'objectif à terme est de **réduire la fenêtre d'exploitation d'une campagne frauduleuse** en accélérant l'analyse, la corrélation, la production de règles défensives et la préparation de signalements exploitables.

## Principe directeur

```text
mail frauduleux
      ↓
preuve conservée
      ↓
analyse locale
      ↓
indicateurs extraits
      ↓
IOC structurés
      ↓
rapport lisible
      ↓
actions défensives préparées
```

Frelon est **défensif, local-first et sûr par défaut**.

Il n'est pas conçu pour riposter contre un serveur, perturber une infrastructure distante ou interagir automatiquement avec un fraudeur.

## Ce que Frelon ne fera pas

Frelon ne doit jamais :

- attaquer, scanner, ralentir, exploiter ou perturber un système distant ;
- répondre automatiquement à un expéditeur ;
- ouvrir automatiquement une URL suspecte ;
- exécuter une pièce jointe ;
- envoyer automatiquement un signalement sans validation humaine ;
- soumettre de fausses données à une infrastructure frauduleuse ;
- exposer des données personnelles dans un export public ou partageable.

> **Moralement.NET n'est pas une dépendance NuGet. C'est une contrainte d'architecture.**

## État actuel

Frelon est en développement actif.

Le premier parcours local de bout en bout est maintenant utilisable. Frelon permet de :

- lire un `.eml` ou un courriel Outlook `.msg` local ;
- parser un message MIME avec MimeKit ou un conteneur Outlook avec MsgReader ;
- calculer le SHA-256 exact de la preuve source ;
- conserver les headers, y compris les headers dupliqués comme `Received` ;
- extraire les identités déclarées (`From`, `Reply-To`, `Return-Path`, `Message-ID`, `Subject`) ;
- analyser localement SPF, DKIM et DMARC depuis `Authentication-Results` ;
- construire un `FraudIncident` structuré ;
- extraire localement les URLs des corps texte et HTML ;
- analyser les pièces jointes en mémoire et calculer leur SHA-256 sans les exécuter ;
- produire des IOC `Url`, `Domain` et `Hash` dédupliqués ;
- calculer un score de risque local, déterministe et expliqué ;
- générer un JSON d'incident ;
- générer un rapport Markdown ;
- générer un JSON dédié aux IOC ;
- générer un CSV d'IOC stable et protégé contre l'injection de formules de tableur ;
- enregistrer et relire des incidents dans une base SQLite locale ;
- consulter les métadonnées des incidents récents sans charger leurs snapshots ;
- exécuter le pipeline depuis la commande `frelon analyze` ;
- analyser et consulter les incidents récents depuis une première interface locale ;
- lire d'abord une synthèse prudente en langage courant, puis déplier l'analyse technique complète sans perte d'information ;
- télécharger depuis l'interface les rapports individuels ou un dossier ZIP complet ;
- enregistrer des décisions humaines horodatées sans altérer l'analyse automatique ;
- consulter leur chronologie complète et repérer les incidents encore à examiner ;
- proposer une piste de classification locale, prudente et toujours expliquée ;
- appliquer des règles défensives locales aux URL et pièces jointes sans les ouvrir ;
- rapprocher localement des incidents en campagnes candidates à partir d'IOC exacts et pondérés ;
- conserver les décisions humaines sur ces campagnes avec le snapshot exact qui a été examiné ;
- consulter ensemble les campagnes recalculées et leur historique de décisions humaines ;
- préparer en mémoire un takedown pack adapté à plusieurs rôles de destinataires ;
- produire un paquet d'IOC strictement minimisé avec un audit sensible séparé ;
- générer un signalement Markdown seulement après confirmation et catégorisation humaines de la fraude ;
- guider l'utilisateur pour récupérer un message depuis Gmail, Outlook ou Thunderbird sans lui demander de connaître le format EML.

La classification portée par l'analyse reste volontairement à `Unknown` : un score n'est ni une probabilité ni une preuve de fraude. Frelon peut désormais fournir une piste automatique séparée, accompagnée d'un niveau de confiance et des signaux qui la motivent. Une décision humaine est conservée séparément dans un historique append-only ; elle peut être exportée dans `review.json` et ne réécrit jamais le snapshot d'analyse.

Les règles défensives restent déterministes et inspectables. Pour les URL, elles ciblent les adresses IP brutes, les identités intégrées avant l'hôte, les chemins sensibles sans HTTPS et la combinaison d'un domaine internationalisé avec un chemin sensible. Pour les pièces jointes, elles examinent uniquement le nom et le type MIME afin de repérer les extensions exécutables ou de script, le contenu actif, les doubles extensions trompeuses et les types MIME exécutables. Elles n'effectuent aucune résolution DNS, requête réseau, ouverture ou exécution.

La corrélation de campagnes est elle aussi déterministe et inspectable. Une empreinte, une URL, une adresse IP ou une adresse email exacte peut justifier un lien ; un domaine isolé ne suffit pas et les noms de fichiers sont ignorés. Les IOC trop incertains et les imports répétés de la même preuve sont exclus. Chaque campagne produite reste une candidate éphémère accompagnée des liens, valeurs normalisées et poids qui la motivent : elle n'est jamais confirmée automatiquement.

Lorsqu'un humain examine une campagne candidate, Frelon peut conserver sa décision dans un historique append-only. Chaque événement embarque le snapshot exact de la composition et des rapprochements examinés, ainsi qu'une empreinte stable des incidents concernés. Avant l'enregistrement, le workflow recalcule la fenêtre courante et compare la totalité du snapshot présenté — incidents, horodatages, liens et indicateurs — plutôt que sa seule empreinte de composition. Une campagne disparue ou modifiée doit être relue : aucune décision périmée n'est enregistrée silencieusement. Une évolution ultérieure des règles de corrélation ne réécrit donc pas les décisions passées. Confirmer une campagne signifie uniquement que ses incidents sont considérés comme liés ; cela ne remplace pas leur revue individuelle ni leur classification de fraude.

Le service de consultation des campagnes réunit sans écriture les candidats recalculés dans la fenêtre récente et leur dernière décision humaine. Le détail restitue tout l'historique demandé dans un ordre stable. Une composition qui n'apparaît plus dans le calcul courant reste consultable à partir du snapshot exact conservé dans sa revue : Frelon distingue donc explicitement une campagne actuelle d'une campagne uniquement historique, sans réinterpréter les décisions passées.

Le noyau des takedown packs prépare des brouillons distincts pour un hébergeur, un registrar, un fournisseur de messagerie ou un service anti-phishing. Le cas d'usage Application part d'une campagne choisie, recharge sa dernière décision, les snapshots exacts de ses incidents et leurs dernières revues individuelles, puis transmet cet ensemble au générateur. Il exige une campagne confirmée, une fraude confirmée et catégorisée pour chacun de ses incidents, un SHA-256 de preuve exploitable et une date de préparation postérieure aux décisions utilisées. Le pack contient un manifeste de traçabilité, un guide de contrôle avant envoi et uniquement les indicateurs adaptés au rôle choisi. Frelon ne recherche pas de coordonnées, ne choisit pas le destinataire réel, n'écrit pas le pack sur le disque et ne transmet rien.

L'export d'IOC à partage contrôlé applique un profil de minimisation strict. Le cas d'usage Application recharge les incidents explicitement choisis et leur dernière décision humaine avant de construire la demande d'export ; une source absente, non validée ou incohérente interrompt la préparation. Le paquet transmissible ne contient que les domaines valides et les SHA-256 d'IOC suffisamment fiables que l'analyste a explicitement sélectionnés ; il exclut les URL complètes, emails, adresses IP, noms de fichiers, sources internes, horodatages précis, identifiants locaux et tout hash identique à une preuve `.eml` ou `.msg`. Les références nécessaires à l'audit restent dans une structure locale séparée, accompagnées du SHA-256 de chaque document produit. Cette minimisation réduit le risque de divulgation mais ne constitue pas une garantie d'anonymisation juridique absolue : chaque valeur et chaque destinataire doivent encore être vérifiés humainement avant partage.

Le fichier `signalement.md` est distinct du rapport automatique. Il rappelle l'identifiant de la revue humaine, la catégorie retenue, la traçabilité de la preuve et les indicateurs techniques. Il n'est disponible que lorsque la dernière décision confirme une fraude avec une catégorie précise. Sa génération reste locale : le choix du destinataire et l'envoi demeurent entièrement manuels.

L'interface parle du « fichier du message suspect » plutôt que d'une « preuve EML ». Une aide intégrée décrit les gestes usuels dans Gmail, Outlook et Thunderbird. Les courriels Outlook MSG sont analysés localement sans nécessiter Outlook ; les rendez-vous, contacts, tâches et autres objets Outlook restent volontairement refusés.

## Pipeline actuel

```text
.eml ou .msg local
    ↓
IEmailParser
    ↓
ParsedEmail
    ├── Headers / corps MIME
    ├── pièces jointes décodées en mémoire
    ├── contenu brut réversible
    └── SHA-256 de la preuve source
    ↓
IEmailHeaderAnalyzer
IEmailUrlExtractor
IUrlIocExtractor
IEmailAttachmentAnalyzer
IAttachmentIocExtractor
    ↓
BasicEmailIncidentAnalyzer
    ↓
BasicIncidentRiskScorer
    ↓
CautiousIncidentClassifier
    ↓
FraudIncident
    ├── Identity
    ├── Authentication
    ├── ReceivedChain
    ├── Urls
    ├── Attachments
    ├── Iocs
    ├── Classification de l'analyse
    ├── ClassificationAssessment explicable
    └── RiskScore
    ↓
Frelon.Reports
    ├── incident.json
    ├── report.md
    └── iocs.json

Frelon.Storage
    ├── snapshot JSON interne SQLite
    ├── métadonnées locales et état de revue consultables
    └── décisions humaines append-only

Frelon.Application
    ├── cas d'usage réunissant stockage et production documentaire
    ├── préparation des takedown packs depuis les validations locales
    └── préparation des exports IOC depuis les incidents choisis

Frelon.Web
    ├── import local d'une preuve .eml ou .msg
    ├── synthèse du risque et des IOC
    ├── historique SQLite local avec état de revue
    ├── décision humaine explicite et chronologie auditable
    └── rapports et dossier ZIP à la demande
```

## Architecture

La [spécification technique reconstruite](docs/technical-specification/README.md)
décrit en détail les composants, le domaine, le schéma SQLite, le moteur d'analyse,
les interfaces, la sécurité, les exports et leur traçabilité vers le code et les tests.

```text
src/
  Frelon.Application/
  Frelon.Core/
  Frelon.Mail/
  Frelon.Reports/
  Frelon.Exporters/
  Frelon.Storage/
  Frelon.Cli/
  Frelon.Web/

tests/
  Frelon.Application.Tests/
  Frelon.Core.Tests/
  Frelon.Mail.Tests/
  Frelon.Reports.Tests/
  Frelon.Storage.Tests/
  Frelon.Cli.Tests/
  Frelon.Web.Tests/
```

### `Frelon.Core`

Modèle métier pur et logique métier indépendante de l'infrastructure.

### `Frelon.Application`

Cas d'usage transverses qui réunissent les contrats métier, le stockage local et la production documentaire sans dépendre d'une interface particulière.

### `Frelon.Mail`

Parsing `.eml` / MIME et Outlook `.msg`, empreinte de la preuve, extraction des headers, URLs et pièces jointes.

### `Frelon.Reports`

Génération des sorties humaines et structurées.

### `Frelon.Exporters`

Exports défensifs locaux. Le premier format disponible est un CSV d'IOC à culture invariante, avec échappement des cellules et neutralisation des formules de tableur. Le JSON IOC reste le format exact à privilégier pour les échanges machine à machine.

### `Frelon.Storage`

Persistance SQLite locale des snapshots d'incidents et consultation bornée de leurs métadonnées.

### `Frelon.Cli`

Interface ligne de commande et orchestration du pipeline local.

### `Frelon.Web`

Interface graphique locale servie uniquement sur la boucle locale. Elle réutilise le même pipeline d'analyse que le CLI, conserve les incidents dans SQLite et n'effectue aucune requête vers une infrastructure suspecte.

## Utilisation

Le SDK stable recommandé est **.NET 10.0.301**. La CI utilise exactement cette version.

Depuis la racine du dépôt :

```bash
dotnet run --project src/Frelon.Cli -- analyze samples/suspicious-demo.eml --output ./out
```

Pour lancer l'interface locale :

```bash
dotnet run --project src/Frelon.Web
```

Puis ouvrir `http://localhost:5127`. La base de l'interface est conservée dans le dossier applicatif local de l'utilisateur (`Frelon/incidents.db`). Le port et le dossier peuvent être configurés avec `Frelon:Port` et `Frelon:DataDirectory`.

### Application locale Windows autonome

Frelon peut être publié comme une application Windows 64 bits autonome : aucune installation préalable de .NET n'est nécessaire sur le poste utilisateur.

```bash
dotnet restore src/Frelon.Web/Frelon.Web.csproj -r win-x64
dotnet publish src/Frelon.Web/Frelon.Web.csproj -c Release --no-restore -p:PublishProfile=win-x64 -o ./artifacts/Frelon-win-x64
```

Il suffit ensuite de conserver tout le dossier `Frelon-win-x64` et de double-cliquer sur `Frelon.Web.exe`. L'application fonctionne discrètement en arrière-plan et le navigateur s'ouvre automatiquement sur l'interface locale. Le bouton **Quitter Frelon** arrête proprement le serveur ; fermer uniquement l'onglet du navigateur ne l'arrête pas. Un nouveau double-clic rouvre l'interface de l'instance déjà active.

L'application écoute exclusivement sur l'ordinateur local. Elle privilégie le port 5127 et en choisit automatiquement un autre s'il est occupé. Un second lancement réutilise l'instance déjà active au lieu de démarrer un serveur supplémentaire. Les données restent dans le dossier applicatif local de l'utilisateur.

Sous Windows, chaque courriel est analysé dans un processus temporaire lancé avec
des privilèges désactivés au maximum, un niveau d'intégrité bas, des ressources
bornées et un AppContainer éphémère sans capability réseau. Le message lui est
transmis par un canal inter-processus ; il ne reçoit aucun droit sur la base, les
rapports ou la preuve source. Si Windows ne peut pas appliquer ces restrictions,
Frelon refuse l'analyse au lieu de relancer silencieusement le worker avec les
droits complets de l'utilisateur.

Le workflow GitHub Actions `Package Windows` fabrique également une archive ZIP versionnée, manuellement ou lors de la création d'un tag `v*`. Il joint un fichier `.sha256` permettant de vérifier que l'archive téléchargée est strictement identique à celle produite par GitHub. La version intégrée à l'exécutable apparaît aussi dans le pied de page de l'interface.

L'exécutable n'est pas encore signé : Windows SmartScreen peut donc afficher un avertissement jusqu'à l'ajout d'une signature de code pour la distribution publique. La présence de l'empreinte SHA-256 garantit l'intégrité du téléchargement, mais ne remplace pas une signature de code.

Chaque ZIP contient également un `LISEZ-MOI.txt` autonome, l'inventaire
`THIRD-PARTY-NOTICES.txt` et les notices officielles du runtime .NET autoporté.
Lorsqu'un tag de version valide est créé depuis `master`, le workflow prépare une
GitHub Release en brouillon : les actifs et les notes doivent encore être
contrôlés humainement avant publication. La procédure complète et la révocation
sont décrites dans
[docs/release-process.md](docs/release-process.md), avec la
[checklist de sortie V1](docs/v1-release-checklist.md).

Une fois l'exécutable publié ou installé :

```bash
frelon analyze suspicious.eml --output ./out
```

Pour conserver également l'incident dans SQLite :

```bash
frelon analyze suspicious.eml --output ./out --database ./data/frelon.db
```

Pour ajouter le CSV défensif destiné à une consultation dans un tableur :

```bash
frelon analyze suspicious.eml --output ./out --csv
```

`--csv` peut être combiné à `--database` dans n'importe quel ordre après `--output <directory>`.

Pour consulter les incidents les plus récents d'une base locale sans relire les emails :

```bash
frelon incidents list --database ./data/frelon.db
frelon incidents list --database ./data/frelon.db --limit 20
```

La liste expose l'identifiant, la date, le fichier source, le risque, la classification automatique et l'état de revue. Le snapshot complet d'un incident peut ensuite être affiché en JSON :

```bash
frelon incidents show <incident-id> --database ./data/frelon.db
```

La consultation est en lecture seule : elle refuse une base absente au lieu d'en créer une nouvelle. La limite est comprise entre 1 et 500 incidents.

Les formes courtes `-o` et `-d` sont également acceptées. La commande refuse d'écraser un fichier existant et produit :

```text
out/
  incident.json
  report.md
  iocs.json
  iocs.csv      # uniquement avec --csv
```

La fixture `samples/suspicious-demo.eml` utilise uniquement le domaine réservé `.example` et peut servir de démonstration locale.

La persistance reste optionnelle et explicitement demandée par `--database`. Le MVP reste volontairement local : pas d'IMAP, pas de base distante, pas d'enrichissement réseau automatique et pas d'envoi de signalement.

## Roadmap

```text
[✓] Modèle FraudIncident
[✓] Parsing .eml minimal
[✓] Parsing MIME avec MimeKit
[✓] Analyse des headers
[✓] Extraction des URLs
[✓] IOC Url / Domain
[✓] Pièces jointes + SHA-256
[✓] IOC Hash
[✓] SHA-256 de la preuve source
[✓] Scoring local explicable
[✓] incident.json
[✓] report.md
[✓] iocs.json
[✓] Persistance locale SQLite
[✓] Consultation locale des incidents récents
[✓] Commande CLI analyze
[✓] Persistance SQLite optionnelle depuis analyze
[✓] Export CSV défensif des IOC
[✓] Première interface locale : analyse, synthèse et historique
[✓] Téléchargement des rapports depuis l'interface locale
[✓] Décisions humaines horodatées et append-only
[✓] Historique visible et état de revue des incidents
[✓] Classification prudente et explicable
[✓] Règles défensives locales et explicables
[✓] Commande CLI de consultation
[✓] Génération de signalements validés humainement
[✓] Parcours guidé d'acquisition d'un message suspect
[✓] Application locale Windows autonome
[✓] Prise en charge des courriels Outlook `.msg`
[✓] Restitution progressive : synthèse guidée et détails techniques complets
[✓] Noyau local explicable de corrélation de campagnes
[✓] Décisions humaines append-only sur les campagnes candidates
[✓] Protection des revues contre les snapshots de campagne périmés
[✓] Noyau local des takedown packs multi-destinataires
[✓] Préparation des takedown packs depuis les validations locales courantes
[✓] Service local de consultation des campagnes candidates et de leurs revues
[✓] Export d'IOC à partage contrôlé avec minimisation stricte
[✓] Préparation des exports IOC depuis les validations locales courantes
[✓] Présentation des campagnes et de leur historique dans l'application
[✓] Worker Windows à faibles privilèges et ressources bornées
[✓] Isolation réseau du worker Windows par AppContainer éphémère
```

La roadmap est volontairement progressive : Frelon préfère un petit noyau juste et testable à une grande usine qui prétend déjà tout comprendre.

## Philosophie de développement

Le projet est développé par petites missions bornées :

```text
concevoir
    ↓
spécifier
    ↓
implémenter
    ↓
tester
    ↓
relire le sens du code
    ↓
stabiliser
    ↓
continuer
```

Un build vert et des tests verts ne suffisent pas si la règle testée est elle-même mauvaise.

La revue cherche donc autant les erreurs d'implémentation que les erreurs de conception.

## Développement assisté par IA

Frelon est conçu et développé avec une assistance IA assumée et explicitement
documentée.

- **Codex** contribue de manière substantielle à l'exploration du dépôt, à
  l'architecture, à l'implémentation, aux tests, à la documentation et à la revue
  critique. Codex a notamment produit, avec le développeur, la spécification
  technique reconstruite de Frelon.
- **Le développeur humain garde la responsabilité et la décision finales** : il
  définit les objectifs, arbitre les choix, relit les changements et fait exécuter
  les compilations et les tests avant de les accepter.

Cette attribution décrit les outils réellement employés. Le projet reste
indépendant, et toute proposition produite avec une IA doit rester relisible,
testable et vérifiable.

Le projet expérimente ainsi une collaboration simple :

> **L'humain garde le cap et la responsabilité. Codex aide à explorer, construire
> et vérifier. Le code doit rester auditable.**

## Participer à la bêta

La bêta terrain est ouverte aux personnes qui peuvent consacrer 20 à 30 minutes
à un parcours guidé sous Windows. Aucune expertise en sécurité n'est exigée : la
compréhension par un utilisateur peu technique fait partie de ce que nous voulons
évaluer.

- [kit de test en 30 minutes](docs/beta-tester-kit.md) ;
- [programme complet et critères de passage à la 1.0](docs/beta-test-program.md) ;
- [plan de recrutement et messages d'invitation](docs/beta-recruitment.md).

Commencez avec le message synthétique fourni dans `samples/`. Ne joignez jamais
un courriel réel, des en-têtes complets ou une pièce jointe à une issue publique.

## Contribution

Le projet est encore jeune et son modèle évolue rapidement.

Avant toute contribution importante, garder trois règles en tête :

1. rester strictement défensif ;
2. ne pas introduire d'interaction distante implicite ;
3. préférer une petite fonctionnalité testée à une abstraction prématurée.

## Licence

Frelon est distribué sous la licence **MPL-2.0**.

Cette licence autorise l'utilisation, la modification et la redistribution du logiciel sous ses conditions, tout en conservant les mentions requises. Elle ne concède aucun droit général sur le nom ou la marque Frelon. Le texte de référence figure dans le fichier [LICENSE](LICENSE) et accompagne également chaque archive Windows officielle.

Copyright © 2026 Moralement.NET.

## Sécurité

Les vulnérabilités doivent être signalées de manière confidentielle, sans joindre
d'échantillon malveillant ni de donnée personnelle dans un ticket public. La
procédure complète est décrite dans [SECURITY.md](SECURITY.md).

Le périmètre de confiance, les menaces prises en compte et les protections
actuelles sont documentés dans
[docs/security-threat-model.md](docs/security-threat-model.md).

---

<p align="center">
  <img src="assets/mr-frelon.png" alt="Mr Frelon" width="110" />
</p>

<p align="center">
  <strong>Frelon</strong><br />
  <em>On ne pique pas les machines. On pique les campagnes.</em>
</p>
