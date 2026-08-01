# Checklist de sortie V1

Cette checklist fige le périmètre fonctionnel de la première version de Frelon.
Une case bloquante non validée interdit la publication, même si le build est vert.
Les essais avec de vrais utilisateurs et les preuves attendues sont organisés par
le [programme de bêta terrain](beta-test-program.md).

## Périmètre fonctionnel gelé

- [ ] import local d'un fichier EML ou MSG ;
- [ ] analyse hors ligne et explicable ;
- [ ] synthèse guidée sans suppression des détails techniques ;
- [ ] historique et décisions humaines locales ;
- [ ] export du dossier d'analyse ;
- [ ] application Windows autonome avec démarrage et arrêt explicites.

Une nouvelle intégration distante, un enrichissement réseau, un compte utilisateur
ou un envoi automatique ne fait pas partie de cette V1.

## Contrôles bloquants

- [ ] la version affichée par l'application correspond exactement au tag prévu ;
- [ ] restauration verrouillée, build Release et totalité des tests réussis ;
- [ ] audit NuGet sans vulnérabilité haute ou critique ;
- [ ] tests Windows du worker restreint, borné et sans capability réseau réussis ;
- [ ] ZIP Windows produit par GitHub et scénario d'analyse empaqueté réussi ;
- [ ] `LICENSE.txt`, `LISEZ-MOI.txt`, `THIRD-PARTY-NOTICES.txt` et
  `DOTNET-THIRD-PARTY-NOTICES.txt` présents dans le ZIP ;
- [ ] empreinte SHA-256 du ZIP vérifiée ;
- [ ] EML et MSG analysés manuellement depuis l'interface ;
- [ ] rapport téléchargé et relu ;
- [ ] historique retrouvé après redémarrage ;
- [ ] bouton « Quitter Frelon », second lancement et port occupé vérifiés ;
- [ ] aucune donnée utilisateur réelle dans le dépôt, les logs ou les fixtures ;
- [ ] avertissement SmartScreen clairement annoncé tant que le binaire est non signé ;
- [ ] brouillon technique du dépôt privé relu avant transfert ;
- [ ] release publique créée dans `hamidem/FrelonMail` avec les deux actifs vérifiés.

## Contrôles de confiance

- [ ] authentification multifacteur active sur le compte de publication ;
- [ ] protections de `master` et revue par PR actives ;
- [ ] signalement privé de vulnérabilité GitHub disponible ;
- [ ] CodeQL actif ou limitation documentée ;
- [ ] source canonique de téléchargement limitée aux
  [GitHub Releases publiques de FrelonMail](https://github.com/hamidem/FrelonMail/releases) ;
- [ ] procédure de remplacement et de révocation relue.

## Non-bloquants explicitement reportés

- signature de code, tant que SmartScreen est annoncé sans ambiguïté ;
- élargissement du corpus réel autorisé ;
- fuzzing guidé par couverture, après résolution de sa compatibilité .NET 10 ;
- enrichissements réseau ou automatisation de signalements ;
- raffinements d'interface qui ne corrigent pas un défaut de compréhension critique.

La décision finale de publication reste humaine. Le workflow prépare un brouillon :
il ne rend jamais une release publique automatiquement.
