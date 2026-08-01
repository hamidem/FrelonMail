# Frelon — Instructions générales pour Copilot

## Identité du projet

Frelon est un outil défensif d’analyse de mails frauduleux.

Son objectif est de transformer des emails suspects ou frauduleux en :

* preuves structurées ;
* indicateurs de compromission ;
* rapports lisibles ;
* règles défensives locales ;
* brouillons de signalement validables humainement.

Frelon ne doit pas être conçu comme un outil de riposte, d’attaque ou de perturbation.

Devise interne du projet :

> Frelon ne pique pas les machines.  
> Il pique les campagnes.

## Ligne éthique et sécurité

Frelon doit rester strictement défensif, local-first et légalement propre.

Le projet ne doit jamais :

* attaquer, scanner, ralentir, exploiter ou perturber un système distant ;
* répondre automatiquement à un expéditeur ;
* envoyer automatiquement des emails ;
* envoyer automatiquement des signalements sans validation humaine ;
* exécuter une pièce jointe ;
* ouvrir automatiquement une URL suspecte dans un navigateur réel ;
* soumettre de fausses données à un formulaire frauduleux ;
* interagir activement avec une infrastructure suspecte dans le MVP ;
* exposer des données personnelles dans un export public ou partageable.

Toute fonctionnalité ambiguë doit être conçue en mode sûr par défaut.

## Objectif MVP

Le premier MVP doit fonctionner uniquement à partir d’un fichier `.eml` local.

Commande cible :
frelon analyze suspicious.eml --output ./out
Sorties attendues à terme :
out/
  incident.json
  report.md
  iocs.json
Le MVP initial ne doit pas inclure :

* IMAP ;
* dashboard ;
* enrichissement réseau ;
* RDAP / WHOIS ;
* consultation automatique d’URLs ;
* envoi d’email ;
* API distante ;
* base communautaire.

## Stack technique

* Langage principal : C#
* Plateforme : .NET
* IDE cible : Visual Studio
* Tests : xUnit
* Nullable reference types activés
* Architecture modulaire
* Modèle métier fortement typé
* Préférer les records et types immuables lorsque c’est pertinent

## Structure de solution attendue
src/
  Frelon.Core/
  Frelon.Mail/
  Frelon.Reports/
  Frelon.Exporters/
  Frelon.Cli/

tests/
  Frelon.Core.Tests/
  Frelon.Mail.Tests/
  Frelon.Reports.Tests/
## Responsabilités des projets

### Frelon.Core

Contient le modèle métier pur et la logique métier indépendante de l’infrastructure.

Ce projet ne doit pas dépendre :

* du système de fichiers ;
* du réseau ;
* d’un parser MIME spécifique ;
* du CLI ;
* de l’interface utilisateur ;
* de bibliothèques externes non indispensables.

Il peut contenir :

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
* règles métier simples
* value objects
* enums

### Frelon.Mail

Contient les fonctionnalités liées à l’analyse d’emails :

* lecture de fichiers `.eml` ;
* parsing MIME ;
* extraction des headers ;
* extraction des URLs ;
* extraction des pièces jointes ;
* calcul de hashes ;
* construction progressive d’un incident.

Ce projet peut dépendre de `Frelon.Core`.

Il ne doit jamais :

* exécuter une pièce jointe ;
* ouvrir une URL suspecte ;
* envoyer un email ;
* interagir avec un serveur distant dans le MVP.

### Frelon.Reports

Contient la génération de rapports :

* Markdown ;
* JSON lisible ;
* rapports humains ;
* futurs brouillons de signalement.

Ce projet peut dépendre de `Frelon.Core`.

### Frelon.Exporters

Contient les futurs exports défensifs :

* IOC ;
* CSV ;
* règles SpamAssassin ;
* règles Rspamd ;
* règles Sieve ;
* formats de partage anonymisé.

Ce projet peut dépendre de `Frelon.Core`.

### Frelon.Cli

Contient l’interface ligne de commande.

Ce projet orchestre les autres modules mais ne doit pas contenir de logique métier profonde.

## Règles d’architecture

Respecter une séparation stricte :
Core
→ modèle métier et règles métier

Mail
→ extraction et analyse technique des emails

Reports
→ génération de sorties lisibles

Exporters
→ formats défensifs externes

Cli
→ orchestration utilisateur
Éviter les dépendances inverses.

`Frelon.Core` doit rester le projet le plus stable et le plus indépendant.

## Style de code

Utiliser :

* C# moderne ;
* noms explicites ;
* types immuables lorsque possible ;
* `required` et `init` quand cela améliore la lisibilité ;
* collections en lecture seule dans le modèle public ;
* exceptions seulement quand elles expriment une vraie erreur ;
* résultats typés quand cela rend le flux plus clair ;
* tests unitaires pour toute logique métier.
* commentaires XML (`<summary>`) en français, concis, sur toutes les classes, interfaces et membres publics.

Éviter :

* les classes fourre-tout ;
* les helpers globaux prématurés ;
* les dépendances NuGet inutiles ;
* le code réseau implicite ;
* les effets de bord cachés ;
* les services trop génériques ;
* les abstractions avant qu’elles soient nécessaires.

## Règles pour Copilot

Avant de modifier le code, respecter strictement la mission fournie.

Ne pas :

* créer de nouveaux projets sans demande explicite ;
* ajouter de package NuGet sans demande explicite ;
* déplacer des fichiers sans demande explicite ;
* modifier plusieurs modules si la mission cible un seul projet ;
* inventer une architecture alternative ;
* ajouter un dashboard ;
* ajouter une base de données ;
* ajouter IMAP ;
* ajouter un service réseau ;
* ajouter un système d’envoi d’email ;
* ajouter une fonctionnalité offensive.

Ne pas inventer de types ou helpers inexistants dans les tests ; utiliser uniquement des éléments réellement présents dans le codebase ou des helpers définis explicitement dans le fichier modifié.

Si une mission est ambiguë, choisir l’option la plus simple, locale, testable et défensive.

## Philosophie de progression

Le projet avance par petites missions contrôlées.

Chaque mission doit avoir :

* un objectif clair ;
* un périmètre limité ;
* des fichiers autorisés ;
* des contraintes ;
* des critères d’acceptation ;
* idéalement des tests.

Ne pas chercher à implémenter Frelon complet en une seule étape.

## Priorité actuelle

Priorité actuelle du projet :

1. stabiliser `Frelon.Core` ;
2. définir le modèle `FraudIncident` ;
3. ajouter des tests métier ;
4. lire un `.eml` local ;
5. produire `incident.json` ;
6. produire `report.md` ;
7. extraire URLs et pièces jointes ;
8. ajouter le scoring ;
9. ajouter les exports défensifs.
