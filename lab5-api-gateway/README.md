# Lab 5 - REST API avec Amazon API Gateway

Ce lab construit une API REST avec Amazon API Gateway devant la fonction Lambda `HelloLambda` du Lab 4.

L'objectif est de passer d'une Lambda invoquee manuellement a un endpoint HTTP public, deploye sur un stage `dev`, avec CORS et validation de requete.

## Resume du lab

Le Lab 5 demande de :

- creer une REST API `HelloAPI` dans API Gateway ;
- recuperer la ressource racine `/` puis creer la ressource `/hello` ;
- ajouter une methode `GET /hello` sans authentification ;
- connecter `GET /hello` a `HelloLambda` avec Lambda Proxy Integration ;
- autoriser API Gateway a invoquer la Lambda avec `lambda add-permission` ;
- modifier le handler Lambda pour retourner une `APIGatewayProxyResponse` ;
- ajouter une methode `OPTIONS /hello` avec integration `MOCK` pour CORS ;
- declarer les headers CORS attendus par le navigateur ;
- creer un modele JSON Schema `HelloModel` pour valider les bodies `POST` ;
- ajouter une methode `POST /hello` avec validation du body ;
- connecter `POST /hello` a la meme Lambda ;
- redeployer l'API sur le stage `dev` ;
- tester `GET`, `POST`, la validation HTTP 400 et les headers CORS.

## Objectifs

- Comprendre la hierarchie API Gateway : API, resource, method, integration, deployment, stage.
- Exposer une Lambda via une URL HTTP stable.
- Utiliser Lambda Proxy Integration.
- Gerer CORS avec une methode `OPTIONS`.
- Valider un body JSON avant qu'il atteigne Lambda.
- Deployer une API sur un stage `dev`.
- Verifier les appels avec `Invoke-RestMethod`, `Invoke-WebRequest` ou `curl`.

## Prerequis

- Labs 1 a 4 termines.
- Fonction Lambda `HelloLambda` deployee.
- AWS CLI configure.
- .NET SDK installe.
- Outil `dotnet lambda` disponible.
- Droits IAM suffisants pour :
  - creer et modifier une API Gateway ;
  - ajouter des permissions Lambda ;
  - invoquer `HelloLambda` ;
  - consulter les logs CloudWatch.

Verifier l'identite AWS active :

```powershell
aws sts get-caller-identity
```

Verifier la region configuree :

```powershell
aws configure list
```

Le support du Lab 5 mentionne `eu-west-1`, mais les labs precedents de ce depot ont ete faits en `eu-west-3`. Pour rester coherent avec le projet, les commandes ci-dessous utilisent une variable :

```powershell
$REGION = "eu-west-3"
```

Si votre Lambda `HelloLambda` existe dans une autre region, utilisez cette region a la place.

## Structure prevue

```text
lab5-api-gateway/
|-- README.md
|-- hello-model.json
|-- template.json
|-- response-params.json
`-- request-models.json
```



## 1. Variables de travail

Depuis le dossier `lab5-api-gateway` :

```powershell
$REGION = "eu-west-3"
$ACCOUNT_ID = aws sts get-caller-identity --query Account --output text
```

Verifier que la Lambda existe :

```powershell
aws lambda get-function --function-name HelloLambda --region $REGION
```

## 2. Creation de la REST API

Creer l'API :

```powershell
aws apigateway create-rest-api `
  --name HelloAPI `
  --description "Lab 5 - 5ENTAPP REST API" `
  --endpoint-configuration types=REGIONAL `
  --region $REGION
```

Noter les champs :

- `id` : identifiant de l'API, a stocker dans `$API_ID` ;
- `rootResourceId` : identifiant de la ressource racine, a stocker dans `$ROOT_ID`.

Exemple :

```powershell
$API_ID = "replace-with-api-id"
$ROOT_ID = "replace-with-root-resource-id"
```

Verifier :

```powershell
aws apigateway get-rest-api --rest-api-id $API_ID --region $REGION
aws apigateway get-resources --rest-api-id $API_ID --region $REGION
```

## 3. Ressource /hello

Creer la ressource `/hello` :

```powershell
aws apigateway create-resource `
  --rest-api-id $API_ID `
  --parent-id $ROOT_ID `
  --path-part "hello" `
  --region $REGION
```

Noter le champ `id` de la ressource creee :

```powershell
$HELLO_ID = "replace-with-hello-resource-id"
```

## 4. Methode GET et integration Lambda

Ajouter la methode `GET` :

```powershell
aws apigateway put-method `
  --rest-api-id $API_ID `
  --resource-id $HELLO_ID `
  --http-method GET `
  --authorization-type NONE `
  --region $REGION
```

Construire l'URI d'integration Lambda :

