using System;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public static class GuideProgressionPolicy
{
	public static GuideProgressPosition SynchronizeToRecognition(GuideSceneMatch scene, int stageStepCount)
	{
		return SynchronizeToRecognition(scene.StageIndex, scene.StepIndex, stageStepCount);
	}

	public static GuideProgressPosition SynchronizeToRecognition(int stageIndex, int stepIndex, int stageStepCount)
	{
		if (stageIndex < 0)
		{
			throw new ArgumentOutOfRangeException("stageIndex", "Recognized stage index must be non-negative.");
		}
		int stepIndex2 = Math.Clamp(stepIndex, 0, Math.Max(0, stageStepCount - 1));
		return new GuideProgressPosition(stageIndex, stepIndex2);
	}
}
