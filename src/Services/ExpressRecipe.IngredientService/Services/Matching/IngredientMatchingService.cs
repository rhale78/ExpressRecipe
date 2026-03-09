using ExpressRecipe.IngredientService.Data;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Matching;
using ExpressRecipe.Shared.Services;

namespace ExpressRecipe.IngredientService.Services.Matching;

public interface IIngredientMatchingRepository
{
    Task<(Guid Id, string Name)?> ExactMatchAsync(string simpleLowerName, CancellationToken ct);
    Task<(Guid Id, string Name)?> AliasMatchAsync(string simpleLowerAlias, CancellationToken ct);
    Task<List<(Guid Id, string Name, string AlternativeNames)>> GetAllForFuzzyAsync(CancellationToken ct);
    Task QueueUnresolvedAsync(string raw, string normalized, string sourceService,
        Guid? sourceEntityId, Guid? bestMatchId, string? bestMatchName,
        decimal? bestConfidence, string? bestStrategy, CancellationToken ct);
    Task IncrementAliasMatchCountAsync(string simpleLowerAlias, CancellationToken ct);
    Task<bool> CreateAliasAsync(Guid ingredientId, string aliasText, string source, CancellationToken ct);
    Task ResolveQueueItemAsync(Guid queueItemId, Guid? resolvedToId, string resolution, CancellationToken ct);
    Task<List<UnresolvedQueueItem>> GetUnresolvedQueueAsync(int page, int pageSize, int minOccurrences, CancellationToken ct);
    Task<UnresolvedQueueItem?> GetQueueItemAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Companion index built from the ingredient catalog for fast token-based and edit-distance matching.
/// </summary>
internal sealed record IngredientTokenIndex(
    Dictionary<string, List<(Guid Id, string Name, string NormName)>> ByToken,
    Dictionary<Guid, (string Name, string NormName)> ById);

public sealed class IngredientMatchingService : IIngredientMatchingService
{
    private const decimal ExactConfidence       = 1.00m;
    private const decimal AliasConfidence       = 0.95m;
    private const decimal NormalizedConfidence  = 0.80m;
    private const decimal AutoAcceptThreshold   = 0.80m;
    private const decimal JaccardMinThreshold   = 0.50m;
    private const decimal EditDistMinThreshold  = 0.50m;
    private const int     EditDistMinLength     = 4;
    private const string  TokenIndexKey         = "ingredient:token-index";
    private const string  AliasCachePrefix      = "ingredient:alias:";
    private const string  ExactCachePrefix      = "ingredient:exact:";

    private readonly IIngredientMatchingRepository _repo;
    private readonly IIngredientRepository _ingredientRepository;
    private readonly HybridCacheService _cache;
    private readonly ILogger<IngredientMatchingService> _logger;

