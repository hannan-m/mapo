using FluentAssertions;
using Mapo.Attributes;
using Xunit;

namespace Mapo.IntegrationTests.Features;

// =============================================================================
// Models: Polymorphic mapping where base Configure provides shared mappings
// that derived types should inherit — this was the bug fixed in ConfigParser.
// =============================================================================

public abstract class PaymentBase
{
    public string ProviderName { get; set; } = "";
    public DateTime AuthorizedAt { get; set; }
}

public class CardPayment : PaymentBase
{
    public string Brand { get; set; } = "";
    public string LastFour { get; set; } = "";
}

public class WirePayment : PaymentBase
{
    public string BankName { get; set; } = "";
    public string RoutingNumber { get; set; } = "";
}

public abstract class PaymentBaseDto
{
    public string Provider { get; set; } = "";
    public DateTime AuthorizedAt { get; set; }
}

public class CardPaymentDto : PaymentBaseDto
{
    public string CardDisplay { get; set; } = "";

    public CardPaymentDto(string provider, DateTime authorizedAt, string cardDisplay)
    {
        Provider = provider;
        AuthorizedAt = authorizedAt;
        CardDisplay = cardDisplay;
    }
}

public class WirePaymentDto : PaymentBaseDto
{
    public string BankInfo { get; set; } = "";

    public WirePaymentDto(string provider, DateTime authorizedAt, string bankInfo)
    {
        Provider = provider;
        AuthorizedAt = authorizedAt;
        BankInfo = bankInfo;
    }
}

[Mapper]
public partial class PolymorphicInheritanceMapper
{
    [MapDerived(typeof(CardPayment), typeof(CardPaymentDto))]
    [MapDerived(typeof(WirePayment), typeof(WirePaymentDto))]
    public partial PaymentBaseDto? MapPayment(PaymentBase? payment);

    // Base Configure: maps Provider from ProviderName for ALL derived types
    static void Configure(IMapConfig<PaymentBase, PaymentBaseDto> config)
    {
        config.Map(d => d.Provider, s => s.ProviderName);
    }

    // Derived Configure: adds CardDisplay mapping
    static void Configure(IMapConfig<CardPayment, CardPaymentDto> config)
    {
        config.Map(d => d.CardDisplay, s => $"{s.Brand} ****{s.LastFour}");
    }

    // Derived Configure: adds BankInfo mapping
    static void Configure(IMapConfig<WirePayment, WirePaymentDto> config)
    {
        config.Map(d => d.BankInfo, s => $"{s.BankName} ({s.RoutingNumber})");
    }
}

public class PolymorphicInheritanceTests
{
    [Fact]
    public void DerivedType_ShouldInheritBaseConfigure_Provider()
    {
        var mapper = new PolymorphicInheritanceMapper();
        var card = new CardPayment
        {
            ProviderName = "Stripe",
            AuthorizedAt = new DateTime(2025, 1, 15),
            Brand = "Visa",
            LastFour = "4242"
        };

        var dto = mapper.MapPayment(card);

        dto.Should().BeOfType<CardPaymentDto>();
        var cardDto = (CardPaymentDto)dto!;
        cardDto.Provider.Should().Be("Stripe", "base Configure maps ProviderName → Provider");
        cardDto.CardDisplay.Should().Be("Visa ****4242");
        cardDto.AuthorizedAt.Should().Be(new DateTime(2025, 1, 15));
    }

    [Fact]
    public void DerivedType_WirePayment_ShouldInheritBaseConfigure()
    {
        var mapper = new PolymorphicInheritanceMapper();
        var wire = new WirePayment
        {
            ProviderName = "SWIFT",
            AuthorizedAt = new DateTime(2025, 3, 1),
            BankName = "Chase",
            RoutingNumber = "021000021"
        };

        var dto = mapper.MapPayment(wire);

        dto.Should().BeOfType<WirePaymentDto>();
        var wireDto = (WirePaymentDto)dto!;
        wireDto.Provider.Should().Be("SWIFT", "base Configure maps ProviderName → Provider");
        wireDto.BankInfo.Should().Be("Chase (021000021)");
    }

    [Fact]
    public void PolymorphicDispatch_ShouldReturnCorrectDerivedType()
    {
        var mapper = new PolymorphicInheritanceMapper();

        PaymentBase card = new CardPayment { ProviderName = "PayPal", Brand = "MC", LastFour = "9999" };
        PaymentBase wire = new WirePayment { ProviderName = "ACH", BankName = "Wells", RoutingNumber = "123" };

        mapper.MapPayment(card).Should().BeOfType<CardPaymentDto>();
        mapper.MapPayment(wire).Should().BeOfType<WirePaymentDto>();
    }

    [Fact]
    public void NullInput_ShouldThrowArgumentNullException()
    {
        var mapper = new PolymorphicInheritanceMapper();

        var act = () => mapper.MapPayment(null);
        act.Should().Throw<ArgumentNullException>();
    }
}
