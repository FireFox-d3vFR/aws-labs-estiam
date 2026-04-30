# Lab 2 - Amazon S3 avec .NET et AWS CLI

Ce lab montre comment utiliser Amazon S3 depuis une application C# avec le SDK AWS, puis comment publier un petit site statique avec S3 et l'AWS CLI.

## Objectifs realises

- Creer un bucket S3 depuis une application C#.
- Verifier que le bucket existe.
- Uploader un objet avec des metadonnees personnalisees.
- Telecharger un objet, transformer son contenu, puis le re-uploader.
- Creer les fichiers d'un site statique.
- Activer l'hebergement statique S3.
- Synchroniser les fichiers du site vers le bucket.
- Autoriser la lecture publique des fichiers via une bucket policy.

## Prerequis

- AWS CLI configure avec `aws configure`.
- Identifiants AWS locaux disponibles, par exemple dans `~/.aws/credentials`.
- .NET SDK installe.
- Droits IAM suffisants sur S3.

## Structure

```text
S3Lab/
|-- Program.cs
|-- S3Lab.csproj
|-- bucket-policy.json
|-- website/
|   |-- index.html
|   `-- error.html
`-- README.md
```

## 1. Configuration du client S3

Dans `Program.cs`, le client S3 est initialise avec la region `EUWest3` :

```csharp
var s3Client = new AmazonS3Client(RegionEndpoint.EUWest3);
```

Les identifiants AWS ne sont pas ecrits dans le code. Le SDK les recupere automatiquement depuis la configuration locale AWS.

## 2. Creation du bucket

Le bucket est cree avec un nom unique base sur le timestamp Unix :

```csharp
string bucketName = $"lab2-bucket-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
```

Puis il est cree avec :

```csharp
await s3Client.PutBucketAsync(new PutBucketRequest
{
    BucketName = bucketName,
    UseClientRegion = true
});
```

Le programme affiche ensuite le nom du bucket. Il faut le conserver pour les commandes CLI suivantes.

En PowerShell, on peut le stocker dans une variable d'environnement :

```powershell
$env:BUCKET_NAME = "lab2-bucket-xxxxxxxxxx"
```

## 3. Verification du bucket

Le programme verifie que le bucket existe avec :

```csharp
await s3Client.GetBucketLocationAsync(bucketName);
```

Si le bucket n'existe pas ou si AWS renvoie une erreur, une exception `AmazonS3Exception` est capturee.

## 4. Upload d'un objet avec metadonnees

Le fichier `data/sample.txt` est cree dans S3 avec un contenu texte :

```text
Hello from Lab 2! This is sample data.
```

Des metadonnees personnalisees sont ajoutees :

- `author`
- `lab`
- `timestamp`

Ces metadonnees permettent d'ajouter des informations descriptives a un objet S3 sans modifier son contenu.

## 5. Download, transformation et re-upload

Le programme telecharge `data/sample.txt`, lit son contenu, puis le transforme en majuscules.

Le resultat est re-uploade dans :

```text
data/processed.txt
```

Exemple de contenu obtenu :

```text
HELLO FROM LAB 2! THIS IS SAMPLE DATA.
[Processed at 2026-04-30 12:16:37Z]
```

Cette logique correspond a un scenario courant de traitement de donnees : lire un fichier, le transformer, puis stocker le resultat.

## 6. Fichiers du site statique

Deux fichiers HTML ont ete crees dans le dossier `website/` :

```text
website/index.html
website/error.html
```

`index.html` est la page principale du site.

`error.html` est la page affichee en cas d'erreur, par exemple si une page demandee n'existe pas.

## 7. Activation du site statique S3

Le bucket est configure pour servir un site web statique avec :

```powershell
aws s3 website s3://$env:BUCKET_NAME --index-document index.html --error-document error.html
```

Cette commande indique a S3 quel fichier utiliser comme page d'accueil et quel fichier utiliser comme page d'erreur.

## 8. Upload des fichiers du site

Les fichiers locaux du dossier `website/` sont envoyes dans le bucket avec :

```powershell
aws s3 sync website/ s3://$env:BUCKET_NAME
```

`aws s3 sync` compare le dossier local et le bucket, puis upload uniquement les fichiers nouveaux ou modifies.

## 9. Desactivation de Block Public Access

Par defaut, AWS bloque l'acces public aux nouveaux buckets.

Pour qu'un site statique S3 soit accessible depuis un navigateur, il faut supprimer ce blocage :

```powershell
aws s3api delete-public-access-block --bucket $env:BUCKET_NAME
```

Cette commande ne rend pas le bucket public directement. Elle enleve seulement le verrou qui empeche une policy publique de fonctionner.

## 10. Bucket policy pour lecture publique

Le fichier `bucket-policy.json` contient une policy de lecture publique. Avant de l'appliquer, remplacer `YOUR-BUCKET-NAME` par le nom du bucket.

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "PublicReadGetObject",
      "Effect": "Allow",
      "Principal": "*",
      "Action": "s3:GetObject",
      "Resource": "arn:aws:s3:::YOUR-BUCKET-NAME/*"
    }
  ]
}
```

Cette policy autorise tout le monde a lire les objets du bucket avec l'action `s3:GetObject`.

Elle s'applique avec :

```powershell
aws s3api put-bucket-policy --bucket $env:BUCKET_NAME --policy file://bucket-policy.json
```

## URL du site

L'URL du site suit ce format pour la region `eu-west-3` :

```text
http://YOUR-BUCKET-NAME.s3-website.eu-west-3.amazonaws.com
```

Attention : pour `eu-west-3`, l'endpoint correct utilise la forme :

```text
s3-website.eu-west-3.amazonaws.com
```

et non :

```text
s3-website-eu-west-3.amazonaws.com
```

## Commandes de verification utiles

Lister les objets du bucket :

```powershell
aws s3 ls s3://$env:BUCKET_NAME
```

Verifier les metadonnees de `data/sample.txt` :

```powershell
aws s3api head-object --bucket $env:BUCKET_NAME --key data/sample.txt
```

Lire le fichier transforme :

```powershell
aws s3 cp s3://$env:BUCKET_NAME/data/processed.txt -
```

Verifier la configuration website :

```powershell
aws s3api get-bucket-website --bucket $env:BUCKET_NAME
```

## Nettoyage

Quand le lab est termine, il faut supprimer le bucket pour eviter de conserver une ressource publique inutile :

```powershell
aws s3 rb s3://$env:BUCKET_NAME --force
```

L'option `--force` supprime d'abord tous les objets du bucket, puis supprime le bucket.

## Points importants a retenir

- Un bucket S3 doit avoir un nom globalement unique.
- Les objets S3 peuvent avoir des metadonnees personnalisees.
- `PutObjectAsync` sert a envoyer un objet dans S3.
- `GetObjectAsync` sert a telecharger un objet depuis S3.
- `aws s3 sync` est pratique pour envoyer un dossier complet vers S3.
- Pour un site statique public, il faut a la fois desactiver Block Public Access et ajouter une bucket policy publique.
- Une bucket policy publique doit etre manipulee avec prudence, car elle rend les objets accessibles a n'importe qui.
