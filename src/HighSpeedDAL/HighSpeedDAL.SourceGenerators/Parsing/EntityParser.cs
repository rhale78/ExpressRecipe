using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HighSpeedDAL.SourceGenerators.Models;
using HighSpeedDAL.SourceGenerators.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HighSpeedDAL.SourceGenerators.Parsing;

/// <summary>
/// Parses entity class declarations and extracts metadata
/// </summary>
internal sealed class EntityParser
{
    private readonly SemanticModel _semanticModel;

    // TODO: The cached schema should be compared against the actual database table schema
    // to detect schema drift (added/removed/modified columns, changed types, indexes, etc.)
    // and automatically update the table structure as needed. This should happen during
    // entity initialization or migration phases to ensure database schema stays in sync
    // with the entity definitions without requiring manual migrations.

    // Cache structure: Key = fully qualified type name, Value = (contentHash, metadata)
    private static readonly Dictionary<string, (string contentHash, EntityMetadata metadata)> _schemaCache = new Dictionary<string, (string, EntityMetadata)>();
    private static readonly object _cacheLock = new object();

    // Version of the parser logic - increment when parser behavior changes
    // v1.0.0: Initial MD5-based content invalidation
    // v1.0.1: Force cache clear after project reference changes
    // v1.0.2: Restored SqlServer reference for test compatibility
    // v1.0.3: Database provider detection implemented
    // v1.0.4: Sqlite-specific SQL generation implemented
    private const string PARSER_VERSION = "1.0.4";

    public EntityParser(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    public EntityMetadata? ParseEntity(ClassDeclarationSyntax classDeclaration)
    {
        INamedTypeSymbol? classSymbol = _semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
        if (classSymbol == null)
        {
            return null;
        }

        string cacheKey = GetCacheKey(classSymbol);

        // Calculate content hash from entity source code + parser version
        string contentHash = CalculateContentHash(classDeclaration, classSymbol);

        // Check cache with content-based invalidation
        if (TryGetCachedSchema(cacheKey, contentHash, out EntityMetadata? cachedMetadata))
        {
            // IMPORTANT: Never return the cached metadata instance directly.
            // The source generator mutates EntityMetadata later (e.g., appending auto-generated
            // audit/soft-delete/primary key properties for DAL/cloning generation).
            // Returning the cached instance would cause those mutations to be re-used on later
            // parses, leading to duplicated properties and invalid generated code.
            return cachedMetadata == null ? null : CloneMetadata(cachedMetadata);
        }

        EntityMetadata metadata = new EntityMetadata
        {
            ClassName = classSymbol.Name,
            Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
            TableName = TableNamePluralizer.Pluralize(classSymbol.Name) // Default to pluralized name
        };

        // Parse class attributes
        ParseClassAttributes(classSymbol, metadata);

        // Parse properties
        IEnumerable<IPropertySymbol> properties = classSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public);

        foreach (IPropertySymbol property in properties)
        {
            PropertyMetadata? propertyMetadata = ParseProperty(property);
            if (propertyMetadata != null && !propertyMetadata.IsNotMapped)
            {
                metadata.Properties.Add(propertyMetadata);

                if (propertyMetadata.IsPrimaryKey)
                {
                    metadata.PrimaryKeyProperty = propertyMetadata;
                    metadata.HasCustomPrimaryKey = true;
                }

                if (propertyMetadata.IsIndexed)
                {
                    string indexName = propertyMetadata.IndexName ?? $"IX_{metadata.TableName}_{propertyMetadata.ColumnName}";
                    metadata.Indexes.Add(new IndexMetadata
                    {
                        IndexName = indexName,
                        ColumnNames = new List<string> { propertyMetadata.ColumnName },
                        IsUnique = propertyMetadata.IsUniqueIndex
                    });
                }
            }
        }

        // If no custom primary key, check for convention-based "Id" property or auto-generate
        if (metadata.PrimaryKeyProperty == null)
        {
            // Check for existing "Id" property (convention)
            PropertyMetadata? existingId = metadata.Properties.FirstOrDefault(p => p.PropertyName == "Id");

            if (existingId != null)
            {
                existingId.IsPrimaryKey = true;
                // Default convention: integer Id is auto-increment
                if (existingId.PropertyType == "int" || existingId.PropertyType == "long")
                {
                    existingId.IsAutoIncrement = true;
                }
                existingId.IsNullable = false;
                metadata.PrimaryKeyProperty = existingId;
            }
            else
            {
                // Auto-generate Id property metadata (for DAL generation)
                // NOTE: Don't add to Properties list here - PropertyAutoGenerator will generate it as a partial class property
                // and add it to Properties after generation
                metadata.PrimaryKeyProperty = new PropertyMetadata
                {
                    PropertyName = "Id",
                    PropertyType = "int",
                    ColumnName = "Id",
                    IsPrimaryKey = true,
                    IsAutoIncrement = true,
                    IsNullable = false
                };
                // DO NOT add to metadata.Properties here - let PropertyAutoGenerator handle it
            }
        }

            // Cache the parsed metadata with content hash
            CacheSchema(cacheKey, contentHash, metadata);

            return metadata;
        }

