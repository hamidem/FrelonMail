# Programme de bêta terrain et passage à Frelon 1.0

## Objet

Ce document transforme la validation par de vrais utilisateurs en une démarche
reproductible. Il complète la [checklist de sortie V1](v1-release-checklist.md) :
la checklist décide si un candidat peut être publié, tandis que ce programme
organise la collecte des preuves nécessaires à cette décision.

Une version `1.0.0` ne signifie pas que Frelon est parfait ou terminé. Elle signifie
que son périmètre V1, ses comportements documentés et ses données sont suffisamment
stables pour devenir un contrat produit assumé.

Pour exécuter ce programme :

- remettre au participant le [kit de test en 30 minutes](beta-tester-kit.md) ;
- utiliser le [plan de recrutement et ses messages prêts à envoyer](beta-recruitment.md) ;
- recueillir les résultats avec le formulaire GitHub « Retour de bêta » ou avec
  la fiche minimale décrite plus bas.

## Principes

1. Tester des parcours, pas demander simplement « qu'en pensez-vous ? ».
2. Observer la compréhension autant que le fonctionnement technique.
3. Ne jamais interpréter l'absence de signal comme une preuve de sécurité.
4. Ne collecter aucun courriel réel, pièce jointe ou donnée personnelle par défaut.
5. Geler les fonctionnalités pendant une release candidate.
6. Conserver une trace des résultats, risques acceptés et décisions de publication.

## Ce que la bêta doit démontrer

### Utilité

- l'utilisateur comprend à quoi sert Frelon avant d'importer un message ;
- il sait récupérer un fichier EML ou MSG depuis son outil habituel ;
- le résultat l'aide à décider d'une action prudente ;
- les rapports et l'historique ont une valeur concrète pour lui.

### Compréhension

- score, piste automatique et décision humaine ne sont pas confondus ;
- `Unknown` ou l'absence de signal n'est jamais compris comme « message sûr » ;
- l'utilisateur comprend que Frelon n'ouvre pas les liens et n'exécute pas les
  pièces jointes ;
- il comprend qu'aucun signalement n'est envoyé automatiquement.

### Robustesse

- EML et MSG courants sont acceptés ou refusés proprement ;
- un fichier invalide, vide, trop volumineux ou hostile ne déstabilise pas
  l'application principale ;
- l'historique survit aux arrêts et redémarrages ;
- les exports restent cohérents avec l'incident affiché ;
- le second lancement, le changement de port et l'arrêt propre fonctionnent.

### Confiance

- les données restent locales ;
- les limitations sont visibles et comprises ;
- le paquet testé est identifiable par sa version et son empreinte ;
- le testeur sait où signaler un défaut fonctionnel ou une vulnérabilité.

## Profils de testeurs

La diversité des profils importe davantage qu'un grand nombre de participants
semblables.

| Profil | Ce qu'il permet d'évaluer |
|---|---|
| utilisateur peu technique | compréhension de la lecture guidée et autonomie |
| référent informatique ou support | installation, diagnostic et utilité opérationnelle |
| analyste sécurité | qualité des preuves, IOC, exports et limites annoncées |
| utilisateur avancé | cas inhabituels, formats, volumes et comportements inattendus |

Point de départ conseillé : 10 à 15 testeurs pour la bêta fermée, puis un groupe
plus large lorsque les principaux défauts de compréhension sont corrigés.

## Progression des versions

### 1. Bêta fermée

Les premières séances sont observées, sur place ou à distance. Le développeur
n'explique pas immédiatement l'interface : il regarde d'abord ce que l'utilisateur
comprend et tente spontanément.

Objectifs :

- découvrir les blocages de première utilisation ;
- repérer les formulations trompeuses ;
- vérifier que le parcours ne pousse jamais à une action dangereuse ;
- améliorer le kit d'accueil avant une diffusion plus large.

### 2. Bêta terrain

Les testeurs utilisent Frelon seuls pendant plusieurs semaines, avec leurs propres
messages conservés sur leur poste et sans les transmettre au projet.

Objectifs :

- rencontrer des provenances et structures de messages variées ;
- mesurer les refus, délais et échecs réels ;
- vérifier la persistance sur la durée ;
- déterminer si de nouvelles catégories majeures de problème continuent
  d'apparaître.

### 3. `1.0.0-rc.1`

