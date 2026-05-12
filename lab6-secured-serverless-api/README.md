# Lab 6 - Capstone Secured Serverless API

Ce lab securise l'API REST `HelloAPI` du Lab 5 avec Amazon Cognito, puis fait evoluer `HelloLambda` pour lire l'identite de l'utilisateur authentifie et journaliser chaque appel dans DynamoDB.

L'objectif est de transformer l'endpoint public `/hello` en API serverless authentifiee : sans token JWT valide, API Gateway retourne HTTP 401 ; avec un token Cognito valide, la Lambda repond en HTTP 200 et ajoute une ligne d'audit dans `ActivityLog`.

## Resume du lab

Le Lab 6 demande de :

- verifier que `HelloLambda` et `HelloAPI` existent encore ;
- creer un User Pool Cognito `HelloPool` avec login par email ;
- creer un App Client `HelloAppClient` sans secret client ;
- creer un utilisateur de test `student@estiam.com` ;
- authentifier cet utilisateur avec AWS CLI pour obtenir des JWT ;
- creer un Cognito Authorizer dans API Gateway ;
- appliquer l'authorizer aux methodes `GET /hello` et `POST /hello` ;
- redeployer le stage `dev` ;
- creer une table DynamoDB `ActivityLog` ;
- donner a la Lambda le droit d'ecrire dans DynamoDB ;
- ajouter le package `AWSSDK.DynamoDBv2` au projet Lambda ;
- modifier `Function.cs` pour lire les claims Cognito et ecrire un audit par requete ;
- verifier les cas HTTP 401 sans token, HTTP 200 avec token, puis les items DynamoDB ;
- consulter CloudWatch Logs pour confirmer que l'identite utilisateur est bien loggee.

## Objectifs

- Comprendre le role d'un User Pool Cognito dans une API serverless.
- Valider des JWT avec un authorizer API Gateway.
- Recuperer les claims Cognito injectes dans `APIGatewayProxyRequest`.
- Persister une trace d'audit dans DynamoDB.
- Distinguer une requete anonyme rejetee au niveau API Gateway d'une requete authentifiee qui atteint Lambda.
- Relier Cognito, API Gateway, Lambda, DynamoDB et IAM dans une architecture complete.

## Prerequis

- Labs 1 a 5 termines.
- Fonction Lambda `HelloLambda` deployee.
- API Gateway REST API `HelloAPI` deployee sur le stage `dev`.
- AWS CLI configure.
- Outil `dotnet lambda` installe.
- Droits IAM suffisants pour :
  - Cognito User Pools ;
  - API Gateway ;
  - Lambda ;
  - DynamoDB ;
  - IAM role policies.

Verifier l'identite AWS active :

```powershell
aws sts get-caller-identity
```

Verifier la region configuree :

```powershell
aws configure list
```

Les labs precedents de ce depot utilisent principalement `eu-west-3` pour API Gateway et Lambda. Le support du Lab 6 signale toutefois un point important : la table DynamoDB doit etre creee dans la meme region que la Lambda. Dans ce README, on utilise une variable commune :

```powershell
$REGION = "eu-west-3"
```

Si `HelloLambda` existe dans une autre region, utilisez cette region partout pour Lambda, API Gateway et DynamoDB.

## Structure prevue

```text
lab6-secured-serverless-api/
`-- README.md
```

Le code Lambda a modifier reste dans :

```text
lab4-lambda-dotnet/HelloLambda/
```

## 1. Verification des ressources existantes

Verifier `HelloLambda` :

```powershell
aws lambda get-function `
  --function-name HelloLambda `
  --region $REGION `
  --query "Configuration.{State:State,Runtime:Runtime,Handler:Handler}"
```

Verifier `HelloAPI` et recuperer son ID :

```powershell
$API_ID = aws apigateway get-rest-apis `
  --region $REGION `
  --query "items[?name=='HelloAPI'].id" `
  --output text

$HELLO_ID = aws apigateway get-resources `
  --rest-api-id $API_ID `
  --region $REGION `
  --query "items[?path=='/hello'].id" `
  --output text

