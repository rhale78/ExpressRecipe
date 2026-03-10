namespace ExpressRecipe.MealPlanningService.Services;

public interface ISeasonalProduceService
{
    List<string> GetInSeasonProduce(string region, DateOnly date);
    bool IsInSeason(string produceName, string region, DateOnly date);
    decimal GetSeasonalScoreBoost(IEnumerable<string> ingredientNames, string region, DateOnly date);
}

public sealed class SeasonalProduceService : ISeasonalProduceService
{
    private static readonly Dictionary<(string region, int month), string[]> Calendar = new()
    {
        // ── Northeast ─────────────────────────────────────────────────────────
        { ("northeast", 1),  new[]{ "Apple","Brussels Sprouts","Kale","Parsnip","Rutabaga","Winter Squash","Carrot","Beet" } },
        { ("northeast", 2),  new[]{ "Apple","Brussels Sprouts","Kale","Parsnip","Rutabaga","Winter Squash","Carrot" } },
        { ("northeast", 3),  new[]{ "Kale","Leek","Parsnip","Potato","Rutabaga","Spinach" } },
        { ("northeast", 4),  new[]{ "Asparagus","Rhubarb","Spinach","Lettuce","Radish","Pea" } },
        { ("northeast", 5),  new[]{ "Asparagus","Strawberry","Pea","Lettuce","Spinach","Radish","Rhubarb" } },
        { ("northeast", 6),  new[]{ "Strawberry","Pea","Lettuce","Spinach","Zucchini","Beet","Cucumber" } },
        { ("northeast", 7),  new[]{ "Tomato","Corn","Bean","Zucchini","Cucumber","Blueberry","Peach","Pepper" } },
        { ("northeast", 8),  new[]{ "Tomato","Corn","Pepper","Eggplant","Peach","Melon","Zucchini","Basil" } },
        { ("northeast", 9),  new[]{ "Tomato","Corn","Pepper","Apple","Pear","Squash","Pumpkin","Broccoli" } },
        { ("northeast", 10), new[]{ "Apple","Pear","Squash","Pumpkin","Kale","Brussels Sprouts","Turnip" } },
        { ("northeast", 11), new[]{ "Apple","Squash","Kale","Brussels Sprouts","Turnip","Carrot","Cranberry" } },
        { ("northeast", 12), new[]{ "Apple","Winter Squash","Kale","Root Vegetables","Potato" } },
        // ── Southeast ─────────────────────────────────────────────────────────
        { ("southeast", 1),  new[]{ "Collard Greens","Turnip","Sweet Potato","Citrus","Broccoli","Cauliflower" } },
        { ("southeast", 2),  new[]{ "Collard Greens","Strawberry","Citrus","Broccoli","Cabbage" } },
        { ("southeast", 3),  new[]{ "Strawberry","Blueberry","Broccoli","Asparagus","Pea" } },
        { ("southeast", 4),  new[]{ "Strawberry","Blueberry","Peach","Asparagus","Lettuce" } },
        { ("southeast", 5),  new[]{ "Strawberry","Blueberry","Peach","Tomato","Squash","Cucumber" } },
        { ("southeast", 6),  new[]{ "Peach","Blueberry","Tomato","Corn","Pepper","Squash","Watermelon" } },
        { ("southeast", 7),  new[]{ "Peach","Watermelon","Tomato","Corn","Okra","Pepper","Fig" } },
        { ("southeast", 8),  new[]{ "Watermelon","Tomato","Corn","Okra","Fig","Muscadine Grape" } },
        { ("southeast", 9),  new[]{ "Sweet Potato","Apple","Muscadine Grape","Squash","Collard Greens" } },
        { ("southeast", 10), new[]{ "Sweet Potato","Turnip","Collard Greens","Broccoli","Cauliflower" } },
        { ("southeast", 11), new[]{ "Sweet Potato","Collard Greens","Turnip","Citrus","Broccoli" } },
        { ("southeast", 12), new[]{ "Collard Greens","Turnip","Sweet Potato","Citrus","Broccoli" } },
        // ── Midwest ───────────────────────────────────────────────────────────
        { ("midwest", 1),  new[]{ "Apple","Winter Squash","Potato","Carrot","Beet","Kale" } },
        { ("midwest", 5),  new[]{ "Asparagus","Rhubarb","Strawberry","Lettuce","Radish" } },
        { ("midwest", 6),  new[]{ "Strawberry","Pea","Lettuce","Spinach","Zucchini","Beet" } },
        { ("midwest", 7),  new[]{ "Tomato","Corn","Bean","Blueberry","Pepper","Cucumber" } },
        { ("midwest", 8),  new[]{ "Tomato","Corn","Pepper","Peach","Melon","Eggplant","Basil" } },
        { ("midwest", 9),  new[]{ "Apple","Pear","Squash","Pumpkin","Tomato","Corn" } },
        { ("midwest", 10), new[]{ "Apple","Squash","Pumpkin","Kale","Brussels Sprouts","Turnip" } },
        // ── California ────────────────────────────────────────────────────────
        { ("california", 1),  new[]{ "Citrus","Avocado","Broccoli","Cauliflower","Kale","Spinach" } },
        { ("california", 2),  new[]{ "Citrus","Avocado","Broccoli","Artichoke","Pea" } },
        { ("california", 3),  new[]{ "Artichoke","Asparagus","Strawberry","Pea","Broccoli" } },
        { ("california", 4),  new[]{ "Artichoke","Avocado","Strawberry","Asparagus","Spinach" } },
        { ("california", 5),  new[]{ "Strawberry","Cherry","Apricot","Asparagus","Artichoke" } },
        { ("california", 6),  new[]{ "Strawberry","Cherry","Apricot","Peach","Blueberry","Tomato","Corn" } },
        { ("california", 7),  new[]{ "Peach","Nectarine","Plum","Tomato","Corn","Pepper","Melon" } },
        { ("california", 8),  new[]{ "Peach","Tomato","Pepper","Eggplant","Fig","Melon","Basil" } },
        { ("california", 9),  new[]{ "Grape","Apple","Pear","Tomato","Pepper","Winter Squash" } },
        { ("california", 10), new[]{ "Apple","Pear","Pomegranate","Persimmon","Kale","Broccoli" } },
        { ("california", 11), new[]{ "Citrus","Pomegranate","Kale","Broccoli","Artichoke" } },
        { ("california", 12), new[]{ "Citrus","Avocado","Kale","Broccoli","Cauliflower" } },
        // ── Southwest ─────────────────────────────────────────────────────────
        { ("southwest", 4),  new[]{ "Strawberry","Peach","Asparagus","Pea" } },
        { ("southwest", 6),  new[]{ "Peach","Watermelon","Tomato","Corn","Chili Pepper" } },
        { ("southwest", 8),  new[]{ "Watermelon","Chili Pepper","Corn","Eggplant","Okra" } },
        { ("southwest", 10), new[]{ "Pumpkin","Squash","Pecan","Sweet Potato","Turnip" } },
        { ("southwest", 12), new[]{ "Citrus","Broccoli","Cauliflower","Cabbage" } },
    };

