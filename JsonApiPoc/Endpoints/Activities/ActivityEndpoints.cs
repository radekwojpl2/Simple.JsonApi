namespace JsonApiPoc.Endpoints.Activities;

public static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        var activities = app.MapGroup("/api/activities").WithTags("Activities");

        ListActivities.Map(activities);
        GetActivityById.Map(activities);
        GetActivityDeal.Map(activities);
        GetActivityDealRelationship.Map(activities);
        GetActivityContact.Map(activities);
        GetActivityContactRelationship.Map(activities);

        return app;
    }
}
