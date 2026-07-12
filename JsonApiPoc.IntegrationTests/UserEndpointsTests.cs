namespace JsonApiPoc.IntegrationTests;

[Collection(ApiCollection.Name)]
public class UserEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task List_Default_ReturnsArrangedUsers()
    {
        // Arrange
        var (sarah, marcus) = await ArrangeAsync(db =>
            (db.Users.Add(Rows.User("Sarah Chen")).Entity, db.Users.Add(Rows.User("Marcus Webb")).Entity));

        // Act
        var document = await Client.GetDocumentAsync(Routes.Users);

        // Assert — the entire response: both users in id order, full pagination links, meta.
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Users,
                null,
                number: 1, size: 25, total: 2,
                [Document.User(sarah), Document.User(marcus)]));
    }

    [Fact]
    public async Task List_SortByName_OrdersAlphabetically()
    {
        // Arrange — inserted in reverse of the expected order.
        var (sarah, marcus) = await ArrangeAsync(db =>
            (db.Users.Add(Rows.User("Sarah Chen")).Entity, db.Users.Add(Rows.User("Marcus Webb")).Entity));

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Users}?sort={Attr.Name}");

        // Assert
        document.ShouldMatchExactly(
            Document.Page(
                Routes.Users,
                $"sort={Attr.Name}",
                1, 25, total: 2,
                [Document.User(marcus), Document.User(sarah)]));
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsUser()
    {
        // Arrange
        var user = await ArrangeAsync(db => db.Users.Add(Rows.User("Sarah Chen")).Entity);

        // Act
        var document = await Client.GetDocumentAsync($"{Routes.Users}/{user.Id}");

        // Assert
        document.ShouldMatchExactly(Document.Single(Document.User(user)));
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        // Act
        var problem = await Client.GetProblemAsync($"{Routes.Users}/99999", 404);

        // Assert
        problem.ShouldMatchExactly(Document.Problem(404, "Not found", "User '99999' does not exist."));
    }
}
