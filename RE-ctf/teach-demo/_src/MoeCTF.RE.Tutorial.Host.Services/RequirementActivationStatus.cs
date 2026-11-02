namespace MoeCTF.RE.Tutorial.Host.Services;

public sealed record RequirementActivationStatus(CourseRequirement Requirement, bool IsSatisfied, string? Path, string DisplayPath, bool IsSelected);
