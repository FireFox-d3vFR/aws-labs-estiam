using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HelloLambda;

public class Function
{
    private static readonly IAmazonS3 S3Client = new AmazonS3Client();

    public string FunctionHandler(string input, ILambdaContext context)
    {
        var stage = Environment.GetEnvironmentVariable("STAGE") ?? "unknown";

        context.Logger.LogInformation($"[{stage}] Received input: {input}");

        return $"Hello, {input}! Stage={stage} at {DateTime.UtcNow:o}";
    }

    public string PresignedUrlHandler(string input, ILambdaContext context)
    {
        var parts = input.Split('/', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new ArgumentException("Input must be 'bucket-name/object-key'.");
        }

        var bucketName = parts[0];
        var objectKey = parts[1];

        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddHours(1)
        };

        var url = S3Client.GetPreSignedURL(request);
        context.Logger.LogInformation($"Generated pre-signed URL for s3://{bucketName}/{objectKey}");

        return url;
    }
}