        /// <summary>
        /// Calculates MD5 hash of entity source code combined with parser version.
        /// This ensures cache invalidation when either the entity or parser logic changes.
        /// </summary>
        private static string CalculateContentHash(ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol)
        {
            StringBuilder contentBuilder = new StringBuilder();

            // Include parser version to invalidate cache when parser logic changes
            contentBuilder.Append("PARSER_VERSION:");
            contentBuilder.Append(PARSER_VERSION);
            contentBuilder.Append(";");

            // Include entity source code (properties, attributes, modifiers)
            contentBuilder.Append("CLASS:");
            contentBuilder.Append(classDeclaration.ToFullString());
            contentBuilder.Append(";");

            // Include attribute information from symbol (in case attributes come from referenced assemblies)
            contentBuilder.Append("ATTRIBUTES:");
            foreach (AttributeData attribute in classSymbol.GetAttributes())
            {
                contentBuilder.Append(attribute.ToString());
                contentBuilder.Append(";");
            }

            // Include properties and their attributes
            contentBuilder.Append("PROPERTIES:");
            IEnumerable<IPropertySymbol> properties = classSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public);

            foreach (IPropertySymbol property in properties)
            {
                contentBuilder.Append(property.Name);
                contentBuilder.Append(":");
                contentBuilder.Append(property.Type.ToDisplayString());
                contentBuilder.Append(":");

                foreach (AttributeData attribute in property.GetAttributes())
                {
                    contentBuilder.Append(attribute.ToString());
                    contentBuilder.Append(",");
                }

                contentBuilder.Append(";");
            }

