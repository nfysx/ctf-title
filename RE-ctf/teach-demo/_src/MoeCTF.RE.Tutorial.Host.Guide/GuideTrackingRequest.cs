using System.Collections.Generic;
using MoeCTF.RE.Tutorial.Host.Stages;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed record GuideTrackingRequest(IReadOnlyList<StageDefinition> Stages, StageDefinition CurrentStage, int StageIndex, int StepIndex, long Revision);
