using Iris.Domain.Common;

namespace Iris.Domain.Tests;

public sealed class EntityTests
{
    private sealed class Server(Guid id) : Entity<Guid>(id);

    private sealed class Application(Guid id) : Entity<Guid>(id);

    [Fact]
    public void Entities_of_same_type_with_same_id_are_equal()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new Server(id), new Server(id));
        Assert.Equal(new Server(id).GetHashCode(), new Server(id).GetHashCode());
    }

    [Fact]
    public void Entities_of_same_type_with_different_ids_are_not_equal()
    {
        Assert.NotEqual(new Server(Guid.NewGuid()), new Server(Guid.NewGuid()));
    }

    [Fact]
    public void Entities_of_different_types_are_never_equal_even_with_same_id()
    {
        var id = Guid.NewGuid();

        Assert.NotEqual<object>(new Server(id), new Application(id));
    }
}
