using Envanex.Application.Authentication;
using Envanex.Web.Extensions;
using Shouldly;

namespace Envanex.IntegrationTests.Api;

/// <summary>
/// No database and no collection: these run in memory. The expected strings are written with
/// literal Turkish characters on purpose, so the assertion also proves that the shipped message
/// decodes to the intended characters rather than to escape sequences that merely match.
/// </summary>
public sealed class TurkishErrorMessagesTests
{
    private const string Fallback = "fallback";

    private const string InvalidCredentialsMessage =
        "E-posta veya parola hatalı. Arka arkaya birkaç başarısız denemeden sonra hesap bir süreliğine kilitlenir.";

    [Fact]
    public void InvalidCredentialsMessage_ShouldBeExactlyTheDecision6String()
    {
        var message = TurkishErrorMessages.GetMessage(AuthErrors.InvalidCredentials.Code, Fallback);

        message.ShouldBe(InvalidCredentialsMessage);
    }

    [Fact]
    public void InvalidCredentialsMessage_ShouldNotConfirmThatThisAccountIsLockedOut()
    {
        var message = TurkishErrorMessages.GetMessage(AuthErrors.InvalidCredentials.Code, Fallback);

        // The aorist "kilitlenir" states the policy. A past tense or a possessive would confirm
        // that this particular account exists and is locked, which is exactly the signal the one
        // indistinguishable answer exists to hide.
        string[] forbidden = ["kilitlendi", "kilitli", "hesabınız"];

        foreach (var term in forbidden)
        {
            message.Contains(term, StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                $"The message must not confirm this account's state, but it contains '{term}': {message}");
        }

        message.ShouldContain("kilitlenir");
    }

    [Fact]
    public void InvalidCredentialsMessage_ShouldOfferBothFactorsWithVeya()
    {
        var message = TurkishErrorMessages.GetMessage(AuthErrors.InvalidCredentials.Code, Fallback);

        int emailIndex = message.IndexOf("E-posta", StringComparison.Ordinal);
        int veyaIndex = message.IndexOf("veya", StringComparison.Ordinal);
        int passwordIndex = message.IndexOf("parola", StringComparison.Ordinal);

        emailIndex.ShouldBeGreaterThanOrEqualTo(0, $"The message must name the e-mail factor: {message}");
        veyaIndex.ShouldBeGreaterThan(emailIndex, $"The two factors must be joined by 'veya': {message}");
        passwordIndex.ShouldBeGreaterThan(veyaIndex, $"The password factor must follow 'veya': {message}");

        // Naming which factor failed would tell an attacker whether the address is registered.
        string[] forbidden = ["bulunamad", "kayıtlı değil", "yanlış parola", "hatalı parola"];

        foreach (var term in forbidden)
        {
            message.Contains(term, StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                $"The message must not single out one factor, but it contains '{term}': {message}");
        }
    }

    [Fact]
    public void PasswordRequiredMessage_ShouldBeExactlyParolaZorunludur()
    {
        var message = TurkishErrorMessages.GetMessage(AuthErrors.PasswordRequired.Code, Fallback);

        // Goes red if the message is written as "Şifre zorunludur."
        message.ShouldBe("Parola zorunludur.");
    }
}
