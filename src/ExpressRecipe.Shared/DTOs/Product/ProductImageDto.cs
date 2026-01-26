namespace ExpressRecipe.Shared.DTOs.Product
{
    public class ProductImageDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }

        // Image source
        public string ImageType { get; set; } = string.Empty; // 'Front', 'Back', 'Side', 'Nutrition', 'Ingredients', 'Other'
        public string? ImageUrl { get; set; } // External URL (OpenFoodFacts, etc.)
        public string? LocalFilePath { get; set; } // Local server file path for uploaded images
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public string? MimeType { get; set; }

        // Image metadata
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsUserUploaded { get; set; }

        // Source tracking
        public string? SourceSystem { get; set; } // 'OpenFoodFacts', 'User', 'Admin', etc.
        public string? SourceId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