```powershell
$LAMBDA_URI = "arn:aws:apigateway:{0}:lambda:path/2015-03-31/functions/arn:aws:lambda:{0}:{1}:function:HelloLambda/invocations" -f $REGION, $ACCOUNT_ID
```

Configurer l'integration :

```powershell
aws apigateway put-integration `
  --rest-api-id $API_ID `
  --resource-id $HELLO_ID `
  --http-method GET `
  --type AWS_PROXY `
  --integration-http-method POST `
  --uri $LAMBDA_URI `
  --region $REGION
```

Autoriser API Gateway a invoquer la Lambda :

```powershell
aws lambda add-permission `
  --function-name HelloLambda `
  --statement-id apigateway-get-hello `
  --action lambda:InvokeFunction `
  --principal apigateway.amazonaws.com `
  --source-arn ("arn:aws:execute-api:{0}:{1}:{2}/*/GET/hello" -f $REGION, $ACCOUNT_ID, $API_ID) `
  --region $REGION
```

## 5. Adaptation de HelloLambda pour API Gateway

Lambda Proxy Integration attend une reponse structuree avec :

- `StatusCode` ;
- `Headers` ;
- `Body`.

Dans `lab4-lambda-dotnet/HelloLambda`, ajouter le package :

```powershell
dotnet add ..\lab4-lambda-dotnet\HelloLambda\HelloLambda.csproj package Amazon.Lambda.APIGatewayEvents
```

Puis adapter `Function.cs` pour recevoir un `APIGatewayProxyRequest` et retourner une `APIGatewayProxyResponse`.

Le handler doit gerer :

- `GET /hello?name=Alice` avec `QueryStringParameters`;
- `POST /hello` avec un body JSON `{"name":"ESTIAM"}`;
- un header CORS `Access-Control-Allow-Origin: *`.

Redeployer ensuite :

```powershell
Set-Location ..\lab4-lambda-dotnet\HelloLambda
dotnet lambda deploy-function HelloLambda
Set-Location ..\..\lab5-api-gateway
```

## 6. CORS avec OPTIONS

Ajouter la methode `OPTIONS` :

```powershell
aws apigateway put-method `
  --rest-api-id $API_ID `
  --resource-id $HELLO_ID `
  --http-method OPTIONS `
  --authorization-type NONE `
  --region $REGION
```

Configurer une integration `MOCK` :

```powershell
aws apigateway put-integration `
  --rest-api-id $API_ID `
  --resource-id $HELLO_ID `
  --http-method OPTIONS `
  --type MOCK `
  --request-templates file://template.json `
  --region $REGION
```

Declarer les headers CORS :

```powershell
aws apigateway put-method-response `
  --rest-api-id $API_ID `
  --resource-id $HELLO_ID `
  --http-method OPTIONS `
  --status-code 200 `
  --response-parameters "method.response.header.Access-Control-Allow-Headers=false,method.response.header.Access-Control-Allow-Methods=false,method.response.header.Access-Control-Allow-Origin=false" `
  --region $REGION
```

Renseigner les valeurs retournees :

```powershell
aws apigateway put-integration-response `
  --rest-api-id $API_ID `
  --resource-id $HELLO_ID `
  --http-method OPTIONS `
  --status-code 200 `
  --response-parameters file://response-params.json `
  --region $REGION
```

## 7. Methode POST avec validation

Creer le modele `HelloModel` depuis `hello-model.json` :

```powershell
aws apigateway create-model `
  --rest-api-id $API_ID `
  --name HelloModel `
  --content-type "application/json" `
  --schema file://hello-model.json `
  --region $REGION
```

Creer le validateur :

```powershell
$VALIDATOR_ID = aws apigateway create-request-validator `
  --rest-api-id $API_ID `
  --name "body-validator" `
  --validate-request-body `
  --no-validate-request-parameters `
  --query id `
  --output text `
  --region $REGION
```

Ajouter la methode `POST` :

```powershell
aws apigateway put-method `
  --rest-api-id $API_ID `
  --resource-id $HELLO_ID `
  --http-method POST `
  --authorization-type NONE `
  --request-validator-id $VALIDATOR_ID `
  --request-models file://request-models.json `
  --region $REGION
```

Configurer l'integration Lambda :

```powershell
aws apigateway put-integration `
  --rest-api-id $API_ID `
  --resource-id $HELLO_ID `
  --http-method POST `
  --type AWS_PROXY `
  --integration-http-method POST `
  --uri $LAMBDA_URI `
  --region $REGION
```

Autoriser API Gateway a invoquer la Lambda en `POST` :

```powershell
aws lambda add-permission `
  --function-name HelloLambda `
  --statement-id apigateway-post-hello `
  --action lambda:InvokeFunction `
  --principal apigateway.amazonaws.com `
  --source-arn ("arn:aws:execute-api:{0}:{1}:{2}/*/POST/hello" -f $REGION, $ACCOUNT_ID, $API_ID) `
  --region $REGION
```

