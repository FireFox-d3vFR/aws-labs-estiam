using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System.Net;

// Credentials from ~/.aws/credentials (configured in Lab 1)
var s3Client = new AmazonS3Client(RegionEndpoint.EUWest3);

string bucketName = $"lab2-bucket-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
const string sampleKey = "data/sample.txt";
const string processedKey = "data/processed.txt";

Console.WriteLine($"Bucket name: {bucketName}");

// Create the bucket.
var createResp = await s3Client.PutBucketAsync(new PutBucketRequest
{
    BucketName = bucketName,
    UseClientRegion = true
});

Console.WriteLine($"Bucket created: HTTP {createResp.HttpStatusCode}");

// Verify using GetBucketLocation (throws if the bucket does not exist).
try
{
    await s3Client.GetBucketLocationAsync(bucketName);
    Console.WriteLine("Bucket verified successfully.");
}
catch (AmazonS3Exception ex)
{
    Console.WriteLine($"Error: {ex.ErrorCode} -- {ex.Message}");
    return;
}

// Upload a first object with custom metadata.
var putRequest = new PutObjectRequest
{
    BucketName = bucketName,
    Key = sampleKey,
    ContentBody = "Hello from Lab 2! This is sample data.",
    ContentType = "text/plain"
};

putRequest.Metadata.Add("x-amz-meta-author", "Dr. Abdelhak TOUITI");
putRequest.Metadata.Add("x-amz-meta-lab", "Lab2");
putRequest.Metadata.Add("x-amz-meta-timestamp", DateTime.UtcNow.ToString("o"));

var putResp = await s3Client.PutObjectAsync(putRequest);
Console.WriteLine($"Object uploaded with metadata: HTTP {putResp.HttpStatusCode}");

if (putResp.HttpStatusCode != HttpStatusCode.OK)
{
    Console.WriteLine("Upload did not return HTTP 200, stopping before processing.");
    return;
}

// Download the object, transform its content, then upload the processed version.
using var getResp = await s3Client.GetObjectAsync(bucketName, sampleKey);
using var reader = new StreamReader(getResp.ResponseStream);
string original = await reader.ReadToEndAsync();

Console.WriteLine($"Downloaded: {original}");

string processed = original.ToUpperInvariant()
    + $"{Environment.NewLine}[Processed at {DateTime.UtcNow:u}]";

var processedResp = await s3Client.PutObjectAsync(new PutObjectRequest
{
    BucketName = bucketName,
    Key = processedKey,
    ContentBody = processed,
    ContentType = "text/plain"
});

Console.WriteLine($"Processed object uploaded: HTTP {processedResp.HttpStatusCode}");
Console.WriteLine();
Console.WriteLine("Next CLI steps:");
Console.WriteLine($"$env:BUCKET_NAME = \"{bucketName}\"");
Console.WriteLine("aws s3 ls s3://$env:BUCKET_NAME/data/");
