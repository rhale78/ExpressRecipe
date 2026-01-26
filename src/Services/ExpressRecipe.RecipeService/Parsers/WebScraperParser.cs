using HtmlAgilityPack;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExpressRecipe.RecipeService.Parsers
{
    /// <summary>
    /// Base parser for web scraping recipes from URLs
    /// Uses HtmlAgilityPack for HTML parsing and Schema.org JSON-LD extraction
    /// </summary>
    public class WebScraperParser : RecipeParserBase
    {
        protected HttpClient? HttpClient { get; set; }

        public override string ParserName => "WebScraperParser";
        public override string SourceType => "WebScraper";

        public override bool CanParse(string content, ParserContext context)
        {
            // Check if context has a URL
            return !string.IsNullOrWhiteSpace(context.SourceUrl) &&
                   (context.SourceUrl.StartsWith("http://") || context.SourceUrl.StartsWith("https://"));
        }

        public override async Task<List<ParsedRecipe>> ParseAsync(string content, ParserContext context)
        {
            List<ParsedRecipe> recipes = [];

            // 1. First try Schema.org JSON-LD (most reliable)
            var jsonLd = ExtractJsonLd(content);
            if (jsonLd != null)
            {
                try
                {
                    JsonRecipeParser jsonParser = new JsonRecipeParser();
                    recipes = await jsonParser.ParseAsync(jsonLd, context);
                    if (recipes.Count > 0)
                    {
                        recipes[0].SourceUrl = context.SourceUrl;
                        return recipes;
                    }
                }
                catch
                {
                    // Fall through to HTML parsing
                }
            }

            // 2. Try HTML parsing with HtmlAgilityPack
            ParsedRecipe? htmlRecipe = ParseHtml(content, context);
            if (htmlRecipe != null)
            {
                recipes.Add(htmlRecipe);
                return recipes;
            }

            // 3. Fallback - return minimal recipe
            recipes.Add(new ParsedRecipe
            {
                Name = "Web Recipe Import",
                Description = "Recipe imported from " + context.SourceUrl,
                SourceUrl = context.SourceUrl
            });

            return recipes;
        }

        /// <summary>
        /// Parse recipe from HTML using HtmlAgilityPack
        /// Looks for common recipe markup patterns
        /// </summary>
        protected virtual ParsedRecipe? ParseHtml(string html, ParserContext context)
        {
            try
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                ParsedRecipe recipe = new ParsedRecipe
                {
                    SourceUrl = context.SourceUrl
                };

                // Extract recipe name (try common selectors)
                var nameSelectors = new[]
                {
                    "//h1[@class='recipe-title']",
                    "//h1[@class='recipe-name']",
                    "//h1[@itemprop='name']",
                    "//h1[contains(@class, 'recipe')]",
                    "//h1"
                };

                foreach (var selector in nameSelectors)
                {
                    HtmlNode nameNode = doc.DocumentNode.SelectSingleNode(selector);
                    if (nameNode != null && !string.IsNullOrWhiteSpace(nameNode.InnerText))
                    {
                        recipe.Name = CleanText(nameNode.InnerText);
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(recipe.Name))
                {
                    recipe.Name = "Imported Recipe";
                }

                // Extract description
                var descSelectors = new[]
                {
                    "//p[@class='recipe-description']",
                    "//div[@class='recipe-summary']",
                    "//p[@itemprop='description']",
                    "//meta[@name='description']/@content"
                };

                foreach (var selector in descSelectors)
                {
                    HtmlNode descNode = doc.DocumentNode.SelectSingleNode(selector);
                    if (descNode != null && !string.IsNullOrWhiteSpace(descNode.InnerText))
                    {
                        recipe.Description = CleanText(descNode.InnerText);
                        break;
                    }
                }

                // Extract ingredients
                var ingredientSelectors = new[]
                {
                    "//li[@class='ingredient']",
                    "//li[@itemprop='recipeIngredient']",
                    "//li[contains(@class, 'ingredient')]",
                    "//ul[contains(@class, 'ingredient')]//li"
                };

                var order = 0;
                foreach (var selector in ingredientSelectors)
                {
                    HtmlNodeCollection ingredientNodes = doc.DocumentNode.SelectNodes(selector);
                    if (ingredientNodes != null && ingredientNodes.Count > 0)
                    {
                        foreach (HtmlNode node in ingredientNodes)
                        {
                            var text = CleanText(node.InnerText);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                (decimal? quantity, string? unit, string? remaining) = ParseQuantityAndUnit(text);
                                (string? ingredient, string? preparation) = ExtractPreparation(remaining);

                                recipe.Ingredients.Add(new ParsedIngredient
                                {
                                    Order = order++,
                                    Quantity = quantity,
                                    Unit = unit,
                                    IngredientName = ingredient,
                                    Preparation = preparation,
                                    IsOptional = IsOptionalIngredient(text),
                                    OriginalText = text
                                });
                            }
                        }
                        break; // Found ingredients, stop searching
                    }
                }

                // Extract instructions
                var instructionSelectors = new[]
                {
                    "//li[@class='instruction']",
                    "//li[@itemprop='recipeInstructions']",
                    "//li[contains(@class, 'step')]",
                    "//ol[contains(@class, 'instruction')]//li",
                    "//div[@class='instructions']//p"
                };

                var stepNumber = 0;
                foreach (var selector in instructionSelectors)
                {
                    HtmlNodeCollection instructionNodes = doc.DocumentNode.SelectNodes(selector);
                    if (instructionNodes != null && instructionNodes.Count > 0)
                    {
                        foreach (HtmlNode node in instructionNodes)
                        {
                            var text = CleanText(node.InnerText);
                            if (!string.IsNullOrWhiteSpace(text) && text.Length > 10)
                            {
                                recipe.Instructions.Add(new ParsedInstruction
                                {
                                    StepNumber = ++stepNumber,
                                    InstructionText = text
                                });
                            }
                        }
                        break; // Found instructions, stop searching
                    }
                }

                // Extract image
                var imageSelectors = new[]
                {
                    "//img[@class='recipe-image']/@src",
                    "//img[@itemprop='image']/@src",
                    "//img[contains(@class, 'recipe')]/@src",
                    "//meta[@property='og:image']/@content"
                };

                foreach (var selector in imageSelectors)
                {
                    HtmlNode imageNode = doc.DocumentNode.SelectSingleNode(selector);
                    if (imageNode != null && !string.IsNullOrWhiteSpace(imageNode.GetAttributeValue("content", imageNode.GetAttributeValue("src", ""))))
                    {
                        var imageUrl = imageNode.GetAttributeValue("content", imageNode.GetAttributeValue("src", ""));
                        if (!string.IsNullOrWhiteSpace(imageUrl))
                        {
                            recipe.ImageUrl = MakeAbsoluteUrl(imageUrl, context.SourceUrl);
                            break;
                        }
                    }
                }

                // Extract prep/cook time
                HtmlNode prepTimeNode = doc.DocumentNode.SelectSingleNode("//time[@itemprop='prepTime']/@datetime");
                if (prepTimeNode != null)
                {
                    recipe.PrepTimeMinutes = ParseIso8601Duration(prepTimeNode.GetAttributeValue("datetime", ""));
                }

                HtmlNode cookTimeNode = doc.DocumentNode.SelectSingleNode("//time[@itemprop='cookTime']/@datetime");
                if (cookTimeNode != null)
                {
                    recipe.CookTimeMinutes = ParseIso8601Duration(cookTimeNode.GetAttributeValue("datetime", ""));
                }

                // Extract servings
                HtmlNode yieldNode = doc.DocumentNode.SelectSingleNode("//span[@itemprop='recipeYield']");
                if (yieldNode != null)
                {
                    var yieldText = yieldNode.InnerText;
                    Match match = System.Text.RegularExpressions.Regex.Match(yieldText, @"\d+");
                    if (match.Success && int.TryParse(match.Value, out var servings))
                    {
                        recipe.Servings = servings;
                    }
                }

                return recipe.Ingredients.Count > 0 || recipe.Instructions.Count > 0 ? recipe : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Parse ISO 8601 duration format (e.g., PT30M = 30 minutes)
        /// </summary>
        private int? ParseIso8601Duration(string duration)
        {
            if (string.IsNullOrWhiteSpace(duration))
            {
                return null;
            }

            try
            {
                TimeSpan timeSpan = System.Xml.XmlConvert.ToTimeSpan(duration);
                return (int)timeSpan.TotalMinutes;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Make relative URL absolute
        /// </summary>
        private string MakeAbsoluteUrl(string url, string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return url;
            }

            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                return url;
            }

            try
            {
                Uri baseUri = new Uri(baseUrl);
                Uri absoluteUri = new Uri(baseUri, url);
                return absoluteUri.ToString();
            }
            catch
            {
                return url;
            }
        }

        /// <summary>
        /// Extract JSON-LD schema.org recipe data from HTML
        /// </summary>
        private string? ExtractJsonLd(string html)
        {
            // Look for <script type="application/ld+json"> containing Recipe schema
            var pattern = @"<script\s+type=[""']application/ld\+json[""']>(.*?)</script>";
            MatchCollection matches = System.Text.RegularExpressions.Regex.Matches(html, pattern,
                System.Text.RegularExpressions.RegexOptions.Singleline |
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var jsonContent = match.Groups[1].Value.Trim();
                if (jsonContent.Contains("\"@type\"") &&
                    (jsonContent.Contains("\"Recipe\"") || jsonContent.Contains("\"recipe\"")))
                {
                    return jsonContent;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// AllRecipes.com specific scraper
    /// </summary>
    public class AllRecipesParser : WebScraperParser
    {
        public override string ParserName => "AllRecipesParser";

        public override bool CanParse(string content, ParserContext context)
        {
            return context.SourceUrl?.Contains("allrecipes.com", StringComparison.OrdinalIgnoreCase) ?? false;
        }
    }

    /// <summary>
    /// Food Network specific scraper
    /// </summary>
    public class FoodNetworkParser : WebScraperParser
    {
        public override string ParserName => "FoodNetworkParser";

        public override bool CanParse(string content, ParserContext context)
        {
            return context.SourceUrl?.Contains("foodnetwork.com", StringComparison.OrdinalIgnoreCase) ?? false;
        }
    }

    /// <summary>
    /// Tasty (BuzzFeed) specific scraper
    /// </summary>
    public class TastyParser : WebScraperParser
    {
        public override string ParserName => "TastyParser";

        public override bool CanParse(string content, ParserContext context)
        {
            return context.SourceUrl?.Contains("tasty.co", StringComparison.OrdinalIgnoreCase) ?? false;
        }
    }

    /// <summary>
    /// Serious Eats specific scraper
    /// </summary>
    public class SeriousEatsParser : WebScraperParser
    {
        public override string ParserName => "SeriousEatsParser";

        public override bool CanParse(string content, ParserContext context)
        {
            return context.SourceUrl?.Contains("seriouseats.com", StringComparison.OrdinalIgnoreCase) ?? false;
        }
    }

    /// <summary>
    /// NYT Cooking specific scraper
    /// </summary>
    public class NYTCookingParser : WebScraperParser
    {
        public override string ParserName => "NYTCookingParser";

        public override bool CanParse(string content, ParserContext context)
        {
            return context.SourceUrl?.Contains("cooking.nytimes.com", StringComparison.OrdinalIgnoreCase) ?? false;
        }
    }
}
