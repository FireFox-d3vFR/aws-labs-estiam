using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Newtonsoft.Json;
using System.Globalization;

const string TableName = "Products";
const string CategoryIndexName = "CategoryIndex";

var ddb = new AmazonDynamoDBClient(RegionEndpoint.EUWest3);

await EnsureTableAsync(ddb);
await PutSingleItemAsync(ddb);
await GetItemByPrimaryKeyAsync(ddb);
await BatchLoadProductsAsync(ddb);
await QueryElectronicsUnder100Async(ddb);
await UpdateOutOfStockProductAsync(ddb);
await QueryBooksWithPartiQlAsync(ddb);
await UseObjectPersistenceModelAsync(ddb);

Console.WriteLine();
Console.WriteLine("Lab 3 completed.");
Console.WriteLine("CLI checks:");
Console.WriteLine($"aws dynamodb scan --table-name {TableName} --output table");
Console.WriteLine($"aws dynamodb get-item --table-name {TableName} --key '{{\"ProductId\":{{\"S\":\"P001\"}},\"Category\":{{\"S\":\"Electronics\"}}}}'");

static async Task EnsureTableAsync(IAmazonDynamoDB ddb)
{
    try
    {
        await ddb.CreateTableAsync(new CreateTableRequest
        {
            TableName = TableName,
            AttributeDefinitions =
            [
                new AttributeDefinition("ProductId", ScalarAttributeType.S),
                new AttributeDefinition("Category", ScalarAttributeType.S)
            ],
            KeySchema =
            [
                new KeySchemaElement("ProductId", KeyType.HASH),
                new KeySchemaElement("Category", KeyType.RANGE)
            ],
            GlobalSecondaryIndexes =
            [
                new GlobalSecondaryIndex
                {
                    IndexName = CategoryIndexName,
                    KeySchema =
                    [
                        new KeySchemaElement("Category", KeyType.HASH),
                        new KeySchemaElement("ProductId", KeyType.RANGE)
                    ],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL }
                }
            ],
            BillingMode = BillingMode.PAY_PER_REQUEST
        });

        Console.WriteLine($"Table {TableName} creation requested.");
    }
    catch (ResourceInUseException)
    {
        Console.WriteLine($"Table {TableName} already exists.");
    }

    Console.Write($"Waiting for table {TableName}");
    while (true)
    {
        var description = await ddb.DescribeTableAsync(TableName);
        if (description.Table.TableStatus == TableStatus.ACTIVE)
        {
            Console.WriteLine(" ACTIVE.");
            return;
        }

        Console.Write(".");
        await Task.Delay(2000);
    }
}

static async Task PutSingleItemAsync(IAmazonDynamoDB ddb)
{
    await ddb.PutItemAsync(new PutItemRequest
    {
        TableName = TableName,
        Item = new Dictionary<string, AttributeValue>
        {
            ["ProductId"] = new() { S = "P001" },
            ["Category"] = new() { S = "Electronics" },
            ["Name"] = new() { S = "Wireless Headphones" },
            ["Price"] = new() { N = "79.99" },
            ["InStock"] = new() { BOOL = true },
            ["UpdatedAt"] = new() { S = DateTime.UtcNow.ToString("o") }
        }
    });

    Console.WriteLine("Item P001 inserted with the low-level API.");
}

static async Task GetItemByPrimaryKeyAsync(IAmazonDynamoDB ddb)
{
    var response = await ddb.GetItemAsync(new GetItemRequest
    {
        TableName = TableName,
        Key = new Dictionary<string, AttributeValue>
        {
            ["ProductId"] = new() { S = "P001" },
            ["Category"] = new() { S = "Electronics" }
        }
    });

    if (response.Item.Count == 0)
    {
        Console.WriteLine("Item P001 not found.");
        return;
    }

    Console.WriteLine($"Found: {response.Item["Name"].S} at {response.Item["Price"].N} EUR.");
}

