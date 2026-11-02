using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MoeCTF.RE.Tutorial.Host.Stages;

public sealed class StageRepository
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	public IReadOnlyList<StageDefinition> Stages { get; }

	public StageRepository(string stagesDir)
	{
		List<string> list = new List<string>();
		string path = Path.Combine(stagesDir, "index.json");
		if (File.Exists(path))
		{
			StageIndex stageIndex = JsonSerializer.Deserialize<StageIndex>(File.ReadAllText(path), Options);
			List<string> list2 = stageIndex?.Stages;
			if (list2 != null && list2.Count > 0)
			{
				list.AddRange(stageIndex.Stages);
			}
		}
		List<StageDefinition> list3 = new List<StageDefinition>();
		foreach (string item in list)
		{
			string path2 = Path.Combine(stagesDir, item + ".json");
			if (File.Exists(path2))
			{
				StageDefinition stageDefinition = JsonSerializer.Deserialize<StageDefinition>(File.ReadAllText(path2), Options);
				if (stageDefinition != null)
				{
					list3.Add(stageDefinition);
				}
			}
		}
		Stages = list3.OrderBy((StageDefinition s) => s.Order).ToList();
	}
}
