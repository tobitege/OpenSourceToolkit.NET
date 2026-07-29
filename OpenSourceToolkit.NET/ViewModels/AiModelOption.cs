namespace OpenSourceToolkit.NET.ViewModels
{
    public sealed class AiModelOption
    {
        public AiModelOption(string modelId, bool isImageGeneration)
        {
            ModelId = modelId;
            IsImageGeneration = isImageGeneration;
        }

        public string ModelId { get; }
        public bool IsImageGeneration { get; }
        public bool IsTextOnly => !IsImageGeneration;
    }
}
