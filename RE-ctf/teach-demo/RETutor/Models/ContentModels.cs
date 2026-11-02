using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RETutor.Models;

// ---------- 课程目录（courses/index.json） ----------
public sealed class CourseIndex
{
    [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = "1";
    [JsonPropertyName("catalogVersion")] public string CatalogVersion { get; set; } = "";
    [JsonPropertyName("courses")] public List<string> Courses { get; set; } = new();
}

public sealed class CourseMetadata
{
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("subtitle")] public string Subtitle { get; set; } = "";
    [JsonPropertyName("summary")] public string Summary { get; set; } = "";
    [JsonPropertyName("track")] public string Track { get; set; } = "";
    [JsonPropertyName("difficulty")] public string Difficulty { get; set; } = "";
    [JsonPropertyName("estimatedMinutes")] public int EstimatedMinutes { get; set; }
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = new();
    [JsonPropertyName("authors")] public List<string> Authors { get; set; } = new();
}

public sealed class CourseCompatibility
{
    [JsonPropertyName("minHostVersion")] public string MinHostVersion { get; set; }
    [JsonPropertyName("maxHostVersionExclusive")] public string MaxHostVersionExclusive { get; set; }
}

public sealed class CourseContent
{
    [JsonPropertyName("stagesDir")] public string StagesDir { get; set; } = "";
    [JsonPropertyName("i18nDir")] public string I18nDir { get; set; } = "";
    [JsonPropertyName("assetsDir")] public string AssetsDir { get; set; } = "";
    [JsonPropertyName("stageIndex")] public string StageIndex { get; set; } = "";
}

public sealed class RequirementMatch
{
    [JsonPropertyName("fileNames")] public List<string> FileNames { get; set; } = new();
    [JsonPropertyName("pathContains")] public List<string> PathContains { get; set; } = new();
    [JsonPropertyName("sha256")] public string Sha256 { get; set; }
    [JsonPropertyName("size")] public long? Size { get; set; }
    [JsonPropertyName("recursive")] public bool Recursive { get; set; }
}

public sealed class RequirementAsset
{
    [JsonPropertyName("path")] public string Path { get; set; }
    [JsonPropertyName("copyMode")] public string CopyMode { get; set; } = "copy";
}

public sealed class RequirementNode
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; }
    [JsonPropertyName("targetId")] public string TargetId { get; set; }
    [JsonPropertyName("toolId")] public string ToolId { get; set; }
    [JsonPropertyName("match")] public RequirementMatch Match { get; set; }
    [JsonPropertyName("asset")] public RequirementAsset Asset { get; set; }
    [JsonPropertyName("allOf")] public List<RequirementNode> AllOf { get; set; }
    [JsonPropertyName("anyOf")] public List<RequirementNode> AnyOf { get; set; }
}

public sealed class RequirementRoot
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; }
    [JsonPropertyName("allOf")] public List<RequirementNode> AllOf { get; set; }
    [JsonPropertyName("anyOf")] public List<RequirementNode> AnyOf { get; set; }
}

public sealed class CourseFlowStage
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("requires")] public List<string> Requires { get; set; } = new();
    [JsonPropertyName("next")] public List<FlowEdge> Next { get; set; } = new();
}

public sealed class FlowEdge
{
    [JsonPropertyName("to")] public string To { get; set; } = "";
    [JsonPropertyName("when")] public GuideConditionSpec When { get; set; }
}

public sealed class CourseFlow
{
    [JsonPropertyName("entry")] public string Entry { get; set; } = "";
    [JsonPropertyName("stages")] public List<CourseFlowStage> Stages { get; set; } = new();
}

public sealed class CourseManifest
{
    [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = "1";
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("compatibility")] public CourseCompatibility Compatibility { get; set; } = new();
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("metadata")] public CourseMetadata Metadata { get; set; } = new();
    [JsonPropertyName("content")] public CourseContent Content { get; set; } = new();
    [JsonPropertyName("updateManifestUrl")] public string UpdateManifestUrl { get; set; }
    [JsonPropertyName("appUpdateManifestUrl")] public string AppUpdateManifestUrl { get; set; }
    [JsonPropertyName("changelog")] public List<CourseChangelog> Changelog { get; set; } = new();
    [JsonPropertyName("requirements")] public RequirementRoot Requirements { get; set; }
    [JsonPropertyName("flow")] public CourseFlow Flow { get; set; } = new();
}

public sealed class CourseChangelog
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("date")] public string Date { get; set; } = "";
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("items")] public List<string> Items { get; set; } = new();
}