static async Task BatchLoadProductsAsync(IAmazonDynamoDB ddb)
{
    var raw = await File.ReadAllTextAsync("products.json");
    var products = JsonConvert.DeserializeObject<List<ProductSeed>>(raw) ?? [];

    var requests = products.Select(product => new WriteRequest
    {
        PutRequest = new PutRequest
        {
            Item = new Dictionary<string, AttributeValue>
            {
                ["ProductId"] = new() { S = product.ProductId },
                ["Category"] = new() { S = product.Category },
                ["Name"] = new() { S = product.Name },
                ["Price"] = new() { N = product.Price.ToString(CultureInfo.InvariantCulture) },
                ["InStock"] = new() { BOOL = product.InStock },
                ["UpdatedAt"] = new() { S = DateTime.UtcNow.ToString("o") }
            }
        }
    }).ToList();

    var requestItems = new Dictionary<string, List<WriteRequest>>
    {
        [TableName] = requests
    };

    do
    {
        var response = await ddb.BatchWriteItemAsync(new BatchWriteItemRequest
        {
            RequestItems = requestItems
        });

        requestItems = response.UnprocessedItems;
    }
    while (requestItems.Count > 0);

    Console.WriteLine($"{products.Count} items batch-loaded from products.json.");
}

static async Task QueryElectronicsUnder100Async(IAmazonDynamoDB ddb)
{
    Console.WriteLine("Electronics under 100 EUR:");

    Dictionary<string, AttributeValue>? lastEvaluatedKey = null;
    var total = 0;

    do
    {
        var response = await ddb.QueryAsync(new QueryRequest
        {
            TableName = TableName,
            IndexName = CategoryIndexName,
            KeyConditionExpression = "#Category = :category",
            FilterExpression = "Price < :max",
            ProjectionExpression = "ProductId, #Name, Price",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#Category"] = "Category",
                ["#Name"] = "Name"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":category"] = new() { S = "Electronics" },
                [":max"] = new() { N = "100" }
            },
            ExclusiveStartKey = lastEvaluatedKey,
            Limit = 2
        });

        foreach (var item in response.Items)
        {
            total++;
            Console.WriteLine($"- {item["Name"].S} ({item["ProductId"].S}) -- {item["Price"].N} EUR");
        }

        lastEvaluatedKey = response.LastEvaluatedKey is null || response.LastEvaluatedKey.Count == 0
            ? null
            : response.LastEvaluatedKey;
    }
    while (lastEvaluatedKey is not null);

    Console.WriteLine($"Found {total} result(s).");
}

static async Task UpdateOutOfStockProductAsync(IAmazonDynamoDB ddb)
{
    try
    {
        await ddb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["ProductId"] = new() { S = "P004" },
                ["Category"] = new() { S = "Books" }
            },
            UpdateExpression = "SET Price = :price, UpdatedAt = :timestamp",
            ConditionExpression = "InStock = :false",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":price"] = new() { N = "24.99" },
                [":timestamp"] = new() { S = DateTime.UtcNow.ToString("o") },
                [":false"] = new() { BOOL = false }
            }
        });

        Console.WriteLine("P004 updated because it was out of stock.");
    }
    catch (ConditionalCheckFailedException)
    {
        Console.WriteLine("P004 update skipped because the item is currently in stock.");
    }
}

static async Task QueryBooksWithPartiQlAsync(IAmazonDynamoDB ddb)
{
    var response = await ddb.ExecuteStatementAsync(new ExecuteStatementRequest
    {
        Statement = $"SELECT ProductId, Name, Price FROM {TableName} WHERE Category = 'Books'"
    });

    Console.WriteLine("Books via PartiQL:");
    foreach (var item in response.Items)
    {
        Console.WriteLine($"- {item["Name"].S} ({item["ProductId"].S}) -- {item["Price"].N} EUR");
    }
}

static async Task UseObjectPersistenceModelAsync(IAmazonDynamoDB ddb)
{
    var context = new DynamoDBContextBuilder()
        .WithDynamoDBClient(() => ddb)
        .Build();

    await context.SaveAsync(new Product
    {
        ProductId = "P007",
        Category = "Electronics",
        Name = "Bluetooth Speaker",
        Price = 59.99m,
        InStock = true,
        UpdatedAt = DateTime.UtcNow.ToString("o")
    });

    var product = await context.LoadAsync<Product>("P007", "Electronics");
    Console.WriteLine($"OPM load: {product.Name} at {product.Price} EUR.");

    await context.DeleteAsync<Product>("P007", "Electronics");
    Console.WriteLine("P007 deleted with the Object Persistence Model.");
}

public class ProductSeed
{
    public string ProductId { get; set; } = "";
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public bool InStock { get; set; }
}

[DynamoDBTable("Products")]
public class Product
{
    [DynamoDBHashKey]
    public string ProductId { get; set; } = "";

    [DynamoDBRangeKey]
    public string Category { get; set; } = "";

    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public bool InStock { get; set; }
    public string UpdatedAt { get; set; } = "";
}
