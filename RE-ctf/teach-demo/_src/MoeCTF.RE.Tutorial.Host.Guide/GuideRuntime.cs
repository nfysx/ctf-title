using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MoeCTF.RE.Tutorial.Host.Stages;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed class GuideRuntime
{
	[CompilerGenerated]
	private sealed class _003CHostKeysFor_003Ed__16 : IEnumerable<string>, IEnumerable, IEnumerator<string>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private string _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private StageStep step;

		public StageStep _003C_003E3__step;

		private GuideSpec _003Cspec_003E5__2;

		private IEnumerator<string> _003C_003E7__wrap2;

		private List<GuideLocatorSpec>.Enumerator _003C_003E7__wrap3;

		string IEnumerator<string>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CHostKeysFor_003Ed__16(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			switch (_003C_003E1__state)
			{
			case -3:
			case 1:
				try
				{
				}
				finally
				{
					_003C_003Em__Finally1();
				}
				break;
			case -4:
			case 2:
				try
				{
				}
				finally
				{
					_003C_003Em__Finally2();
				}
				break;
			}
			_003Cspec_003E5__2 = null;
			_003C_003E7__wrap2 = null;
			_003C_003E7__wrap3 = default(List<GuideLocatorSpec>.Enumerator);
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			try
			{
				switch (_003C_003E1__state)
				{
				default:
					return false;
				case 0:
					_003C_003E1__state = -1;
					_003Cspec_003E5__2 = step.Guide ?? BuildLegacyGuide(step);
					_003C_003E7__wrap2 = HostKeysFor(_003Cspec_003E5__2.Target).GetEnumerator();
					_003C_003E1__state = -3;
					goto IL_0095;
				case 1:
					_003C_003E1__state = -3;
					goto IL_0095;
				case 2:
					{
						_003C_003E1__state = -4;
						break;
					}
					IL_0095:
					if (_003C_003E7__wrap2.MoveNext())
					{
						string current = _003C_003E7__wrap2.Current;
						_003C_003E2__current = current;
						_003C_003E1__state = 1;
						return true;
					}
					_003C_003Em__Finally1();
					_003C_003E7__wrap2 = null;
					_003C_003E7__wrap3 = _003Cspec_003E5__2.Locators.GetEnumerator();
					_003C_003E1__state = -4;
					break;
				}
				while (_003C_003E7__wrap3.MoveNext())
				{
					GuideLocatorSpec current2 = _003C_003E7__wrap3.Current;
					if (string.Equals(GuideSpecHelpers.NormalizeScope(current2.Scope ?? _003Cspec_003E5__2.Target.Scope), "host", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(current2.Key))
					{
						_003C_003E2__current = current2.Key;
						_003C_003E1__state = 2;
						return true;
					}
				}
				_003C_003Em__Finally2();
				_003C_003E7__wrap3 = default(List<GuideLocatorSpec>.Enumerator);
				return false;
			}
			catch
			{
				//try-fault
				((IDisposable)this).Dispose();
				throw;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		private void _003C_003Em__Finally1()
		{
			_003C_003E1__state = -1;
			if (_003C_003E7__wrap2 != null)
			{
				_003C_003E7__wrap2.Dispose();
			}
		}

		private void _003C_003Em__Finally2()
		{
			_003C_003E1__state = -1;
			((IDisposable)_003C_003E7__wrap3/*cast due to .constrained prefix*/).Dispose();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<string> IEnumerable<string>.GetEnumerator()
		{
			_003CHostKeysFor_003Ed__16 _003CHostKeysFor_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CHostKeysFor_003Ed__ = this;
			}
			else
			{
				_003CHostKeysFor_003Ed__ = new _003CHostKeysFor_003Ed__16(0);
			}
			_003CHostKeysFor_003Ed__.step = _003C_003E3__step;
			return _003CHostKeysFor_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<string>)this).GetEnumerator();
		}
	}

	[CompilerGenerated]
	private sealed class _003CHostKeysFor_003Ed__22 : IEnumerable<string>, IEnumerable, IEnumerator<string>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private string _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private GuideTargetSpec target;

		public GuideTargetSpec _003C_003E3__target;

		string IEnumerator<string>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CHostKeysFor_003Ed__22(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			switch (_003C_003E1__state)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				if (string.Equals(GuideSpecHelpers.NormalizeScope(target.Scope), "host", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(target.Key))
				{
					_003C_003E2__current = target.Key;
					_003C_003E1__state = 1;
					return true;
				}
				break;
			case 1:
				_003C_003E1__state = -1;
				break;
			}
			return false;
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<string> IEnumerable<string>.GetEnumerator()
		{
			_003CHostKeysFor_003Ed__22 _003CHostKeysFor_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CHostKeysFor_003Ed__ = this;
			}
			else
			{
				_003CHostKeysFor_003Ed__ = new _003CHostKeysFor_003Ed__22(0);
			}
			_003CHostKeysFor_003Ed__.target = _003C_003E3__target;
			return _003CHostKeysFor_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<string>)this).GetEnumerator();
		}
	}

	private readonly GuideRuntimeContext _context;

	private readonly GuideSceneMatcher _matcher;

	private readonly GuideRecognitionEngine _recognizer;

	public GuideRuntime(GuideRuntimeContext context)
	{
		_context = context;
		_matcher = new GuideSceneMatcher(context);
		_recognizer = new GuideRecognitionEngine(context, _matcher);
	}

	public GuideRenderPlan Resolve(StageStep step)
	{
		return Resolve(step.Guide ?? BuildLegacyGuide(step));
	}

	public IReadOnlyList<GuideSceneMatch> MatchStage(StageDefinition stage)
	{
		return _matcher.MatchStage(stage);
	}

	public GuideSceneMatch? BestExternalMatch(StageDefinition stage, int minScore = 90)
	{
		return _matcher.BestExternalMatch(stage, minScore);
	}

	public GuideSceneMatch? BestExternalStageMatch(StageDefinition stage, int minScore = 90)
	{
		return _matcher.BestExternalStageMatch(stage, minScore);
	}

	public GuideOverlayMatch? LocateExternalOverlay(StageDefinition stage, int stageIndex, int stepIndex, int minScore = 30, CancellationToken cancellationToken = default(CancellationToken))
	{
		return _matcher.LocateExternalOverlay(stage, stageIndex, stepIndex, minScore, cancellationToken);
	}

	public GuideRecognitionMatch? RecognizeCourse(IReadOnlyList<StageDefinition> stages, int minScore = 140, CancellationToken cancellationToken = default(CancellationToken))
	{
		return _recognizer.Recognize(stages, minScore, cancellationToken);
	}

	public Task<GuideOverlayMatch?> LocateExternalOverlayAsync(StageDefinition stage, int stageIndex, int stepIndex, int minScore = 30, TimeSpan? timeout = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		return RunMatcherAsync((CancellationToken token) => _matcher.LocateExternalOverlay(stage, stageIndex, stepIndex, minScore, token), timeout, cancellationToken, null);
	}

	public Task<GuideRecognitionMatch?> RecognizeCourseAsync(IReadOnlyList<StageDefinition> stages, int minScore = 140, TimeSpan? timeout = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		return RunMatcherAsync((CancellationToken token) => _recognizer.Recognize(stages, minScore, token), timeout, cancellationToken, null);
	}

	public GuideRenderPlan Resolve(GuideSpec spec)
	{
		int score;
		IReadOnlyList<string> evidence;
		string scope;
		GuideRenderPlan guideRenderPlan = _matcher.ResolveSpec(spec, null, out score, out evidence, out scope);
		if (guideRenderPlan.Layer == GuideLayerKind.Host || guideRenderPlan.IsExternalDisplayable || (guideRenderPlan.Layer == GuideLayerKind.None && guideRenderPlan.FailureReason == null))
		{
			return guideRenderPlan;
		}
		if (spec.Fallback != null)
		{
			return Resolve(new GuideSpec
			{
				Target = spec.Fallback,
				Overlay = spec.Overlay
			});
		}
		return new GuideRenderPlan
		{
			Layer = GuideLayerKind.Host,
			BoundsScreen = TryGetHostBounds("coach"),
			Overlay = spec.Overlay,
			FailureReason = "guide target not found"
		};
	}

	public int? ResolveBranchIndex(StageDefinition stage, StageStep step, string trigger)
	{
		foreach (GuideBranchSpec branch in step.Branches)
		{
			if (TriggerMatches(branch.Trigger, trigger) && Evaluate(branch.When))
			{
				if (string.IsNullOrWhiteSpace(branch.Next))
				{
					return null;
				}
				int num = stage.Steps.FindIndex((StageStep s) => string.Equals(s.Id, branch.Next, StringComparison.OrdinalIgnoreCase));
				return (num >= 0) ? new int?(num) : ((int?)null);
			}
		}
		return null;
	}

	public GuideDeviationSpec? ResolveDeviation(StageDefinition stage)
	{
		return (from d in stage.Deviations
			where Evaluate(d.When)
			orderby d.Priority descending
			select d).FirstOrDefault();
	}

	public static GuideSpec BuildLegacyGuide(StageStep step)
	{
		string scope = (string.IsNullOrWhiteSpace(step.HighlightScope) ? "host" : step.HighlightScope);
		GuideTargetSpec guideTargetSpec = new GuideTargetSpec
		{
			Scope = GuideSpecHelpers.NormalizeScope(scope)
		};
		GuideLocatorSpec guideLocatorSpec = new GuideLocatorSpec
		{
			Scope = guideTargetSpec.Scope,
			Strategy = "auto"
		};
		if (guideTargetSpec.Scope == "host")
		{
			guideLocatorSpec.Key = step.HighlightHost;
			if (string.IsNullOrWhiteSpace(guideLocatorSpec.Key))
			{
				if (step.Action == "launch_tool" && step.Tool != null)
				{
					guideLocatorSpec.Key = "tool:" + step.Tool;
				}
				else if (step.Action == "open_in_tool")
				{
					guideLocatorSpec.Key = "action";
				}
				else if (step.Action == "launch_target" && step.File != null)
				{
					guideLocatorSpec.Key = "target:" + step.File;
				}
				else
				{
					guideLocatorSpec.Key = "coach";
				}
			}
			guideTargetSpec.Key = guideLocatorSpec.Key;
		}
		else if (guideTargetSpec.Scope == "process")
		{
			guideLocatorSpec.Process = step.HighlightProcess;
			if (string.IsNullOrWhiteSpace(guideLocatorSpec.Process) && step.Tool != null)
			{
				guideLocatorSpec.Process = "tool:" + step.Tool;
			}
			if (string.IsNullOrWhiteSpace(guideLocatorSpec.Process) && step.File != null)
			{
				guideLocatorSpec.Process = "target:" + step.File;
			}
			guideLocatorSpec.NameContains = step.HighlightNames;
			guideLocatorSpec.ControlType = step.HighlightControlType;
			guideLocatorSpec.FallbackToWindow = true;
			guideTargetSpec.Process = guideLocatorSpec.Process;
		}
		return new GuideSpec
		{
			Target = guideTargetSpec,
			Locators = new List<GuideLocatorSpec> { guideLocatorSpec },
			Overlay = new GuideOverlaySpec
			{
				Placement = "auto",
				Padding = 6.0
			}
		};
	}

	[IteratorStateMachine(typeof(_003CHostKeysFor_003Ed__16))]
	public static IEnumerable<string> HostKeysFor(StageStep step)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CHostKeysFor_003Ed__16(-2)
		{
			_003C_003E3__step = step
		};
	}

	public bool Evaluate(GuideConditionSpec condition)
	{
		return _matcher.EvaluateCondition(condition).Matched;
	}

	public Task<GuideConditionEvaluation> EvaluateAsync(GuideConditionSpec condition, TimeSpan? timeout = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		return RunMatcherAsync((CancellationToken token) => _matcher.EvaluateCondition(condition, null, token), timeout, cancellationToken, new GuideConditionEvaluation
		{
			Evidence = new string[1] { "condition:evaluation-timeout" }
		});
	}

	private static async Task<T> RunMatcherAsync<T>(Func<CancellationToken, T> operation, TimeSpan? timeout, CancellationToken cancellationToken, T timeoutValue)
	{
		CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		try
		{
			timeoutSource.CancelAfter(timeout ?? TimeSpan.FromSeconds(2.0));
			Task<T> task = Task.Run(() => operation(timeoutSource.Token), CancellationToken.None);
			try
			{
				return await task.WaitAsync(timeoutSource.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				return timeoutValue;
			}
		}
		finally
		{
			if (timeoutSource != null)
			{
				((IDisposable)timeoutSource).Dispose();
			}
		}
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

	private static bool TriggerMatches(string branchTrigger, string trigger)
	{
		if (!string.Equals(branchTrigger, trigger, StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(branchTrigger, "any", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	[IteratorStateMachine(typeof(_003CHostKeysFor_003Ed__22))]
	private static IEnumerable<string> HostKeysFor(GuideTargetSpec target)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CHostKeysFor_003Ed__22(-2)
		{
			_003C_003E3__target = target
		};
	}
}
