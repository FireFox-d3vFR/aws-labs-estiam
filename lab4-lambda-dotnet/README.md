# Lab 4 - AWS Lambda avec .NET

Ce lab montre comment creer, deployer, invoquer et surveiller une fonction AWS Lambda ecrite en C#.

Il prolonge les labs precedents en passant d'une application console locale a une application serverless executee par AWS. La fonction est d'abord un handler simple qui retourne un message avec un timestamp et une variable d'environnement, puis elle est etendue pour generer une URL S3 pre-signee.

## Resume du lab

Le Lab 4 demande de :

- installer les outils Lambda pour .NET ;
- creer un projet Lambda C# avec le template `lambda.EmptyFunction` ;
- modifier le handler pour lire une variable d'environnement `STAGE` ;
- retourner une reponse contenant l'input, le stage et un timestamp UTC ;
- creer un role IAM d'execution pour Lambda ;
- deployer la fonction avec `dotnet lambda deploy-function` ;
- invoquer la fonction avec AWS CLI ;
- consulter les logs d'execution dans CloudWatch Logs ;
- ajouter un deuxieme handler qui genere une URL S3 pre-signee valable 1 heure ;
- tester l'URL pre-signee sur un objet S3 prive ;
- nettoyer la fonction, le role IAM, le bucket de test et les logs.

## Objectifs

- Comprendre le cycle de vie d'une fonction Lambda .NET.
- Utiliser `Amazon.Lambda.Tools` et `Amazon.Lambda.Templates`.
- Configurer le runtime, la memoire, le timeout, le handler et les variables d'environnement.
- Deployer une fonction Lambda depuis le terminal.
- Invoquer une Lambda avec une payload JSON simple.
- Lire les logs CloudWatch generes automatiquement.
- Donner a Lambda un acces S3 via son role IAM.
- Generer une URL S3 pre-signee depuis le code C#.

## Prerequis

- Lab 1 termine : AWS CLI configure avec `aws configure`.
- .NET SDK installe.
- Droits IAM suffisants pour creer :
  - une fonction Lambda ;
  - un role IAM ;
  - des logs CloudWatch ;
  - un bucket et un objet S3 de test.
- Connaissances de base en C# et `async/await`.

Verifier l'identite AWS active :

```powershell
aws sts get-caller-identity
```

Verifier la region configuree :

```powershell
aws configure list
```

Pour rester coherent avec les labs precedents, utiliser la region :

```text
eu-west-3
```

## Structure prevue

Apres creation du projet Lambda, la structure attendue sera proche de :

```text
lab4-lambda-dotnet/
|-- README.md
|-- trust-policy.json
|-- payload-world.json
|-- payload-estiam.json
|-- test-object.txt
`-- HelloLambda/
    |-- Function.cs
    |-- HelloLambda.csproj
    |-- aws-lambda-tools-defaults.json
    `-- Readme.md
```

Le dossier `HelloLambda/` sera genere par le template Lambda .NET.

## 1. Installation des outils Lambda .NET

Depuis le dossier `lab4-lambda-dotnet` :

```powershell
dotnet tool install -g Amazon.Lambda.Tools
dotnet new install Amazon.Lambda.Templates
dotnet lambda
```

Si `dotnet lambda` n'est pas trouve sous Windows, ajouter ce dossier au `PATH` :

```text
%USERPROFILE%\.dotnet\tools
```

Versions observees pendant le lab :

```text
Amazon.Lambda.Tools 6.0.5
Amazon.Lambda.Templates 8.0.3
```

Note : avec `Amazon.Lambda.Tools 6.0.5`, la commande `dotnet lambda --version` affiche l'en-tete avec la version, puis indique `Unknown command: --version`. Pour verifier que l'outil est bien installe, utiliser simplement :

```powershell
dotnet lambda
```

## 2. Creation du projet Lambda

Toujours depuis `lab4-lambda-dotnet` :

```powershell
dotnet new lambda.EmptyFunction -n HelloLambda
```

Le fichier principal a modifier sera :

```text
HelloLambda/Function.cs
```

Note : avec les templates `Amazon.Lambda.Templates 8.0.3`, le projet genere utilise `net10.0` et le runtime `dotnet10`. Le support du lab mentionne `.NET 6`, mais ce depot suit le template reellement genere sur cette machine.

## 3. Handler de greeting

Le premier handler doit :

- recevoir une chaine de caracteres ;
- lire la variable d'environnement `STAGE` ;
- ecrire une ligne de log avec `context.Logger.LogInformation` ;
- retourner une chaine contenant le nom, le stage et la date UTC.

Exemple de reponse attendue :

```text
"Hello, World! Stage=dev at 2026-05-11T08:30:00.0000000Z"
```

