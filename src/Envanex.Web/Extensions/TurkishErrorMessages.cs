using System.Collections.Frozen;

namespace Envanex.Web.Extensions;

public static class TurkishErrorMessages
{
    private static readonly FrozenDictionary<string, string> Messages = new Dictionary<string, string>
    {
        // Validation
        ["Validation.Failed"] = "Bir veya daha fazla do\u011frulama hatas\u0131 olu\u015ftu.",

        // Product - domain
        ["Product.CodeRequired"] = "\u00dcr\u00fcn kodu zorunludur.",
        ["Product.NameRequired"] = "\u00dcr\u00fcn ad\u0131 zorunludur.",
        ["Product.UnitOfMeasureRequired"] = "\u00d6l\u00e7\u00fc birimi se\u00e7ilmelidir.",
        ["Product.CodeTooLong"] = "\u00dcr\u00fcn kodu en fazla 50 karakter olabilir.",
        ["Product.NameTooLong"] = "\u00dcr\u00fcn ad\u0131 en fazla 200 karakter olabilir.",
        ["Product.NotFound"] = "\u00dcr\u00fcn bulunamad\u0131.",
        ["Product.ConcurrencyConflict"] = "Kay\u0131t ba\u015fka bir kullan\u0131c\u0131 taraf\u0131ndan de\u011fi\u015ftirilmi\u015f. L\u00fctfen sayfay\u0131 yenileyip tekrar deneyin.",
        ["Product.DuplicateCode"] = "Bu \u00fcr\u00fcn kodu zaten kullan\u0131l\u0131yor.",
        ["Product.UnitOfMeasureNotFound"] = "Belirtilen \u00f6l\u00e7\u00fc birimi bulunamad\u0131.",
        ["Product.UnitOfMeasureInactive"] = "Pasif bir \u00f6l\u00e7\u00fc birimi atanamaz.",

        // Product - validator
        ["Product.IdRequired"] = "\u00dcr\u00fcn kimli\u011fi zorunludur.",
        ["Product.RowVersionRequired"] = "E\u015fzamanl\u0131l\u0131k kontrol\u00fc i\u00e7in sat\u0131r s\u00fcr\u00fcm\u00fc zorunludur.",
        ["Product.ListPriceAmountNegative"] = "Liste fiyat\u0131 negatif olamaz.",
        ["Product.ReorderPointNegative"] = "Yeniden sipari\u015f noktas\u0131 negatif olamaz.",
        ["Product.ListPriceCurrencyRequired"] = "Liste fiyat\u0131 para birimi zorunludur.",

        // UnitOfMeasure
        ["UnitOfMeasure.CodeRequired"] = "\u00d6l\u00e7\u00fc birimi kodu zorunludur.",
        ["UnitOfMeasure.NameRequired"] = "\u00d6l\u00e7\u00fc birimi ad\u0131 zorunludur.",
        ["UnitOfMeasure.InvalidBaseUnitId"] = "Temel birim kimli\u011fi bo\u015f GUID olamaz.",
        ["UnitOfMeasure.BaseUnitFactorMustBeOne"] = "Temel birim i\u00e7in d\u00f6n\u00fc\u015f\u00fcm katsay\u0131s\u0131 1 olmal\u0131d\u0131r.",
        ["UnitOfMeasure.ConversionFactorMustBePositive"] = "T\u00fcretilmi\u015f birim i\u00e7in d\u00f6n\u00fc\u015f\u00fcm katsay\u0131s\u0131 s\u0131f\u0131rdan b\u00fcy\u00fck olmal\u0131d\u0131r.",
        ["UnitOfMeasure.CodeTooLong"] = "\u00d6l\u00e7\u00fc birimi kodu en fazla 20 karakter olabilir.",
        ["UnitOfMeasure.NameTooLong"] = "\u00d6l\u00e7\u00fc birimi ad\u0131 en fazla 200 karakter olabilir.",
        ["UnitOfMeasure.DuplicateCode"] = "Bu \u00f6l\u00e7\u00fc birimi kodu zaten kullan\u0131l\u0131yor.",
        ["UnitOfMeasure.NotFound"] = "\u00d6l\u00e7\u00fc birimi bulunamad\u0131.",
        ["UnitOfMeasure.BaseUnitNotFound"] = "Belirtilen temel \u00f6l\u00e7\u00fc birimi bulunamad\u0131.",
        ["UnitOfMeasure.BaseUnitInactive"] = "Pasif bir temel \u00f6l\u00e7\u00fc birimi atanamaz.",

        // Warehouse
        ["Warehouse.CodeRequired"] = "Depo kodu zorunludur.",
        ["Warehouse.NameRequired"] = "Depo ad\u0131 zorunludur.",

        // Value Objects
        ["Currency.InvalidCode"] = "Ge\u00e7ersiz para birimi kodu.",
        ["Money.CurrencyMismatch"] = "Farkl\u0131 para birimleri ile i\u015flem yap\u0131lamaz.",
        ["Quantity.Negative"] = "Miktar negatif olamaz.",
        ["Quantity.NegativeResult"] = "\u0130\u015flem sonucu negatif bir miktar olu\u015furdu.",
    }.ToFrozenDictionary();

    public static string GetMessage(string errorCode, string fallbackMessage)
    {
        return Messages.TryGetValue(errorCode, out var turkishMessage)
            ? turkishMessage
            : fallbackMessage;
    }

    public static IReadOnlySet<string> TranslatedErrorCodes { get; } = Messages.Keys.ToFrozenSet();
}