Write-Host "API_ID   = $API_ID"
Write-Host "HELLO_ID = $HELLO_ID"
```

Si l'une des deux variables est vide, reprendre le Lab 5 avant de continuer.

## 2. Creation du User Pool Cognito

Creer le User Pool :

```powershell
$POOL_ID = aws cognito-idp create-user-pool `
  --pool-name HelloPool `
  --region $REGION `
  --policies "PasswordPolicy={MinimumLength=8,RequireUppercase=true,RequireLowercase=true,RequireNumbers=true,RequireSymbols=false}" `
  --auto-verified-attributes email `
  --username-attributes email `
  --query "UserPool.Id" `
  --output text

Write-Host "POOL_ID = $POOL_ID"
```

Creer l'App Client sans secret :

```powershell
$CLIENT_ID = aws cognito-idp create-user-pool-client `
  --user-pool-id $POOL_ID `
  --region $REGION `
  --client-name HelloAppClient `
  --no-generate-secret `
  --explicit-auth-flows ALLOW_USER_PASSWORD_AUTH ALLOW_REFRESH_TOKEN_AUTH `
  --query "UserPoolClient.ClientId" `
  --output text

Write-Host "CLIENT_ID = $CLIENT_ID"
```

Recuperer l'ARN du User Pool :

```powershell
$POOL_ARN = aws cognito-idp describe-user-pool `
  --user-pool-id $POOL_ID `
  --region $REGION `
  --query "UserPool.Arn" `
  --output text

Write-Host "POOL_ARN = $POOL_ARN"
```

## 3. Creation et authentification d'un utilisateur

Creer l'utilisateur de test :

```powershell
aws cognito-idp admin-create-user `
  --user-pool-id $POOL_ID `
  --region $REGION `
  --username student@estiam.com `
  --user-attributes Name=email,Value=student@estiam.com Name=email_verified,Value=true `
  --temporary-password "TempPass1!"
```

Definir un mot de passe permanent :

```powershell
aws cognito-idp admin-set-user-password `
  --user-pool-id $POOL_ID `
  --region $REGION `
  --username student@estiam.com `
  --password "Secure123!" `
  --permanent
```

Demander les tokens :

```powershell
$AUTH = aws cognito-idp initiate-auth `
  --auth-flow USER_PASSWORD_AUTH `
  --client-id $CLIENT_ID `
  --region $REGION `
  --auth-parameters USERNAME=student@estiam.com,PASSWORD=Secure123! `
  | ConvertFrom-Json

$ACCESS_TOKEN = $AUTH.AuthenticationResult.AccessToken
$ID_TOKEN = $AUTH.AuthenticationResult.IdToken
$REFRESH_TOKEN = $AUTH.AuthenticationResult.RefreshToken
```

Important : pour les tests de ce lab, utiliser `$ID_TOKEN` dans le header `Authorization`. Il contient le claim `email`, alors que l'Access Token peut ne pas le contenir.

## 4. Cognito Authorizer dans API Gateway

Creer l'authorizer :

```powershell
$AUTHORIZER_ID = aws apigateway create-authorizer `
  --rest-api-id $API_ID `
  --region $REGION `
  --name CognitoAuth `
  --type COGNITO_USER_POOLS `
  --provider-arns $POOL_ARN `
  --identity-source "method.request.header.Authorization" `
  --query "id" `
  --output text

Write-Host "AUTHORIZER_ID = $AUTHORIZER_ID"
```

Appliquer l'authorizer a `GET /hello` et `POST /hello` :

```powershell
aws apigateway update-method `
  --rest-api-id $API_ID `
  --resource-id $HELLO_ID `
  --region $REGION `
  --http-method GET `
  --patch-operations "op=replace,path=/authorizationType,value=COGNITO_USER_POOLS" "op=replace,path=/authorizerId,value=$AUTHORIZER_ID"

aws apigateway update-method `
  --rest-api-id $API_ID `
  --resource-id $HELLO_ID `
  --region $REGION `
  --http-method POST `
  --patch-operations "op=replace,path=/authorizationType,value=COGNITO_USER_POOLS" "op=replace,path=/authorizerId,value=$AUTHORIZER_ID"
```

Ne pas appliquer l'authorizer a `OPTIONS /hello`, sinon le preflight CORS navigateur echouera.

Redeployer le stage `dev` :

```powershell
aws apigateway create-deployment `
  --rest-api-id $API_ID `
  --region $REGION `
  --stage-name dev `
  --description "Lab 6 - Cognito authorizer applied"
```

## 5. Table DynamoDB ActivityLog