// ---------- 关卡 / 步骤 ----------
public sealed class StageIndexFile
{
    [JsonPropertyName("stages")] public List<string> Stages { get; set; } = new();
}

public sealed class StageDefinition
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("order")] public int Order { get; set; }
    [JsonPropertyName("titleKey")] public string TitleKey { get; set; } = "";
    [JsonPropertyName("goalKey")] public string GoalKey { get; set; } = "";
    [JsonPropertyName("answerKey")] public string AnswerKey { get; set; } = "";
    [JsonPropertyName("completionMode")] public string CompletionMode { get; set; } = "answer";
    [JsonPropertyName("fakeAnswerKeys")] public List<string> FakeAnswerKeys { get; set; } = new();
    [JsonPropertyName("targets")] public List<string> Targets { get; set; } = new();
    [JsonPropertyName("tools")] public List<string> Tools { get; set; } = new();
    [JsonPropertyName("guideMode")] public string GuideMode { get; set; } = "forced";
    [JsonPropertyName("freeExploreTextKey")] public string FreeExploreTextKey { get; set; }
    [JsonPropertyName("deviations")] public List<GuideDeviationSpec> Deviations { get; set; } = new();
    [JsonPropertyName("steps")] public List<StageStep> Steps { get; set; } = new();
}

public sealed class StageStep
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("textKey")] public string TextKey { get; set; } = "";
    [JsonPropertyName("bodyKey")] public string BodyKey { get; set; }
    [JsonPropertyName("image")] public string Image { get; set; }
    [JsonPropertyName("action")] public string Action { get; set; } = "none";
    [JsonPropertyName("tool")] public string Tool { get; set; }
    [JsonPropertyName("file")] public string File { get; set; }
    [JsonPropertyName("guide")] public GuideSpec Guide { get; set; }
    [JsonPropertyName("trigger")] public GuideConditionSpec Trigger { get; set; }
    [JsonPropertyName("branches")] public List<GuideBranchSpec> Branches { get; set; } = new();
    [JsonPropertyName("highlightScope")] public string HighlightScope { get; set; }
    [JsonPropertyName("highlightHost")] public string HighlightHost { get; set; }
    [JsonPropertyName("highlightProcess")] public string HighlightProcess { get; set; }
    [JsonPropertyName("highlightNames")] public List<string> HighlightNames { get; set; } = new();
    [JsonPropertyName("highlightControlType")] public string HighlightControlType { get; set; }
}

// ---------- Guide 契约（与 schemas/guide-stage.schema.json 一一对应） ----------
public sealed class GuideSpec
{
    [JsonPropertyName("target")] public GuideTargetSpec Target { get; set; } = new();
    [JsonPropertyName("locators")] public List<GuideLocatorSpec> Locators { get; set; } = new();
    [JsonPropertyName("overlay")] public GuideOverlaySpec Overlay { get; set; } = new();
    [JsonPropertyName("fallback")] public GuideTargetSpec Fallback { get; set; }
}

public sealed class GuideTargetSpec
{
    [JsonPropertyName("scope")] public string Scope { get; set; } = "host";
    [JsonPropertyName("key")] public string Key { get; set; }
    [JsonPropertyName("process")] public string Process { get; set; }
    [JsonPropertyName("adapter")] public string Adapter { get; set; }
    [JsonPropertyName("titleContains")] public List<string> TitleContains { get; set; } = new();
    [JsonPropertyName("processNameContains")] public List<string> ProcessNameContains { get; set; } = new();
    [JsonPropertyName("exeNameContains")] public List<string> ExeNameContains { get; set; } = new();
    [JsonPropertyName("exePathContains")] public List<string> ExePathContains { get; set; } = new();
    [JsonPropertyName("windowClassNameContains")] public List<string> WindowClassNameContains { get; set; } = new();
    [JsonPropertyName("titlePidFromProcess")] public string TitlePidFromProcess { get; set; }
}

