using APIHandler.Constants;
using System.Reflection;

namespace APIHandler.Tests.Constants;

public class ResourceNamesTests
{
    [Fact]
    public void ResourceNames_EnvironmentBackedProperties_UseConfiguredVariablesAndFailWhenMissing()
    {
        var mappings = new (string Property, string Variable, Type MissingException)[]
        {
            (nameof(ResourceNames.Region), "REGION", typeof(InvalidOperationException)),
            (nameof(ResourceNames.FeedbackTableName), "FEEDBACK_TABLE_NAME", typeof(ArgumentException)),
            (nameof(ResourceNames.WaiterListTableName), "WaiterListTableName", typeof(ArgumentException)),
            (nameof(ResourceNames.LocationsTableName), "locationsTable", typeof(ArgumentException)),
            (nameof(ResourceNames.DishesTableName), "dishesTable", typeof(ArgumentException)),
            (nameof(ResourceNames.ProfileImagesTableName), "profileImagesTable", typeof(ArgumentException)),
            (nameof(ResourceNames.ReportsTableName), "reportsTable", typeof(ArgumentException)),
            (nameof(ResourceNames.ReportsHistoryTableName), "reportsHistoryTable", typeof(ArgumentException)),
            (nameof(ResourceNames.UserPoolId), "COGNITO_USER_POOL_ID", typeof(ArgumentException)),
            (nameof(ResourceNames.ClientId), "COGNITO_USER_POOL_CLIENT_ID", typeof(ArgumentException)),
            (nameof(ResourceNames.ReservationsTableName), "reservationsTable", typeof(ArgumentException)),
            (nameof(ResourceNames.GroupReservationsTableName), "groupReservationsTable", typeof(ArgumentException)),
            (nameof(ResourceNames.TablesTableName), "tablesTable", typeof(ArgumentException)),
            (nameof(ResourceNames.OrdersTableName), "ordersTable", typeof(ArgumentException)),
            (nameof(ResourceNames.NotificationsTableName), "notificationsTable", typeof(ArgumentException)),
            (nameof(ResourceNames.NotificationsUserIdIndex), "notificationsUserIdIndex", typeof(ArgumentException)),
            (nameof(ResourceNames.ReservationsTableGlobalIndex), "reservationsTableGlobalIndex", typeof(ArgumentException)),
            (nameof(ResourceNames.WaitersGlobalIndex), "waitersGlobalIndex", typeof(ArgumentException)),
            (nameof(ResourceNames.ReservationsCustomerIdGlobalIndex), "reservationsCustomerIdGlobalIndex", typeof(ArgumentException)),
            (nameof(ResourceNames.ReservationsWaiterIdGlobalIndex), "reservationsWaiterIdGlobalIndex", typeof(ArgumentException)),
            (nameof(ResourceNames.ImagesBucketName), "imagesBucketName", typeof(ArgumentException)),
            (nameof(ResourceNames.FeedbackGlobalIndex), "feedbackGlobalIndex", typeof(ArgumentException)),
            (nameof(ResourceNames.ProfileImagesBucketName), "profileImagesBucketName", typeof(ArgumentException)),
            (nameof(ResourceNames.OrdersIndexReservationId), "OrderIndexReservationId", typeof(ArgumentException)),
            (nameof(ResourceNames.ReportQueueName), "reports_queue_name", typeof(ArgumentException)),
            (nameof(ResourceNames.GuestSessionTableName), "guestSessionTable", typeof(InvalidOperationException)),
            (nameof(ResourceNames.TablesLocationIdIndex), "tableLocationIdIndex", typeof(InvalidOperationException))
        };

        foreach (var (propertyName, variableName, missingException) in mappings)
        {
            var property = typeof(ResourceNames).GetProperty(propertyName)!;
            var original = Environment.GetEnvironmentVariable(variableName);
            try
            {
                Environment.SetEnvironmentVariable(variableName, "unit-test-value");
                Assert.Equal("unit-test-value", property.GetValue(null));

                Environment.SetEnvironmentVariable(variableName, null);
                var invocation = Assert.Throws<TargetInvocationException>(() => property.GetValue(null));
                Assert.IsType(missingException, invocation.InnerException);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variableName, original);
            }
        }
    }

    [Fact]
    public void ResourceNames_StableGlobalIndexNames_ReturnTheirContractValues()
    {
        Assert.Equal("GSI_CustomerId", ResourceNames.OrdersTableCustomerIdGlobalIndex);
        Assert.Equal("GSI_ReservationId", ResourceNames.OrdersTableReservationIdGlobalIndex);
        Assert.Equal("UserIdIndex", ResourceNames.ReportsHistoryTableUserIdGlobalIndex);
    }
}
