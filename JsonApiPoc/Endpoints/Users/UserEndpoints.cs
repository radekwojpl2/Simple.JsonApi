namespace JsonApiPoc.Endpoints.Users;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/users").WithTags("Users");

        ListUsers.Map(users);
        GetUserById.Map(users);

        return app;
    }
}
