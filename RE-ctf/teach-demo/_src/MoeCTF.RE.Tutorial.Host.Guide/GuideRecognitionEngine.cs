using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MoeCTF.RE.Tutorial.Host.Services;
using MoeCTF.RE.Tutorial.Host.Stages;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideRecognitionEngine
{
	private const int AmbiguitySpecificityMargin = 36;

	private readonly GuideRuntimeContext _context;

	private readonly GuideSceneMatcher _matcher;

	public GuideRecognitionEngine(GuideRuntimeContext context, GuideSceneMatcher matcher)
	{
		_context = context;
		_matcher = matcher;
	}

	public GuideRecognitionMatch? Recognize(IReadOnlyList<StageDefinition> stages, int minimumScore = 140, CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		IReadOnlyList<WindowInfo> windows = _context.VisibleWindows();
		List<GuideSceneMatch> list = new List<GuideSceneMatch>();
		for (int i = 0; i < stages.Count; i++)
		{
			StageDefinition stageDefinition = stages[i];
			for (int j = 0; j < stageDefinition.Steps.Count; j++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				StageStep stageStep = stageDefinition.Steps[j];
				if (stageStep.Trigger == null)
				{
					continue;
				}
				GuideConditionEvaluation guideConditionEvaluation = _matcher.EvaluateCondition(stageStep.Trigger, windows, cancellationToken);
				if (!guideConditionEvaluation.IsConclusive)
				{
					continue;
				}
				GuideRenderPlan recognitionPlan = guideConditionEvaluation.RecognitionPlan;
				if (recognitionPlan != null && recognitionPlan.IsExternalDisplayable)
				{
					int score = Math.Max(recognitionPlan.Score, minimumScore) + Math.Min(guideConditionEvaluation.Specificity, 100);
					GuideSceneMatch guideSceneMatch = new GuideSceneMatch
					{
						Stage = stageDefinition,
						StageIndex = i,
						Step = stageStep,
						StepIndex = j,
						RenderPlan = recognitionPlan,
						Score = score,
						Scope = "process",
						Evidence = guideConditionEvaluation.Evidence.Append("step-trigger:" + stageStep.Id).ToArray(),
						IsStepTrigger = true,
						HasHardTriggerEvidence = (guideConditionEvaluation.HardEvidenceCount > 0),
						HasContradictoryTriggerEvidence = guideConditionEvaluation.Contradicted,
						TriggerSpecificity = guideConditionEvaluation.Specificity,
						TriggerEvaluation = guideConditionEvaluation
					};
					if (GuideSceneMatcher.IsReliableExternalRecognition(guideSceneMatch))
					{
						list.Add(guideSceneMatch);
					}
				}
			}
		}
		GuideSceneMatch guideSceneMatch2 = SelectUnambiguousWinner(list);
		GuideConditionEvaluation guideConditionEvaluation2 = guideSceneMatch2?.TriggerEvaluation;
		if (guideConditionEvaluation2 == null)
		{
			return null;
		}
		int num = Math.Clamp(35 * guideConditionEvaluation2.ApplicationEvidenceCount + 30 * guideConditionEvaluation2.StateEvidenceCount + 25 * guideConditionEvaluation2.DocumentEvidenceCount + (guideSceneMatch2.RenderPlan.IsPreciseExternal ? 10 : 0), 0, 100);
		if (num >= 65)
		{
			return new GuideRecognitionMatch(guideSceneMatch2, num, guideSceneMatch2.Evidence);
		}
		return null;
	}

	public static GuideSceneMatch? SelectUnambiguousWinner(IEnumerable<GuideSceneMatch> source)
	{
		GuideSceneMatch[] array = source.Where((GuideSceneMatch candidate) => candidate.IsStepTrigger && candidate.HasHardTriggerEvidence && !candidate.HasContradictoryTriggerEvidence).OrderByDescending(EvidenceStrength).ThenByDescending((GuideSceneMatch candidate) => candidate.TriggerSpecificity)
			.ThenByDescending((GuideSceneMatch candidate) => candidate.RenderPlan.IsPreciseExternal)
			.ThenByDescending((GuideSceneMatch candidate) => candidate.StageIndex)
			.ThenByDescending((GuideSceneMatch candidate) => candidate.StepIndex)
			.ToArray();
		if (array.Length == 0)
		{
			return null;
		}
		if (array.Length == 1)
		{
			return array[0];
		}
		GuideSceneMatch guideSceneMatch = array[0];
		GuideSceneMatch guideSceneMatch2 = array[1];
		if (SameScene(guideSceneMatch, guideSceneMatch2) && Dominates(guideSceneMatch, guideSceneMatch2))
		{
			return guideSceneMatch;
		}
		return null;
	}

	private static int EvidenceStrength(GuideSceneMatch candidate)
	{
		GuideConditionEvaluation triggerEvaluation = candidate.TriggerEvaluation;
		if (triggerEvaluation == null)
		{
			return 0;
		}
		return triggerEvaluation.ApplicationEvidenceCount * 1000 + triggerEvaluation.DocumentEvidenceCount * 400 + triggerEvaluation.StateEvidenceCount * 120 + (candidate.RenderPlan.IsPreciseExternal ? 80 : 0);
	}

	private static bool SameScene(GuideSceneMatch left, GuideSceneMatch right)
	{
		WindowInfo targetWindow = left.RenderPlan.TargetWindow;
		WindowInfo targetWindow2 = right.RenderPlan.TargetWindow;
		if (targetWindow != null && targetWindow2 != null && targetWindow.Pid == targetWindow2.Pid)
		{
			return targetWindow.Hwnd == targetWindow2.Hwnd;
		}
		return false;
	}

	private static bool Dominates(GuideSceneMatch winner, GuideSceneMatch runnerUp)
	{
		_ = winner.TriggerEvaluation;
		_ = runnerUp.TriggerEvaluation;
		if (EvidenceStrength(winner) > EvidenceStrength(runnerUp))
		{
			return true;
		}
		if (winner.TriggerSpecificity - runnerUp.TriggerSpecificity >= 36)
		{
			return true;
		}
		if (winner.RenderPlan.IsPreciseExternal)
		{
			return !runnerUp.RenderPlan.IsPreciseExternal;
		}
		return false;
	}
}