## 4. Role IAM d'execution

Le fichier `trust-policy.json` de ce dossier permet a Lambda d'assumer le role IAM.

Creer le role :

```powershell
aws iam create-role --role-name lambda-execution-role --assume-role-policy-document file://trust-policy.json
```

Autoriser l'ecriture dans CloudWatch Logs :

```powershell
aws iam attach-role-policy --role-name lambda-execution-role --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole
```

Recuperer l'ARN du role :

```powershell
aws iam get-role --role-name lambda-execution-role --query "Role.Arn" --output text
```

Cet ARN sera a reporter dans `HelloLambda/aws-lambda-tools-defaults.json`.

## 5. Configuration du deploiement

Dans `HelloLambda/aws-lambda-tools-defaults.json`, verifier ou ajuster :

```json
{
  "profile": "default",
  "region": "eu-west-3",
  "configuration": "Release",
  "function-architecture": "x86_64",
  "function-runtime": "dotnet10",
  "function-memory-size": 256,
  "function-timeout": 30,
  "function-handler": "HelloLambda::HelloLambda.Function::FunctionHandler",
  "function-role": "arn:aws:iam::YOUR_ACCOUNT_ID:role/lambda-execution-role",
  "environment-variables": "STAGE=dev"
}
```

Remplacer `YOUR_ACCOUNT_ID` par l'identifiant du compte AWS. Pendant ce lab, l'ARN du role a ete recupere avec :

```powershell
aws iam get-role --role-name lambda-execution-role --query "Role.Arn" --output text
```

## 6. Build et deploiement

Depuis le dossier du projet :

```powershell
Set-Location HelloLambda
dotnet lambda package
dotnet lambda deploy-function HelloLambda
```

Verifier la fonction :

```powershell
aws lambda get-function --function-name HelloLambda --query "Configuration.{State:State,Runtime:Runtime,Memory:MemorySize}"
```

Resultat observe :

```json
{
  "State": "Active",
  "Runtime": "dotnet10",
  "Memory": 256
}
```

## 7. Invocation CLI

Depuis `lab4-lambda-dotnet`, invoquer avec une payload simple :

```powershell
aws lambda invoke --function-name HelloLambda --payload fileb://payload-world.json --cli-binary-format raw-in-base64-out output.json
Get-Content output.json
```

Resultat observe :

```json
{
  "StatusCode": 200,
  "ExecutedVersion": "$LATEST"
}
```

Contenu de `output.json` :

```text
"Hello, World! Stage=dev at 2026-05-11T10:10:43.5388661Z"
```

Deuxieme test :

```powershell
aws lambda invoke --function-name HelloLambda --payload fileb://payload-estiam.json --cli-binary-format raw-in-base64-out output2.json
Get-Content output2.json
```

Resultat observe :

```json
{
  "StatusCode": 200,
  "ExecutedVersion": "$LATEST"
}
```

Contenu de `output2.json` :

```text
"Hello, ESTIAM! Stage=dev at 2026-05-11T10:14:00.1187291Z"
```

## 8. Logs CloudWatch

Suivre les logs en temps reel :

```powershell
aws logs tail /aws/lambda/HelloLambda --follow
```

Lister les streams de logs :

```powershell
aws logs describe-log-streams --log-group-name /aws/lambda/HelloLambda --order-by LastEventTime --descending
```

Les logs doivent contenir :

- `START` ;
- la ligne ecrite par `context.Logger.LogInformation` ;
- `END` ;
- `REPORT`.

Resultats observes :

```text
[dev] Received input: World
[dev] Received input: ESTIAM
```

Le premier appel contient aussi une ligne `INIT_START`, car Lambda initialise le runtime .NET lors du cold start. Les rapports CloudWatch indiquent notamment :

- memoire configuree : `256 MB` ;
- memoire maximale observee : `73 MB` ;
- duree du premier appel : environ `325 ms`, avec initialisation du runtime ;
- duree du deuxieme appel : environ `41 ms`, sans cold start.

Log stream observe :

```text
2026/05/11/[$LATEST]159ec4c84c1b4a1881592a23163a06ed
```

## 9. Handler d'URL S3 pre-signee

Le deuxieme handler doit recevoir une chaine au format :

```text
bucket-name/object-key
```

Il genere ensuite une URL HTTPS valable 1 heure pour telecharger l'objet sans credentials AWS.

Ajouter le package S3 :

```powershell
Set-Location HelloLambda
dotnet add package AWSSDK.S3
```

Depuis `lab4-lambda-dotnet`, on peut aussi utiliser le chemin explicite du projet :

```powershell
dotnet add .\HelloLambda\HelloLambda.csproj package AWSSDK.S3
```

