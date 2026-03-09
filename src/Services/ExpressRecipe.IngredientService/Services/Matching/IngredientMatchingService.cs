using ExpressRecipe.IngredientService.Data;
using ExpressRecipe.Shared.Matching;
using ExpressRecipe.Shared.Services;

namespace ExpressRecipe.IngredientService.Services.Matching;

public interface IIngredientMatchingRepository
{
    Task<(Guid Id, string Name)?> ExactMatchAsync(string normalizedName, CancellationToken ct);
    Task<(Guid Id, string Name)?> AliasMatchAsync(string normalizedAlias, CancellationToken ct);
    Task<List<(Guid Id, string Name, string AlternativeNames)>> GetAllForFuzzyAsync(CancellationToken ct);
    Task QueueUnresolvedAsync(string raw, string normalized, string sourceService,
        Guid? sourceEntityId, Guid? bestMatchId, string? bestMatchName,
        decimal? bestConfidence, string? bestStrategy, CancellationToken ct);
    Task IncrementAliasMatchCountAsync(string normalizedAlias, CancellationToken ct);
    Task<bool> CreateAliasAsync(Guid ingredientId, string aliasText, string source, CancellationToken ct);
    Task ResolveQueueItemAsync(Guid queueItemId, Guid? resolvedToId, string resolution, CancellationToken ct);
    Task<List<UnresolvedQueueItem>> GetUnresolvedQueueAsync(int page, int pageSize, int minOccurrences, CancellationToken ct);
}

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
    private readonly HybridCacheService _cache;
    private readonly ILogger<IngredientMatchingService> _logger;

    public IngredientMatchingService(IIngredientMatchingRepository repo,
        HybridCacheService cache, ILogger<IngredientMatchingService> logger)
    {
        _repo = repo;
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
        Dictionary<string, string> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in inputs) { normalized[raw] = IngredientNormalizer.Normalize(raw); }

        // Pipeline: Exact → Alias → Normalized → Fuzzy (Jaccard + EditDist) → Queue
        List<string> remaining = await ResolveExactBatchAsync(normalized, results, ct);
        if (remaining.Count > 0) { remaining = await ResolveAliasBatchAsync(normalized, remaining, results, ct); }
        if (remaining.Count > 0) { remaining = await ResolveNormalizedBatchAsync(normalized, remaining, results, ct); }
        if (remaining.Count > 0) { remaining = await ResolveFuzzyBatchAsync(normalized, remaining, results, ct); }
        if (remaining.Count > 0) { await QueueUnresolvedBatchAsync(normalized, remaining, results, sourceService, sourceEntityId, ct); }

        return inputs.Select(raw => results.TryGetValue(raw, out MatchResult? r)
            ? r : MatchResult.Unresolved(raw, normalized[raw])).ToList();
    }

    private async Task<List<string>> ResolveExactBatchAsync(Dictionary<string, string> normalized,
        Dictionary<string, MatchResult> results, CancellationToken ct)
    {
        List<string> remaining = new();
        foreach ((string raw, string norm) in normalized)
        {
            (Guid Id, string Name)? hit = await _cache.GetOrSetAsync(
                $"{ExactCachePrefix}{norm}",
                innerCt => new ValueTask<(Guid, string)?>(_repo.ExactMatchAsync(norm, innerCt)),
                cancellationToken: ct);
            if (hit.HasValue)
            {
                results[raw] = new MatchResult
                {
                    RawInput = raw, NormalizedInput = norm,
                    IngredientId = hit.Value.Id, IngredientName = hit.Value.Name,
                    Confidence = ExactConfidence, Strategy = MatchStrategy.Exact
                };
            }
            else { remaining.Add(raw); }
        }
        return remaining;
    }

    private async Task<List<string>> ResolveAliasBatchAsync(Dictionary<string, string> normalized,
        List<string> candidates, Dictionary<string, MatchResult> results, CancellationToken ct)
    {
        List<string> remaining = new();
        foreach (string raw in candidates)
        {
            string norm = normalized[raw];
            (Guid Id, string Name)? hit = await _cache.GetOrSetAsync(
                $"{AliasCachePrefix}{norm}",
                innerCt => new ValueTask<(Guid, string)?>(_repo.AliasMatchAsync(norm, innerCt)),
                cancellationToken: ct);
            if (hit.HasValue)
            {
                results[raw] = new MatchResult
                {
                    RawInput = raw, NormalizedInput = norm,
                    IngredientId = hit.Value.Id, IngredientName = hit.Value.Name,
                    Confidence = AliasConfidence, Strategy = MatchStrategy.Alias
                };
                _ = _repo.IncrementAliasMatchCountAsync(norm, CancellationToken.None);
            }
            else { remaining.Add(raw); }
        }
        return remaining;
    }

    private async Task<List<string>> ResolveNormalizedBatchAsync(Dictionary<string, string> normalized,
        List<string> candidates, Dictionary<string, MatchResult> results, CancellationToken ct)
    {
        List<string> remaining = new();
        foreach (string raw in candidates)
        {
            string norm = normalized[raw];
            if (norm == raw.ToLowerInvariant()) { remaining.Add(raw); continue; }
            (Guid Id, string Name)? hit = await _repo.ExactMatchAsync(norm, ct);
            if (hit.HasValue)
            {
                results[raw] = new MatchResult
                {
                    RawInput = raw, NormalizedInput = norm,
                    IngredientId = hit.Value.Id, IngredientName = hit.Value.Name,
                    Confidence = NormalizedConfidence, Strategy = MatchStrategy.Normalized
                };
                await _repo.CreateAliasAsync(hit.Value.Id, raw, "AutoAccepted", ct);
            }
            else { remaining.Add(raw); }
        }
        return remaining;
    }

    private async Task<List<string>> ResolveFuzzyBatchAsync(Dictionary<string, string> normalized,
        List<string> candidates, Dictionary<string, MatchResult> results, CancellationToken ct)
    {
        Dictionary<string, List<(Guid Id, string Name, string NormName)>> tokenIndex =
            await _cache.GetOrSetAsync(
                TokenIndexKey,
                async innerCt => await BuildTokenIndexAsync(innerCt),
                cancellationToken: ct) ?? new();

        List<string> unresolved = new();
        foreach (string raw in candidates)
        {
            string norm = normalized[raw];
            HashSet<string> inputTokens = IngredientNormalizer.Tokenize(norm);
            MatchResult? best = null;

            // Pre-filter: ingredient IDs sharing at least one token
            HashSet<Guid> candidateIds = new();
            foreach (string token in inputTokens)
            {
                if (tokenIndex.TryGetValue(token, out List<(Guid, string, string)>? ids))
                {
                    foreach ((Guid id, string _, string _) in ids) { candidateIds.Add(id); }
                }
            }

            // Jaccard over pre-filtered set
            foreach (Guid candidateId in candidateIds)
            {
                (string? name, string? normName) = FindInIndex(tokenIndex, candidateId);
                if (name is null) { continue; }
                decimal jaccard = IngredientNormalizer.JaccardSimilarity(
                    inputTokens, IngredientNormalizer.Tokenize(normName!));
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
                            IngredientName = name, Confidence = conf, Strategy = MatchStrategy.TokenOverlap
                        };
                    }
                }
            }

            // Edit distance for single-token inputs that failed Jaccard
            if (best is null && inputTokens.Count == 1 && norm.Length >= EditDistMinLength)
            {
                string inputToken = inputTokens.First();
                foreach ((string token, List<(Guid Id, string Name, string NormName)> entries) in tokenIndex)
                {
                    if (token.Length < EditDistMinLength) { continue; }
                    int dist = IngredientNormalizer.EditDistance(inputToken, token);
                    int maxLen = Math.Max(inputToken.Length, token.Length);
                    decimal conf = maxLen == 0 ? 0m : Math.Min(1m - (decimal)dist / maxLen, 0.60m);
                    if (conf >= EditDistMinThreshold && (best is null || conf > best.Confidence))
                    {
                        (Guid id, string name, string _) = entries[0];
                        best = new MatchResult
                        {
                            RawInput = raw, NormalizedInput = norm, IngredientId = id,
                            IngredientName = name, Confidence = conf, Strategy = MatchStrategy.EditDistance
                        };
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
            }
            else { unresolved.Add(raw); }
        }
        return unresolved;
    }

    private static (string? Name, string? NormName) FindInIndex(
        Dictionary<string, List<(Guid Id, string Name, string NormName)>> index, Guid id)
    {
        foreach (List<(Guid Id, string Name, string NormName)> list in index.Values)
        {
            foreach ((Guid listId, string name, string normName) in list)
            {
                if (listId == id) { return (name, normName); }
            }
        }
        return (null, null);
    }

    private async Task<Dictionary<string, List<(Guid, string, string)>>> BuildTokenIndexAsync(CancellationToken ct)
    {
        List<(Guid Id, string Name, string AlternativeNames)> all = await _repo.GetAllForFuzzyAsync(ct);
        Dictionary<string, List<(Guid, string, string)>> index = new(StringComparer.Ordinal);
        foreach ((Guid id, string name, string _) in all)
        {
            string normName = IngredientNormalizer.Normalize(name);
            foreach (string token in IngredientNormalizer.Tokenize(normName))
            {
                if (!index.TryGetValue(token, out List<(Guid, string, string)>? list))
                {
                    list = new();
                    index[token] = list;
                }
                list.Add((id, name, normName));
            }
        }
        return index;
    }

    private async Task QueueUnresolvedBatchAsync(Dictionary<string, string> normalized,
        List<string> unresolvedRaws, Dictionary<string, MatchResult> results,
        string sourceService, Guid? sourceEntityId, CancellationToken ct)
    {
        foreach (string raw in unresolvedRaws)
        {
            await _repo.QueueUnresolvedAsync(raw, normalized[raw], sourceService, sourceEntityId,
                null, null, null, null, ct);
            results[raw] = MatchResult.Unresolved(raw, normalized[raw]);
        }
    }

    public async Task ConfirmMatchAsync(Guid queueItemId, Guid ingredientId, bool createAlias,
        string resolvedBy, CancellationToken ct = default)
    {
        await _repo.ResolveQueueItemAsync(queueItemId, ingredientId, "AliasCreated", ct);
        if (createAlias) { await _repo.CreateAliasAsync(ingredientId, resolvedBy, "UserConfirmed", ct); }
        await _cache.RemoveAsync($"{AliasCachePrefix}{resolvedBy}", ct);
    }

    public async Task CreateAndResolveAsync(Guid queueItemId, string newIngredientName,
        string category, CancellationToken ct = default)
    {
        await _repo.ResolveQueueItemAsync(queueItemId, null, "NewIngredient", ct);
        await _cache.RemoveAsync(TokenIndexKey, ct);
    }

    public async Task RejectAsync(Guid queueItemId, string reason, CancellationToken ct = default) =>
        await _repo.ResolveQueueItemAsync(queueItemId, null, "Rejected", ct);
}
