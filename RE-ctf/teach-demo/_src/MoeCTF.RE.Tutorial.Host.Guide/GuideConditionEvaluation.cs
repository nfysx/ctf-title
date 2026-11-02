using System;
using System.Collections.Generic;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideConditionEvaluation
{
	public bool Matched { get; init; }

	public bool Contradicted { get; init; }

	public int HardEvidenceCount { get; init; }

	public int ApplicationEvidenceCount { get; init; }

	public int DocumentEvidenceCount { get; init; }

	public int StateEvidenceCount { get; init; }

	public int Specificity { get; init; }

	public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();

	public GuideRenderPlan? RecognitionPlan { get; init; }

	public bool IsConclusive
	{
		get
		{
			if (Matched && !Contradicted && ApplicationEvidenceCount > 0 && StateEvidenceCount > 0)
			{
				return HardEvidenceCount > 0;
			}
			return false;
		}
	}
}
