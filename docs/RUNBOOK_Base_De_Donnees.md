# Runbook — base de données : migrations, sauvegardes, restauration

*Comment le schéma avance, ce qui est sauvegardé, et comment on restaure — avec le temps que ça a réellement pris.*

Ce document couvre les trois points bloquants du lot 1 (§4.1 à §4.3 du plan de développement). Il est destiné à être lu **avant** un incident, pas pendant.

---

## 1. Migrations : appliquées au déploiement, jamais par l'application

### Pourquoi

L'application n'applique plus les migrations au démarrage en dehors du poste de développement. Sur une seule instance, `MigrateAsync()` au démarrage fonctionne ; dès la deuxième — une montée en charge, ou simplement le recouvrement entre l'ancienne et la nouvelle instance pendant un déploiement — deux processus écrivent dans `__EFMigrationsHistory` en même temps. Le résultat va de l'erreur bruyante au schéma à moitié migré. Sur une base partagée par toutes les agences, ce n'est pas un client qui tombe, c'est tout le monde.

### Ce qui se passe maintenant au démarrage

Réglé par la section `Database` de la configuration (voir `DatabaseOptions`) :

| Réglage | Défaut | Effet |
|---|---|---|
| `Database:MigrateOnStartup` | *non défini* → vrai en Development uniquement | Applique les migrations en attente |
| `Database:FailOnPendingMigrations` | `true` | Si la base est en retard sur le code, l'application **refuse de démarrer** |
| `Database:SeedOnStartup` | `true` | Insère les données de référence (rôles, pays, formules, administrateur plateforme), sous verrou applicatif pour que deux instances ne se marchent pas dessus |
| `Database:PlatformAdminEmail` | `platformadmin@localhost` | Identifiant du compte d'amorçage |
| `Database:PlatformAdminPassword` | *aucun* | Sans valeur hors Development, **aucun compte n'est créé** (un mot de passe en dur dans le binaire serait une porte dérobée publiée) |

Le refus de démarrer est volontaire : servir des requêtes contre un schéma auquel il manque les colonnes que le code lit produit des erreurs indéchiffrables de l'extérieur. Un déploiement qui a sauté son étape de migration doit échouer à la porte.

### Ce que fait le déploiement

`.github/workflows/azure-dev.yml` construit un *bundle* de migrations et l'applique **avant** `azd deploy`, en un seul processus :

```bash
dotnet ef migrations bundle \
  --project src/Infrastructure/Infrastructure.csproj \
  --startup-project src/Web/Web.csproj \
  --configuration Release \
  --self-contained -r linux-x64 \
  --output efbundle --force

./efbundle --connection "<chaîne de connexion>"
```

Le bundle est un exécutable autonome : pas de SDK ni de sources sur la machine qui l'exécute. Le relancer est sans effet (il ne rejoue pas ce qui est déjà appliqué), donc réexécuter le workflow est sans danger.

> **Contrainte qui découle de l'ordre.** Les migrations passent **avant** le nouveau code, donc pendant quelques minutes l'ancienne version tourne sur le nouveau schéma. Toute migration doit donc être **additive** : on ajoute une colonne dans une version, on arrête de l'écrire dans la suivante, on la supprime dans une troisième. Supprimer ou renommer une colonne dans la même livraison que le code qui cesse de l'utiliser casse l'instance encore en service.

### À la main (environnement de recette, incident)

```powershell
# Depuis la racine du dépôt
dotnet ef migrations bundle --project src/Infrastructure/Infrastructure.csproj `
  --startup-project src/Web/Web.csproj --output efbundle.exe --force
./efbundle.exe --connection "Server=...;Database=...;User ID=...;Password=..."
```

Pour voir ce qui serait appliqué sans rien écrire :

```powershell
dotnet ef migrations list --project src/Infrastructure/Infrastructure.csproj `
  --startup-project src/Web/Web.csproj
```

### Secrets nécessaires au premier déploiement

L'application démarre en `Production` (et non plus en `Development`, ce qui chargeait la clé de signature JWT de développement et activait le jeu de données de démonstration). Elle a donc besoin, dans Key Vault :

| Secret | Pourquoi |
|---|---|
| `ConnectionStrings--RemSolutionDb` | créé par le module Bicep de la base |
| `Jwt--SigningKey` | l'hôte refuse de démarrer sans (une clé de développement publiée permettrait à quiconque d'émettre des jetons) |
| `Database--PlatformAdminPassword` | uniquement pour le tout premier démarrage : se connecter, changer le mot de passe, retirer le secret |
| `Email--Password` | si l'envoi de courriels est configuré (sinon les messages sont seulement journalisés) |

---

## 2. Sauvegardes

### Base de données — configuré dans `infra/core/database/sqlserver/sqlserver.bicep`