Lorsque le périmètre V1 est complet, utiliser une préversion rattachée à la future
version stable, par exemple `1.0.0-rc.1`.

Pendant la RC :

- aucune nouvelle fonctionnalité ;
- uniquement corrections, documentation et durcissement ;
- test du paquet final sur des installations propres ;
- test de conservation d'une base existante ;
- nouvelle RC après toute modification significative ;
- redémarrage de la période d'observation si le contrat ou le stockage change.

### 4. `1.0.0`

Publier lorsque la dernière RC ne nécessite plus de changement structurel, que les
portes de sortie sont satisfaites et que la décision humaine est enregistrée pour
le commit exact publié.

## Missions confiées aux testeurs

Chaque séance doit utiliser un candidat identifié et produire une fiche de résultat.

### Parcours A — Première utilisation

1. Télécharger et vérifier le paquet proposé.
2. Extraire puis lancer Frelon.
3. Expliquer avec ses propres mots ce que fait l'application.
4. Trouver comment obtenir un fichier de message.
5. Importer un EML ou MSG.

À observer : demandes d'aide, avertissements Windows, vocabulaire incompris,
confiance accordée à l'application.

### Parcours B — Comprendre une analyse

1. Lire d'abord la synthèse guidée.
2. Expliquer ce que signifie le niveau de risque.
3. Ouvrir la vue analyste.
4. Distinguer observations, IOC et piste de classification.
5. Dire ce que l'utilisateur ferait ensuite.

Question de contrôle obligatoire :

> Si Frelon ne relève aucun signal, cela signifie-t-il que le message est sûr ?

La réponse attendue est non.

### Parcours C — Revue et mémoire locale

1. Enregistrer une décision humaine cohérente.
2. Fermer puis relancer Frelon.
3. Retrouver l'incident et la décision.
4. Consulter l'historique des décisions.

### Parcours D — Exports

1. Télécharger le rapport et les IOC.
2. Ouvrir le dossier ZIP.
3. Identifier la preuve, l'analyse automatique et la décision humaine.
4. Expliquer ce qui peut ou non être transmis à un tiers.

### Parcours E — Cycle de vie

1. Lancer une seconde fois l'application.
2. Tester un démarrage lorsque le port préféré est occupé.
3. Utiliser le bouton d'arrêt.
4. Vérifier que les données sont toujours présentes.

### Parcours F — Entrées difficiles

Utiliser uniquement des exemples autorisés ou synthétiques :

- fichier vide ;
- extension incorrecte ;
- EML ou MSG malformé ;
- message proche de la limite de taille ;
- pièces jointes nombreuses ou noms inhabituels ;
- message sans signal détecté.

Le résultat attendu peut être un refus : il doit être sûr, compréhensible et ne
pas rendre l'application inutilisable.

## Collecte des retours

### Fiche minimale

```text
Identifiant du testeur ou pseudonyme :
Version exacte de Frelon :
Version de Windows :
Provenance du message : Outlook / Gmail / Thunderbird / autre
Format : EML / MSG
Taille approximative :
Parcours réalisé :
Résultat attendu :
Résultat observé :
Message affiché par Frelon :
Une aide a-t-elle été nécessaire ?
Le résultat a-t-il été compris correctement ?
L'incident est-il présent après redémarrage ?
Suggestion ou difficulté principale :
```

### Données à ne pas demander par défaut

- message original ;
- corps du message ;
- adresses email réelles ;
- en-têtes complets ;
- base SQLite ;
- pièce jointe ;
- rapport contenant des données personnelles.

Si une reproduction est indispensable :

1. privilégier un exemple synthétique ;
2. sinon obtenir une autorisation explicite ;
3. anonymiser et minimiser localement ;
4. ne jamais déposer un échantillon sensible dans une issue publique ;
5. utiliser le canal privé pour toute vulnérabilité.

## Classification des problèmes

| Priorité | Définition | Conséquence |
|---|---|---|
| P0 | vulnérabilité, fuite, exécution dangereuse, corruption ou perte de données | suspendre la diffusion du candidat |
| P1 | crash fréquent, format courant inutilisable, résultat gravement trompeur | correction obligatoire avant RC/stable |
| P2 | fonction importante incorrecte avec contournement possible | corriger ou accepter explicitement le risque |
| P3 | ergonomie, formulation, finition ou amélioration future | peut être reporté |

