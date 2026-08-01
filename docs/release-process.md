# Publication, remplacement et révocation

## Principe

Une release Frelon provient uniquement d'un tag SemVer `v<version>` appartenant à
`master`. GitHub Actions reconstruit le paquet depuis ce tag, rejoue les tests
Windows et l'analyse empaquetée, vérifie les actifs, puis crée une **release
technique en brouillon dans le dépôt privé**. Après validation humaine, les deux
actifs sont publiés dans le dépôt public de distribution
[`hamidem/FrelonMail`](https://github.com/hamidem/FrelonMail).

Le workflow refuse de fabriquer silencieusement une release depuis une branche de
travail. Il refuse aussi un tag qui ne correspond pas à la version du paquet.

## Préparer un candidat

1. Geler les fonctionnalités et remplir
   [la checklist V1](v1-release-checklist.md).
2. Exécuter le [programme de bêta terrain](beta-test-program.md) et conserver le
   registre de décision du candidat.
3. Mettre à jour `VersionPrefix` et `VersionSuffix` dans
   `src/Frelon.Web/Frelon.Web.csproj`.
4. Faire valider la modification sur `dev`.
5. Fusionner `dev` dans `master` par PR.
6. Attendre les contrôles verts sur `master`.
7. Vérifier localement que le commit visé est bien celui de `origin/master`.

La première diffusion publique prudente peut conserver un suffixe de préversion,
par exemple `0.1.0-beta.1`. Le passage à `1.0.0` signifie que le contrat V1 est
considéré stable ; il ne doit pas servir uniquement à obtenir un numéro plus
valorisant.

## Créer le tag

Depuis un `master` propre et synchronisé :

```powershell
git switch master
git pull --ff-only origin master
git tag -a v0.1.0-beta.1 -m "Frelon 0.1.0-beta.1"
git push origin v0.1.0-beta.1
```

Adapter le numéro à la version réellement inscrite dans le projet. Ne jamais
déplacer ni réutiliser un tag publié.

## Vérifier le brouillon

Le workflow `Package Windows` :

1. restaure les dépendances verrouillées et bloque toute vulnérabilité connue
   haute ou critique, y compris transitive ;
2. vérifie l'isolation du worker Windows ;
3. publie l'application autonome ;
4. analyse le message synthétique empaqueté ;
5. fabrique le ZIP et son `.sha256` ;
6. exige la licence Frelon, les instructions utilisateur et les notices des
   composants tiers, y compris celles du runtime .NET autoporté ;
7. vérifie la cohérence du ZIP et de son empreinte ;
8. crée ou met à jour la release technique en brouillon dans le dépôt privé.

Avant de transférer les actifs vers le dépôt public :

- télécharger les deux actifs depuis GitHub ;
- recalculer l'empreinte avec `Get-FileHash` ;
- extraire le ZIP dans un dossier neuf ;
- exécuter les contrôles manuels de la checklist ;
- relire les notes générées et les limites annoncées ;
- confirmer le statut de préversion lorsqu'un suffixe est présent.

## Publier dans le dépôt de distribution

Le dépôt source reste privé. La release destinée aux utilisateurs est créée dans
[`hamidem/FrelonMail`](https://github.com/hamidem/FrelonMail/releases) avec le
même tag et contient uniquement :

- le ZIP Windows validé ;
- le fichier `.sha256` produit avec ce ZIP ;
- les notes de version, limites connues et instructions de vérification.

Après publication :

1. vérifier que la release n'est plus en brouillon et porte le bon statut de
   préversion ;
2. vérifier sans authentification que la page, le ZIP et le `.sha256` répondent ;
3. comparer l'empreinte déclarée par GitHub, le contenu du `.sha256` et le ZIP
   validé dans le dépôt privé ;
4. mettre à jour `frelonmail.fr` uniquement après ces contrôles.

Le tag du dépôt public marque les métadonnées de distribution. La correspondance
avec le commit source exact reste enregistrée dans le registre privé de release.

## Remplacer une release défectueuse

Une release publiée est immuable du point de vue de Frelon :

- ne jamais remplacer son ZIP ou son fichier SHA-256 ;
- ne jamais déplacer son tag ;
- corriger le défaut sur `dev`, valider, fusionner dans `master` ;
- incrémenter la version et publier un nouveau tag ;
- ajouter dans les notes de l'ancienne release un avertissement visible et un lien
  vers la version corrigée.

Si GitHub permet l'activation des releases immuables pour le dépôt, l'activer avant
la première diffusion publique.

## Révoquer en urgence

En cas de paquet compromis ou de vulnérabilité grave :

1. dépublier ou retirer immédiatement les actifs dangereux ;
2. conserver une trace publique minimale indiquant la version retirée et la date ;
3. ouvrir un GitHub Security Advisory privé pour coordonner la correction ;
4. révoquer le certificat de signature s'il est concerné ;
5. publier une nouvelle version et un nouveau tag, jamais une reconstruction sous
   l'ancien numéro ;
6. documenter l'impact, les versions touchées et l'action attendue des utilisateurs.

La priorité est d'empêcher un nouveau téléchargement dangereux. La conservation
des preuves techniques s'effectue dans le canal privé de sécurité, sans exposer de
donnée personnelle ni d'échantillon hostile.
