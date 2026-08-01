# Modèle de menace de Frelon

## Objectif

Frelon examine localement des courriels non fiables sans ouvrir leurs liens ni
transmettre leur contenu. Le modèle de menace privilégie la protection du poste,
des preuves locales et de la chaîne de distribution avant la richesse de
l'analyse.

## Actifs à protéger

- le poste et le compte Windows de l'utilisateur ;
- les métadonnées et indicateurs issus des courriels ;
- la base SQLite et les décisions humaines ;
- l'intégrité de l'exécutable et des mises à jour ;
- l'identité publique de Frelon et de Moralement.NET ;
- le dépôt, les secrets et les workflows de publication.

## Frontières de confiance

1. Le fichier EML ou MSG est entièrement hostile.
2. Le navigateur est local mais peut aussi afficher des sites hostiles.
3. Un autre processus du même compte Windows n'est pas considéré comme fiable.
4. Les dépendances NuGet et GitHub Actions appartiennent à la chaîne
   d'approvisionnement.
5. Le dépôt GitHub et l'identité de signature deviennent des actifs critiques dès
   la première diffusion publique.

## Menaces prioritaires

### T1 — Exploitation d'un parseur

Un message malformé cherche à provoquer un plantage, une consommation excessive de
ressources ou l'exploitation d'une dépendance.

Contrôles actuels :

- formats EML et MSG seulement ;
- limite centrale de 25 Mo appliquée par les parseurs, le Web et la CLI ;
- quotas de profondeur MIME, d'en-têtes, de corps et de pièces jointes décodées ;
- arrêt du décodage d'une pièce jointe dès que son quota est atteint ;
- analyse complète dans un processus jetable distinct du serveur local ;
- quota de 30 secondes garanti par arrêt forcé de l'arbre du processus d'analyse ;
- protocole inter-processus borné, sans contenu hostile dans les arguments ;
- résultat structuré borné ; aucun détail interne du worker n'est exposé ;
- sous Windows, création du worker avec un jeton restreint, ses privilèges
  désactivés au maximum, le mode LUA et un niveau d'intégrité bas vérifié ;
- sous Windows, création suspendue du worker puis affectation vérifiée à un
  Job Object limité à un processus et 256 Mio de mémoire engagée ;
- sous Windows, exécution dans un AppContainer éphémère sans aucune capability,
  notamment sans capability réseau ;
- droit temporaire de lecture et d'exécution accordé à cet AppContainer sur le
  seul dossier du code du worker, puis retiré à la fin de l'analyse ;
- message source transmis au worker uniquement par le canal inter-processus :
  l'AppContainer ne reçoit aucun droit sur la preuve, la base ou les rapports ;
- fermeture du Job Object entraînant l'arrêt de tout processus encore associé ;
- refus de lancer l'analyse plutôt que repli silencieux vers les droits complets
  si Windows ne peut pas appliquer ou vérifier ces restrictions ;
- héritage limité aux trois canaux anonymes nécessaires au protocole ;
- aucune ouverture automatique de lien ou de pièce jointe ;
- tests de parsing et d'entrées hostiles synthétiques ;
- premier corpus externe versionné, attribué et vérifié par SHA-256, sans
  donnée personnelle ;
- corruptions MSG déterministes dérivées d'un message synthétique local.
- campagne de mutations reproductible à chaque PR, étendue après fusion sur les
  branches partagées et exécutée quotidiennement sur la branche par défaut.

Travaux restants avant production :

- élargissement progressif du corpus avec des cas réels dont la redistribution
  et l'anonymisation sont explicitement autorisées ;
- fuzzing guidé par couverture et minimisation automatique des nouveaux cas.

Le processus jetable apporte une frontière de panne et de temps. Sous Windows, le
jeton restreint et l'intégrité basse réduisent aussi fortement ses possibilités
d'écriture ou d'action privilégiée. Le Job Object borne sa mémoire engagée, empêche
la coexistence d'un processus descendant et garantit l'arrêt du groupe à la
fermeture de son dernier handle. L'AppContainer sans capability ajoute une frontière
Windows qui ne lui accorde pas d'accès réseau. Son identité et son profil sont
propres à une seule analyse puis supprimés ; un profil qu'un arrêt brutal laisserait
verrouillé ne serait jamais réutilisé. Cette protection dépend des garanties
AppContainer du système et ne prétend pas protéger un poste déjà compromis ou
modifié par un administrateur. Sur les autres systèmes, utilisés pour le
développement et les tests, le worker conserve les droits du processus parent.

### T2 — Contournement de la détection

Le message évite les IOC réutilisables ou place l'information dans une image, un QR
code, une archive protégée ou une destination dynamique.

Réponse :

- ne jamais présenter l'absence de signal comme une preuve de sécurité ;
- conserver une restitution explicable et une validation humaine ;
- ajouter uniquement des extracteurs locaux et bornés ;
- mesurer les faux négatifs sur un corpus autorisé.

### T3 — Attaque du serveur local

Une page distante ou un nom DNS contrôlé tente d'atteindre les API de boucle locale.

Réponse :

- écoute Kestrel limitée à la boucle locale ;
- validation stricte de l'adresse distante, de `Host`, de `Origin` et de
  `Sec-Fetch-Site` ;
- refus du framing et politique CSP restrictive ;
- jeton aléatoire pour l'arrêt de l'application ;
- aucun CORS et aucune interface réseau externe.

### T4 — Altération de la chaîne d'approvisionnement

Une dépendance ou une Action compromise modifie le build ou le paquet.

Réponse :

- versions et graphes NuGet verrouillés ;
- audit NuGet et rejet des vulnérabilités hautes ou critiques ;
- Actions épinglées sur des commits vérifiés ;
- permissions minimales du jeton GitHub ;
- workflow CodeQL prêt, actif sur un dépôt public ou, après activation de GitHub
  Code Security sur le dépôt privé, avec la variable
  `FRELON_CODEQL_ENABLED=true` ;
- mises à jour Dependabot ;
- paquet versionné accompagné d'une empreinte SHA-256.

### T5 — Usurpation de la distribution

Un tiers diffuse un faux Frelon ou compromet le compte de publication.

Réponse :

- source de téléchargement canonique unique ;
- releases GitHub traçables ;
- authentification forte et protection des branches ;
- signature de code stable avant diffusion large ;
- Microsoft Store ou WinGet après stabilisation.

## Hors périmètre

Frelon ne protège pas un poste déjà compromis avec les mêmes privilèges que
l'utilisateur. Il ne visite pas les destinations, ne déchiffre pas les archives
protégées et ne remplace ni un antivirus ni une analyse dynamique.

## Critères de sortie de bêta

- aucun risque critique ou élevé connu sans mitigation documentée ;
- chaîne CI reproductible et analyse de dépendances active ;
- canal privé de vulnérabilité opérationnel ;
- paquet signé ou distribution expliquant clairement SmartScreen ;
- tests hostiles des parseurs et limites de ressources validés ;
- procédure de révocation et de remplacement d'une release documentée et relue.
