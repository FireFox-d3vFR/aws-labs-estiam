# AWS Labs

Depot de travaux pratiques AWS.

## Contenu

- `lab1-environment-setup` : notes et commandes de validation du Lab 1.
- `lab2-s3-dotnet` : projet .NET du Lab 2 sur Amazon S3.
- `lab3-dynamodb-dotnet` : projet .NET du Lab 3 sur Amazon DynamoDB.
- `lab4-lambda-dotnet` : projet .NET du Lab 4 sur AWS Lambda.

Les supports de cours PDF/HTML sont conserves localement mais ignores par Git.

## Lab 1

Le dossier `lab1-environment-setup` documente la configuration de l'environnement local :

- AWS CLI ;
- credentials locaux avec `aws configure` ;
- SDK .NET ;
- commandes de verification AWS ;
- test S3 temporaire.

Voir la documentation detaillee dans [`lab1-environment-setup/README.md`](lab1-environment-setup/README.md).

## Lab 2

Le projet `lab2-s3-dotnet` contient une application console C# qui :

- cree un bucket S3 ;
- upload un objet avec metadonnees ;
- telecharge et transforme un objet ;
- re-upload le resultat ;
- prepare un site statique S3 avec une bucket policy de lecture publique.

Voir la documentation detaillee dans [`lab2-s3-dotnet/README.md`](lab2-s3-dotnet/README.md).

## Lab 3

Le projet `lab3-dynamodb-dotnet` contient une application console C# qui :

- cree une table DynamoDB `Products` ;
- insere et lit des items avec l'API bas niveau ;
- charge un dataset depuis `products.json` ;
- utilise `Query`, `FilterExpression`, pagination et un index secondaire ;
- fait une mise a jour conditionnelle ;
- interroge DynamoDB avec PartiQL ;
- utilise `DynamoDBContext` avec une classe POCO.

Voir la documentation detaillee dans [`lab3-dynamodb-dotnet/README.md`](lab3-dynamodb-dotnet/README.md).

## Lab 4

Le dossier `lab4-lambda-dotnet` prepare le developpement d'une fonction AWS Lambda C# qui :

- utilise les outils Lambda .NET et le template `lambda.EmptyFunction` ;
- lit une variable d'environnement `STAGE` ;
- retourne une reponse avec input, stage et timestamp UTC ;
- est packagee et deployee avec `dotnet lambda` ;
- est invoquee avec AWS CLI ;
- ecrit ses logs dans CloudWatch Logs ;
- genere une URL S3 pre-signee valable 1 heure depuis un deuxieme handler.

Voir la documentation detaillee dans [`lab4-lambda-dotnet/README.md`](lab4-lambda-dotnet/README.md).

## Avant de publier sur GitHub

Ne jamais committer :

- les dossiers `bin/` et `obj/` ;
- les fichiers de credentials AWS ;
- les cles privees ou tokens ;
- les fichiers locaux propres a l'IDE.

Ces elements sont exclus par le fichier `.gitignore`.
