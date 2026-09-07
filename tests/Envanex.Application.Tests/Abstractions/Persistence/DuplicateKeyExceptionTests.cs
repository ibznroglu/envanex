using Envanex.Application.Abstractions.Persistence;
using Shouldly;

namespace Envanex.Application.Tests.Abstractions.Persistence;

public class DuplicateKeyExceptionTests
{
    [Fact]
    public void DuplicateKeyException_ShouldExposeConstraintName_AndMeaningfulMessage()
    {
        const string constraintName = "IX_Products_Code";
        var innerException = new InvalidOperationException("inner");

        var exception = new DuplicateKeyException(constraintName, innerException);

        exception.ConstraintName.ShouldBe(constraintName);
        exception.Message.ShouldContain("duplicate key violation");
        exception.Message.ShouldContain(constraintName);
        exception.InnerException.ShouldBe(innerException);
    }
}
