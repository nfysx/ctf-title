using System.Collections.Generic;

namespace MoeCTF.RE.Tutorial.Host.Guide;

public sealed record GuideRecognitionMatch(GuideSceneMatch Scene, int Confidence, IReadOnlyList<string> Evidence);
