namespace TriInspector
{
    public readonly struct TriValidationResult
    {
        public static TriValidationResult Valid => new TriValidationResult(true, null, TriMessageType.None);

        public TriValidationResult(bool valid, string message, TriMessageType messageType)
        {
            IsValid = valid;
            Message = message;
            MessageType = messageType;
        }

        public bool IsValid { get; }
        public string Message { get; }
        public TriMessageType MessageType { get; }

        public static TriValidationResult Error(string error)
        {
            return new TriValidationResult(false, error, TriMessageType.Error);
        }

        public static TriValidationResult Warning(string error)
        {
            return new TriValidationResult(false, error, TriMessageType.Warning);
        }
    }

    public enum TriMessageType
    {
        None,
        Info,
        Warning,
        Error,
    }
}