# Kit testeur — Frelon `0.1.0-beta.1`

Merci de consacrer 20 à 30 minutes à Frelon. Le but n'est pas seulement de voir
si l'application fonctionne : nous voulons savoir si elle est compréhensible et
prudente dans les mains d'une vraie personne.

## Avant de commencer

Il vous faut :

- un ordinateur Windows 64 bits ;
- environ 30 minutes ;
- le droit d'exécuter une application autonome sur cet ordinateur ;
- de préférence le [message synthétique fourni avec le projet](../samples/suspicious-demo.eml).

N'envoyez au projet **aucun courriel réel**, corps de message, en-tête complet,
pièce jointe ou base de données. Si vous utilisez ensuite un de vos messages, il
reste sur votre poste : votre retour doit uniquement décrire ce que vous avez vu.

## 1. Télécharger et vérifier

1. Ouvrez la [page officielle de FrelonMail `0.1.0-beta.1`](https://github.com/hamidem/FrelonMail/releases/tag/v0.1.0-beta.1).
2. Téléchargez `Frelon-0.1.0-beta.1-win-x64.zip` et le fichier `.sha256` associé.
3. Dans PowerShell, depuis le dossier de téléchargement, exécutez :

```powershell
Get-FileHash .\Frelon-0.1.0-beta.1-win-x64.zip -Algorithm SHA256
```

L'empreinte attendue est :

```text
1aa347a795d6589c4431b2fea843eeeebd91851364eb88e26070862978c1f7d9
```

L'exécutable n'est pas encore signé. Windows SmartScreen peut afficher un
avertissement : l'empreinte vérifie l'intégrité du ZIP, mais ne remplace pas une
signature de l'éditeur.

## 2. Démarrer

1. Extrayez **tout** le contenu du ZIP dans un nouveau dossier.
2. Double-cliquez sur `Frelon.Web.exe`.
3. Attendez l'ouverture de l'interface locale dans votre navigateur.
4. N'ouvrez aucun lien et n'exécutez aucune pièce jointe d'un message suspect.

Fermer l'onglet ne ferme pas l'application. À la fin du test, utilisez le bouton
**Quitter Frelon**.

## 3. Réaliser les quatre missions

### Mission A — Première impression

Sans lire le reste de la documentation, expliquez en une phrase ce que vous
pensez que Frelon fait. Notez tout mot, bouton ou avertissement qui vous bloque.

### Mission B — Analyse synthétique

Importez `samples/suspicious-demo.eml`, puis répondez à ces questions :

1. Quel niveau de risque comprenez-vous ?
2. Quels éléments vous ont conduit à cette lecture ?
3. Que feriez-vous ensuite avec ce message ?
4. Si Frelon ne trouve aucun signal, cela veut-il dire que le message est sûr ?

La réponse attendue à la dernière question est **non**.

### Mission C — Décision et mémoire

1. Enregistrez une décision humaine.
2. Fermez puis relancez Frelon.
3. Retrouvez l'incident et la décision dans l'historique.

### Mission D — Dossier d'analyse

1. Téléchargez le dossier d'analyse.
2. Ouvrez son contenu sans diffuser les données.
3. Identifiez ce qui vient de l'analyse automatique et ce qui vient de la revue
   humaine.
4. Vérifiez que vous comprenez ce qui peut être transmis à un tiers.

## 4. Envoyer le retour

Utilisez le
[formulaire public « Retour de bêta »](https://github.com/hamidem/FrelonMail/issues/new?template=beta-feedback.yml)
ou copiez la fiche suivante dans un message adressé au responsable du test :

```text
Version de Frelon : 0.1.0-beta.1
Version de Windows :
Profil : utilisateur / support informatique / sécurité / autre
Missions réalisées : A / B / C / D
Ai-je eu besoin d'aide ?
Ai-je compris que l'absence de signal ne signifie pas « message sûr » ?
Résultat attendu :
Résultat observé :
Message affiché par Frelon :
Difficulté principale :
Suggestion principale :
```

Ne déposez aucune donnée personnelle ou vulnérabilité dans ce formulaire public.
Une vulnérabilité se signale uniquement par le
[canal privé de sécurité](https://github.com/hamidem/FrelonMail/security/advisories/new).

## Arrêter de participer

Il suffit de ne plus utiliser la bêta. Aucun compte Frelon n'a été créé et aucune
donnée d'analyse n'a été envoyée au projet. Vous pouvez supprimer le dossier de
l'application et, si vous le souhaitez, ses données locales après avoir fermé
Frelon.