Creer la table :

```powershell
aws dynamodb create-table `
  --region $REGION `
  --table-name ActivityLog `
  --attribute-definitions AttributeName=UserId,AttributeType=S AttributeName=Timestamp,AttributeType=S `
  --key-schema AttributeName=UserId,KeyType=HASH AttributeName=Timestamp,KeyType=RANGE `
  --billing-mode PAY_PER_REQUEST

aws dynamodb wait table-exists `
  --table-name ActivityLog `
  --region $REGION
```

Donner a la Lambda le droit d'ecrire dans DynamoDB :

```powershell
aws iam attach-role-policy `
  --role-name lambda-execution-role `
  --policy-arn arn:aws:iam::aws:policy/AmazonDynamoDBFullAccess
```

Note : `AmazonDynamoDBFullAccess` est pratique pour le lab. En production, creer une policy limitee a `dynamodb:PutItem` sur la table `ActivityLog`.

## 6. Mise a jour de HelloLambda

Ajouter le SDK DynamoDB au projet Lambda :

```powershell
Set-Location ..\lab4-lambda-dotnet\HelloLambda
dotnet add package AWSSDK.DynamoDBv2
```

Mettre ensuite a jour `Function.cs` pour :

- initialiser un client `AmazonDynamoDBClient` hors du handler ;
- lire les claims via `request.RequestContext.Authorizer.Claims` ;
- recuperer au minimum `sub` et `email` ;
- ecrire un item dans `ActivityLog` avec `UserId`, `Timestamp`, `Email`, `Method`, `Name` et `Stage` ;
- retourner une reponse JSON qui inclut aussi l'utilisateur authentifie.

Exemple de comportement attendu dans la reponse :

```json
{
  "message": "Hello, Alice!",
  "user": "student@estiam.com",
  "method": "GET",
  "stage": "dev",
  "timestamp": "2026-05-12T10:30:00.0000000Z"
}
```

Redeployer :

```powershell
dotnet lambda deploy-function HelloLambda
Set-Location ..\..\lab6-secured-serverless-api
```

## 7. Tests end-to-end

Construire l'URL :

```powershell
$BASE_URL = "https://$API_ID.execute-api.$REGION.amazonaws.com/dev"
```

Sans token, l'appel doit echouer en HTTP 401 :

```powershell
Invoke-WebRequest `
  -UseBasicParsing `
  -Uri "$BASE_URL/hello"
```

Avec token, `GET /hello` doit reussir :

```powershell
Invoke-RestMethod `
  -Uri "$BASE_URL/hello?name=Alice" `
  -Headers @{ Authorization = "Bearer $ID_TOKEN" }
```

Avec token, `POST /hello` doit reussir :

```powershell
Invoke-RestMethod `
  -Uri "$BASE_URL/hello" `
  -Method POST `
  -ContentType "application/json" `
  -Headers @{ Authorization = "Bearer $ID_TOKEN" } `
  -Body '{"name":"ESTIAM"}'
```

Verifier l'audit dans DynamoDB :

```powershell
aws dynamodb scan `
  --table-name ActivityLog `
  --region $REGION `
  --query "Items[*].{User:Email.S,Method:Method.S,Name:Name.S,Time:Timestamp.S}"
```

Tester un token invalide :

```powershell
Invoke-RestMethod `
  -Uri "$BASE_URL/hello?name=Test" `
  -Headers @{ Authorization = "Bearer INVALID_TOKEN_HERE" }
```

Resultat attendu : HTTP 401. Lambda ne doit pas etre invoquee, donc aucun item DynamoDB ne doit etre ajoute.

Suivre les logs Lambda :

```powershell
aws logs tail /aws/lambda/HelloLambda `
  --region $REGION `
  --follow
