namespace ClassPrestige.Models;
public readonly record struct KillValidationResult(bool IsValid, double Multiplier, string? RejectionReason);
