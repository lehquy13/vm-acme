using VmAcme.Common;

namespace VmAcme.Todos;

public sealed class ResultTests
{
    [Fact]
    public void Failure_does_not_allow_value_access()
    {
        var result = Result<int>.Failure(new Error("Todos.NotFound", "Missing", ErrorType.NotFound));
        Assert.True(result.IsFailure);
        Assert.Equal("Todos.NotFound", result.Error!.Code);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Success_has_value_and_no_error()
    {
        var result = Result<int>.Success(42);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(42, result.Value);
    }
}