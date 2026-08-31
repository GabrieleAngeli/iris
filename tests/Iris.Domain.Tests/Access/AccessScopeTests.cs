using Iris.Domain.Access;

namespace Iris.Domain.Tests.Access;

public sealed class AccessScopeTests
{
    private static readonly Guid CustomerA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CustomerB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ContextA1 = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid ContextA2 = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    [Fact]
    public void Global_covers_every_target()
    {
        var global = AccessScope.Global();

        Assert.True(global.Covers(AccessScope.Global()));
        Assert.True(global.Covers(AccessScope.ForCustomer(CustomerA)));
        Assert.True(global.Covers(AccessScope.ForContext(CustomerA, ContextA1)));
    }

    [Fact]
    public void Customer_scope_covers_that_customer_and_its_contexts_only()
    {
        var scope = AccessScope.ForCustomer(CustomerA);

        Assert.True(scope.Covers(AccessScope.ForCustomer(CustomerA)));
        Assert.True(scope.Covers(AccessScope.ForContext(CustomerA, ContextA1)));
        Assert.False(scope.Covers(AccessScope.ForCustomer(CustomerB)));
        Assert.False(scope.Covers(AccessScope.Global()));
    }

    [Fact]
    public void Context_scope_covers_only_itself()
    {
        var scope = AccessScope.ForContext(CustomerA, ContextA1);

        Assert.True(scope.Covers(AccessScope.ForContext(CustomerA, ContextA1)));
        Assert.False(scope.Covers(AccessScope.ForContext(CustomerA, ContextA2)));
        Assert.False(scope.Covers(AccessScope.ForCustomer(CustomerA)));
    }

    [Fact]
    public void Factories_reject_empty_ids()
    {
        Assert.Throws<ArgumentException>(() => AccessScope.ForCustomer(Guid.Empty));
        Assert.Throws<ArgumentException>(() => AccessScope.ForContext(Guid.Empty, ContextA1));
        Assert.Throws<ArgumentException>(() => AccessScope.ForContext(CustomerA, Guid.Empty));
    }

    [Fact]
    public void Value_equality_is_by_components()
    {
        Assert.Equal(AccessScope.ForCustomer(CustomerA), AccessScope.ForCustomer(CustomerA));
        Assert.NotEqual(AccessScope.ForCustomer(CustomerA), AccessScope.ForCustomer(CustomerB));
    }
}
