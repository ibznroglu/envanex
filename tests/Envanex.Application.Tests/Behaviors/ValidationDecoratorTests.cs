using Envanex.Application.Abstractions.Messaging;
using Envanex.Application.Behaviors;
using Envanex.Domain.Common;
using FluentValidation;
using FluentValidation.Results;
using Shouldly;

namespace Envanex.Application.Tests.Behaviors;

public class ValidationDecoratorTests
{
    private sealed record TestCommand(string Name, string Code);

    private sealed class StubHandler : ICommandHandler<TestCommand, Guid>
    {
        public int CallCount { get; private set; }
        private readonly Guid _returnValue = Guid.CreateVersion7();

        public Task<Result<Guid>> HandleAsync(TestCommand command, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(Result.Success(_returnValue));
        }
    }

    private sealed class FailingNameValidator : AbstractValidator<TestCommand>
    {
        public FailingNameValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithErrorCode("Test.NameRequired");
        }
    }

    private sealed class FailingCodeValidator : AbstractValidator<TestCommand>
    {
        public FailingCodeValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty()
                .WithErrorCode("Test.CodeRequired");
        }
    }

    [Fact]
    public async Task HandleAsync_WithValidCommand_ShouldDelegateToInner()
    {
        var inner = new StubHandler();
        var validator = new FailingNameValidator();
        var decorator = new ValidationDecorator<TestCommand, Guid>(inner, [validator]);

        var result = await decorator.HandleAsync(new TestCommand("Valid", "ABC"));

        result.IsSuccess.ShouldBeTrue();
        inner.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidCommand_ShouldReturnFailure_WithoutCallingInner()
    {
        var inner = new StubHandler();
        var validator = new FailingNameValidator();
        var decorator = new ValidationDecorator<TestCommand, Guid>(inner, [validator]);

        var result = await decorator.HandleAsync(new TestCommand("", "ABC"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
        inner.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_WithNoValidators_ShouldDelegateToInner()
    {
        var inner = new StubHandler();
        var decorator = new ValidationDecorator<TestCommand, Guid>(
            inner, Enumerable.Empty<IValidator<TestCommand>>());

        var result = await decorator.HandleAsync(new TestCommand("", ""));

        result.IsSuccess.ShouldBeTrue();
        inner.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_WithMultipleInvalidFields_ShouldReturnAllFailures()
    {
        var inner = new StubHandler();
        var nameValidator = new FailingNameValidator();
        var codeValidator = new FailingCodeValidator();
        var decorator = new ValidationDecorator<TestCommand, Guid>(
            inner, [nameValidator, codeValidator]);

        var result = await decorator.HandleAsync(new TestCommand("", ""));

        result.IsFailure.ShouldBeTrue();
        var validationError = result.Error.ShouldBeOfType<ValidationError>();
        validationError.Failures.Count.ShouldBe(2);
        validationError.Failures.ShouldContain(f => f.ErrorCode == "Test.NameRequired");
        validationError.Failures.ShouldContain(f => f.ErrorCode == "Test.CodeRequired");
        inner.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_ValidationError_ShouldCarryPropertyNames()
    {
        var inner = new StubHandler();
        var nameValidator = new FailingNameValidator();
        var codeValidator = new FailingCodeValidator();
        var decorator = new ValidationDecorator<TestCommand, Guid>(
            inner, [nameValidator, codeValidator]);

        var result = await decorator.HandleAsync(new TestCommand("", ""));

        result.IsFailure.ShouldBeTrue();
        var validationError = result.Error.ShouldBeOfType<ValidationError>();
        validationError.Failures.ShouldContain(f => f.PropertyName == "Name" && f.ErrorCode == "Test.NameRequired");
        validationError.Failures.ShouldContain(f => f.PropertyName == "Code" && f.ErrorCode == "Test.CodeRequired");
    }
}
