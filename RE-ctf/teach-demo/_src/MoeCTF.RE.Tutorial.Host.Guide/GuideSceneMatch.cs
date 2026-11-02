using System;
using System.Collections.Generic;
using MoeCTF.RE.Tutorial.Host.Stages;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideSceneMatch
{
	public StageDefinition? Stage { get; init; }

	public GuideDeviationSpec? Deviation { get; init; }

	public int StageIndex { get; init; } = -1;

	public required StageStep Step { get; init; }

	public int StepIndex { get; init; }

	public GuideRenderPlan RenderPlan { get; init; } = new GuideRenderPlan();

	public int Score { get; init; }

	public string Scope { get; init; } = "";

	public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();

	public bool IsStepTrigger { get; init; }

	public bool HasHardTriggerEvidence { get; init; }

	public bool HasContradictoryTriggerEvidence { get; init; }

	public int TriggerSpecificity { get; init; }

	public GuideConditionEvaluation? TriggerEvaluation { get; init; }

	public bool IsExternal => string.Equals(Scope, "process", StringComparison.OrdinalIgnoreCase);

	public bool IsDeviation => Deviation != null;
}