    private static readonly Dictionary<string, int> FreshnessDays = new(StringComparer.OrdinalIgnoreCase)
    {
        {"Tomato",7}, {"Lettuce",5}, {"Basil",5}, {"Cucumber",7}, {"Zucchini",5}, {"Corn",3},
        {"Strawberry",3}, {"Peach",5}, {"Pepper",10}, {"Carrot",14}, {"Kale",7}, {"Apple",21},
        {"Potato",30}, {"Squash",21}, {"Beet",14}, {"Cilantro",5}, {"Spinach",5}, {"Broccoli",5},
    };

    public static int GetFreshnessDays(string plantName)
        => FreshnessDays.TryGetValue(plantName, out int days) ? days : 7;

    public List<string> GetInSeasonProduce(string region, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(region)) { return new List<string>(); }
        return Calendar.TryGetValue((region.ToLowerInvariant(), date.Month), out string[]? items) ? items.ToList() : new List<string>();
    }

    public bool IsInSeason(string produceName, string region, DateOnly date)
        => GetInSeasonProduce(region, date).Any(i => i.Equals(produceName, StringComparison.OrdinalIgnoreCase));

    public decimal GetSeasonalScoreBoost(IEnumerable<string> ingredientNames, string region, DateOnly date)
    {
        List<string> inSeason = GetInSeasonProduce(region, date);
        int matched = ingredientNames.Count(name => inSeason.Any(s => s.Equals(name, StringComparison.OrdinalIgnoreCase)));
        return Math.Min(matched * 0.05m, 0.25m);
    }
}