| | Réglage | Fenêtre |
|---|---|---|
| Restauration dans le temps (PITR) | `retentionDays: 35` | 35 jours, à la seconde près |
| Sauvegardes différentielles | toutes les 12 h | réduit la durée de restauration |
| Rétention hebdomadaire | `P12W` | 12 semaines |
| Rétention mensuelle | `P12M` | 12 mois |
| Rétention annuelle | `P5Y` | 5 ans (semaine 1) |

Le niveau de service est lui aussi posé explicitement (`databaseSkuName`, défaut `S0`) : le niveau Basic plafonne la restauration dans le temps à 7 jours, ce qui rendrait la rétention ci-dessus silencieusement invalide. `S0` est un point de départ — **à dimensionner sur la charge réelle avant l'ouverture à des clients**, ce n'est pas une recommandation de capacité.

Les sauvegardes automatiques d'Azure SQL sont actives par défaut, mais avec **7 jours** de rétention et rien au-delà. Les deux valeurs sont désormais posées explicitement : sur une base partagée par toutes les agences, « jusqu'où peut-on remonter » n'est pas un réglage qu'on hérite en silence.

### Fichiers téléversés (pièces d'identité, contrats, photos)

Ils sont écrits sur disque par `LocalFileStorage`, sous le chemin `FileStorage:RootPath`.

**Deux corrections ont été apportées ici, et une décision reste à prendre.**

1. **Corrigé — ils ne sont plus dans `wwwroot`.** Le déploiement remplace le contenu publié en entier : tout fichier stocké sous `wwwroot/uploads` était détruit à chaque mise en production. Le chemin est maintenant `/home/data/uploads` (partage persistant du service applicatif), et l'application sert ce dossier explicitement.
2. **Corrigé — le partage `/home` survit aux redémarrages et aux déploiements.**
3. **À décider — leur sauvegarde.** Le partage `/home` n'est pas sauvegardé automatiquement. Deux options, à arbitrer :
   - **Sauvegarde App Service** : nécessite au minimum un plan Standard (S1) ; le plan actuel est B1. Sauvegarde le contenu du site et, en option, la base. Coût : la différence de plan.
   - **Passer `IFileStorage` sur le stockage Blob** : l'interface existe précisément pour ça (`LocalFileStorage` est l'implémentation « poste de développement »). Blob apporte la suppression réversible, le versionnage et la redondance géographique. Coût : ~1 j de développement.

   Tant que ni l'une ni l'autre n'est en place, **les documents téléversés ne sont pas sauvegardés** — la base l'est, les pièces jointes non.

---

## 3. Restauration — l'exercice

> Une sauvegarde jamais restaurée n'est pas une sauvegarde : c'est une hypothèse.

`scripts/restore-drill.ps1` restaure la base **dans une copie neuve** (la base vive n'est jamais touchée), compte ce qui est revenu, mesure le temps que ça a pris, et affiche la ligne à coller dans le journal ci-dessous.

```powershell
./scripts/restore-drill.ps1 `
    -ResourceGroup  rg-remsolution-prod `
    -ServerName     sql-abc123 `
    -DatabaseName   sqldb-abc123 `
    -SqlAdminUser   sqlAdmin `
    -KeyVaultName   kv-abc123
```

Options utiles : `-RestorePointUtc` pour viser un instant précis (par défaut il y a 10 minutes), `-KeepRestore` pour garder la copie et l'inspecter — elle est facturée comme n'importe quelle base, donc à supprimer ensuite.

### Restauration réelle (incident)

En cas de perte ou de corruption avérée :

1. **Arrêter les écritures.** Mettre le service applicatif à l'arrêt (`az webapp stop`). Restaurer sous une application qui écrit encore, c'est perdre ce qu'elle écrit.
2. **Restaurer à côté**, jamais par-dessus :
   ```bash
   az sql db restore --resource-group <rg> --server <srv> --name <db> \
       --dest-name <db>-restored --time 2026-08-13T09:15:00
   ```
3. **Vérifier** la copie (comptages, dernière migration, quelques enregistrements récents connus).
4. **Basculer** : renommer la base vive en `<db>-corrupted-<date>`, puis la copie restaurée en `<db>`.
5. **Redémarrer** l'application et vérifier `/health`.
6. **Garder** la base corrompue quelques jours : c'est la seule pièce à conviction.

### Journal des restaurations

Une ligne par exercice ou par restauration réelle. La colonne qui compte est la durée : c'est le temps d'indisponibilité en cas d'incident, et il augmente avec la taille de la base.

| Date | Point restauré | Durée | Taille | Par | Remarques |
|---|---|---|---|---|---|
| | | | | | *(aucun exercice effectué à ce jour — à faire au premier déploiement réel)* |

**Fréquence recommandée :** au premier déploiement, puis tous les trimestres, et après tout changement de niveau de service ou de réglage de sauvegarde.
