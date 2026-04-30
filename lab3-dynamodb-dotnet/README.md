# Lab 3 - Amazon DynamoDB avec .NET

Ce lab montre comment manipuler Amazon DynamoDB depuis une application console C#.

## Objectifs

- Creer une table DynamoDB `Products`.
- Inserer un item avec l'API bas niveau.
- Recuperer un item avec sa cle primaire composee.
- Charger plusieurs produits depuis un fichier JSON.
- Rechercher des produits avec `Query`, `FilterExpression` et pagination.
- Faire une mise a jour conditionnelle.
- Interroger la table avec PartiQL.
- Utiliser le modele objet avec `DynamoDBContext`.

## Modele de donnees

Table : `Products`

| Attribut | Type | Role |
| --- | --- | --- |
| `ProductId` | String | Partition key |
| `Category` | String | Sort key |
| `Name` | String | Nom du produit |
| `Price` | Number | Prix en EUR |
| `InStock` | Boolean | Disponibilite |
| `UpdatedAt` | String | Date de mise a jour ISO 8601 |

Le programme cree aussi un index secondaire global `CategoryIndex`.

Pourquoi ? Avec `ProductId` comme partition key, DynamoDB ne peut pas requeter directement "tous les produits Electronics". L'index `CategoryIndex` permet de faire une vraie query par categorie, puis de filtrer les produits sous 100 EUR.

## Execution

Depuis le dossier `lab3-dynamodb-dotnet` :

```powershell
dotnet run
```

Le programme utilise la region `eu-west-3` et les credentials AWS locaux configures pendant le Lab 1.

## Etapes realisees

### 1. Creation de la table

La table `Products` est creee avec :

- `ProductId` comme partition key ;
- `Category` comme sort key ;
- `PAY_PER_REQUEST` comme mode de facturation ;
- `CategoryIndex` comme global secondary index.

Le programme attend ensuite que la table passe en statut `ACTIVE`.

### 2. Insertion bas niveau

L'item `P001` est insere avec un dictionnaire d'`AttributeValue`.

Cette API est verbeuse, mais elle donne un controle complet sur les types DynamoDB.

### 3. Lecture par cle primaire

L'item `P001` est recupere avec :

- `ProductId = P001`
- `Category = Electronics`

Comme la table a une cle composee, les deux valeurs sont necessaires.

### 4. Batch load depuis JSON

Le fichier `products.json` contient cinq produits :

- `P002`
- `P003`
- `P004`
- `P005`
- `P006`

Ils sont charges avec `BatchWriteItemAsync`.

### 5. Query avec filtre et pagination

Le programme cherche les produits :

- de categorie `Electronics` ;
- avec un prix inferieur a 100 EUR.

La query utilise :

- `KeyConditionExpression` pour chercher dans `CategoryIndex` ;
- `FilterExpression` pour filtrer sur le prix ;
- `ProjectionExpression` pour ne retourner que certains champs ;
- `LastEvaluatedKey` pour gerer la pagination.

### 6. Update conditionnel

Le produit `P004` est mis a jour uniquement si :

```text
InStock = false
```

Cela evite d'ecraser un produit si son etat ne correspond pas a ce qu'on attend.

### 7. PartiQL

PartiQL permet d'interroger DynamoDB avec une syntaxe proche de SQL :

```sql
SELECT ProductId, Name, Price FROM Products WHERE Category = 'Books'
```

### 8. Object Persistence Model

Le programme definit une classe C# `Product`, puis utilise `DynamoDBContext` pour :

- sauvegarder `P007` ;
- le recharger ;
- le supprimer.

Cette approche est plus confortable que l'API bas niveau pour du code applicatif.

## Verification CLI

Scanner la table :

```powershell
aws dynamodb scan --table-name Products --output table
```

Recuperer `P001`.

Sous PowerShell, le plus fiable est de creer un petit fichier temporaire `key-p001.json` :

```json
{
  "ProductId": {
    "S": "P001"
  },
  "Category": {
    "S": "Electronics"
  }
}
```

Puis :

```powershell
aws dynamodb get-item --table-name Products --key file://key-p001.json
```

Lister les tables :

```powershell
aws dynamodb list-tables
```

On peut aussi scan l'ensemble de la table :

```powershell
aws dynamodb scan --table-name Products
```

## Nettoyage

Quand le lab est termine :

```powershell
aws dynamodb delete-table --table-name Products
```

Puis verifier :

```powershell
aws dynamodb list-tables
```

## Points importants

- Une table DynamoDB est optimisee pour ses patterns de requete.
- `Query` est preferable a `Scan` quand on connait la cle ou un index.
- Une `FilterExpression` filtre apres la lecture des items.
- Les updates conditionnels evitent des ecritures incorrectes.
- PartiQL est pratique, mais il ne remplace pas une bonne modelisation des cles.
- `DynamoDBContext` simplifie le code C# avec des classes POCO.
