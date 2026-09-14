# Test de charge — chemin de recherche du marketplace

Plan §3.6. Ce que ce test mesure : la recherche publique, qui est la requête la
plus chère de l'application et la seule ouverte au monde entier.

`SearchAvailableCarsQuery` filtre les voitures offertes, applique le viewport,
puis écarte tout ce qui chevauche une location, une réservation confirmée ou une
indisponibilité — **trois anti-jointures par voiture candidate**, sans locataire
pour réduire l'ensemble. C'est le point qui casse en premier quand le catalogue
grossit, et c'est pour ça qu'il a son propre test plutôt qu'un test de charge
générique.

Le scénario suit l'entonnoir réel : `destinations` → `cars` (page 1) → `map` →
la fiche de la première voiture retournée. Chaque itération tire une fenêtre de
dates différente, sinon SQL Server répond à tous les utilisateurs virtuels
depuis le même plan et les mêmes pages en cache — soit exactement le coût qu'on
cherche à mesurer.

## Avant de lancer — deux réglages, sinon le résultat ne veut rien dire

**1. La limitation de débit.** `PublicBrowse` est à **60 requêtes/minute par
IP** (`RateLimitingOptions`). Les moteurs d'Azure Load Testing sortent derrière
une poignée d'adresses : au bout de quelques secondes tout est en 429 et on
mesure le throttle, pas la base. Sur l'environnement de test uniquement, et le
temps du tir :

```powershell
az webapp config appsettings set -g <rg> -n <app> `
  --settings RateLimiting__PublicBrowsePerMinute=100000 RateLimiting__AnonymousPerMinute=100000
# … lancer le test …
az webapp config appsettings delete -g <rg> -n <app> `
  --setting-names RateLimiting__PublicBrowsePerMinute RateLimiting__AnonymousPerMinute
```

Le plan assertionne `200` sur chaque appel : un 429 fait échouer
l'échantillon, donc un oubli se voit immédiatement dans le rapport.

**2. Des données.** Une base vide répond en quelques millisecondes et ne prouve
rien. Il faut des agences **publiées** (`Agency.PublishedAt`) **et habilitées**
(abonnement actif, `OnlineReservations` au plan — sinon `MarketplaceCars.Offered`
les écarte et la recherche renvoie zéro résultat en un temps flatteur), des
voitures `Active`, et de quoi faire travailler les anti-jointures : locations,
réservations confirmées et indisponibilités qui chevauchent les fenêtres
tirées. Viser l'ordre de grandeur visé à l'ouverture, pas le jeu de démo.

Deux rappels sur ce que le chiffre obtenu vaut : le plan App Service est **B1**
(`services/web.bicep`) et la base **S0** (`sqlserver.bicep`). Un tir mesure ce
dimensionnement-là ; changer de palier change le résultat.

## Lancer

### En local, contre `dotnet run`

JMeter 5.6+ requis. La limitation de débit se désactive dans
`appsettings.Development.json` (`RateLimiting:Enabled: false`).

```powershell
jmeter -n -t marketplace-search.jmx -l results.jtl -e -o report `
  -Jhost=localhost -Jport=5001 -Jthreads=20 -Jduration=120
```

Le rapport HTML est dans `report/`. Utile pour vérifier que le plan fait ce
qu'on croit avant de payer des engine-hours.

### Dans Azure Load Testing

La ressource n'est pas provisionnée par défaut — elle coûte à l'heure-moteur et
ne sert qu'au moment d'un tir :

```powershell
azd env set DEPLOY_LOAD_TESTING true
azd provision
```

Puis, après avoir mis le vrai `host` dans `loadtest.yaml` :

```powershell
az load test create --load-test-resource <lt-name> -g <rg> --load-test-config-file loadtest.yaml
az load test-run create --load-test-resource <lt-name> -g <rg> --test-id marketplace-search --test-run-id run-$(Get-Date -Format yyyyMMdd-HHmm)
```

Les seuils d'échec (`failureCriteria`) sont dans `loadtest.yaml` : moyenne
> 1,5 s, p95 > 3 s, ou plus de 1 % d'erreurs. Un tir rouge est un résultat, pas
une panne du test.

Remettre `DEPLOY_LOAD_TESTING` à `false` et reprovisionner une fois la campagne
finie.

## Journal des tirs

Une ligne par campagne. La colonne qui compte est le p95 : c'est ce que voit le
visiteur qui attend ses résultats.

| Date | Cible (plan / SKU) | VUs | Durée | p95 | Erreurs | Verdict |
|---|---|---|---|---|---|---|
| | | | | | | *(aucun tir effectué à ce jour — à faire au premier déploiement réel)* |