```

Resultats observes pendant le lab :

```text
Account ID: <ACCOUNT_ID>
Region: eu-west-3
API_ID: <API_ID>
HELLO_ID: <HELLO_ID>
POOL_ID: <POOL_ID>
CLIENT_ID: <CLIENT_ID>
AUTHORIZER_ID: <AUTHORIZER_ID>
Deployment ID: <DEPLOYMENT_ID>
ActivityLog ARN: arn:aws:dynamodb:eu-west-3:<ACCOUNT_ID>:table/ActivityLog
```

`GET /hello` sans token :

```text
HTTP 401
{"message":"Unauthorized"}
```

`GET /hello?name=Alice` avec `$ID_TOKEN` :

```text
message   : Hello, Alice!
user      : student@estiam.com
method    : GET
stage     : dev
timestamp : 2026-05-12T15:24:03.6949381Z
```

`POST /hello` avec `{"name":"ESTIAM"}` et `$ID_TOKEN` :

```text
message   : Hello, ESTIAM!
user      : student@estiam.com
method    : POST
stage     : dev
timestamp : 2026-05-12T15:24:50.9459235Z
```

Scan DynamoDB observe :

```json
[
  {
    "User": "student@estiam.com",
    "Method": "GET",
    "Name": "Alice",
    "Time": "2026-05-12T15:24:03.6949381Z"
  },
  {
    "User": "student@estiam.com",
    "Method": "POST",
    "Name": "ESTIAM",
    "Time": "2026-05-12T15:24:50.9459235Z"
  }
]
```

Token invalide :

```text
HTTP 401
{"message":"Unauthorized"}
```

Logs CloudWatch observes apres mise a jour Lambda :

```text
[dev] GET /hello by student@estiam.com (3119b05e-e031-705f-4997-0a5e809b2bc4) name=Alice
[dev] POST /hello by student@estiam.com (3119b05e-e031-705f-4997-0a5e809b2bc4) name=ESTIAM
```

## Checklist de validation

- User Pool `HelloPool` cree.
- App Client `HelloAppClient` cree sans secret.
- Utilisateur `student@estiam.com` cree avec mot de passe permanent.
- Tokens JWT recuperes avec `initiate-auth`.
- Cognito Authorizer `CognitoAuth` cree dans API Gateway.
- `GET /hello` et `POST /hello` proteges par Cognito.
- `OPTIONS /hello` reste sans authentification.
- Stage `dev` redeploye.
- Table DynamoDB `ActivityLog` active.
- Lambda autorisee a ecrire dans DynamoDB.
- `GET /hello` sans token retourne HTTP 401.
- `GET /hello` avec token retourne HTTP 200 et l'email utilisateur.
- `POST /hello` avec token retourne HTTP 200.
- `ActivityLog` contient les appels authentifies.
- CloudWatch Logs montre l'utilisateur authentifie.

## Nettoyage

Remettre les methodes API Gateway sans authorizer :

```powershell
aws apigateway update-method `
  --rest-api-id $API_ID `
  --resource-id $HELLO_ID `
  --region $REGION `
  --http-method GET `
  --patch-operations "op=replace,path=/authorizationType,value=NONE"

aws apigateway update-method `
  --rest-api-id $API_ID `
  --resource-id $HELLO_ID `
  --region $REGION `
  --http-method POST `
  --patch-operations "op=replace,path=/authorizationType,value=NONE"
```

Supprimer l'authorizer :

```powershell
aws apigateway delete-authorizer `
  --rest-api-id $API_ID `
  --authorizer-id $AUTHORIZER_ID `
  --region $REGION
```

Supprimer Cognito :

```powershell
aws cognito-idp delete-user-pool-client `
  --user-pool-id $POOL_ID `
  --client-id $CLIENT_ID `
  --region $REGION

aws cognito-idp delete-user-pool `
  --user-pool-id $POOL_ID `
  --region $REGION
```

Supprimer DynamoDB :

```powershell
aws dynamodb delete-table `
  --table-name ActivityLog `
  --region $REGION
```

Detacher la policy DynamoDB du role Lambda :

```powershell
aws iam detach-role-policy `
  --role-name lambda-execution-role `
  --policy-arn arn:aws:iam::aws:policy/AmazonDynamoDBFullAccess
```

Conserver `HelloLambda`, `HelloAPI` et Cognito si ce stack sert de base au projet de groupe.

## Points importants

- Cognito gere l'authentification et emet les JWT.
- API Gateway valide le JWT avant l'invocation Lambda.
- Les claims utilisateur arrivent dans le contexte authorizer de la requete Lambda.
- DynamoDB est adapte a un audit simple, peu couteux et scalable.
- Une requete rejetee par l'authorizer ne declenche pas Lambda.
- Pour un client web ou mobile, ne jamais embarquer de credentials AWS : le client s'authentifie via Cognito puis appelle l'API securisee.
