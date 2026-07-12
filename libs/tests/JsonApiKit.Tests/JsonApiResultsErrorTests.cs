using Microsoft.AspNetCore.Http.HttpResults;

namespace JsonApiKit.Tests;

public class JsonApiResultsErrorTests
{
    [Fact]
    public void Error_produces_problem_details_result()
    {
        var result = JsonApiResults.Error(new JsonApiError
        {
            StatusCode = 422,
            Title = "Validation failed",
            Detail = "The 'title' field is required."
        });

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(422, problem.StatusCode);
        Assert.Equal("Validation failed", problem.ProblemDetails.Title);
        Assert.Equal("The 'title' field is required.", problem.ProblemDetails.Detail);
    }

    [Fact]
    public void Shorthands_map_to_status_codes()
    {
        Assert.Equal(404, Assert.IsType<ProblemHttpResult>(JsonApiResults.NotFound("gone")).StatusCode);
        Assert.Equal(422, Assert.IsType<ProblemHttpResult>(JsonApiResults.Validation("bad")).StatusCode);
        Assert.Equal(400, Assert.IsType<ProblemHttpResult>(JsonApiResults.BadRequest("Invalid", "nope")).StatusCode);
    }
}