Une incompréhension qui conduit l'utilisateur à considérer un message comme sûr
peut être P1 même si le logiciel n'a pas planté.

## Indicateurs de suivi

- nombre de parcours commencés et terminés ;
- taux d'analyse réussie par format et provenance ;
- refus, timeout et crash par version ;
- temps d'analyse perçu ;
- utilisation sans aide du premier parcours ;
- compréhension correcte du caractère non conclusif de l'analyse ;
- incidents et décisions retrouvés après redémarrage ;
- problèmes nouveaux par semaine et par catégorie ;
- P0, P1, P2 et P3 ouverts/fermés ;
- retours qui demandent une rupture de contrat plutôt qu'une correction.

Les retours sur les faux positifs sont utiles. Les faux négatifs doivent être
évalués sur un corpus dont le résultat attendu a été établi par une personne
compétente : un utilisateur ne sait pas nécessairement qu'un signal a été manqué.

## Portes de sortie vers `1.0.0`

### Fonctionnelles

- tous les parcours du périmètre V1 réussissent sur le paquet final ;
- EML et MSG ont été exercés avec plusieurs provenances ;
- aucune assistance répétée n'est nécessaire pour le parcours principal ;
- les exports et l'historique restent cohérents.

### Compréhension

- aucun testeur ne confond durablement absence de signal et sécurité ;
- score, piste automatique et décision humaine sont distingués ;
- les limites et le fonctionnement local sont compris.

### Qualité et sécurité

- aucun P0 ou P1 ouvert ;
- aucun risque critique ou élevé connu sans mitigation documentée ;
- tests automatisés, audit et tests Windows verts ;
- aucune perte de données observée ;
- procédure de remplacement et de révocation vérifiée.

### Stabilité du contrat

Les responsables ont explicitement décidé ce qui doit rester compatible :

- commandes et codes de sortie de la CLI ;
- formats JSON, CSV et Markdown ;
- comportement de la base locale entre versions ;
- routes de l'API locale considérées publiques ou internes ;
- sémantique des scores, pistes et décisions humaines.

### Saturation

Les nouveaux tests ne révèlent plus de catégorie majeure de problème. Les retours
restants concernent principalement des améliorations compatibles ou le périmètre
d'une version future.

Un point de départ raisonnable est 30 à 50 parcours complets, répartis entre les
profils, formats et provenances. Ce nombre n'est pas une preuve statistique : la
diversité et la saturation priment.

## Registre de décision `1.0.0`

À conserver avec les éléments de release :

```text
Version candidate :
Commit :
Date de décision :
Participants à la décision :
Nombre de testeurs :
Nombre de parcours terminés :
Environnements couverts :
P0 ouverts :
P1 ouverts :
P2 acceptés et justification :
Contrats publics figés :
Limitations publiées :
Résultat de la checklist V1 :
Décision : publier / nouvelle RC / suspendre
Justification :
```

La publication reste une décision humaine. Les indicateurs aident à la prendre ;
ils ne la prennent pas automatiquement.

## Proposer Frelon sans le survendre

### Promesse courte

> Frelon aide à examiner localement un fichier de message suspect, à en extraire
> des éléments techniques compréhensibles et à conserver une analyse, sans ouvrir
> ses liens ni exécuter ses pièces jointes.

Éviter les formulations telles que « Frelon garantit qu'un message est sûr »,
« détecte toutes les fraudes » ou « remplace un antivirus ».

### Invitation type

```text
Je cherche quelques personnes pour tester Frelon, une application Windows locale
qui aide à examiner des courriels suspects à partir d'un fichier EML ou MSG.

La bêta ne transmet pas le message, n'ouvre aucun lien et n'exécute aucune pièce
jointe. Le but du test est autant de vérifier la compréhension de l'interface que
la robustesse de l'analyse.

Le test prend environ 20 à 30 minutes. Aucun courriel réel ne sera demandé : les
retours portent sur le parcours, les messages affichés et les éventuels problèmes.
```

### Kit à fournir

- page de présentation concise ;
- avertissement clair sur le statut bêta ;
- paquet versionné et empreinte SHA-256 ;
- guide de démarrage en une page ;
- message synthétique de démonstration ;
- liste des missions ci-dessus ;
- formulaire de retour structuré ;
- procédure privée de signalement de vulnérabilité ;
- moyen simple de se retirer du programme.
