using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Helpers;
using APIHandler.Mappers;
using APIHandler.Models.Locations;
using APIHandler.Services.Caching;
using APIHandler.Services.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.Locations;

public class GetLocationsHandler : IApiRequestHandler
{
    private readonly IDynamoDBContext _dbContext;
    private readonly ILocationService _locationService;
    private readonly IApiResponseCache _responseCache;

    public GetLocationsHandler(
        IDynamoDBContext dbContext,
        ILocationService locationService,
        IApiResponseCache responseCache)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
        _responseCache = responseCache ?? throw new ArgumentNullException(nameof(responseCache));
    }

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            var responseModel = await _responseCache.GetOrCreateAsync(
                "GET /locations",
                request.QueryStringParameters,
                async () =>
            {
                var locations = await GetLocationsAsync();
                var mappedLocations = locations
                    .Select(LocationMapper.ToLocationResponse).ToList();
                foreach (var location in mappedLocations)
                {
                    var capacity = await _locationService.GetLocationCapacityAsync(location.Id);
                    var rating = await _locationService.GetLocationRatingAsync(location.Id);
                    location.TotalCapacity = capacity;
                    location.Rating = rating;

                    location.AverageOccupancy = OccupancyHelper.SanitizeLegacy(location.AverageOccupancy);
                }

                return mappedLocations;
            });

            return ResponseBuilder.CreateSuccessResponse(responseModel);
        }
        catch (JsonException ex)
        {
            return ResponseBuilder.CreateErrorResponse(400, "Invalid json body", context.Logger, ex);
        }
        catch (AmazonDynamoDBException ex)
        {
            return ResponseBuilder.CreateErrorResponse(503, "Database error occurred", context.Logger, ex);
        }
    }

    private async Task<List<Location>> GetLocationsAsync()
    {
        var scanConfig = DynamoConfig.ScanConfig(ResourceNames.LocationsTableName);
        return await _dbContext.ScanAsync<Location>(default, scanConfig).GetRemainingAsync();
    }
}