public sealed class GuideLocatorSpec
{
    [JsonPropertyName("strategy")] public string Strategy { get; set; } = "auto";
    [JsonPropertyName("scope")] public string Scope { get; set; }
    [JsonPropertyName("key")] public string Key { get; set; }
    [JsonPropertyName("process")] public string Process { get; set; }
    [JsonPropertyName("adapter")] public string Adapter { get; set; }
    [JsonPropertyName("state")] public string State { get; set; }
    [JsonPropertyName("role")] public string Role { get; set; }
    [JsonPropertyName("nameEquals")] public string NameEquals { get; set; }
    [JsonPropertyName("nameContains")] public List<string> NameContains { get; set; } = new();
    [JsonPropertyName("contentContains")] public List<string> ContentContains { get; set; } = new();
    [JsonPropertyName("documentContains")] public List<string> DocumentContains { get; set; } = new();
    [JsonPropertyName("contentMode")] public string ContentMode { get; set; } = "evidence";
    [JsonPropertyName("rejectTitleContains")] public List<string> RejectTitleContains { get; set; } = new();
    [JsonPropertyName("rejectContentContains")] public List<string> RejectContentContains { get; set; } = new();
    [JsonPropertyName("requireAllNameContains")] public bool RequireAllNameContains { get; set; }
    [JsonPropertyName("automationId")] public string AutomationId { get; set; }
    [JsonPropertyName("className")] public string ClassName { get; set; }
    [JsonPropertyName("controlType")] public string ControlType { get; set; }
    [JsonPropertyName("titleContains")] public List<string> TitleContains { get; set; } = new();
    [JsonPropertyName("processNameContains")] public List<string> ProcessNameContains { get; set; } = new();
    [JsonPropertyName("exeNameContains")] public List<string> ExeNameContains { get; set; } = new();
    [JsonPropertyName("exePathContains")] public List<string> ExePathContains { get; set; } = new();
    [JsonPropertyName("windowClassNameContains")] public List<string> WindowClassNameContains { get; set; } = new();
    [JsonPropertyName("titlePidFromProcess")] public string TitlePidFromProcess { get; set; }
    [JsonPropertyName("rect")] public GuideRectSpec Rect { get; set; }
    [JsonPropertyName("adjust")] public GuideRectAdjustSpec Adjust { get; set; }
    [JsonPropertyName("fallbackToWindow")] public bool FallbackToWindow { get; set; }
    [JsonPropertyName("maxNodes")] public int? MaxNodes { get; set; }
    [JsonPropertyName("timeoutMs")] public int? TimeoutMs { get; set; }
}

public sealed class GuideRectSpec
{
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("width")] public double Width { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }
    [JsonPropertyName("space")] public string Space { get; set; } = "screen";
}

public sealed class GuideRectAdjustSpec
{
    [JsonPropertyName("offsetX")] public double OffsetX { get; set; }
    [JsonPropertyName("offsetY")] public double OffsetY { get; set; }
    [JsonPropertyName("inflate")] public double Inflate { get; set; }
    [JsonPropertyName("width")] public double? Width { get; set; }
    [JsonPropertyName("height")] public double? Height { get; set; }
}

public sealed class GuideOverlaySpec
{
    [JsonPropertyName("placement")] public string Placement { get; set; } = "auto";
    [JsonPropertyName("alignment")] public string Alignment { get; set; } = "center";
    [JsonPropertyName("padding")] public double Padding { get; set; } = 6;
    [JsonPropertyName("gap")] public double Gap { get; set; } = 14;
    [JsonPropertyName("offsetX")] public double OffsetX { get; set; }
    [JsonPropertyName("offsetY")] public double OffsetY { get; set; }
    [JsonPropertyName("maxWidth")] public double? MaxWidth { get; set; }
}

public sealed class GuideConditionSpec
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = "always";
    [JsonPropertyName("not")] public bool Not { get; set; }
    [JsonPropertyName("process")] public string Process { get; set; }
    [JsonPropertyName("key")] public string Key { get; set; }
    [JsonPropertyName("value")] public string Value { get; set; }
    [JsonPropertyName("valueKey")] public string ValueKey { get; set; }
    [JsonPropertyName("locator")] public GuideLocatorSpec Locator { get; set; }
    [JsonPropertyName("all")] public List<GuideConditionSpec> All { get; set; }
    [JsonPropertyName("any")] public List<GuideConditionSpec> Any { get; set; }
}

public sealed class GuideBranchSpec
{
    [JsonPropertyName("trigger")] public string Trigger { get; set; } = "next";
    [JsonPropertyName("when")] public GuideConditionSpec When { get; set; }
    [JsonPropertyName("next")] public string Next { get; set; }
}

public sealed class GuideDeviationSpec
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("textKey")] public string TextKey { get; set; } = "";
    [JsonPropertyName("bodyKey")] public string BodyKey { get; set; }
    [JsonPropertyName("image")] public string Image { get; set; }
    [JsonPropertyName("priority")] public int Priority { get; set; }
    [JsonPropertyName("when")] public GuideConditionSpec When { get; set; }
    [JsonPropertyName("guide")] public GuideSpec Guide { get; set; }
}
