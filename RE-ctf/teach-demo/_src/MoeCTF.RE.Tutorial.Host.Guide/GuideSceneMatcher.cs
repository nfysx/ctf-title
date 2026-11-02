using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using MoeCTF.RE.Tutorial.Host.Services;
using MoeCTF.RE.Tutorial.Host.Stages;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideSceneMatcher
{
	private readonly GuideRuntimeContext _context;

	private readonly Func<GuideConditionSpec, bool>? _legacyConditionEvaluator;

	public GuideSceneMatcher(GuideRuntimeContext context, Func<GuideConditionSpec, bool>? conditionEvaluator = null)
	{
		_context = context;
		_legacyConditionEvaluator = conditionEvaluator;
	}

	public IReadOnlyList<GuideSceneMatch> MatchStage(StageDefinition stage)
	{
		IReadOnlyList<WindowInfo> windows = _context.VisibleWindows();
		List<GuideSceneMatch> matches = new List<GuideSceneMatch>();
		AddDeviationMatches(matches, stage, -1, windows);
		AddStepMatches(matches, stage, -1, windows);
		return SortMatches(matches);
	}

	public GuideSceneMatch? BestExternalMatch(StageDefinition stage, int minScore = 90)
	{
		return MatchStage(stage).FirstOrDefault((GuideSceneMatch m) => m.IsExternal && m.Score >= minScore && IsDisplayableExternalPlan(m.RenderPlan));
	}

	public GuideSceneMatch? BestExternalStageMatch(StageDefinition stage, int minScore = 90)
	{
		IReadOnlyList<WindowInfo> windows = _context.VisibleWindows();
		List<GuideSceneMatch> matches = new List<GuideSceneMatch>();
		AddDeviationMatches(matches, stage, -1, windows, externalOnly: true);
		AddStepMatches(matches, stage, -1, windows, externalOnly: true);
		return SortMatches(matches).FirstOrDefault((GuideSceneMatch m) => m.IsExternal && m.Score >= minScore && IsDisplayableExternalPlan(m.RenderPlan));
	}

	public GuideSceneMatch? BestExternalStepOverlayMatch(StageDefinition stage, int stageIndex, int stepIndex, int minScore = 30, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (stepIndex < 0 || stepIndex >= stage.Steps.Count)
		{
			return null;
		}
		cancellationToken.ThrowIfCancellationRequested();
		IReadOnlyList<WindowInfo> windows = _context.VisibleWindows();
		List<GuideSceneMatch> matches = new List<GuideSceneMatch>();
		AddDeviationMatches(matches, stage, stageIndex, windows, externalOnly: true, cancellationToken);
		GuideSceneMatch guideSceneMatch = SortMatches(matches).FirstOrDefault((GuideSceneMatch m) => m.IsExternal && m.Score >= minScore && IsAnchoredExternalPlan(m.RenderPlan));
		if (guideSceneMatch != null)
		{
			return guideSceneMatch;
		}
		List<GuideSceneMatch> matches2 = new List<GuideSceneMatch>();
		AddSingleStepMatch(matches2, stage, stageIndex, stepIndex, windows, externalOnly: true);
		return SortMatches(matches2).FirstOrDefault((GuideSceneMatch m) => m.IsExternal && m.Score >= minScore && IsDisplayableExternalPlan(m.RenderPlan));
	}

	public GuideOverlayMatch? LocateExternalOverlay(StageDefinition stage, int stageIndex, int stepIndex, int minScore = 30, CancellationToken cancellationToken = default(CancellationToken))
	{
		GuideSceneMatch guideSceneMatch = BestExternalStepOverlayMatch(stage, stageIndex, stepIndex, minScore, cancellationToken);
		if (guideSceneMatch != null)
		{
			return new GuideOverlayMatch(guideSceneMatch);
		}
		return null;
	}

	public IReadOnlyList<GuideSceneMatch> MatchCourse(IReadOnlyList<StageDefinition> stages, CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		IReadOnlyList<WindowInfo> windows = _context.VisibleWindows();
		List<GuideSceneMatch> matches = new List<GuideSceneMatch>();
		for (int i = 0; i < stages.Count; i++)
		{
			AddTriggeredStepStates(matches, stages[i], i, windows, externalOnly: false, cancellationToken);
		}
		IReadOnlyList<GuideSceneMatch> readOnlyList = RankDeterministicTriggers(matches);
		if (readOnlyList.Count > 0)
		{
			GuideSceneMatch guideSceneMatch = ResolveWinningTrigger(readOnlyList[0], windows);
			if (guideSceneMatch != null)
			{
				return new GuideSceneMatch[1] { guideSceneMatch };
			}
			return Array.Empty<GuideSceneMatch>();
		}
		List<GuideSceneMatch> matches2 = new List<GuideSceneMatch>();
		for (int j = 0; j < stages.Count; j++)
		{
			AddDeviationMatches(matches2, stages[j], j, windows, externalOnly: false, cancellationToken);
		}
		return SortMatches(matches2);
	}

	private void AddStepMatches(List<GuideSceneMatch> matches, StageDefinition stage, int stageIndex, IReadOnlyList<WindowInfo> windows, bool externalOnly = false)
	{
		for (int i = 0; i < stage.Steps.Count; i++)
		{
			StageStep step = stage.Steps[i];
			if (!externalOnly || UsesProcessGuide(step))
			{
				AddSingleStepMatch(matches, stage, stageIndex, i, windows, externalOnly);
			}
		}
	}

	private void AddSingleStepMatch(List<GuideSceneMatch> matches, StageDefinition stage, int stageIndex, int stepIndex, IReadOnlyList<WindowInfo> windows, bool externalOnly = false, GuideConditionEvaluation? triggerEvaluation = null)
	{
		if (stepIndex < 0 || stepIndex >= stage.Steps.Count)
		{
			return;
		}
		StageStep stageStep = stage.Steps[stepIndex];
		if (externalOnly && !UsesProcessGuide(stageStep))
		{
			return;
		}
		int score;
		IReadOnlyList<string> evidence;
		string scope;
		GuideRenderPlan guideRenderPlan = ResolveStep(stageStep, windows, out score, out evidence, out scope);
		GuideRenderPlan guideRenderPlan2 = triggerEvaluation?.RecognitionPlan;
		if (guideRenderPlan2 != null && guideRenderPlan2.IsExternalDisplayable && (!guideRenderPlan.IsExternalDisplayable || !string.Equals(scope, "process", StringComparison.OrdinalIgnoreCase)))
		{
			guideRenderPlan = guideRenderPlan2;
			score = guideRenderPlan2.Score;
			scope = "process";
			evidence = guideRenderPlan2.Evidence;
		}
		if (guideRenderPlan.BoundsScreen.HasValue && score > 0 && (!string.Equals(scope, "process", StringComparison.OrdinalIgnoreCase) || IsDisplayableExternalPlan(guideRenderPlan)))
		{
			GuideSceneMatch obj = new GuideSceneMatch
			{
				Stage = stage,
				StageIndex = stageIndex,
				Step = stageStep,
				StepIndex = stepIndex,
				RenderPlan = guideRenderPlan,
				Score = ((triggerEvaluation == null) ? score : (Math.Max(score, 140) + Math.Min(triggerEvaluation.Specificity, 100))),
				Scope = scope
			};
			IReadOnlyList<string> evidence2;
			if (triggerEvaluation == null)
			{
				evidence2 = evidence;
			}
			else
			{
				IReadOnlyList<string> readOnlyList = evidence.Concat(triggerEvaluation.Evidence).Append("step-trigger:" + stageStep.Id).Distinct<string>(StringComparer.OrdinalIgnoreCase)
					.ToArray();
				evidence2 = readOnlyList;
			}
			obj.Evidence = evidence2;
			obj.IsStepTrigger = triggerEvaluation != null;
			obj.HasHardTriggerEvidence = triggerEvaluation != null && triggerEvaluation.HardEvidenceCount > 0;
			obj.HasContradictoryTriggerEvidence = triggerEvaluation?.Contradicted ?? false;
			obj.TriggerSpecificity = triggerEvaluation?.Specificity ?? 0;
			obj.TriggerEvaluation = triggerEvaluation;
			matches.Add(obj);
		}
	}

	private void AddTriggeredStepStates(List<GuideSceneMatch> matches, StageDefinition stage, int stageIndex, IReadOnlyList<WindowInfo> windows, bool externalOnly = false, CancellationToken cancellationToken = default(CancellationToken))
	{
		for (int i = 0; i < stage.Steps.Count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			StageStep step = stage.Steps[i];
			if (!externalOnly || UsesProcessGuide(step))
			{
				GuideConditionEvaluation guideConditionEvaluation = EvaluateStepTrigger(step, windows, cancellationToken);
				if (guideConditionEvaluation.IsConclusive)
				{
					AddTriggeredStepState(matches, stage, stageIndex, i, guideConditionEvaluation, externalOnly);
				}
			}
		}
	}

	private static void AddTriggeredStepState(ICollection<GuideSceneMatch> matches, StageDefinition stage, int stageIndex, int stepIndex, GuideConditionEvaluation trigger, bool externalOnly)
	{
		StageStep stageStep = stage.Steps[stepIndex];
		GuideRenderPlan guideRenderPlan = trigger.RecognitionPlan ?? new GuideRenderPlan();
		bool isExternalDisplayable = guideRenderPlan.IsExternalDisplayable;
		if (!externalOnly || UsesProcessGuide(stageStep) || isExternalDisplayable)
		{
			matches.Add(new GuideSceneMatch
			{
				Stage = stage,
				StageIndex = stageIndex,
				Step = stageStep,
				StepIndex = stepIndex,
				RenderPlan = guideRenderPlan,
				Score = guideRenderPlan.Score,
				Scope = (isExternalDisplayable ? "process" : GuideSpecHelpers.NormalizeScope(stageStep.Guide?.Target.Scope)),
				Evidence = trigger.Evidence,
				IsStepTrigger = true,
				HasHardTriggerEvidence = (trigger.HardEvidenceCount > 0),
				HasContradictoryTriggerEvidence = trigger.Contradicted,
				TriggerSpecificity = trigger.Specificity,
				TriggerEvaluation = trigger
			});
		}
	}

	private GuideSceneMatch? ResolveWinningTrigger(GuideSceneMatch state, IReadOnlyList<WindowInfo> windows)
	{
		if (state.Stage == null || state.TriggerEvaluation == null)
		{
			return null;
		}
		List<GuideSceneMatch> list = new List<GuideSceneMatch>(1);
		AddSingleStepMatch(list, state.Stage, state.StageIndex, state.StepIndex, windows, externalOnly: false, state.TriggerEvaluation);
		return list.FirstOrDefault();
	}

	private GuideConditionEvaluation EvaluateStepTrigger(StageStep step, IReadOnlyList<WindowInfo> windows, CancellationToken cancellationToken)
	{
		if (step.Trigger != null)
		{
			return EvaluateCondition(step.Trigger, windows, cancellationToken);
		}
		return new GuideConditionEvaluation();
	}

	private void AddDeviationMatches(List<GuideSceneMatch> matches, StageDefinition stage, int stageIndex, IReadOnlyList<WindowInfo> windows, bool externalOnly = false, CancellationToken cancellationToken = default(CancellationToken))
	{
		foreach (GuideDeviationSpec item in stage.Deviations.OrderByDescending((GuideDeviationSpec d) => d.Priority))
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (item.Guide != null && EvaluateCondition(item.When, windows, cancellationToken).Matched && (!externalOnly || UsesProcessGuide(item.Guide)))
			{
				int score;
				IReadOnlyList<string> evidence;
				string scope;
				GuideRenderPlan guideRenderPlan = ResolveSpec(item.Guide, windows, out score, out evidence, out scope);
				if (guideRenderPlan.BoundsScreen.HasValue && score > 0 && (!string.Equals(scope, "process", StringComparison.OrdinalIgnoreCase) || IsAnchoredExternalPlan(guideRenderPlan)))
				{
					string[] evidence2 = evidence.Concat(new string[1] { "deviation:" + item.Id }).ToArray();
					matches.Add(new GuideSceneMatch
					{
						Stage = stage,
						Deviation = item,
						StageIndex = stageIndex,
						Step = new StageStep
						{
							Id = "deviation:" + item.Id,
							TextKey = item.TextKey,
							BodyKey = item.BodyKey,
							Image = item.Image,
							Action = "none",
							Guide = item.Guide
						},
						StepIndex = -1,
						RenderPlan = guideRenderPlan,
						Score = score + Math.Clamp(item.Priority, 0, 120),
						Scope = scope,
						Evidence = evidence2
					});
				}
			}
		}
	}

	private static bool UsesProcessGuide(StageStep step)
	{
		return UsesProcessGuide(step.Guide ?? GuideRuntime.BuildLegacyGuide(step));
	}

	private static bool UsesProcessGuide(GuideSpec spec)
	{
		if (GuideSpecHelpers.NormalizeScope(spec.Target.Scope) == "process")
		{
			return true;
		}
		return spec.Locators.Any((GuideLocatorSpec locator) => GuideSpecHelpers.NormalizeScope(locator.Scope ?? spec.Target.Scope) == "process");
	}

	private static IReadOnlyList<GuideSceneMatch> SortMatches(IEnumerable<GuideSceneMatch> matches)
	{
		return (from m in matches
			orderby m.IsExternal descending, m.IsStepTrigger descending, m.IsDeviation descending, m.Score descending, m.StageIndex, m.StepIndex
			select m).ToList();
	}

	public static IReadOnlyList<GuideSceneMatch> RankDeterministicTriggers(IEnumerable<GuideSceneMatch> matches)
	{
		return (from m in matches
			where m.IsStepTrigger && m.HasHardTriggerEvidence && !m.HasContradictoryTriggerEvidence
			orderby m.StageIndex descending, m.StepIndex descending, m.TriggerSpecificity descending, m.RenderPlan.IsPreciseExternal descending, m.Score descending
			select m).ToList();
	}

	public GuideConditionEvaluation EvaluateCondition(GuideConditionSpec condition, IReadOnlyList<WindowInfo>? windows = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (windows == null)
		{
			windows = _context.VisibleWindows();
		}
		GuideConditionEvaluation guideConditionEvaluation = EvaluateConditionCore(condition, windows, cancellationToken);
		if (!condition.Not)
		{
			return guideConditionEvaluation;
		}
		return new GuideConditionEvaluation
		{
			Matched = !guideConditionEvaluation.Matched,
			Specificity = guideConditionEvaluation.Specificity,
			Evidence = guideConditionEvaluation.Evidence.Prepend("condition:not").ToArray()
		};
	}

	private GuideConditionEvaluation EvaluateConditionCore(GuideConditionSpec condition, IReadOnlyList<WindowInfo> windows, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		string text = (condition.Kind ?? "always").Trim().ToLowerInvariant();
		return text switch
		{
			"all" => EvaluateAll(condition.All, windows, cancellationToken), 
			"any" => EvaluateAny(condition.Any, windows, cancellationToken), 
			"locator_found" => EvaluateLocatorCondition(condition.Locator, windows, cancellationToken), 
			"value_equals" => EvaluateValueCondition(condition, exact: true), 
			"value_contains" => EvaluateValueCondition(condition, exact: false), 
			"process_running" => EvaluateBroadCondition(text, IsManagedProcessRunning(condition.Process)), 
			"window_visible" => EvaluateBroadCondition(text, IsManagedWindowVisible(condition.Process, windows)), 
			"host_visible" => EvaluateBroadCondition(text, !string.IsNullOrWhiteSpace(condition.Key) && TryGetHostBounds(condition.Key).HasValue), 
			"always" => EvaluateBroadCondition(text, matched: true), 
			_ => EvaluateBroadCondition(text, _legacyConditionEvaluator?.Invoke(condition) ?? false), 
		};
	}

	private GuideConditionEvaluation EvaluateAll(IReadOnlyList<GuideConditionSpec> conditions, IReadOnlyList<WindowInfo> windows, CancellationToken cancellationToken)
	{
		if (conditions.Count == 0)
		{
			return EvaluateBroadCondition("all:empty", matched: true);
		}
		List<GuideConditionEvaluation> list = new List<GuideConditionEvaluation>(conditions.Count);
		foreach (GuideConditionSpec condition in conditions)
		{
			cancellationToken.ThrowIfCancellationRequested();
			list.Add(EvaluateCondition(condition, windows, cancellationToken));
		}
		GuideRenderPlan recognitionPlan = (from e in list
			where e.RecognitionPlan != null
			orderby e.Specificity descending
			select e.RecognitionPlan).FirstOrDefault();
		return new GuideConditionEvaluation
		{
			Matched = list.All((GuideConditionEvaluation e) => e.Matched),
			Contradicted = list.Any((GuideConditionEvaluation e) => e.Contradicted),
			HardEvidenceCount = list.Sum((GuideConditionEvaluation e) => e.HardEvidenceCount),
			ApplicationEvidenceCount = list.Sum((GuideConditionEvaluation e) => e.ApplicationEvidenceCount),
			DocumentEvidenceCount = list.Sum((GuideConditionEvaluation e) => e.DocumentEvidenceCount),
			StateEvidenceCount = list.Sum((GuideConditionEvaluation e) => e.StateEvidenceCount),
			Specificity = list.Sum((GuideConditionEvaluation e) => e.Specificity),
			Evidence = list.SelectMany((GuideConditionEvaluation e) => e.Evidence).Prepend("condition:all").ToArray(),
			RecognitionPlan = recognitionPlan
		};
	}

	private GuideConditionEvaluation EvaluateAny(IReadOnlyList<GuideConditionSpec> conditions, IReadOnlyList<WindowInfo> windows, CancellationToken cancellationToken)
	{
		if (conditions.Count == 0)
		{
			return EvaluateBroadCondition("any:empty", matched: false);
		}
		List<GuideConditionEvaluation> list = new List<GuideConditionEvaluation>(conditions.Count);
		foreach (GuideConditionSpec condition in conditions)
		{
			cancellationToken.ThrowIfCancellationRequested();
			list.Add(EvaluateCondition(condition, windows, cancellationToken));
		}
		GuideConditionEvaluation guideConditionEvaluation = (from e in list
			where e.Matched && !e.Contradicted
			orderby e.IsConclusive descending, e.Specificity descending
			select e).FirstOrDefault();
		if (guideConditionEvaluation != null)
		{
			return new GuideConditionEvaluation
			{
				Matched = true,
				HardEvidenceCount = guideConditionEvaluation.HardEvidenceCount,
				ApplicationEvidenceCount = guideConditionEvaluation.ApplicationEvidenceCount,
				DocumentEvidenceCount = guideConditionEvaluation.DocumentEvidenceCount,
				StateEvidenceCount = guideConditionEvaluation.StateEvidenceCount,
				Specificity = guideConditionEvaluation.Specificity,
				Evidence = guideConditionEvaluation.Evidence.Prepend("condition:any").ToArray(),
				RecognitionPlan = guideConditionEvaluation.RecognitionPlan
			};
		}
		return new GuideConditionEvaluation
		{
			Matched = false,
			Contradicted = (list.Count > 0 && list.All((GuideConditionEvaluation e) => e.Contradicted)),
			Specificity = ((list.Count != 0) ? list.Max((GuideConditionEvaluation e) => e.Specificity) : 0),
			Evidence = list.SelectMany((GuideConditionEvaluation e) => e.Evidence).Prepend("condition:any:no-match").ToArray()
		};
	}

	private GuideConditionEvaluation EvaluateLocatorCondition(GuideLocatorSpec? locator, IReadOnlyList<WindowInfo> windows, CancellationToken cancellationToken)
	{
		if (locator == null)
		{
			GuideConditionEvaluation guideConditionEvaluation = new GuideConditionEvaluation();
			guideConditionEvaluation.Evidence = new string[1] { "condition:locator-missing" };
			return guideConditionEvaluation;
		}
		cancellationToken.ThrowIfCancellationRequested();
		int score;
		IReadOnlyList<string> evidence;
		string scope;
		GuideRenderPlan guideRenderPlan = ResolveLocator(locator, LocatorToTarget(locator), new GuideOverlaySpec(), windows, out score, out evidence, out scope);
		cancellationToken.ThrowIfCancellationRequested();
		bool flag = (string.Equals(scope, "host", StringComparison.OrdinalIgnoreCase) ? guideRenderPlan.BoundsScreen.HasValue : IsRecognizedExternalPlan(guideRenderPlan));
		bool flag2 = evidence.Any(IsContradictoryEvidence);
		int num = (flag ? CountObservedApplicationEvidence(evidence) : 0);
		int num2 = (flag ? CountObservedHints(evidence, locator.DocumentContains) : 0);
		int num3 = (flag ? CountObservedStateEvidence(locator, evidence) : 0);
		bool flag3 = locator.DocumentContains.Count == 0 || num2 == locator.DocumentContains.Count;
		int num4 = ((flag && flag3 && num > 0 && num3 > 0 && HasHardTriggerLocator(locator)) ? CountHardLocatorEvidence(locator) : 0);
		return new GuideConditionEvaluation
		{
			Matched = (flag && !flag2 && flag3),
			Contradicted = flag2,
			HardEvidenceCount = num4,
			ApplicationEvidenceCount = num,
			DocumentEvidenceCount = num2,
			StateEvidenceCount = num3,
			Specificity = LocatorSpecificity(locator, num4, score),
			Evidence = evidence.Prepend("condition:locator-found").ToArray(),
			RecognitionPlan = ((flag && !flag2) ? guideRenderPlan : null)
		};
	}

	private GuideConditionEvaluation EvaluateValueCondition(GuideConditionSpec condition, bool exact)
	{
		GuideConditionEvaluation guideConditionEvaluation;
		if (string.IsNullOrWhiteSpace(condition.Key))
		{
			guideConditionEvaluation = new GuideConditionEvaluation();
			guideConditionEvaluation.Evidence = new string[1] { "condition:value-key-missing" };
			return guideConditionEvaluation;
		}
		string text = ((condition.ValueKey != null) ? _context.TextProvider(condition.ValueKey) : condition.Value);
		string text2 = _context.ValueProvider(condition.Key) ?? string.Empty;
		bool flag = !string.IsNullOrEmpty(text) && (exact ? string.Equals(text2, text, StringComparison.Ordinal) : (text2.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0));
		int num = ((flag && text.Length >= 4) ? 1 : 0);
		guideConditionEvaluation = new GuideConditionEvaluation();
		guideConditionEvaluation.Matched = flag;
		guideConditionEvaluation.HardEvidenceCount = num;
		guideConditionEvaluation.StateEvidenceCount = num;
		guideConditionEvaluation.Specificity = ((num == 0) ? 5 : (40 + Math.Min(text.Length, 80)));
		guideConditionEvaluation.Evidence = new string[1] { "condition:" + (exact ? "value-equals" : "value-contains") + ":" + condition.Key };
		return guideConditionEvaluation;
	}

	private static GuideConditionEvaluation EvaluateBroadCondition(string kind, bool matched)
	{
		GuideConditionEvaluation guideConditionEvaluation = new GuideConditionEvaluation();
		guideConditionEvaluation.Matched = matched;
		guideConditionEvaluation.Specificity = (matched ? 1 : 0);
		guideConditionEvaluation.Evidence = new string[1] { "condition:broad:" + kind };
		return guideConditionEvaluation;
	}

	private bool IsManagedProcessRunning(string? key)
	{
		if (!TryGetManagedProcess(key, out ManagedProcess process))
		{
			return false;
		}
		try
		{
			return !process.Process.HasExited;
		}
		catch
		{
			return false;
		}
	}

	private bool IsManagedWindowVisible(string? key, IReadOnlyList<WindowInfo> windows)
	{
		if (!TryGetManagedProcess(key, out ManagedProcess managed))
		{
			return false;
		}
		try
		{
			if (managed.Process.HasExited)
			{
				return false;
			}
			return windows.Any((WindowInfo w) => w.Pid == managed.Process.Id);
		}
		catch
		{
			return false;
		}
	}

	private bool TryGetManagedProcess(string? key, out ManagedProcess process)
	{
		process = null;
		if (string.IsNullOrWhiteSpace(key))
		{
			return false;
		}
		if (_context.Processes.TryGetValue(key, out process))
		{
			return true;
		}
		if (!key.StartsWith("target:", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(key).Equals(".exe", StringComparison.OrdinalIgnoreCase))
		{
			return _context.Processes.TryGetValue("target:" + key, out process);
		}
		return false;
	}

	private static GuideTargetSpec LocatorToTarget(GuideLocatorSpec locator)
	{
		return new GuideTargetSpec
		{
			Scope = (locator.Scope ?? "process"),
			Key = locator.Key,
			Process = locator.Process,
			Adapter = locator.Adapter,
			TitleContains = locator.TitleContains,
			ProcessNameContains = locator.ProcessNameContains,
			ExeNameContains = locator.ExeNameContains,
			ExePathContains = locator.ExePathContains,
			WindowClassNameContains = locator.WindowClassNameContains,
			TitlePidFromProcess = locator.TitlePidFromProcess
		};
	}

	private static bool HasHardTriggerLocator(GuideLocatorSpec locator)
	{
		if (HasDeclaredApplicationIdentity(locator))
		{
			return HasDistinctStateEvidence(locator);
		}
		return false;
	}

	private static bool HasDeclaredApplicationIdentity(GuideLocatorSpec locator)
	{
		if (string.IsNullOrWhiteSpace(locator.Process) && string.IsNullOrWhiteSpace(locator.Adapter) && string.IsNullOrWhiteSpace(locator.TitlePidFromProcess) && locator.ProcessNameContains.Count <= 0 && locator.ExeNameContains.Count <= 0 && locator.ExePathContains.Count <= 0)
		{
			return locator.WindowClassNameContains.Count > 0;
		}
		return true;
	}

	private static bool HasDistinctStateEvidence(GuideLocatorSpec locator)
	{
		if (!locator.ContentContains.Any(IsDistinctHint) && !locator.NameContains.Any(IsDistinctHint) && !IsDistinctHint(locator.NameEquals) && !IsDistinctHint(locator.State) && !IsDistinctHint(locator.Role) && !IsDistinctHint(locator.AutomationId))
		{
			return locator.TitleContains.Any(IsDistinctHint);
		}
		return true;
	}

	private static int CountObservedApplicationEvidence(IReadOnlyList<string> evidence)
	{
		return HasStrongWindowEvidence(evidence) ? 1 : 0;
	}

	private static int CountObservedHints(IReadOnlyList<string> evidence, IReadOnlyList<string> hints)
	{
		if (hints.Count == 0)
		{
			return 0;
		}
		return hints.Count((string hint) => evidence.Any((string item) => item.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0));
	}

	private static int CountObservedStateEvidence(GuideLocatorSpec locator, IReadOnlyList<string> evidence)
	{
		int num = CountObservedHints(evidence, locator.ContentContains);
		if (!string.IsNullOrWhiteSpace(locator.State) && evidence.Any((string item) => item.IndexOf(locator.State, StringComparison.OrdinalIgnoreCase) >= 0))
		{
			num++;
		}
		if (!string.IsNullOrWhiteSpace(locator.Role) && evidence.Any((string item) => item.IndexOf(locator.Role, StringComparison.OrdinalIgnoreCase) >= 0))
		{
			num++;
		}
		if (!string.IsNullOrWhiteSpace(locator.NameEquals) && evidence.Any((string item) => item.IndexOf(locator.NameEquals, StringComparison.OrdinalIgnoreCase) >= 0))
		{
			num++;
		}
		return num;
	}

	private static bool IsDistinctHint(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Trim().Length >= 3;
		}
		return false;
	}

	private static int CountHardLocatorEvidence(GuideLocatorSpec locator)
	{
		int num = 1;
		if (locator.DocumentContains.Count > 0)
		{
			num++;
		}
		if (locator.ContentContains.Count > 0)
		{
			num++;
		}
		if (!string.IsNullOrWhiteSpace(locator.State) || !string.IsNullOrWhiteSpace(locator.Role))
		{
			num++;
		}
		if (!string.IsNullOrWhiteSpace(locator.NameEquals) || locator.NameContains.Count > 0)
		{
			num++;
		}
		if (!string.IsNullOrWhiteSpace(locator.AutomationId))
		{
			num++;
		}
		return num;
	}

	private static int LocatorSpecificity(GuideLocatorSpec locator, int hardEvidence, int score)
	{
		int num = (from value in locator.ContentContains.Concat(locator.NameContains).Concat(locator.TitleContains)
			where !string.IsNullOrWhiteSpace(value)
			select value).Sum((string value) => Math.Min(value.Length, 32));
		return hardEvidence * 100 + num + (locator.RequireAllNameContains ? 40 : 0) + ((!string.IsNullOrWhiteSpace(locator.NameEquals)) ? 50 : 0) + ((!string.IsNullOrWhiteSpace(locator.AutomationId)) ? 40 : 0) + Math.Min(score, 100);
	}

	private static bool IsContradictoryEvidence(string evidence)
	{
		if (!evidence.StartsWith("reject-title:", StringComparison.OrdinalIgnoreCase))
		{
			return evidence.StartsWith("reject-content:", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static bool IsPreciseRecognition(GuideSceneMatch match)
	{
		if (!match.Evidence.Any((string e) => e.StartsWith("ambiguous-window-candidates:", StringComparison.OrdinalIgnoreCase)))
		{
			return match.RenderPlan.IsPreciseExternal;
		}
		return false;
	}

	public static bool IsReliableExternalRecognition(GuideSceneMatch match)
	{
		if (!match.Evidence.Any((string e) => e.StartsWith("ambiguous-window-candidates:", StringComparison.OrdinalIgnoreCase)))
		{
			return IsRecognizedExternalPlan(match.RenderPlan);
		}
		return false;
	}

	public static bool IsDisplayableExternalPlan(GuideRenderPlan plan)
	{
		return plan.IsExternalDisplayable;
	}

	public static bool IsRecognizedExternalPlan(GuideRenderPlan plan)
	{
		return IsAnchoredExternalPlan(plan);
	}

	private static bool IsAnchoredExternalPlan(GuideRenderPlan plan)
	{
		if (plan.IsExternalDisplayable)
		{
			return plan.Attachment != GuideAttachmentState.Window;
		}
		return false;
	}

	private static bool HasVerifiedRegionProof(IReadOnlyList<string> evidence)
	{
		if (HasStrongWindowEvidence(evidence))
		{
			return evidence.Any((string e) => e.Contains(":state:", StringComparison.OrdinalIgnoreCase) || e.Contains(":content:", StringComparison.OrdinalIgnoreCase) || e.StartsWith("content:", StringComparison.OrdinalIgnoreCase) || e.StartsWith("content-target:", StringComparison.OrdinalIgnoreCase));
		}
		return false;
	}

	private static bool HasStrongWindowEvidence(IReadOnlyList<string> evidence)
	{
		if (evidence.Any((string e) => e.StartsWith("managed-pid:", StringComparison.OrdinalIgnoreCase) || e.StartsWith("managed-title:", StringComparison.OrdinalIgnoreCase) || e.StartsWith("managed-process:", StringComparison.OrdinalIgnoreCase) || e.StartsWith("title-pid:", StringComparison.OrdinalIgnoreCase) || e.StartsWith("target-process-exact:", StringComparison.OrdinalIgnoreCase) || e.StartsWith("target-exe-exact:", StringComparison.OrdinalIgnoreCase)))
		{
			return true;
		}
		int num = 0;
		foreach (string item in evidence)
		{
			if (item.StartsWith("adapter:", StringComparison.OrdinalIgnoreCase) || item.StartsWith("process:", StringComparison.OrdinalIgnoreCase) || item.StartsWith("exe:", StringComparison.OrdinalIgnoreCase) || item.StartsWith("exe-name:", StringComparison.OrdinalIgnoreCase) || item.StartsWith("title:", StringComparison.OrdinalIgnoreCase) || item.StartsWith("window-class:", StringComparison.OrdinalIgnoreCase) || item.StartsWith("class:", StringComparison.OrdinalIgnoreCase) || item.StartsWith("exe-path:", StringComparison.OrdinalIgnoreCase) || item.StartsWith("modal-window", StringComparison.OrdinalIgnoreCase))
			{
				num++;
			}
		}
		return num >= 2;
	}

	private static bool TrySelectReliableWindow(IReadOnlyList<GuideWindowCandidate> candidates, out GuideWindowCandidate selected, out IReadOnlyList<string> evidence)
	{
		selected = ((candidates.Count > 0) ? candidates[0] : null);
		evidence = Array.Empty<string>();
		if (candidates.Count == 0)
		{
			return false;
		}
		if (candidates.Count == 1)
		{
			return true;
		}
		GuideWindowCandidate[] array = candidates.Where((GuideWindowCandidate c) => HasExactWindowEvidence(c.Evidence)).ToArray();
		if (array.Length == 1)
		{
			selected = array[0];
			return true;
		}
		GuideWindowCandidate guideWindowCandidate = candidates[0];
		GuideWindowCandidate guideWindowCandidate2 = candidates[1];
		if (guideWindowCandidate.Score - guideWindowCandidate2.Score >= 60 && HasStrongWindowEvidence(guideWindowCandidate.Evidence))
		{
			selected = guideWindowCandidate;
			return true;
		}
		evidence = guideWindowCandidate.Evidence.Concat(new string[1] { "ambiguous-window-candidates:" + candidates.Count }).ToArray();
		return false;
	}

	private static bool HasExactWindowEvidence(IReadOnlyList<string> evidence)
	{
		return evidence.Any((string e) => e.StartsWith("managed-pid:", StringComparison.OrdinalIgnoreCase) || e.StartsWith("title-pid:", StringComparison.OrdinalIgnoreCase) || e.StartsWith("target-process-exact:", StringComparison.OrdinalIgnoreCase) || e.StartsWith("target-exe-exact:", StringComparison.OrdinalIgnoreCase));
	}

	public GuideRenderPlan ResolveLocator(GuideLocatorSpec locator, GuideTargetSpec target, GuideOverlaySpec overlay, IReadOnlyList<WindowInfo>? windows, out int score, out IReadOnlyList<string> evidence, out string scope)
	{
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0346: Unknown result type (might be due to invalid IL or missing references)
		//IL_034b: Unknown result type (might be due to invalid IL or missing references)
		//IL_034e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0399: Unknown result type (might be due to invalid IL or missing references)
		//IL_039e: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d6: Unknown result type (might be due to invalid IL or missing references)
		List<string> list = new List<string>();
		scope = GuideSpecHelpers.NormalizeScope(locator.Scope ?? target.Scope);
		GuideTargetSpec guideTargetSpec = GuideSpecHelpers.MergeTarget(locator, target, scope);
		if (scope == "none")
		{
			score = 1;
			evidence = new string[1] { "scope:none" };
			return new GuideRenderPlan
			{
				Layer = GuideLayerKind.None,
				Overlay = overlay
			};
		}
		if (scope == "host")
		{
			Rect? rect = ResolveHost(locator, guideTargetSpec);
			score = (rect.HasValue ? 70 : 0);
			evidence = ((!rect.HasValue) ? ((IReadOnlyList<string>)Array.Empty<string>()) : ((IReadOnlyList<string>)new string[1] { "host:" + (locator.Key ?? guideTargetSpec.Key ?? "coach") }));
			return new GuideRenderPlan
			{
				Layer = GuideLayerKind.Host,
				BoundsScreen = GuideSpecHelpers.Adjust(rect, locator.Adjust),
				Overlay = overlay,
				Attachment = GuideAttachmentState.Host,
				Evidence = evidence,
				Score = score,
				CoordinateSpace = "host"
			};
		}
		if (scope == "screen")
		{
			Rect? rect2 = GuideSpecHelpers.ResolveExplicitRect(locator, null);
			score = (rect2.HasValue ? 50 : 0);
			evidence = ((!rect2.HasValue) ? ((IReadOnlyList<string>)Array.Empty<string>()) : ((IReadOnlyList<string>)new string[1] { "screen:rect" }));
			return new GuideRenderPlan
			{
				Layer = GuideLayerKind.Screen,
				BoundsScreen = GuideSpecHelpers.Adjust(rect2, locator.Adjust),
				Overlay = overlay,
				Attachment = GuideAttachmentState.Region,
				Evidence = evidence,
				Score = score,
				CoordinateSpace = "screen"
			};
		}
		IReadOnlyList<GuideWindowCandidate> readOnlyList = FindWindowCandidates(guideTargetSpec, locator, windows);
		if (readOnlyList.Count == 0)
		{
			score = 0;
			evidence = new string[1] { "window:not-found" };
			return new GuideRenderPlan
			{
				Layer = GuideLayerKind.Screen,
				Overlay = overlay,
				FailureReason = "process window not found",
				Evidence = evidence
			};
		}
		if (GuideSpecHelpers.IsWindowStrategy(locator))
		{
			if (!TrySelectReliableWindow(readOnlyList, out GuideWindowCandidate selected, out IReadOnlyList<string> evidence2))
			{
				score = 0;
				evidence = evidence2;
				return new GuideRenderPlan
				{
					Layer = GuideLayerKind.None,
					Overlay = overlay,
					FailureReason = "window candidate ambiguous",
					Evidence = evidence
				};
			}
			WindowInfo window = selected.Window;
			list.AddRange(selected.Evidence);
			int score2 = selected.Score;
			score = score2 + 24;
			list.Add("locator:window");
			evidence = list;
			return new GuideRenderPlan
			{
				Layer = GuideLayerKind.Screen,
				BoundsScreen = GuideSpecHelpers.Adjust(window.Bounds, locator.Adjust),
				TargetWindow = window,
				Overlay = overlay,
				Attachment = GuideAttachmentState.Window,
				Score = score,
				Evidence = evidence,
				CoordinateSpace = "window"
			};
		}
		if (locator.Rect != null && !GuideSpecHelpers.IsControlRelativeRect(locator.Rect) && !GuideSpecHelpers.HasRecognitionAnchor(locator))
		{
			WindowInfo window2 = readOnlyList[0].Window;
			list.AddRange(readOnlyList[0].Evidence);
			int score3 = readOnlyList[0].Score;
			Rect val = WindowReferenceBounds(window2, locator.Rect);
			Rect? rect3 = GuideSpecHelpers.ResolveExplicitRect(locator, val);
			score = (rect3.HasValue ? (score3 + 36) : 0);
			list.Add("locator:rect");
			evidence = list;
			Rect? val2 = GuideSpecHelpers.Adjust(rect3, locator.Adjust);
			object obj;
			if (val2.HasValue)
			{
				Rect valueOrDefault = val2.GetValueOrDefault();
				obj = LocatedRect.FromWindow(valueOrDefault, window2, "explicit-rect", val);
			}
			else
			{
				obj = null;
			}
			LocatedRect locatedRect = (LocatedRect)obj;
			return new GuideRenderPlan
			{
				Layer = GuideLayerKind.Screen,
				BoundsScreen = locatedRect?.BoundsScreen,
				BoundsInWindow = locatedRect?.BoundsInWindow,
				TargetWindow = window2,
				Overlay = overlay,
				Attachment = (HasVerifiedRegionProof(list) ? GuideAttachmentState.VerifiedRegion : GuideAttachmentState.Region),
				Score = score,
				Evidence = evidence,
				CoordinateSpace = (locatedRect?.CoordinateSpace ?? "client-normalized")
			};
		}
		GuideRenderPlan guideRenderPlan = null;
		int bestScore = 0;
		IReadOnlyList<string> readOnlyList2 = Array.Empty<string>();
		IReadOnlyList<string> readOnlyList3 = Array.Empty<string>();
		List<int> list2 = new List<int>();
		foreach (GuideWindowCandidate item in readOnlyList)
		{
			int score4;
			IReadOnlyList<string> evidence3;
			GuideRenderPlan guideRenderPlan2 = TryResolveProcessControls(item, locator, overlay, out score4, out evidence3);
			if (!(guideRenderPlan2?.BoundsScreen).HasValue || score4 <= bestScore)
			{
				if (evidence3.Count > 0 && readOnlyList3.Count == 0)
				{
					readOnlyList3 = evidence3;
				}
				if ((guideRenderPlan2?.BoundsScreen).HasValue && score4 > 0)
				{
					list2.Add(score4);
				}
			}
			else
			{
				list2.Add(score4);
				guideRenderPlan = guideRenderPlan2;
				bestScore = score4;
				readOnlyList2 = evidence3;
			}
		}
		if (guideRenderPlan != null)
		{
			score = bestScore;
			int num = list2.Count((int s) => bestScore - s < 25);
			IReadOnlyList<string> readOnlyList4;
			if (num <= 1)
			{
				readOnlyList4 = readOnlyList2;
			}
			else
			{
				IReadOnlyList<string> readOnlyList5 = readOnlyList2.Concat(new string[1] { "ambiguous-window-candidates:" + num }).ToArray();
				readOnlyList4 = readOnlyList5;
			}
			evidence = readOnlyList4;
			if (num > 1)
			{
				score = 0;
				return new GuideRenderPlan
				{
					Layer = GuideLayerKind.None,
					Overlay = overlay,
					FailureReason = "ambiguous precise target",
					Evidence = evidence
				};
			}
			return guideRenderPlan;
		}
		score = 0;
		IReadOnlyList<string> readOnlyList6;
		if (readOnlyList3.Count <= 0)
		{
			IReadOnlyList<string> readOnlyList5 = readOnlyList[0].Evidence.Concat(new string[1] { "control:not-found" }).ToArray();
			readOnlyList6 = readOnlyList5;
		}
		else
		{
			readOnlyList6 = readOnlyList3;
		}
		evidence = readOnlyList6;
		return new GuideRenderPlan
		{
			Layer = GuideLayerKind.Screen,
			Overlay = overlay,
			FailureReason = "process control not found",
			Evidence = evidence
		};
	}

	private GuideRenderPlan? TryResolveProcessControls(GuideWindowCandidate candidate, GuideLocatorSpec locator, GuideOverlaySpec overlay, out int score, out IReadOnlyList<string> evidence)
	{
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_05af: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_0335: Unknown result type (might be due to invalid IL or missing references)
		//IL_08a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0899: Unknown result type (might be due to invalid IL or missing references)
		//IL_0717: Unknown result type (might be due to invalid IL or missing references)
		//IL_08a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_08b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_08be: Unknown result type (might be due to invalid IL or missing references)
		//IL_08c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_08ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_08c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0796: Unknown result type (might be due to invalid IL or missing references)
		//IL_07b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_07bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_091a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0930: Unknown result type (might be due to invalid IL or missing references)
		//IL_0932: Unknown result type (might be due to invalid IL or missing references)
		//IL_0953: Unknown result type (might be due to invalid IL or missing references)
		//IL_080a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0820: Unknown result type (might be due to invalid IL or missing references)
		//IL_0822: Unknown result type (might be due to invalid IL or missing references)
		//IL_0843: Unknown result type (might be due to invalid IL or missing references)
		WindowInfo window = candidate.Window;
		List<string> list = new List<string>(candidate.Evidence);
		int score2 = candidate.Score;
		AppControlMatch appControlMatch = _context.AppLocator.Locate(window, locator, _context.ControlLocator);
		if (appControlMatch != null)
		{
			list.Add(appControlMatch.Reason);
			if (appControlMatch.UsedFallbackRegion)
			{
				list.Add("control:adapter-fallback-region");
			}
			if (locator.RejectContentContains.Count > 0)
			{
				ControlTextEvidence controlTextEvidence = _context.ControlLocator.FindTextEvidence(window.Hwnd, locator.RejectContentContains, locator.MaxNodes ?? 3000, locator.TimeoutMs ?? 1200, stopAfterFirstMatch: true);
				if (controlTextEvidence.MatchedHints.Count > 0)
				{
					score = 0;
					evidence = list.Concat(new string[1] { "reject-content:" + string.Join(",", controlTextEvidence.MatchedHints) }).ToArray();
					return null;
				}
			}
			ControlMatch controlMatch = null;
			int num = 0;
			if (GuideSpecHelpers.IsContentTarget(locator))
			{
				controlMatch = FindContentTarget(window, locator, appControlMatch.Bounds);
				if (controlMatch == null)
				{
					score = 0;
					evidence = list.Concat(new string[1] { (locator.ContentContains.Count == 0) ? "content-target:not-configured" : "content-target:not-found" }).ToArray();
					return null;
				}
				list.Add("content-target:" + controlMatch.Provider + ":" + string.Join(",", controlMatch.Reasons));
			}
			else
			{
				IReadOnlyList<string> readOnlyList = RecognitionContentHints(locator);
				if (readOnlyList != null && readOnlyList.Count > 0)
				{
					ControlTextEvidence controlTextEvidence2 = _context.ControlLocator.FindTextEvidence(window.Hwnd, readOnlyList, locator.MaxNodes ?? 3000, locator.TimeoutMs ?? 1200, !locator.RequireAllNameContains && locator.DocumentContains.Count == 0);
					if (controlTextEvidence2.MatchedHints.Count == 0 || (locator.RequireAllNameContains && controlTextEvidence2.MatchedHints.Count < readOnlyList.Count))
					{
						score = 0;
						evidence = list.Concat(new string[1] { "content:not-found" }).ToArray();
						return null;
					}
					num = controlTextEvidence2.Score;
					list.Add("content:" + string.Join(",", controlTextEvidence2.MatchedHints));
				}
			}
			score = score2 + appControlMatch.Score + num + (controlMatch?.Score ?? 0);
			evidence = list;
			Rect val = controlMatch?.Bounds ?? appControlMatch.Bounds;
			Rect? val2 = GuideSpecHelpers.ResolveControlRelativeRect(locator, val, list);
			if (val2.HasValue)
			{
				Rect valueOrDefault = val2.GetValueOrDefault();
				val = GuideSpecHelpers.Adjust(valueOrDefault, locator.Adjust) ?? valueOrDefault;
			}
			LocatedRect locatedRect = LocatedRect.FromWindow(val, window, appControlMatch.Reason);
			GuideAttachmentState attachment = ((controlMatch != null || GuideSpecHelpers.IsContentTarget(locator)) ? GuideAttachmentState.Content : ((!appControlMatch.UsedFallbackRegion) ? GuideAttachmentState.Control : (HasVerifiedRegionProof(list) ? GuideAttachmentState.VerifiedRegion : GuideAttachmentState.Region)));
			return new GuideRenderPlan
			{
				Layer = GuideLayerKind.Screen,
				BoundsScreen = locatedRect.BoundsScreen,
				BoundsInWindow = locatedRect.BoundsInWindow,
				TargetWindow = window,
				Overlay = overlay,
				Attachment = attachment,
				Score = score,
				Evidence = list,
				CoordinateSpace = locatedRect.CoordinateSpace
			};
		}
		IReadOnlyList<string> readOnlyList2 = RecognitionContentHints(locator);
		bool flag = readOnlyList2.Count > 0 && !GuideSpecHelpers.IsContentTarget(locator);
		if ((!string.IsNullOrWhiteSpace(locator.Adapter) || !string.IsNullOrWhiteSpace(locator.State) || !string.IsNullOrWhiteSpace(locator.Role)) && !flag)
		{
			score = 0;
			evidence = list.Concat(new string[1] { "adapter-control:not-found" }).ToArray();
			return null;
		}
		if (locator.RejectContentContains.Count > 0)
		{
			ControlTextEvidence controlTextEvidence3 = _context.ControlLocator.FindTextEvidence(window.Hwnd, locator.RejectContentContains, locator.MaxNodes ?? 3000, locator.TimeoutMs ?? 1200, stopAfterFirstMatch: true);
			if (controlTextEvidence3.MatchedHints.Count > 0)
			{
				score = 0;
				evidence = list.Concat(new string[1] { "reject-content:" + string.Join(",", controlTextEvidence3.MatchedHints) }).ToArray();
				return null;
			}
		}
		bool flag2 = GuideSpecHelpers.IsContentTarget(locator);
		int num2 = 0;
		if (readOnlyList2.Count > 0 && !flag2)
		{
			ControlTextEvidence controlTextEvidence4 = _context.ControlLocator.FindTextEvidence(window.Hwnd, readOnlyList2, locator.MaxNodes ?? 3000, locator.TimeoutMs ?? 1200, !locator.RequireAllNameContains && locator.DocumentContains.Count == 0);
			if (controlTextEvidence4.MatchedHints.Count == 0 || (locator.RequireAllNameContains && controlTextEvidence4.MatchedHints.Count < readOnlyList2.Count))
			{
				score = 0;
				evidence = list.Concat(new string[1] { "content:not-found" }).ToArray();
				return null;
			}
			num2 = controlTextEvidence4.Score;
			list.Add("content:" + string.Join(",", controlTextEvidence4.MatchedHints));
		}
		if (num2 > 0 && !GuideSpecHelpers.HasControlAnchor(locator))
		{
			score = score2 + num2 + 36;
			list.Add("content:distributed-evidence");
			evidence = list;
			LocatedRect locatedRect2 = LocatedRect.FromWindow(LocatedRect.WindowReferenceBounds(window), window, "content-evidence");
			return new GuideRenderPlan
			{
				Layer = GuideLayerKind.Screen,
				BoundsScreen = locatedRect2.BoundsScreen,
				BoundsInWindow = locatedRect2.BoundsInWindow,
				TargetWindow = window,
				Overlay = overlay,
				Attachment = GuideAttachmentState.VerifiedRegion,
				Score = score,
				Evidence = evidence,
				CoordinateSpace = locatedRect2.CoordinateSpace
			};
		}
		ControlLocatorOptions options = GuideSpecHelpers.ToControlOptions(locator);
		bool flag3 = GuideSpecHelpers.HasControlAnchor(locator) || !flag2;
		ControlMatch controlMatch2 = (flag3 ? _context.ControlLocator.FindChildControlMatch(window.Hwnd, options) : null);
		if (controlMatch2 == null && !locator.FallbackToWindow && !flag2)
		{
			score = 0;
			evidence = list.Concat(new string[1] { "control:not-found" }).ToArray();
			return null;
		}
		if (controlMatch2 == null && !locator.FallbackToWindow && flag2 && flag3)
		{
			score = 0;
			evidence = list.Concat(new string[1] { "control-anchor:not-found" }).ToArray();
			return null;
		}
		if (controlMatch2 == null && GuideSpecHelpers.IsControlRelativeRect(locator.Rect))
		{
			score = 0;
			evidence = list.Concat(new string[1] { "control-anchor:not-found" }).ToArray();
			return null;
		}
		if (flag2)
		{
			ControlMatch controlMatch3 = FindContentTarget(window, locator, controlMatch2?.Bounds);
			if (controlMatch3 == null)
			{
				score = 0;
				evidence = list.Concat(new string[1] { (locator.ContentContains.Count == 0) ? "content-target:not-configured" : "content-target:not-found" }).ToArray();
				return null;
			}
			if (controlMatch2 != null)
			{
				list.Add("control:" + controlMatch2.Provider + ":" + string.Join(",", controlMatch2.Reasons));
			}
			Rect val3 = (Rect)(((_003F?)GuideSpecHelpers.ResolveControlRelativeRect(locator, controlMatch3.Bounds, list)) ?? controlMatch3.Bounds);
			score = score2 + (controlMatch2?.Score ?? 0) + controlMatch3.Score;
			list.Add("content-target:" + controlMatch3.Provider + ":" + string.Join(",", controlMatch3.Reasons));
			evidence = list;
			LocatedRect locatedRect3 = LocatedRect.FromWindow(GuideSpecHelpers.Adjust(val3, locator.Adjust) ?? val3, window, "content-target");
			return new GuideRenderPlan
			{
				Layer = GuideLayerKind.Screen,
				BoundsScreen = locatedRect3.BoundsScreen,
				BoundsInWindow = locatedRect3.BoundsInWindow,
				TargetWindow = window,
				Overlay = overlay,
				Attachment = GuideAttachmentState.Content,
				Score = score,
				Evidence = evidence,
				CoordinateSpace = locatedRect3.CoordinateSpace
			};
		}
		Rect val4 = controlMatch2?.Bounds ?? LocatedRect.WindowReferenceBounds(window);
		val4 = ((controlMatch2 != null) ? (GuideSpecHelpers.ResolveControlRelativeRect(locator, val4, list) ?? val4) : val4);
		score = score2 + (controlMatch2?.Score ?? 18) + num2;
		list.Add((controlMatch2 == null) ? "control:window-fallback" : ("control:" + controlMatch2.Provider + ":" + string.Join(",", controlMatch2.Reasons)));
		evidence = list;
		LocatedRect locatedRect4 = LocatedRect.FromWindow(GuideSpecHelpers.Adjust(val4, locator.Adjust) ?? val4, window, "control");
		return new GuideRenderPlan
		{
			Layer = GuideLayerKind.Screen,
			BoundsScreen = locatedRect4.BoundsScreen,
			BoundsInWindow = locatedRect4.BoundsInWindow,
			TargetWindow = window,
			Overlay = overlay,
			Attachment = ((controlMatch2 == null) ? GuideAttachmentState.Window : GuideAttachmentState.Control),
			Score = score,
			Evidence = evidence,
			CoordinateSpace = locatedRect4.CoordinateSpace
		};
	}

	private ControlMatch? FindContentTarget(WindowInfo window, GuideLocatorSpec locator, Rect? anchorBounds)
	{
		if (locator.ContentContains.Count == 0)
		{
			return null;
		}
		ControlMatch controlMatch = _context.ControlLocator.FindTextControlMatch(window.Hwnd, locator.ContentContains, locator.MaxNodes ?? 3000, locator.TimeoutMs ?? 1200, locator.RequireAllNameContains, GuideSpecHelpers.ControlBackend(locator.Strategy), anchorBounds);
		if (controlMatch != null || !anchorBounds.HasValue)
		{
			return controlMatch;
		}
		ControlMatch controlMatch2 = _context.ControlLocator.FindTextControlMatch(window.Hwnd, locator.ContentContains, locator.MaxNodes ?? 3000, locator.TimeoutMs ?? 1200, locator.RequireAllNameContains, GuideSpecHelpers.ControlBackend(locator.Strategy), null);
		if (controlMatch2 != null)
		{
			return WithReason(controlMatch2, "content-anchor-relaxed");
		}
		return null;
	}

	private static IReadOnlyList<string> RecognitionContentHints(GuideLocatorSpec locator)
	{
		return (from value in locator.DocumentContains.Concat(locator.ContentContains)
			where !string.IsNullOrWhiteSpace(value)
			select value).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private static ControlMatch WithReason(ControlMatch match, string reason)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		ControlMatch controlMatch = new ControlMatch();
		controlMatch.Bounds = match.Bounds;
		controlMatch.Score = match.Score;
		controlMatch.VisitedNodes = match.VisitedNodes;
		controlMatch.Name = match.Name;
		controlMatch.AutomationId = match.AutomationId;
		controlMatch.ClassName = match.ClassName;
		controlMatch.ControlType = match.ControlType;
		controlMatch.Provider = match.Provider;
		controlMatch.MatchedHints = match.MatchedHints;
		controlMatch.Reasons = match.Reasons.Concat(new string[1] { reason }).ToArray();
		return controlMatch;
	}

	public IReadOnlyList<GuideWindowCandidate> FindWindowCandidates(GuideTargetSpec target, GuideLocatorSpec? locator, IReadOnlyList<WindowInfo>? windows = null)
	{
		if (windows == null)
		{
			windows = _context.VisibleWindows();
		}
		List<GuideWindowCandidate> list = new List<GuideWindowCandidate>();
		foreach (WindowInfo window in windows)
		{
			IReadOnlyList<string> evidence;
			int num = ScoreWindow(window, target, locator, out evidence);
			if (num > 0)
			{
				list.Add(new GuideWindowCandidate
				{
					Window = window,
					Score = num,
					Evidence = evidence
				});
			}
		}
		return list.OrderByDescending((GuideWindowCandidate c) => c.Score).ThenByDescending(delegate(GuideWindowCandidate c)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			Rect bounds = c.Window.Bounds;
			double width = ((Rect)(ref bounds)).Width;
			bounds = c.Window.Bounds;
			return width * ((Rect)(ref bounds)).Height;
		}).ToList();
	}

	private GuideRenderPlan ResolveStep(StageStep step, IReadOnlyList<WindowInfo> windows, out int score, out IReadOnlyList<string> evidence, out string scope)
	{
		GuideSpec spec = step.Guide ?? GuideRuntime.BuildLegacyGuide(step);
		return ResolveSpec(spec, windows, out score, out evidence, out scope);
	}

	public GuideRenderPlan ResolveSpec(GuideSpec spec, IReadOnlyList<WindowInfo>? windows, out int score, out IReadOnlyList<string> evidence, out string scope)
	{
		if (windows == null)
		{
			windows = _context.VisibleWindows();
		}
		object obj = ((spec.Locators.Count > 0) ? ((object)spec.Locators) : ((object)new List<GuideLocatorSpec> { GuideSpecHelpers.TargetToLocator(spec.Target) }));
		bool specHasRecognitionLocator = ((IEnumerable<GuideLocatorSpec>)obj).Any(GuideSpecHelpers.HasRecognitionAnchor);
		GuideRenderPlan guideRenderPlan = null;
		int num = 0;
		IReadOnlyList<string> readOnlyList = Array.Empty<string>();
		string text = GuideSpecHelpers.NormalizeScope(spec.Target.Scope);
		GuideRenderPlan guideRenderPlan2 = null;
		int num2 = 0;
		IReadOnlyList<string> readOnlyList2 = Array.Empty<string>();
		string text2 = GuideSpecHelpers.NormalizeScope(spec.Target.Scope);
		GuideRenderPlan guideRenderPlan3 = null;
		int num3 = 0;
		IReadOnlyList<string> readOnlyList3 = Array.Empty<string>();
		string text3 = GuideSpecHelpers.NormalizeScope(spec.Target.Scope);
		IReadOnlyList<string> readOnlyList4 = Array.Empty<string>();
		string text4 = GuideSpecHelpers.NormalizeScope(spec.Target.Scope);
		foreach (GuideLocatorSpec item in (List<GuideLocatorSpec>)obj)
		{
			GuideRenderPlan guideRenderPlan4 = ResolveLocator(item, spec.Target, spec.Overlay, windows, out score, out evidence, out scope);
			readOnlyList4 = evidence;
			text4 = scope;
			if (guideRenderPlan4.Layer == GuideLayerKind.None)
			{
				return guideRenderPlan4;
			}
			if (guideRenderPlan4.Layer == GuideLayerKind.Host && guideRenderPlan4.BoundsScreen.HasValue)
			{
				if (guideRenderPlan3 == null)
				{
					guideRenderPlan3 = guideRenderPlan4;
				}
				num3 = Math.Max(num3, score);
				readOnlyList3 = evidence;
				text3 = scope;
				continue;
			}
			if (guideRenderPlan4.IsPreciseExternal)
			{
				return guideRenderPlan4;
			}
			if (guideRenderPlan4.Attachment == GuideAttachmentState.VerifiedRegion && guideRenderPlan4.IsExternalDisplayable && score > num)
			{
				guideRenderPlan = guideRenderPlan4;
				num = score;
				readOnlyList = evidence;
				text = scope;
			}
			else if (guideRenderPlan4.Attachment == GuideAttachmentState.Window && guideRenderPlan4.IsExternalDisplayable && CanUseWindowPlan(item, specHasRecognitionLocator) && score > num2)
			{
				guideRenderPlan2 = guideRenderPlan4;
				num2 = score;
				readOnlyList2 = evidence;
				text2 = scope;
			}
		}
		if (guideRenderPlan != null)
		{
			score = num;
			evidence = readOnlyList;
			scope = text;
			return guideRenderPlan;
		}
		if (guideRenderPlan2 != null)
		{
			score = num2;
			evidence = readOnlyList2;
			scope = text2;
			return guideRenderPlan2;
		}
		if (guideRenderPlan3 != null)
		{
			score = num3;
			evidence = readOnlyList3;
			scope = text3;
			return guideRenderPlan3;
		}
		score = 0;
		evidence = readOnlyList4;
		scope = text4;
		if (evidence.Count == 0)
		{
			evidence = Array.Empty<string>();
		}
		return new GuideRenderPlan
		{
			Layer = GuideLayerKind.None,
			Overlay = spec.Overlay,
			FailureReason = "guide target not attached",
			Evidence = evidence
		};
	}

	private static bool CanUseWindowPlan(GuideLocatorSpec locator, bool specHasRecognitionLocator)
	{
		if (GuideSpecHelpers.IsWindowStrategy(locator))
		{
			return true;
		}
		if (!specHasRecognitionLocator)
		{
			return !GuideSpecHelpers.HasRecognitionAnchor(locator);
		}
		return false;
	}

	private int ScoreWindow(WindowInfo window, GuideTargetSpec target, GuideLocatorSpec? locator, out IReadOnlyList<string> evidence)
	{
		List<string> list = new List<string>();
		IReadOnlyList<string> readOnlyList = MatchHints(window.Title, locator?.RejectTitleContains);
		if (readOnlyList.Count > 0)
		{
			evidence = readOnlyList.Select((string h) => "reject-title:" + h).ToArray();
			return 0;
		}
		IReadOnlyList<string> hints = Combine(target.TitleContains, locator?.TitleContains);
		IReadOnlyList<string> hints2 = Combine(target.ProcessNameContains, locator?.ProcessNameContains);
		IReadOnlyList<string> hints3 = Combine(target.ExeNameContains, locator?.ExeNameContains);
		IReadOnlyList<string> hints4 = Combine(target.ExePathContains, locator?.ExePathContains);
		IReadOnlyList<string> hints5 = Combine(target.WindowClassNameContains, locator?.WindowClassNameContains);
		if (!MatchesRequiredHints(window.Title, hints) || !MatchesRequiredHints(window.ProcessName, hints2) || !MatchesRequiredHints(window.ExeName, hints3) || !MatchesRequiredHints(window.ExePath, hints4) || !MatchesRequiredHints(window.ClassName, hints5))
		{
			evidence = new string[1] { "window:required-identity-mismatch" };
			return 0;
		}
		int num = 0;
		string text = GuideSpecHelpers.FirstNonEmpty(locator?.Adapter, target.Adapter);
		int num2 = _context.AppLocator.ScoreWindow(window, text, list);
		num += num2;
		num += ScoreHints("title", window.Title, hints, 32, list);
		num += ScoreHints("process", window.ProcessName, hints2, 34, list);
		num += ScoreHints("exe-name", window.ExeName, hints3, 34, list);
		num += ScoreHints("exe-path", window.ExePath, hints4, 24, list);
		num += ScoreHints("window-class", window.ClassName, hints5, 24, list);
		if (num > 0 && window.IsForeground)
		{
			num += 30;
			list.Add("foreground-window");
		}
		else if (num > 0 && window.LastActivePopupHwnd == window.Hwnd)
		{
			num += 12;
			list.Add("last-active-popup");
		}
		if (IsModalState(locator?.State) && IsLikelyModalWindow(window))
		{
			num += 140;
			list.Add("modal-window");
		}
		string key = GuideSpecHelpers.FirstNonEmpty(locator?.Process, target.Process);
		if (TryScoreDeclaredProcessKey(window, key, text, out int score, out IReadOnlyList<string> reasons))
		{
			num += score;
			list.AddRange(reasons);
		}
		string key2 = GuideSpecHelpers.FirstNonEmpty(locator?.TitlePidFromProcess, target.TitlePidFromProcess);
		if (TryScoreTitlePidProcess(window, key2, out int score2, out IReadOnlyList<string> reasons2))
		{
			num += score2;
			list.AddRange(reasons2);
		}
		string text2 = GuideSpecHelpers.FirstNonEmpty(locator?.Key, target.Key);
		if (!string.IsNullOrWhiteSpace(text2) && text2.StartsWith("target:", StringComparison.OrdinalIgnoreCase))
		{
			string text3 = text2;
			if (IsExactTargetProcess(window, text3.Substring(7, text3.Length - 7)))
			{
				string title = window.Title;
				text3 = text2;
				if (title.IndexOf(text3.Substring(7, text3.Length - 7), StringComparison.OrdinalIgnoreCase) >= 0)
				{
					num += 44;
					text3 = text2;
					list.Add("target-title:" + text3.Substring(7, text3.Length - 7));
				}
			}
		}
		evidence = list;
		return num;
	}

	private bool TryScoreTitlePidProcess(WindowInfo window, string? key, out int score, out IReadOnlyList<string> reasons)
	{
		List<string> list = new List<string>();
		score = 0;
		if (string.IsNullOrWhiteSpace(key))
		{
			reasons = list;
			return false;
		}
		if (!TryGetManagedPid(key, out var pid))
		{
			reasons = list;
			return false;
		}
		if (ContainsPidToken(window.Title, pid.ToString()))
		{
			score = 130;
			list.Add($"title-pid:{key}:{pid}");
		}
		reasons = list;
		return score > 0;
	}

	private bool TryGetManagedPid(string key, out int pid)
	{
		pid = 0;
		if (!_context.Processes.TryGetValue(key, out ManagedProcess value) && !key.StartsWith("target:", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(key).Equals(".exe", StringComparison.OrdinalIgnoreCase))
		{
			_context.Processes.TryGetValue("target:" + key, out value);
		}
		if (value == null)
		{
			return false;
		}
		try
		{
			if (value.Process.HasExited)
			{
				return false;
			}
			pid = value.Process.Id;
			return pid > 0;
		}
		catch
		{
			return false;
		}
	}

	private static bool ContainsPidToken(string title, string pid)
	{
		if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(pid))
		{
			return false;
		}
		for (int num = title.IndexOf(pid, StringComparison.Ordinal); num >= 0; num = title.IndexOf(pid, num + pid.Length, StringComparison.Ordinal))
		{
			char c = ((num != 0) ? title[num - 1] : '\0');
			int num2 = num + pid.Length;
			char c2 = ((num2 < title.Length) ? title[num2] : '\0');
			if (!char.IsDigit(c) && !char.IsDigit(c2))
			{
				return true;
			}
		}
		return false;
	}

	private bool TryScoreDeclaredProcessKey(WindowInfo window, string? key, string? adapterAlreadyScored, out int score, out IReadOnlyList<string> reasons)
	{
		List<string> list = new List<string>();
		score = 0;
		if (string.IsNullOrWhiteSpace(key))
		{
			reasons = list;
			return false;
		}
		if (_context.Processes.TryGetValue(key, out ManagedProcess value))
		{
			try
			{
				if (!value.Process.HasExited && value.Process.Id == window.Pid)
				{
					score += 52;
					list.Add("managed-pid:" + key);
				}
			}
			catch
			{
			}
			if (value.TitleHints.Any((string h) => window.Title.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0))
			{
				score += 30;
				list.Add("managed-title:" + key);
			}
			if (value.ProcessHints.Any((string h) => window.ProcessName.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0))
			{
				score += 34;
				list.Add("managed-process:" + key);
			}
		}
		if (key.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
		{
			string text = key;
			string text2 = text.Substring(5, text.Length - 5);
			if (!string.Equals(adapterAlreadyScored, text2, StringComparison.OrdinalIgnoreCase))
			{
				int num = _context.AppLocator.ScoreWindow(window, text2, list);
				if (num > 0)
				{
					score += num;
				}
			}
		}
		else if (key.StartsWith("target:", StringComparison.OrdinalIgnoreCase))
		{
			string text = key;
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text.Substring(7, text.Length - 7));
			if (string.Equals(window.ProcessName, fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
			{
				score += 48;
				list.Add("target-process-exact:" + fileNameWithoutExtension);
			}
			if (string.Equals(window.ExeName, fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
			{
				score += 48;
				list.Add("target-exe-exact:" + fileNameWithoutExtension);
			}
		}
		else
		{
			string fileNameWithoutExtension2 = Path.GetFileNameWithoutExtension(key);
			score += ScoreHints("process-key-title", window.Title, new string[2] { key, fileNameWithoutExtension2 }, 16, list);
			score += ScoreHints("process-key-process", window.ProcessName, new string[1] { fileNameWithoutExtension2 }, 20, list);
			score += ScoreHints("process-key-exe", window.ExeName, new string[1] { fileNameWithoutExtension2 }, 20, list);
		}
		reasons = list;
		return score > 0;
	}

	private static bool IsExactTargetProcess(WindowInfo window, string file)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
		if (!string.IsNullOrWhiteSpace(fileNameWithoutExtension))
		{
			if (!string.Equals(window.ProcessName, fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
			{
				return string.Equals(window.ExeName, fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}

	private Rect? ResolveHost(GuideLocatorSpec locator, GuideTargetSpec target)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		if (locator.Rect != null)
		{
			return GuideSpecHelpers.ResolveExplicitRect(locator, GetHostWindowScreenBounds());
		}
		string text = GuideSpecHelpers.FirstNonEmpty(locator.Key, target.Key);
		if (!string.IsNullOrWhiteSpace(text))
		{
			return TryGetHostBounds(text);
		}
		return TryGetHostBounds("coach");
	}

	private Rect? TryGetHostBounds(string key)
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		if (!_context.HostHotspots.TryGetValue(key, out FrameworkElement value))
		{
			return null;
		}
		try
		{
			if (!value.IsVisible || value.ActualWidth < 1.0 || value.ActualHeight < 1.0)
			{
				return null;
			}
			Point val = value.PointToScreen(new Point(0.0, 0.0));
			Point val2 = value.PointToScreen(new Point(value.ActualWidth, value.ActualHeight));
			return new Rect(((Point)(ref val)).X, ((Point)(ref val)).Y, Math.Max(1.0, ((Point)(ref val2)).X - ((Point)(ref val)).X), Math.Max(1.0, ((Point)(ref val2)).Y - ((Point)(ref val)).Y));
		}
		catch
		{
			return null;
		}
	}

	private Rect GetHostWindowScreenBounds()
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		Point val = _context.HostWindow.PointToScreen(new Point(0.0, 0.0));
		Point val2 = _context.HostWindow.PointToScreen(new Point(_context.HostWindow.ActualWidth, _context.HostWindow.ActualHeight));
		return new Rect(((Point)(ref val)).X, ((Point)(ref val)).Y, Math.Max(1.0, ((Point)(ref val2)).X - ((Point)(ref val)).X), Math.Max(1.0, ((Point)(ref val2)).Y - ((Point)(ref val)).Y));
	}

	private static Rect WindowReferenceBounds(WindowInfo window, GuideRectSpec? rect)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		bool flag;
		switch ((rect?.Space ?? string.Empty).Trim().ToLowerInvariant())
		{
		case "outer-window":
		case "window-frame":
		case "dwm-frame":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return LocatedRect.WindowReferenceBounds(window);
		}
		return window.Bounds;
	}

	private static IReadOnlyList<string> Combine(IReadOnlyList<string>? first, IReadOnlyList<string>? second)
	{
		return (from s in (first ?? Array.Empty<string>()).Concat(second ?? Array.Empty<string>())
			where !string.IsNullOrWhiteSpace(s)
			select s).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private static int ScoreHints(string kind, string value, IEnumerable<string> hints, int weight, ICollection<string> reasons)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return 0;
		}
		int num = 0;
		foreach (string hint in hints)
		{
			if (!string.IsNullOrWhiteSpace(hint) && value.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				num += weight + Math.Min(hint.Length, 18);
				reasons.Add(kind + ":" + hint);
			}
		}
		return num;
	}

	private static IReadOnlyList<string> MatchHints(string value, IEnumerable<string>? hints)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return Array.Empty<string>();
		}
		return (hints ?? Array.Empty<string>()).Where((string h) => !string.IsNullOrWhiteSpace(h) && value.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	private static bool MatchesRequiredHints(string value, IReadOnlyList<string> hints)
	{
		if (hints.Count != 0)
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				return hints.Any((string hint) => !string.IsNullOrWhiteSpace(hint) && value.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0);
			}
			return false;
		}
		return true;
	}

	private static bool IsModalState(string? state)
	{
		if (!string.IsNullOrWhiteSpace(state))
		{
			return state.IndexOf("modal", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return false;
	}

	private static bool IsLikelyModalWindow(WindowInfo window)
	{
		if (!string.Equals(window.ClassName, "#32770", StringComparison.OrdinalIgnoreCase) && !window.Title.Contains("Please confirm", StringComparison.OrdinalIgnoreCase) && !window.Title.Contains("Load a new file", StringComparison.OrdinalIgnoreCase))
		{
			return window.Title.Contains("DWARF info found", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}
}
