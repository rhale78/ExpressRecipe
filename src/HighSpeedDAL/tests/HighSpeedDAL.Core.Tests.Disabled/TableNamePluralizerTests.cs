using Xunit;
using FluentAssertions;
using System;
using HighSpeedDAL.SourceGenerators.Utilities;

namespace HighSpeedDAL.Tests.Utilities;

/// <summary>
/// Comprehensive tests for TableNamePluralizer.
/// Validates English pluralization rules for table name generation.
/// </summary>
public class TableNamePluralizerTests
{
    #region Standard Pluralization Tests

    [Theory]
    [InlineData("Product", "Products")]
    [InlineData("Customer", "Customers")]
    [InlineData("Order", "Orders")]
    [InlineData("Invoice", "Invoices")]
    [InlineData("Employee", "Employees")]
    [InlineData("Department", "Departments")]
    public void Pluralize_StandardNouns_AddsS(string singular, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singular);

        // Assert
        result.Should().Be(expectedPlural);
    }

    #endregion

    #region Words Ending in Y Tests

    [Theory]
    [InlineData("Category", "Categories")]
    [InlineData("Company", "Companies")]
    [InlineData("City", "Cities")]
    [InlineData("Country", "Countries")]
    [InlineData("Facility", "Facilities")]
    public void Pluralize_ConsonantPlusY_ChangesToIes(string singular, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singular);

        // Assert
        result.Should().Be(expectedPlural);
    }

    [Theory]
    [InlineData("Day", "Days")]
    [InlineData("Key", "Keys")]
    [InlineData("Boy", "Boys")]
    [InlineData("Guy", "Guys")]
    public void Pluralize_VowelPlusY_AddsS(string singular, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singular);

        // Assert
        result.Should().Be(expectedPlural);
    }

    #endregion

    #region Words Ending in S, X, Z, CH, SH Tests

    [Theory]
    [InlineData("Address", "Addresses")]
    [InlineData("Business", "Businesses")]
    [InlineData("Status", "Statuses")]
    public void Pluralize_EndsInS_AddsEs(string singular, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singular);

        // Assert
        result.Should().Be(expectedPlural);
    }

    [Theory]
    [InlineData("Box", "Boxes")]
    [InlineData("Tax", "Taxes")]
    [InlineData("Index", "Indexes")] // Note: Both indexes and indices are valid
    public void Pluralize_EndsInX_AddsEs(string singular, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singular);

        // Assert
        result.Should().Be(expectedPlural);
    }

    [Theory]
    [InlineData("Church", "Churches")]
    [InlineData("Branch", "Branches")]
    [InlineData("Watch", "Watches")]
    public void Pluralize_EndsInCh_AddsEs(string singular, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singular);

        // Assert
        result.Should().Be(expectedPlural);
    }

    [Theory]
    [InlineData("Brush", "Brushes")]
    [InlineData("Dish", "Dishes")]
    [InlineData("Wish", "Wishes")]
    public void Pluralize_EndsInSh_AddsEs(string singular, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singular);

        // Assert
        result.Should().Be(expectedPlural);
    }

    #endregion

    #region Words Ending in F/FE Tests

    [Theory]
    [InlineData("Leaf", "Leaves")]
    [InlineData("Shelf", "Shelves")]
    [InlineData("Wolf", "Wolves")]
    [InlineData("Half", "Halves")]
    public void Pluralize_EndsInF_ChangesToVes(string singular, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singular);

        // Assert
        result.Should().Be(expectedPlural);
    }

    [Theory]
    [InlineData("Life", "Lives")]
    [InlineData("Wife", "Wives")]
    [InlineData("Knife", "Knives")]
    public void Pluralize_EndsInFe_ChangesToVes(string singular, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singular);

        // Assert
        result.Should().Be(expectedPlural);
    }

    #endregion

    #region Words Ending in O Tests

    [Theory]
    [InlineData("Hero", "Heroes")]
    [InlineData("Tomato", "Tomatoes")]
    [InlineData("Potato", "Potatoes")]
    public void Pluralize_ConsonantPlusO_AddsEs(string singular, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singular);

        // Assert
        result.Should().Be(expectedPlural);
    }

    [Theory]
    [InlineData("Video", "Videos")]
    [InlineData("Radio", "Radios")]
    [InlineData("Studio", "Studios")]
    public void Pluralize_VowelPlusO_AddsS(string singular, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singular);

        // Assert
        result.Should().Be(expectedPlural);
    }

    #endregion

    #region Irregular Plurals Tests

    [Theory]
    [InlineData("Person", "People")]
    [InlineData("Man", "Men")]
    [InlineData("Woman", "Women")]
    [InlineData("Child", "Children")]
    [InlineData("Tooth", "Teeth")]
    [InlineData("Foot", "Feet")]
    [InlineData("Mouse", "Mice")]
    [InlineData("Goose", "Geese")]
    [InlineData("Ox", "Oxen")]
    public void Pluralize_IrregularNouns_ReturnsCorrectPlural(string singular, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singular);

        // Assert
        result.Should().Be(expectedPlural);
    }

    [Theory]
    [InlineData("Deer", "Deer")]
    [InlineData("Sheep", "Sheep")]
    [InlineData("Fish", "Fish")]
    [InlineData("Moose", "Moose")]
    [InlineData("Series", "Series")]
    [InlineData("Species", "Species")]
    public void Pluralize_SameSingularAndPlural_ReturnsUnchanged(string singularPlural, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singularPlural);

        // Assert
        result.Should().Be(expectedPlural);
    }

    #endregion

    #region Case Sensitivity Tests

    [Theory]
    [InlineData("person", "people")]
    [InlineData("PERSON", "people")]
    [InlineData("Person", "People")]
    public void Pluralize_IrregularNouns_CaseInsensitive(string singular, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singular);

        // Assert
        result.Should().BeEquivalentTo(expectedPlural);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void Pluralize_NullInput_ThrowsArgumentException()
    {
        // Act
        Action act = () => TableNamePluralizer.Pluralize(null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public void Pluralize_EmptyString_ThrowsArgumentException()
    {
        // Act
        Action act = () => TableNamePluralizer.Pluralize(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public void Pluralize_WhitespaceOnly_ThrowsArgumentException()
    {
        // Act
        Action act = () => TableNamePluralizer.Pluralize("   ");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Theory]
    [InlineData("A", "As")]
    [InlineData("I", "Is")]
    [InlineData("O", "Oes")] // Single O is consonant+O rule
    public void Pluralize_SingleCharacter_HandlesCorrectly(string singular, string expectedPlural)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(singular);

        // Assert
        result.Should().Be(expectedPlural);
    }

    #endregion

    #region Real-World Entity Examples Tests

    [Theory]
    [InlineData("User", "Users")]
    [InlineData("Account", "Accounts")]
    [InlineData("Transaction", "Transactions")]
    [InlineData("Payment", "Payments")]
    [InlineData("Subscription", "Subscriptions")]
    [InlineData("Notification", "Notifications")]
    [InlineData("Message", "Messages")]
    [InlineData("Comment", "Comments")]
    [InlineData("Review", "Reviews")]
    [InlineData("Rating", "Ratings")]
    public void Pluralize_CommonEntityNames_GeneratesCorrectTableNames(string entity, string expectedTable)
    {
        // Act
        string result = TableNamePluralizer.Pluralize(entity);

        // Assert
        result.Should().Be(expectedTable);
    }

    #endregion
}
