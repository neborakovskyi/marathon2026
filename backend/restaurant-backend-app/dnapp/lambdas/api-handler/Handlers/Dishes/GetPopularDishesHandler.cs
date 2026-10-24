using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Helpers;
using APIHandler.Mappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.Dishes;

public class GetPopularDishesHandler : IApiRequestHandler
{
    private static readonly string DishesTableName = ResourceNames.DishesTableName;
    private readonly IDynamoDBContext _dbContext;
    private readonly IAmazonDynamoDB _dynamoDb;

    public GetPopularDishesHandler(IDynamoDBContext dbContext, IAmazonDynamoDB dynamoDb)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
    }

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var query = request.QueryStringParameters;
        var limit = 12;
        if (query?.TryGetValue("Limit", out var rawLimit) == true &&
            (!int.TryParse(rawLimit, out limit) || limit is < 1 or > 100))
        {
            return ResponseBuilder.CreateErrorResponse(400, "Limit must be between 1 and 100.", context.Logger);
        }

        Dictionary<string, AttributeValue>? startKey;
        try
        {
            var cursor = query is not null && query.TryGetValue("ExclusiveStartKeyJson", out var rawCursor)
                ? rawCursor
                : null;
            startKey = DecodeCursor(cursor);
        }
        catch (Exception)
        {
            return ResponseBuilder.CreateErrorResponse(400, "ExclusiveStartKeyJson is invalid.", context.Logger);
        }

        // Dish has only a hash key (id), so it cannot be queried without a partition value.
        // Scan still uses DynamoDB's key-based continuation; it does not use Skip/Take.
        context.Logger.LogLine($"Loading dishes page with limit {limit}.");
        var page = await _dynamoDb.ScanAsync(new ScanRequest
        {
            TableName = DishesTableName,
            Limit = limit,
            ExclusiveStartKey = startKey
        });

        var dishes = page.Items
            .Select(item => _dbContext.FromDocument<Dish>(Document.FromAttributeMap(item)))
            .ToList();
        var responseItems = dishes.ConvertAll(DishMapper.ToDishListResponse);
        context.Logger.LogLine($"Loaded {responseItems.Count} dishes; hasNextPage={page.LastEvaluatedKey?.Count > 0}.");

        return ResponseBuilder.CreateSuccessResponse(new
        {
            items = responseItems,
            nextPageCursor = EncodeCursor(page.LastEvaluatedKey)
        });
    }

    // Cursor JSON is { "id": "..." }, sent as base64 to remain URL-safe.
    private static Dictionary<string, AttributeValue>? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;

        string json;
        try { json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)); }
        catch (FormatException) { json = cursor; }

        using var document = JsonDocument.Parse(json);
        var id = document.RootElement.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(id)) throw new JsonException("Cursor has no id.");
        return new Dictionary<string, AttributeValue> { ["id"] = new AttributeValue { S = id } };
    }

    private static string? EncodeCursor(Dictionary<string, AttributeValue>? key)
    {
        if (key is null || key.Count == 0 || !key.TryGetValue("id", out var id) || string.IsNullOrEmpty(id.S))
            return null;
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id = id.S })));
    }
}
