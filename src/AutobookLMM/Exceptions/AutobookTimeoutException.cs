using System;

namespace AutobookLMM.Exceptions;

/// <summary>
/// Exception thrown when operations time out.
/// </summary>
public class AutobookTimeoutException : AutobookException
{
    public AutobookTimeoutException(string message) : base(message) { }
    public AutobookTimeoutException(string message, Exception innerException) : base(message, innerException) { }
}