    public IngredientMatchingService(IIngredientMatchingRepository repo,
        IIngredientRepository ingredientRepository,
        HybridCacheService cache, ILogger<IngredientMatchingService> logger)
    {
        _repo = repo;
        _ingredientRepository = ingredientRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<MatchResult> MatchAsync(string rawText, string sourceService,
        Guid? sourceEntityId = null, CancellationToken ct = default)
    {
        List<MatchResult> results = await MatchBulkAsync([rawText], sourceService, sourceEntityId, ct);
        return results[0];
    }

    public async Task<List<MatchResult>> MatchBulkAsync(IEnumerable<string> rawTexts,
        string sourceService, Guid? sourceEntityId = null, CancellationToken ct = default)
    {
        List<string> inputs = rawTexts
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        Dictionary<string, MatchResult> results = new(StringComparer.OrdinalIgnoreCase);

        // Exact/Alias stages use SimpleLower (matches DB computed column LOWER(Name) and seeded aliases).
        // Normalized/Fuzzy/Queue stages use the aggressive Normalize() form.
        Dictionary<string, string> simpleNorms     = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> aggressiveNorms = new(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in inputs)
        {
            simpleNorms[raw]     = IngredientNormalizer.SimpleLower(raw);
            aggressiveNorms[raw] = IngredientNormalizer.Normalize(raw);
        }

        // Drop inputs whose aggressive normalisation produces an empty string (pure quantity/unit/modifier).
        inputs = inputs.Where(raw => !string.IsNullOrEmpty(aggressiveNorms[raw])).ToList();
        if (inputs.Count == 0) { return new(); }

        // Pipeline: Exact → Alias → Normalized → Fuzzy (Jaccard + EditDist) → Queue
        List<string> remaining = await ResolveExactBatchAsync(simpleNorms, aggressiveNorms, inputs, results, ct);
        if (remaining.Count > 0) { remaining = await ResolveAliasBatchAsync(simpleNorms, aggressiveNorms, remaining, results, ct); }
        if (remaining.Count > 0) { remaining = await ResolveNormalizedBatchAsync(simpleNorms, aggressiveNorms, remaining, results, ct); }
        if (remaining.Count > 0) { remaining = await ResolveFuzzyBatchAsync(aggressiveNorms, remaining, results, sourceService, sourceEntityId, ct); }
        if (remaining.Count > 0) { await QueueUnresolvedBatchAsync(aggressiveNorms, remaining, results, sourceService, sourceEntityId, ct); }

        return inputs.Select(raw => results.TryGetValue(raw, out MatchResult? r)
            ? r : MatchResult.Unresolved(raw, aggressiveNorms[raw])).ToList();
    }

    private async Task<List<string>> ResolveExactBatchAsync(
        Dictionary<string, string> simpleNorms, Dictionary<string, string> aggressiveNorms,
        List<string> candidates, Dictionary<string, MatchResult> results, CancellationToken ct)
    {
        List<string> remaining = new();
        foreach (string raw in candidates)
        {
            string simple = simpleNorms[raw];
            (Guid Id, string Name)? hit = await _cache.GetOrSetAsync(
                $"{ExactCachePrefix}{simple}",
                innerCt => new ValueTask<(Guid, string)?>(_repo.ExactMatchAsync(simple, innerCt)),
                cancellationToken: ct);
            if (hit.HasValue)
            {
                results[raw] = new MatchResult
                {
                    RawInput = raw, NormalizedInput = aggressiveNorms[raw],
                    IngredientId = hit.Value.Id, IngredientName = hit.Value.Name,
                    Confidence = ExactConfidence, Strategy = MatchStrategy.Exact
                };
            }
            else { remaining.Add(raw); }
        }
        return remaining;
    }

    private async Task<List<string>> ResolveAliasBatchAsync(
        Dictionary<string, string> simpleNorms, Dictionary<string, string> aggressiveNorms,
        List<string> candidates, Dictionary<string, MatchResult> results, CancellationToken ct)
    {
        List<string> remaining = new();
        foreach (string raw in candidates)
        {
            string simple = simpleNorms[raw];
            (Guid Id, string Name)? hit = await _cache.GetOrSetAsync(
                $"{AliasCachePrefix}{simple}",
                innerCt => new ValueTask<(Guid, string)?>(_repo.AliasMatchAsync(simple, innerCt)),
                cancellationToken: ct);
            if (hit.HasValue)
            {
                results[raw] = new MatchResult
                {
                    RawInput = raw, NormalizedInput = aggressiveNorms[raw],
                    IngredientId = hit.Value.Id, IngredientName = hit.Value.Name,
                    Confidence = AliasConfidence, Strategy = MatchStrategy.Alias
                };
                await _repo.IncrementAliasMatchCountAsync(simple, ct);
            }
            else { remaining.Add(raw); }
        }
        return remaining;
    }

    private async Task<List<string>> ResolveNormalizedBatchAsync(
        Dictionary<string, string> simpleNorms, Dictionary<string, string> aggressiveNorms,
        List<string> candidates, Dictionary<string, MatchResult> results, CancellationToken ct)
    {
        List<string> remaining = new();
        foreach (string raw in candidates)
        {
            string aggressive = aggressiveNorms[raw];
            // Skip if aggressive normalisation produced the same key as the simple-lower form —
            // the Exact stage already tried that lookup and failed.
            if (aggressive == simpleNorms[raw]) { remaining.Add(raw); continue; }
            (Guid Id, string Name)? hit = await _repo.ExactMatchAsync(aggressive, ct);
            if (hit.HasValue)
            {
                results[raw] = new MatchResult
                {
                    RawInput = raw, NormalizedInput = aggressive,
                    IngredientId = hit.Value.Id, IngredientName = hit.Value.Name,
                    Confidence = NormalizedConfidence, Strategy = MatchStrategy.Normalized
                };
                await _repo.CreateAliasAsync(hit.Value.Id, raw, "AutoAccepted", ct);
            }
            else { remaining.Add(raw); }
        }
        return remaining;
    }

    private async Task<List<string>> ResolveFuzzyBatchAsync(
        Dictionary<string, string> aggressiveNorms, List<string> candidates,
        Dictionary<string, MatchResult> results,
        string sourceService, Guid? sourceEntityId, CancellationToken ct)
    {
        IngredientTokenIndex tokenIndex =
            await _cache.GetOrSetAsync(
                TokenIndexKey,
                async innerCt => await BuildTokenIndexAsync(innerCt),
                cancellationToken: ct) ?? new(new(), new());

        List<string> unresolved = new();
        foreach (string raw in candidates)
        {
            string norm = aggressiveNorms[raw];
            HashSet<string> inputTokens = IngredientNormalizer.Tokenize(norm);
            MatchResult? best = null;

            // Pre-filter: ingredient IDs sharing at least one token
            HashSet<Guid> candidateIds = new();
            foreach (string token in inputTokens)
            {
                if (tokenIndex.ByToken.TryGetValue(token, out List<(Guid, string, string)>? ids))
                {
                    foreach ((Guid id, string _, string _) in ids) { candidateIds.Add(id); }
                }
            }

            // Jaccard over pre-filtered set
            foreach (Guid candidateId in candidateIds)
            {
                if (!tokenIndex.ById.TryGetValue(candidateId, out (string Name, string NormName) entry)) { continue; }
                decimal jaccard = IngredientNormalizer.JaccardSimilarity(
                    inputTokens, IngredientNormalizer.Tokenize(entry.NormName));
                if (jaccard >= JaccardMinThreshold)
                {
                    decimal conf = Math.Min(
                        0.40m + (jaccard - JaccardMinThreshold) * (0.79m - 0.40m) / (1.0m - JaccardMinThreshold),
                        0.79m);
                    if (best is null || conf > best.Confidence)
                    {
                        best = new MatchResult
                        {
                            RawInput = raw, NormalizedInput = norm, IngredientId = candidateId,
                            IngredientName = entry.Name, Confidence = conf, Strategy = MatchStrategy.TokenOverlap
                        };
                    }
                }
            }

            // Edit distance for single-token inputs that failed Jaccard
            if (best is null && inputTokens.Count == 1 && norm.Length >= EditDistMinLength)
            {
                string inputToken = inputTokens.First();
                foreach ((string token, List<(Guid Id, string Name, string NormName)> entries) in tokenIndex.ByToken)
                {
                    if (token.Length < EditDistMinLength) { continue; }
                    int dist = IngredientNormalizer.EditDistance(inputToken, token);
                    int maxLen = Math.Max(inputToken.Length, token.Length);
                    decimal conf = maxLen == 0 ? 0m : Math.Min(1m - (decimal)dist / maxLen, 0.60m);
                    if (conf >= EditDistMinThreshold)
                    {
                        // Iterate all entries for the token to pick the best candidate
                        foreach ((Guid id, string name, string _) in entries)
                        {
                            if (best is null || conf > best.Confidence)
                            {
                                best = new MatchResult
                                {
                                    RawInput = raw, NormalizedInput = norm, IngredientId = id,
                                    IngredientName = name, Confidence = conf, Strategy = MatchStrategy.EditDistance
                                };
                            }
                        }
                    }
                }
            }

            if (best is not null)
            {
                results[raw] = best;
                if (best.Confidence >= AutoAcceptThreshold && best.IngredientId.HasValue)
                {
                    await _repo.CreateAliasAsync(best.IngredientId.Value, raw, "AutoAccepted", ct);
                }
                else
                {
                    // Low-confidence: surface the best guess to the caller AND queue for admin review.
                    await _repo.QueueUnresolvedAsync(raw, norm, sourceService, sourceEntityId,
                        best.IngredientId, best.IngredientName, best.Confidence,
                        best.Strategy.ToString(), ct);
                }
            }
            else { unresolved.Add(raw); }
        }
        return unresolved;
    }

    private async Task<IngredientTokenIndex> BuildTokenIndexAsync(CancellationToken ct)
    {
        List<(Guid Id, string Name, string AlternativeNames)> all = await _repo.GetAllForFuzzyAsync(ct);
        Dictionary<string, List<(Guid, string, string)>> byToken = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<Guid, (string Name, string NormName)> byId   = new();

        foreach ((Guid id, string name, string altNames) in all)
        {
            string normName = IngredientNormalizer.Normalize(name);
            byId[id] = (name, normName);

            // Index tokens from the canonical name
            foreach (string token in IngredientNormalizer.Tokenize(normName))
            {
                if (!byToken.TryGetValue(token, out List<(Guid, string, string)>? list))
                {
                    list = new();
                    byToken[token] = list;
                }
                if (!list.Any(e => e.Item1 == id)) { list.Add((id, name, normName)); }
            }

            // Also index tokens from each alternative name
            if (!string.IsNullOrWhiteSpace(altNames))
            {
                foreach (string alt in altNames.Split(',', System.StringSplitOptions.RemoveEmptyEntries))
                {
                    string normAlt = IngredientNormalizer.Normalize(alt);
                    foreach (string token in IngredientNormalizer.Tokenize(normAlt))
                    {
                        if (!byToken.TryGetValue(token, out List<(Guid, string, string)>? list))
                        {
                            list = new();
                            byToken[token] = list;
                        }
                        if (!list.Any(e => e.Item1 == id)) { list.Add((id, name, normName)); }
                    }
                }
            }
        }
        return new IngredientTokenIndex(byToken, byId);
    }

    private async Task QueueUnresolvedBatchAsync(Dictionary<string, string> aggressiveNorms,
        List<string> unresolvedRaws, Dictionary<string, MatchResult> results,
        string sourceService, Guid? sourceEntityId, CancellationToken ct)
    {
        foreach (string raw in unresolvedRaws)
        {
            string norm = aggressiveNorms[raw];
            await _repo.QueueUnresolvedAsync(raw, norm, sourceService, sourceEntityId,
                null, null, null, null, ct);
            results[raw] = MatchResult.Unresolved(raw, norm);
        }
    }

    public async Task ConfirmMatchAsync(Guid queueItemId, Guid ingredientId, bool createAlias,
        string resolvedBy, CancellationToken ct = default)
    {
        await _repo.ResolveQueueItemAsync(queueItemId, ingredientId, "AliasCreated", ct);
        if (createAlias)
        {
            // Use the queue item's RawText as the alias, not the resolvedBy (user identifier).
            UnresolvedQueueItem? item = await _repo.GetQueueItemAsync(queueItemId, ct);
            if (item is not null)
            {
                await _repo.CreateAliasAsync(ingredientId, item.RawText, "UserConfirmed", ct);
                await _cache.RemoveAsync($"{AliasCachePrefix}{item.NormalizedText}", ct);
            }
        }
    }

    public async Task CreateAndResolveAsync(Guid queueItemId, string newIngredientName,
        string category, CancellationToken ct = default)
    {
        Guid newId = await _ingredientRepository.CreateIngredientAsync(
            new CreateIngredientRequest { Name = newIngredientName, Category = category });
        await _repo.ResolveQueueItemAsync(queueItemId, newId, "NewIngredient", ct);
        await _cache.RemoveAsync(TokenIndexKey, ct);
    }

    public async Task RejectAsync(Guid queueItemId, string reason, CancellationToken ct = default)
    {
        string resolution = string.IsNullOrWhiteSpace(reason)
            ? "Rejected"
            : $"Rejected: {reason.Trim()}"[..Math.Min(50, 10 + reason.Trim().Length)];
        await _repo.ResolveQueueItemAsync(queueItemId, null, resolution, ct);
    }
}

