# Corpus défensif des parseurs

Ce répertoire contient uniquement des preuves de régression autorisées,
anonymisées ou synthétiques. Il ne doit jamais recevoir un courriel utilisateur,
un secret, une adresse personnelle ou une pièce jointe active.

## Corpus externe EML

Les trois fichiers proviennent du dépôt MIT
[`jstedfast/MimeKit`](https://github.com/jstedfast/MimeKit), révision
`cf6d38dd0a1a26b5145c10b35e971efc133e1fb3`. Ils sont conservés octet pour
octet et renommés avec l'extension réellement présentée à Frelon.

| Fichier local | Chemin amont | SHA-256 local |
|---|---|---|
| `mimekit-empty-multipart.eml` | `UnitTests/TestData/messages/empty-multipart.txt` | `075b14b04c98f9b9d81370ca6a4cc73491ecf18ae374e5a637c7dbb28c115c6f` |
| `mimekit-missing-subtype.eml` | `UnitTests/TestData/messages/missing-subtype.txt` | `5226395ab60ce2f9dfeed86695d114d3219bd5054ed689b61623b5224a871929` |
| `mimekit-long-address-list.eml` | `UnitTests/TestData/messages/stack-overflow.txt` | `bfd547668029267401f2dcf6228c20d1193cdbc8b91cf73641146365c8bf8008` |

Ils couvrent respectivement un multipart vide et incohérent, un type MIME sans
sous-type conforme, et une liste d'adresses anormalement longue associée à une
ancienne régression de débordement de pile.

## MSG

Aucun MSG externe examiné n'a été retenu : les fixtures disponibles contenaient
des identités et des adresses réelles. Les tests MSG partent donc d'un message
entièrement synthétique généré localement, puis appliquent des troncatures et
corruptions déterministes. Cette décision évite de transformer une exigence de
sécurité en risque de confidentialité.

## Règles d'ajout

Tout nouveau fichier doit :

1. avoir une licence autorisant explicitement sa redistribution ;
2. être exempt de donnée personnelle et de secret ;
3. être utile à une classe de panne documentée ;
4. rester nettement sous la limite produit de 25 Mo ;
5. être accompagné de sa provenance, de sa révision et de son SHA-256 ;
6. être marqué `-text` s'il doit rester identique octet pour octet.

La licence des fixtures tierces est reproduite dans
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

## Campagne de mutations

`ParserMutationFuzzTests` applique aux graines EML et MSG des mutations
déterministes et bornées : inversion de bits, remplissage, troncature,
suppression, duplication, insertion et écrasement par des jetons hostiles.

La campagne courte de 128 cas fait partie des tests ordinaires. Le workflow
`Parser fuzzing` exécute 2 000 cas après une modification pertinente fusionnée
sur `dev` ou `master`, et 5 000 cas chaque jour sur la branche par défaut.

Une panne indique la graine et le numéro exact du cas. Pour la rejouer localement
dans PowerShell :

```powershell
$env:FRELON_FUZZ_SEED = "1179796812"
$env:FRELON_FUZZ_CASE = "<numero>"
dotnet test tests/Frelon.Mail.Tests/Frelon.Mail.Tests.csproj `
  --configuration Release `
  --filter "FullyQualifiedName~ParserMutationFuzzTests.CampagneDeMutations"
```

Le moteur pseudo-aléatoire est implémenté dans le test et stable par cas : le
résultat ne dépend ni de l'ordre d'exécution ni de l'algorithme interne d'une
version donnée de .NET.