## 8. Deploiement sur le stage dev

Creer un deployment :

```powershell
aws apigateway create-deployment `
  --rest-api-id $API_ID `
  --stage-name dev `
  --stage-description "Development stage - Lab 5" `
  --region $REGION
```

Verifier :

```powershell
aws apigateway get-stage `
  --rest-api-id $API_ID `
  --stage-name dev `
  --region $REGION
```

URL de base :

```powershell
$BASE_URL = "https://$API_ID.execute-api.$REGION.amazonaws.com/dev"
```

Important : apres chaque modification de l'API Gateway, il faut creer un nouveau deployment pour que le stage `dev` prenne les changements.

## 9. Tests

Tester `GET` :

```powershell
Invoke-RestMethod -Uri "$BASE_URL/hello"
Invoke-RestMethod -Uri "$BASE_URL/hello?name=Alice"
```

Tester `POST` valide :

```powershell
Invoke-RestMethod `
  -Uri "$BASE_URL/hello" `
  -Method POST `
  -ContentType "application/json" `
  -Body '{"name":"ESTIAM"}'
```

Tester la validation avec un body invalide :

```powershell
Invoke-RestMethod `
  -Uri "$BASE_URL/hello" `
  -Method POST `
  -ContentType "application/json" `
  -Body '{}'
```

Resultat attendu : HTTP 400 avec un message proche de `Invalid request body`.

Tester CORS :

```powershell
Invoke-WebRequest -UseBasicParsing -Uri "$BASE_URL/hello?name=Test" |
  Select-Object -ExpandProperty Headers
```

Tester le preflight `OPTIONS` :

```powershell
Invoke-WebRequest `
  -UseBasicParsing `
  -Uri "$BASE_URL/hello" `
  -Method OPTIONS `
  -Headers @{
    "Origin" = "http://localhost:5000"
    "Access-Control-Request-Method" = "POST"
  }
```

Resultats observes pendant le lab :

```text
Base URL: https://<API_ID>.execute-api.eu-west-3.amazonaws.com/dev
```

`GET /hello` :

```text
message       method stage timestamp
Hello, World! GET    dev   2026-05-12T10:30:57.0891860Z
```

`GET /hello?name=Jonathan` :

```text
message          method stage timestamp
Hello, Jonathan! GET    dev   2026-05-12T10:31:20.1964103Z
```

`POST /hello` avec `{"name":"ESTIAM"}` :

```text
message        method stage timestamp
Hello, ESTIAM! POST   dev   2026-05-12T10:33:13.2629420Z
```

`POST /hello` avec `{}` :

```text
HTTP 400
{"message": "Invalid request body"}
```

Ce resultat confirme que la validation API Gateway rejette la requete avant l'invocation Lambda.

Headers CORS observes sur `GET /hello?name=Test` :

```text
Access-Control-Allow-Origin: *
Content-Type: application/json
```

Preflight `OPTIONS /hello` observe :

```text
StatusCode: 200
Access-Control-Allow-Origin: *
Access-Control-Allow-Headers: Content-Type,Authorization
Access-Control-Allow-Methods: GET,POST,OPTIONS
```

## Checklist de validation

- REST API `HelloAPI` creee.
- Ressource `/hello` creee.
- `GET /hello` retourne HTTP 200.
- `GET /hello?name=Alice` retourne un message pour Alice.
- Header `Access-Control-Allow-Origin` present.
- `POST /hello` avec `{"name":"ESTIAM"}` retourne HTTP 200.
- `POST /hello` avec `{}` retourne HTTP 400.
- Stage `dev` cree et endpoint public fonctionnel.
- CloudWatch Logs contient les invocations `GET` et `POST`.

## Nettoyage

Le support indique de ne pas supprimer `HelloLambda` ni `HelloAPI` si le Lab 6 suit juste apres, car ils servent de base pour l'autorisation Cognito.

Si on ne continue pas vers le Lab 6 :

```powershell
aws apigateway delete-rest-api --rest-api-id $API_ID --region $REGION
aws lambda delete-function --function-name HelloLambda --region $REGION
```

Puis supprimer le role IAM Lambda et les logs CloudWatch si necessaire, comme dans le Lab 4.

## Points importants

- API Gateway sert de porte d'entree HTTP pour une application serverless.
- Lambda Proxy Integration transmet la requete HTTP complete a Lambda.
- La Lambda doit retourner une reponse compatible HTTP.
- CORS doit etre configure explicitement.
- La validation API Gateway bloque les requetes invalides avant Lambda.
- Un stage API Gateway est un snapshot deploye : une modification non redeployee n'est pas visible.
- Les permissions Lambda doivent limiter API Gateway via `source-arn`.
