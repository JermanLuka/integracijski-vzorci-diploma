namespace ArchC.Messaging;

public sealed record ConsumeResult(int Sent, int DeadLettered);
