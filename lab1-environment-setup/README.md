# Lab 1 - Configuration de l'environnement AWS

Ce lab prepare l'environnement local utilise par les labs suivants.

Contrairement aux Labs 2 et 3, il ne contient pas une application C# principale. Il documente surtout l'installation des outils, la configuration AWS locale et les commandes de verification.

## Objectifs

- Installer AWS CLI v2.
- Configurer un profil AWS local avec `aws configure`.
- Installer le SDK .NET.
- Installer un IDE adapte au developpement AWS/.NET.
- Verifier l'identite AWS active.
- Verifier les permissions de base avec AWS CLI.
- Tester l'acces S3 en creant puis supprimant un bucket temporaire.

## Outils verifies

AWS CLI :

```powershell
aws --version
```

Resultat observe :

```text
aws-cli/2.14.5 Python/3.11.6 Windows/10 exe/AMD64 prompt/off
```

.NET SDK :

```powershell
dotnet --version
```

Resultat observe :

```text
10.0.203
```

SDKs .NET disponibles :

```powershell
dotnet --list-sdks
```

Resultat observe :

```text
9.0.313 [C:\Program Files\dotnet\sdk]
10.0.203 [C:\Program Files\dotnet\sdk]
```

Le lab demande .NET 6 ou plus recent, donc cette machine est compatible.

## Configuration AWS

La configuration AWS locale se fait avec :

```powershell
aws configure
```

Cette commande demande :

- `AWS Access Key ID`
- `AWS Secret Access Key`
- `Default region name`
- `Default output format`

Pour ces labs, la region utilisee dans le code est :

```text
eu-west-3
```

Important : les fichiers de credentials AWS ne doivent jamais etre ajoutes au depot Git.

Ils sont stockes localement, generalement dans :

```text
~/.aws/credentials
~/.aws/config
```

Le fichier `.gitignore` du repo ignore le dossier `.aws/` par securite.

## Verification de l'identite AWS

Pour verifier quel utilisateur ou role AWS est actif :

```powershell
aws sts get-caller-identity
```

Cette commande affiche notamment :

- `UserId`
- `Account`
- `Arn`

Ces informations peuvent etre sensibles. Il vaut mieux eviter de les publier telles quelles dans un depot public.

## Verification du profil AWS

Pour voir quel profil et quelle region sont utilises :

```powershell
aws configure list
```

Cette commande permet de detecter rapidement si AWS CLI utilise le bon profil local.

## Verification des permissions

Lister les buckets S3 :

```powershell
aws s3 ls
```

Lister les roles IAM :

```powershell
aws iam list-roles --query "Roles[*].RoleName" --output table
```

Lister les regions EC2 :

```powershell
aws ec2 describe-regions --query "Regions[*].RegionName" --output table
```

Si une commande renvoie `AccessDenied`, il faut verifier les permissions IAM attachees a l'utilisateur ou au role.

## Test S3 temporaire

Le Lab 1 demande de verifier l'ecriture S3 avec un bucket temporaire.

Exemple PowerShell :

```powershell
$bucket = "lab1-test-$(Get-Date -UFormat %s)"
aws s3 mb s3://$bucket
aws s3 ls
aws s3 rb s3://$bucket
aws s3 ls
```

Le bucket doit etre cree, visible, puis supprime.

## Nettoyage

Verifier qu'aucun bucket de test ne reste :

```powershell
aws s3 ls
```

Les credentials doivent rester uniquement sur la machine locale, jamais dans GitHub.

## Points importants

- AWS CLI et AWS SDK for .NET utilisent les memes credentials locaux.
- `aws sts get-caller-identity` est la commande la plus pratique pour verifier son identite AWS active.
- Les access keys sont acceptables pour un lab local, mais il faut preferer les roles IAM pour des workloads deployes.
- Il faut toujours supprimer les ressources temporaires creees pendant les tests.
- Il ne faut jamais committer de secret AWS dans un depot Git.
