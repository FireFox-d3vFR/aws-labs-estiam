using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HelloLambda;

public class Function
{
    private const string ActivityLogTableName = "ActivityLog";
    private static readonly IAmazonDynamoDB DynamoDb = new AmazonDynamoDBClient();
    private static readonly IAmazonS3 S3Client = new AmazonS3Client();

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var stage = Environment.GetEnvironmentVariable("STAGE") ?? "unknown";
        var name = GetName(request);
        var claims = request.RequestContext?.Authorizer?.Claims ?? new Dictionary<string, string>();
        var userId = claims.GetValueOrDefault("sub", "anonymous");
        var email = claims.GetValueOrDefault("email", "unknown");
        var timestamp = DateTime.UtcNow.ToString("o");

        context.Logger.LogInformation($"[{stage}] {request.HttpMethod} /hello by {email} ({userId}) name={name}");

        await DynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = ActivityLogTableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["UserId"] = new() { S = userId },
                ["Timestamp"] = new() { S = timestamp },
                ["Email"] = new() { S = email },
                ["Method"] = new() { S = request.HttpMethod },
                ["Name"] = new() { S = name },
                ["Stage"] = new() { S = stage }
            }
        });

        var body = JsonSerializer.Serialize(new
        {
            message = $"Hello, {name}!",
            user = email,
            method = request.HttpMethod,
            stage,
            timestamp
        });

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["Access-Control-Allow-Origin"] = "*"
            },
            Body = body
        };
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

    private static string GetName(APIGatewayProxyRequest request)
    {
        if (string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(request.Body))
        {
            using var document = JsonDocument.Parse(request.Body);
            if (document.RootElement.TryGetProperty("name", out var nameProperty))
            {
                return nameProperty.GetString() ?? "World";
            }
        }

        if (request.QueryStringParameters?.TryGetValue("name", out var name) == true)
        {
            return name;
        }

        return "World";
    }
}