Version observee :

```text
AWSSDK.S3 4.0.23
```

Autoriser le role Lambda a lire S3 :

```powershell
aws iam attach-role-policy --role-name lambda-execution-role --policy-arn arn:aws:iam::aws:policy/AmazonS3ReadOnlyAccess
```

Pour tester, creer un bucket dedie :

```powershell
$env:PRESIGN_BUCKET = "lab4-presign-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
aws s3 mb s3://$env:PRESIGN_BUCKET
aws s3 cp test-object.txt s3://$env:PRESIGN_BUCKET/test-object.txt
aws s3 ls s3://$env:PRESIGN_BUCKET
```

Resultats observes :

```text
make_bucket: lab4-presign-1778495567
upload: .\test-object.txt to s3://lab4-presign-1778495567/test-object.txt
2026-05-11 12:34:08         38 test-object.txt
```

Apres redeploiement, pointer Lambda vers le handler d'URL pre-signee :

```powershell
aws lambda update-function-configuration --function-name HelloLambda --handler "HelloLambda::HelloLambda.Function::PresignedUrlHandler"
aws lambda wait function-updated --function-name HelloLambda
```

Configuration observee apres mise a jour :

```text
Handler: HelloLambda::HelloLambda.Function::PresignedUrlHandler
Runtime: dotnet10
MemorySize: 256
State: Active
Environment: STAGE=dev
```

Invoquer le handler :

```powershell
$payload = '"' + $env:PRESIGN_BUCKET + '/test-object.txt"'
aws lambda invoke --function-name HelloLambda --payload $payload --cli-binary-format raw-in-base64-out presigned.json
Get-Content presigned.json
```

Sous PowerShell, si AWS CLI recoit la payload sans les guillemets JSON, creer plutot un fichier payload :

```powershell
Set-Content -Path presigned-payload.json -Value ('"{0}/test-object.txt"' -f $env:PRESIGN_BUCKET) -NoNewline
Get-Content presigned-payload.json
aws lambda invoke --function-name HelloLambda --payload fileb://presigned-payload.json --cli-binary-format raw-in-base64-out presigned.json
Get-Content presigned.json
```

Resultat observe :

```json
{
  "StatusCode": 200,
  "ExecutedVersion": "$LATEST"
}
```

Le fichier `presigned.json` contient une URL HTTPS de ce type :

```text
"https://lab4-presign-1778495567.s3.eu-west-3.amazonaws.com/test-object.txt?...&X-Amz-Expires=3600&..."
```

Ne pas publier l'URL complete : elle contient un token temporaire et donne acces a l'objet pendant sa duree de validite.

Remettre ensuite le handler principal :

```powershell
aws lambda update-function-configuration --function-name HelloLambda --handler "HelloLambda::HelloLambda.Function::FunctionHandler"
aws lambda wait function-updated --function-name HelloLambda
```

Configuration observee apres retour au handler principal :

```text
Handler: HelloLambda::HelloLambda.Function::FunctionHandler
Runtime: dotnet10
MemorySize: 256
State: Active
Environment: STAGE=dev
```

## Nettoyage

Supprimer le bucket de test :

```powershell
aws s3 rb s3://$env:PRESIGN_BUCKET --force
```

Resultat observe :

```text
delete: s3://lab4-presign-1778495567/test-object.txt
remove_bucket: lab4-presign-1778495567
```

Supprimer la fonction Lambda :

```powershell
aws lambda delete-function --function-name HelloLambda
```

Commande executee avec succes.

Detacher les policies puis supprimer le role IAM :

```powershell
aws iam detach-role-policy --role-name lambda-execution-role --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole
aws iam detach-role-policy --role-name lambda-execution-role --policy-arn arn:aws:iam::aws:policy/AmazonS3ReadOnlyAccess
aws iam delete-role --role-name lambda-execution-role
```

Commandes executees avec succes.

Optionnel : supprimer le groupe de logs CloudWatch :

```powershell
aws logs delete-log-group --log-group-name /aws/lambda/HelloLambda
```

Commande executee avec succes.

Important : si le Lab 5 utilise cette Lambda comme backend API Gateway, conserver la fonction `HelloLambda`.

## Points importants

- Lambda execute du code sans gerer de serveur.
- Le role IAM d'execution controle les services AWS accessibles par la fonction.
- Les variables d'environnement permettent de separer configuration et code.
- CloudWatch Logs est le premier endroit a consulter en cas d'erreur Lambda.
- Les clients SDK peuvent etre initialises hors du handler pour etre reutilises entre invocations chaudes.
- Une URL pre-signee donne un acces temporaire a un objet S3 prive sans exposer de credentials.
