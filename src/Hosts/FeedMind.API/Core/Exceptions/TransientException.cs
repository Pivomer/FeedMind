namespace FeedMind.API.Core.Exceptions;

public sealed class TransientException(string message) : Exception(message);