            // Calculate MD5 hash
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(contentBuilder.ToString());
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert to hex string
                StringBuilder hashBuilder = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    hashBuilder.Append(hashBytes[i].ToString("x2"));
                }

                return hashBuilder.ToString();
            }
        }

        /// <summary>
        /// Gets the cache key for an entity type
        /// </summary>
        private static string GetCacheKey(INamedTypeSymbol classSymbol)
        {
            return $"{classSymbol.ContainingNamespace.ToDisplayString()}.{classSymbol.Name}";
        }

        /// <summary>
        /// Attempts to retrieve cached schema metadata with content-based invalidation.
        /// Returns cached metadata only if content hash matches (entity and parser unchanged).
        /// </summary>
        private static bool TryGetCachedSchema(string cacheKey, string currentContentHash, out EntityMetadata? metadata)
        {
            lock (_cacheLock)
            {
                if (_schemaCache.TryGetValue(cacheKey, out var cachedEntry))
                {
                    // Validate content hash - if it matches, use cached metadata
                    if (cachedEntry.contentHash == currentContentHash)
                    {
                        metadata = cachedEntry.metadata;
                        return true;
                    }

                    // Hash mismatch - entity or parser changed, invalidate this cache entry
                    _schemaCache.Remove(cacheKey);
                }

                metadata = null;
                return false;
            }
        }

        /// <summary>
        /// Caches the parsed entity metadata with content hash for invalidation
        /// </summary>
        private static void CacheSchema(string cacheKey, string contentHash, EntityMetadata metadata)
        {
            lock (_cacheLock)
            {
                _schemaCache[cacheKey] = (contentHash, metadata);
            }
        }

        private static EntityMetadata CloneMetadata(EntityMetadata source)
        {
            EntityMetadata clone = new EntityMetadata
            {
                ClassName = source.ClassName,
                Namespace = source.Namespace,
                TableName = source.TableName,
                IsAuditable = source.IsAuditable,
                HasSoftDelete = source.HasSoftDelete,
                SoftDeleteColumn = source.SoftDeleteColumn,
                SoftDeleteDateColumn = source.SoftDeleteDateColumn,
                AutoCreate = source.AutoCreate,
                HasCache = source.HasCache,
                CacheStrategy = source.CacheStrategy,
                CacheExpirationSeconds = source.CacheExpirationSeconds,
                CacheMaxSize = source.CacheMaxSize,
                CachePromotionIntervalSeconds = source.CachePromotionIntervalSeconds,
                IsReferenceTable = source.IsReferenceTable,
                ReferenceLoadFromCode = source.ReferenceLoadFromCode,
                HasRowVersion = source.HasRowVersion,
                HasCustomPrimaryKey = source.HasCustomPrimaryKey,
                HasStagingTable = source.HasStagingTable,
                HasAutoPurge = source.HasAutoPurge,
                HasInMemoryTable = source.HasInMemoryTable,
                HasMemoryMappedTable = source.HasMemoryMappedTable,
                MemoryMappedFileName = source.MemoryMappedFileName,
                MemoryMappedSizeMB = source.MemoryMappedSizeMB,
                MemoryMappedAutoLoadOnStartup = source.MemoryMappedAutoLoadOnStartup,
                MemoryMappedSyncMode = source.MemoryMappedSyncMode,
                MemoryMappedFlushIntervalSeconds = source.MemoryMappedFlushIntervalSeconds
            };

            if (source.PrimaryKeyProperty != null)
            {
                clone.PrimaryKeyProperty = CloneProperty(source.PrimaryKeyProperty);
            }

            foreach (PropertyMetadata p in source.Properties)
            {
                clone.Properties.Add(CloneProperty(p));
            }

            foreach (IndexMetadata idx in source.Indexes)
            {
                clone.Indexes.Add(new IndexMetadata
                {
                    IndexName = idx.IndexName,
                    ColumnNames = idx.ColumnNames?.ToList() ?? new List<string>(),
                    IsUnique = idx.IsUnique
                });
            }

            return clone;
        }

        private static PropertyMetadata CloneProperty(PropertyMetadata source)
        {
            return new PropertyMetadata
            {
                PropertyName = source.PropertyName,
                PropertyType = source.PropertyType,
                ColumnName = source.ColumnName,
                IsPrimaryKey = source.IsPrimaryKey,
                IsAutoIncrement = source.IsAutoIncrement,
                IsNullable = source.IsNullable,
                IsNotMapped = source.IsNotMapped,
                IsIndexed = source.IsIndexed,
                IndexName = source.IndexName,
                IsUniqueIndex = source.IsUniqueIndex,
                MaxLength = source.MaxLength,
                CustomSqlType = source.CustomSqlType,
                IsOneToOne = source.IsOneToOne,
                IsOneToMany = source.IsOneToMany,
                IsManyToMany = source.IsManyToMany,
                ForeignKeyProperty = source.ForeignKeyProperty,
                JunctionTableName = source.JunctionTableName,
                RelatedEntityType = source.RelatedEntityType
            };
        }

    /// <summary>
    /// Clears the entire schema cache
    /// </summary>
    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            _schemaCache.Clear();
        }
    }

    /// <summary>
    /// Invalidates a specific cached schema by fully qualified type name
    /// </summary>
    public static void InvalidateCache(string fullyQualifiedTypeName)
    {
        lock (_cacheLock)
        {
            _schemaCache.Remove(fullyQualifiedTypeName);
        }
    }

    private void ParseClassAttributes(INamedTypeSymbol classSymbol, EntityMetadata metadata)
    {
        foreach (AttributeData attribute in classSymbol.GetAttributes())
        {
            string? attributeName = attribute.AttributeClass?.Name;

            switch (attributeName)
            {
                case "TableAttribute":
                case "Table":
                    ParseTableAttribute(attribute, metadata);
                    break;

                case "DalEntityAttribute":
                case "DalEntity":
                    ParseDalEntityAttribute(attribute, metadata);
                    break;

                case "CacheAttribute":
                case "Cache":
                case "CachedAttribute":
                    ParseCacheAttribute(attribute, metadata);
                    break;

                case "ReferenceTableAttribute":
                case "ReferenceTable":
                    ParseReferenceTableAttribute(attribute, metadata);
                    break;

                case "AutoAuditAttribute":
                case "AutoAudit":
                case "AuditableAttribute":
                case "Auditable":
                    metadata.IsAuditable = true;
                    break;

                case "RowVersionAttribute":
                case "RowVersion":
                    metadata.HasRowVersion = true;
                    ParseRowVersionAttribute(attribute, metadata);
                    break;

                            case "SoftDeleteAttribute":
                            case "SoftDelete":
                                metadata.HasSoftDelete = true;
                                ParseSoftDeleteAttribute(attribute, metadata);
                                break;

                            case "StagingTableAttribute":
                            case "StagingTable":
                                metadata.HasStagingTable = true;
                                ParseStagingTableAttribute(attribute, metadata);
                                break;

                            case "AutoPurgeAttribute":
                            case "AutoPurge":
                                metadata.HasAutoPurge = true;
                                ParseAutoPurgeAttribute(attribute, metadata);
                                break;

                                        case "InMemoryTableAttribute":
                                        case "InMemoryTable":
                                            metadata.HasInMemoryTable = true;
                                            ParseInMemoryTableAttribute(attribute, metadata);
                                            break;

                                        case "MemoryMappedTableAttribute":
                                        case "MemoryMappedTable":
                                            metadata.HasMemoryMappedTable = true;
                                            ParseMemoryMappedTableAttribute(attribute, metadata);
                                            break;
                                    }
                                }
                            }

    private void ParseTableAttribute(AttributeData attribute, EntityMetadata metadata)
    {
        // Get table name from constructor argument if provided
        if (attribute.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Value is string tableName &&
            !string.IsNullOrWhiteSpace(tableName))
        {
            metadata.TableName = tableName;
        }
        else
        {
            // No table name specified - use pluralized class name
            metadata.TableName = TableNamePluralizer.Pluralize(metadata.ClassName);
        }

        // Check for Schema named argument
        foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key == "Schema" && namedArg.Value.Value is string schema)
            {
                metadata.Schema = schema;
            }
            else if (namedArg.Key == "Name" && namedArg.Value.Value is string nameOverride &&
                     !string.IsNullOrWhiteSpace(nameOverride))
            {
                // Handle [Table(Name = "...")] syntax
                metadata.TableName = nameOverride;
            }
        }
    }

    private void ParseDalEntityAttribute(AttributeData attribute, EntityMetadata metadata)
    {
        foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "TableName":
                    if (namedArg.Value.Value is string tableName)
                    {
                        metadata.TableName = tableName;
                    }
                    break;

                case "AutoCreate":
                    if (namedArg.Value.Value is bool autoCreate)
                    {
                        metadata.AutoCreate = autoCreate;
                    }
                    break;

                case "AutoMigrate":
                    if (namedArg.Value.Value is bool autoMigrate)
                    {
                        metadata.AutoMigrate = autoMigrate;
                    }
                    break;
            }
        }
    }

    private void ParseCacheAttribute(AttributeData attribute, EntityMetadata metadata)
    {
        metadata.HasCache = true;

        foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "MaxSize":
                    if (namedArg.Value.Value is int maxSize)
                    {
                        metadata.CacheMaxSize = maxSize;
                    }
                    break;

                case "ExpirationSeconds":
                    if (namedArg.Value.Value is int expirationSeconds)
                    {
                        metadata.CacheExpirationSeconds = expirationSeconds;
                    }
                    break;

                case "Strategy":
                    if (namedArg.Value.Value is int strategyValue)
                    {
                        // Match CacheStrategy enum: None=0, Memory=1, Distributed=2, TwoLayer=3
                        metadata.CacheStrategy = strategyValue switch
                        {
                            0 => "None",
                            1 => "Memory",
                            2 => "Distributed",
                            3 => "TwoLayer",
                            _ => "Memory"
                        };
                    }
                    break;

                case "PromotionIntervalSeconds":
                    if (namedArg.Value.Value is int promotionSeconds)
                    {
                        metadata.CachePromotionIntervalSeconds = promotionSeconds;
                    }
                    break;

                case "PreloadOnStartup":
                    if (namedArg.Value.Value is bool preload)
                    {
                        metadata.CachePreloadOnStartup = preload;
                    }
                    break;
            }
        }
    }

    private void ParseReferenceTableAttribute(AttributeData attribute, EntityMetadata metadata)
    {
        metadata.IsReferenceTable = true;

        foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "CsvDataPath":
                    if (namedArg.Value.Value is string csvPath)
                    {
                        metadata.ReferenceCsvPath = csvPath;
                    }
                    break;

                case "LoadFromCode":
                    if (namedArg.Value.Value is bool loadFromCode)
                    {
                        metadata.ReferenceLoadFromCode = loadFromCode;
                    }
                    break;
            }
        }
    }

    private void ParseRowVersionAttribute(AttributeData attribute, EntityMetadata metadata)
    {
        foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key == "TrackHistory" && namedArg.Value.Value is bool trackHistory)
            {
                metadata.RowVersionTrackHistory = trackHistory;
            }
        }
    }

    private void ParseStagingTableAttribute(AttributeData attribute, EntityMetadata metadata)
    {
        foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "MergeIntervalSeconds":
                    if (namedArg.Value.Value is int mergeInterval)
                    {
                        metadata.StagingMergeIntervalSeconds = mergeInterval;
                    }
                    break;

                case "MinBatchSize":
                    if (namedArg.Value.Value is int minBatchSize)
                    {
                        metadata.StagingMinBatchSize = minBatchSize;
                    }
                    break;
            }
        }
    }

    private void ParseAutoPurgeAttribute(AttributeData attribute, EntityMetadata metadata)
    {
        foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "RetentionDays":
                    if (namedArg.Value.Value is int retentionDays)
                    {
                        metadata.AutoPurgeRetentionDays = retentionDays;
                    }
                    break;

                case "DatePropertyName":
                    if (namedArg.Value.Value is string dateProperty)
                    {
                        metadata.AutoPurgeDatePropertyName = dateProperty;
                    }
                    break;
            }
        }
    }

    private void ParseSoftDeleteAttribute(AttributeData attribute, EntityMetadata metadata)
    {
        foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
                    {
                        case "DeletedColumn":
                            if (namedArg.Value.Value is string deletedColumn && !string.IsNullOrWhiteSpace(deletedColumn))
                            {
                                metadata.SoftDeleteColumn = deletedColumn;
                            }
                            break;

                        case "DeletedDateColumn":
                            if (namedArg.Value.Value is string deletedDateColumn && !string.IsNullOrWhiteSpace(deletedDateColumn))
                            {
                                metadata.SoftDeleteDateColumn = deletedDateColumn;
                            }
                            break;
                    }
                }
            }

            private void ParseInMemoryTableAttribute(AttributeData attribute, EntityMetadata metadata)
            {
                foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
                {
                    switch (namedArg.Key)
                    {
                        case "FlushIntervalSeconds":
                            if (namedArg.Value.Value is int flushInterval)
                            {
                                metadata.InMemoryFlushIntervalSeconds = flushInterval;
                            }
                            break;

                        case "MaxRowCount":
                            if (namedArg.Value.Value is int maxRowCount)
                            {
                                metadata.InMemoryMaxRowCount = maxRowCount;
                            }
                            break;

                        case "FlushToStaging":
                            if (namedArg.Value.Value is bool flushToStaging)
                            {
                                metadata.InMemoryFlushToStaging = flushToStaging;
                            }
                            break;

                        case "AutoGenerateId":
                            if (namedArg.Value.Value is bool autoGenerateId)
                            {
                                metadata.InMemoryAutoGenerateId = autoGenerateId;
                            }
                            break;

                        case "EnforceConstraints":
                            if (namedArg.Value.Value is bool enforceConstraints)
                            {
                                metadata.InMemoryEnforceConstraints = enforceConstraints;
                            }
                            break;

                        case "ValidateOnWrite":
                            if (namedArg.Value.Value is bool validateOnWrite)
                            {
                                metadata.InMemoryValidateOnWrite = validateOnWrite;
                            }
                            break;

                        case "FlushBatchSize":
                            if (namedArg.Value.Value is int flushBatchSize)
                            {
                                metadata.InMemoryFlushBatchSize = flushBatchSize;
                            }
                            break;

                        case "RetainAfterFlush":
                            if (namedArg.Value.Value is bool retainAfterFlush)
                            {
                                metadata.InMemoryRetainAfterFlush = retainAfterFlush;
                            }
                            break;

                        case "FlushPriority":
                            if (namedArg.Value.Value is int flushPriority)
                            {
                                metadata.InMemoryFlushPriority = flushPriority;
                            }
                            break;

                        case "TrackOperations":
                            if (namedArg.Value.Value is bool trackOperations)
                            {
                                metadata.InMemoryTrackOperations = trackOperations;
                            }
                                            break;
                                    }
                                }
                            }

                            private void ParseMemoryMappedTableAttribute(AttributeData attribute, EntityMetadata metadata)
                            {
                                foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
                                {
                                    switch (namedArg.Key)
                                    {
                                        case "FileName":
                                            if (namedArg.Value.Value is string fileName)
                                            {
                                                metadata.MemoryMappedFileName = fileName;
                                            }
                                            break;

                                        case "SizeMB":
                                            if (namedArg.Value.Value is int sizeMB)
                                            {
                                                metadata.MemoryMappedSizeMB = sizeMB;
                                            }
                                            break;

                                        case "SyncMode":
                                            if (namedArg.Value.Value is int syncMode)
                                            {
                                                metadata.MemoryMappedSyncMode = syncMode;
                                            }
                                            break;

                                        case "FlushIntervalSeconds":
                                            if (namedArg.Value.Value is int flushInterval)
                                            {
                                                metadata.MemoryMappedFlushIntervalSeconds = flushInterval;
                                            }
                                            break;

                                        case "AutoCreateFile":
                                            if (namedArg.Value.Value is bool autoCreateFile)
                                            {
                                                metadata.MemoryMappedAutoCreateFile = autoCreateFile;
                                            }
                                            break;

                                        case "AutoLoadOnStartup":
                                            if (namedArg.Value.Value is bool autoLoadOnStartup)
                                            {
                                                metadata.MemoryMappedAutoLoadOnStartup = autoLoadOnStartup;
                                            }
                                            break;

                                        case "ReadOnlyCache":
                                            if (namedArg.Value.Value is bool readOnlyCache)
                                            {
                                                metadata.MemoryMappedReadOnlyCache = readOnlyCache;
                                            }
                                            break;

                                        case "MaxCachedRows":
                                            if (namedArg.Value.Value is int maxCachedRows)
                                            {
                                                metadata.MemoryMappedMaxCachedRows = maxCachedRows;
                                            }
                                            break;

                                        case "TimeToLiveSeconds":
                                            if (namedArg.Value.Value is int ttl)
                                            {
                                                metadata.MemoryMappedTimeToLiveSeconds = ttl;
                                            }
                                            break;
                                    }
                                }
                            }

                            private PropertyMetadata? ParseProperty(IPropertySymbol property)
    {
        PropertyMetadata metadata = new PropertyMetadata
        {
            PropertyName = property.Name,
            PropertyType = property.Type.ToDisplayString(),
            ColumnName = property.Name,
            IsNullable = property.Type.NullableAnnotation == NullableAnnotation.Annotated ||
                        (property.Type is INamedTypeSymbol namedType && 
                         namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        };

        // Parse property attributes
        foreach (AttributeData attribute in property.GetAttributes())
        {
            string? attributeName = attribute.AttributeClass?.Name;

            switch (attributeName)
            {
                case "NotMappedAttribute":
                case "NotMapped":
                    metadata.IsNotMapped = true;
                    return metadata;

                case "ColumnAttribute":
                case "Column":
                    // Column name from constructor or Name property
                    if (attribute.ConstructorArguments.Length > 0 &&
                        attribute.ConstructorArguments[0].Value is string colName)
                    {
                        metadata.ColumnName = colName;
                    }
                    foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
                    {
                        switch (namedArg.Key)
                        {
                            case "Name":
                                if (namedArg.Value.Value is string name)
                                {
                                    metadata.ColumnName = name;
                                }
                                break;
                            case "MaxLength":
                                if (namedArg.Value.Value is int maxLen)
                                {
                                    metadata.MaxLength = maxLen;
                                }
                                break;
                            case "TypeName":
                                if (namedArg.Value.Value is string typeName)
                                {
                                    metadata.CustomSqlType = typeName;
                                }
                                break;
                        }
                    }
                    break;

                case "PrimaryKeyAttribute":
                case "PrimaryKey":
                    metadata.IsPrimaryKey = true;
                    foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
                    {
                        if (namedArg.Key == "AutoIncrement" && namedArg.Value.Value is bool autoIncrement)
                        {
                            metadata.IsAutoIncrement = autoIncrement;
                        }
                    }
                    break;

                case "IdentityAttribute":
                case "Identity":
                    metadata.IsAutoIncrement = true;
                    break;

                case "IndexAttribute":
                case "Index":
                    metadata.IsIndexed = true;
                    foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
                    {
                        switch (namedArg.Key)
                        {
                            case "IsUnique":
                                if (namedArg.Value.Value is bool unique)
                                {
                                    metadata.IsUniqueIndex = unique;
                                }
                                break;
                            case "Name":
                                if (namedArg.Value.Value is string indexName)
                                {
                                    metadata.IndexName = indexName;
                                }
                                break;
                        }
                    }
                    break;

                case "SqlTypeAttribute":
                    if (attribute.ConstructorArguments.Length > 0 && 
                        attribute.ConstructorArguments[0].Value is string sqlType)
                    {
                        metadata.CustomSqlType = sqlType;
                    }
                    break;

                case "MaxLengthAttribute":
                case "MaxLength":
                    if (attribute.ConstructorArguments.Length > 0 &&
                        attribute.ConstructorArguments[0].Value is int maxLength)
                    {
                        metadata.MaxLength = maxLength;
                    }
                    break;

                case "OneToOneAttribute":
                    metadata.IsOneToOne = true;
                    foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
                    {
                        if (namedArg.Key == "ForeignKeyProperty" && namedArg.Value.Value is string fkProperty)
                        {
                            metadata.ForeignKeyProperty = fkProperty;
                        }
                    }
                    break;

                case "OneToManyAttribute":
                    metadata.IsOneToMany = true;
                    foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
                    {
                        if (namedArg.Key == "ForeignKeyProperty" && namedArg.Value.Value is string fkProperty)
                        {
                            metadata.ForeignKeyProperty = fkProperty;
                        }
                    }
                    break;

                case "ManyToManyAttribute":
                    metadata.IsManyToMany = true;
                    foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
                    {
                        if (namedArg.Key == "JunctionTableName" && namedArg.Value.Value is string junctionTable)
                        {
                            metadata.JunctionTableName = junctionTable;
                        }
                    }
                    break;
            }
        }

        return metadata;
    }
}
