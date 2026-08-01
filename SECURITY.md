# Politique de sécurité

La sécurité de Frelon est une fonction du produit. Le logiciel analyse des fichiers
entièrement contrôlés par des tiers potentiellement hostiles ; toute faiblesse
affectant les parseurs, l'application locale, le stockage ou la distribution doit
donc être signalée de façon responsable.

## Versions prises en charge

| Version | Prise en charge |
|:---|:---:|
| `0.1.x` bêta la plus récente | Oui |
| Versions antérieures | Non |

Avant la première version stable, seul le dernier paquet bêta publié reçoit des
correctifs de sécurité.

## Signaler une vulnérabilité

Utilisez de préférence le
[signalement privé GitHub](https://github.com/hamidem/Frelon/security/advisories/new).
Ne publiez pas de vulnérabilité, de preuve de concept active, de donnée personnelle
ou de fichier malveillant dans une issue publique.

Si le formulaire privé n'est pas disponible, ouvrez uniquement une issue demandant
un canal de contact sécurisé, sans révéler les détails techniques.

Le rapport devrait indiquer :

- la version de Frelon concernée ;
- le composant et le scénario observé ;
- l'impact estimé ;
- les étapes minimales de reproduction ;
- l'empreinte SHA-256 de tout échantillon utile.

N'envoyez aucun courriel réel ni échantillon dangereux avant d'avoir convenu d'un
canal et de conditions de manipulation adaptés.

## Délais visés

- accusé de réception sous trois jours ouvrés ;
- première qualification sous dix jours ouvrés ;
- information régulière jusqu'au correctif ou au classement du rapport.

Ces délais sont des objectifs de bonne foi pour un projet maintenu à titre
indépendant, pas une garantie contractuelle.

## Périmètre prioritaire

Sont notamment concernés :

- contournement des limites de taille ou de type de fichier ;
- plantage, épuisement de ressources ou exécution de code pendant l'analyse ;
- lecture ou modification distante des données locales ;
- injection de contenu actif dans l'interface ou les exports ;
- compromission de la chaîne de compilation ou du paquet officiel ;
- fuite implicite d'un courriel, d'un IOC ou d'une donnée de revue.

Les erreurs de détection sans conséquence de sécurité peuvent être signalées comme
des anomalies ordinaires après suppression de toute donnée personnelle.
