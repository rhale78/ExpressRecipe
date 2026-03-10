using ExpressRecipe.Shared.Settings;
using ExpressRecipe.Shared.Units;

namespace ExpressRecipe.RecipeParser.Tests.Units;

public class UserDisplaySettingsTests
{
    [Fact]
    public void Defaults_ReturnsExpectedValues()
    {
        UserDisplaySettings settings = UserDisplaySettings.Defaults;
        settings.NumberFormat.Should().Be(NumberFormat.Fraction);
        settings.UnitSystem.Should().Be(UnitSystemPreference.US);
    }

    [Fact]
    public void FromJson_ValidJson_DeserializesCorrectly()
    {
        string json = """{"NumberFormat":"Decimal","UnitSystem":"Metric"}""";
        UserDisplaySettings settings = UserDisplaySettings.FromJson(json);
        settings.NumberFormat.Should().Be(NumberFormat.Decimal);
        settings.UnitSystem.Should().Be(UnitSystemPreference.Metric);
    }

    [Fact]
    public void FromJson_NullInput_ReturnsDefaults()
    {
        UserDisplaySettings settings = UserDisplaySettings.FromJson(null);
        settings.NumberFormat.Should().Be(NumberFormat.Fraction);
        settings.UnitSystem.Should().Be(UnitSystemPreference.US);
    }

    [Fact]
    public void FromJson_EmptyInput_ReturnsDefaults()
    {
        UserDisplaySettings settings = UserDisplaySettings.FromJson("");
        settings.NumberFormat.Should().Be(NumberFormat.Fraction);
        settings.UnitSystem.Should().Be(UnitSystemPreference.US);
    }

    [Fact]
    public void FromJson_InvalidJson_ReturnsDefaults()
    {
        UserDisplaySettings settings = UserDisplaySettings.FromJson("{totally invalid}");
        settings.NumberFormat.Should().Be(NumberFormat.Fraction);
        settings.UnitSystem.Should().Be(UnitSystemPreference.US);
    }

    [Fact]
    public void ToJson_RoundTrip_PreservesValues()
    {
        UserDisplaySettings original = new()
        {
            NumberFormat = NumberFormat.Decimal,
            UnitSystem = UnitSystemPreference.UK
        };
        string json = original.ToJson();
        UserDisplaySettings restored = UserDisplaySettings.FromJson(json);
        restored.NumberFormat.Should().Be(NumberFormat.Decimal);
        restored.UnitSystem.Should().Be(UnitSystemPreference.UK);
    }
}
