#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LxyDemo.UIFramework.Editor
{
    [Serializable]
    public sealed class UIEffectSchema
    {
        public string version = "2.0";
        public string name = "UIExample";
        public float designWidth = 1080f;
        public float designHeight = 1920f;
        public string referenceImage = string.Empty;
        public List<UIEffectNode> children =
            new List<UIEffectNode>();
    }

    [Serializable]
    public sealed class UIEffectNode
    {
        public string name = "Node";
        public string type = "Container";
        public string semantic = string.Empty;
        public string anchor = "Auto";
        public float x;
        public float y;
        public float width = 100f;
        public float height = 100f;
        public string text = string.Empty;
        public float fontSize = 32f;
        public string alignment = "Center";
        public bool bold;
        public string color = string.Empty;
        public string resource = string.Empty;
        public List<string> resourceCandidates =
            new List<string>();
        public bool intentionalColor;
        public bool preserveAspect;
        public bool sliced;
        public bool raycastTarget;
        public string scrollDirection = "Vertical";
        public string binding = "Auto";
        public int repeatCount = 1;
        public float repeatOffsetX;
        public float repeatOffsetY;
        public int repeatNameDigits = 2;
        public List<UIEffectNodeVariant> variants =
            new List<UIEffectNodeVariant>();
        public List<UIEffectNode> children =
            new List<UIEffectNode>();
    }

    [Serializable]
    public sealed class UIEffectNodeVariant
    {
        public int index;
        public string text;
        public string color;
        public string semantic;
        public string resource;
        public List<string> resourceCandidates;
    }

    public static class UIEffectSchemaUtility
    {
        private static readonly HashSet<string> SupportedTypes =
            new HashSet<string>(
                new[]
                {
                    "Container",
                    "Image",
                    "Text",
                    "Button",
                    "ScrollRect",
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> SupportedAnchors =
            new HashSet<string>(
                new[]
                {
                    "Auto",
                    "TopLeft",
                    "TopCenter",
                    "TopRight",
                    "MiddleLeft",
                    "MiddleCenter",
                    "MiddleRight",
                    "BottomLeft",
                    "BottomCenter",
                    "BottomRight",
                    "StretchHorizontal",
                    "StretchVertical",
                    "StretchAll",
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> SupportedScrollDirections =
            new HashSet<string>(
                new[]
                {
                    "Vertical",
                    "Horizontal",
                    "Both",
                },
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> SupportedBindings =
            new HashSet<string>(
                new[]
                {
                    "Auto",
                    "Yes",
                    "No",
                },
                StringComparer.OrdinalIgnoreCase);

        public static UIEffectSchema Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException(
                    "UISchema JSON 不能为空。");
            }

            UIEffectSchema schema =
                JsonUtility.FromJson<UIEffectSchema>(json);
            if (schema == null)
            {
                throw new InvalidOperationException(
                    "无法解析 UISchema JSON。");
            }

            Validate(schema);
            return schema;
        }

        public static void Validate(UIEffectSchema schema)
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            schema.name = schema.name?.Trim() ?? string.Empty;
            schema.version = schema.version?.Trim() ?? string.Empty;
            schema.referenceImage =
                schema.referenceImage?.Trim() ?? string.Empty;
            schema.children ??= new List<UIEffectNode>();

            if (schema.version != "1.0" && schema.version != "2.0")
            {
                throw new InvalidOperationException(
                    $"不支持的 UISchema 版本：{schema.version}。");
            }

            if (schema.name.Length == 0)
            {
                throw new InvalidOperationException(
                    "UISchema.name 不能为空。");
            }

            if (schema.designWidth <= 0f ||
                schema.designHeight <= 0f)
            {
                throw new InvalidOperationException(
                    "UISchema 的设计宽高必须大于 0。");
            }

            var paths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0;
                 index < schema.children.Count;
                 index++)
            {
                ValidateNode(
                    schema.children[index],
                    schema.name,
                    paths,
                    schema.version == "1.0");
            }
        }

        public static UIEffectSchema ExpandRepeats(UIEffectSchema source)
        {
            Validate(source);
            var expanded = new UIEffectSchema
            {
                version = source.version,
                name = source.name,
                designWidth = source.designWidth,
                designHeight = source.designHeight,
                referenceImage = source.referenceImage,
                children = new List<UIEffectNode>(),
            };

            int expandedNodeCount = 0;
            foreach (UIEffectNode node in source.children)
            {
                ExpandNode(
                    node,
                    string.Empty,
                    0,
                    expanded.children,
                    ref expandedNodeCount);
            }

            Validate(expanded);
            return expanded;
        }

        public static int OptimizeRepeatedNodes(UIEffectSchema schema)
        {
            Validate(schema);
            int collapsedNodeCount = OptimizeChildren(schema.children);
            schema.version = "2.0";
            Validate(schema);
            return collapsedNodeCount;
        }

        public static string ToCompactJson(UIEffectSchema schema)
        {
            Validate(schema);
            var writer = new CompactJsonWriter();
            writer.WriteSchema(schema);
            return writer.ToString();
        }

        private static void ExpandNode(
            UIEffectNode source,
            string inheritedSuffix,
            int inheritedVariantIndex,
            List<UIEffectNode> destination,
            ref int expandedNodeCount)
        {
            int repeatCount = Mathf.Max(1, source.repeatCount);
            for (int repeatIndex = 0;
                 repeatIndex < repeatCount;
                 repeatIndex++)
            {
                string suffix = inheritedSuffix;
                int variantIndex = inheritedVariantIndex;
                if (repeatCount > 1)
                {
                    suffix += (repeatIndex + 1).ToString(
                        new string('0', source.repeatNameDigits),
                        CultureInfo.InvariantCulture);
                    variantIndex = repeatIndex + 1;
                }

                UIEffectNode node = CloneNode(source);
                node.name += suffix;
                node.x += source.repeatOffsetX * repeatIndex;
                node.y += source.repeatOffsetY * repeatIndex;
                node.repeatCount = 1;
                node.repeatOffsetX = 0f;
                node.repeatOffsetY = 0f;
                node.variants.Clear();
                node.children.Clear();
                ApplyVariant(node, source.variants, variantIndex);

                expandedNodeCount++;
                if (expandedNodeCount > 5000)
                {
                    throw new InvalidOperationException(
                        "UISchema 展开后的节点数不能超过 5000。");
                }

                foreach (UIEffectNode child in source.children)
                {
                    ExpandNode(
                        child,
                        suffix,
                        variantIndex,
                        node.children,
                        ref expandedNodeCount);
                }

                destination.Add(node);
            }
        }

        private static UIEffectNode CloneNode(UIEffectNode source)
        {
            return new UIEffectNode
            {
                name = source.name,
                type = source.type,
                semantic = source.semantic,
                anchor = source.anchor,
                x = source.x,
                y = source.y,
                width = source.width,
                height = source.height,
                text = source.text,
                fontSize = source.fontSize,
                alignment = source.alignment,
                bold = source.bold,
                color = source.color,
                resource = source.resource,
                resourceCandidates = new List<string>(
                    source.resourceCandidates ?? new List<string>()),
                intentionalColor = source.intentionalColor,
                preserveAspect = source.preserveAspect,
                sliced = source.sliced,
                raycastTarget = source.raycastTarget,
                scrollDirection = source.scrollDirection,
                binding = source.binding,
                repeatCount = source.repeatCount,
                repeatOffsetX = source.repeatOffsetX,
                repeatOffsetY = source.repeatOffsetY,
                repeatNameDigits = source.repeatNameDigits,
                variants = new List<UIEffectNodeVariant>(
                    source.variants ?? new List<UIEffectNodeVariant>()),
                children = new List<UIEffectNode>(
                    source.children ?? new List<UIEffectNode>()),
            };
        }

        private static void ApplyVariant(
            UIEffectNode node,
            List<UIEffectNodeVariant> variants,
            int variantIndex)
        {
            if (variantIndex <= 0 || variants == null)
            {
                return;
            }

            UIEffectNodeVariant variant = variants.Find(
                item => item != null && item.index == variantIndex);
            if (variant == null)
            {
                return;
            }

            if (variant.text != null)
            {
                node.text = variant.text;
            }

            if (variant.color != null)
            {
                node.color = variant.color;
            }

            if (variant.semantic != null)
            {
                node.semantic = variant.semantic;
            }

            if (variant.resource != null)
            {
                node.resource = variant.resource;
            }

            if (variant.resourceCandidates != null &&
                variant.resourceCandidates.Count > 0)
            {
                node.resourceCandidates = new List<string>(
                    variant.resourceCandidates);
            }
        }

        private static int OptimizeChildren(List<UIEffectNode> children)
        {
            if (children == null || children.Count == 0)
            {
                return 0;
            }

            int collapsedNodeCount = 0;
            foreach (UIEffectNode child in children)
            {
                collapsedNodeCount += OptimizeChildren(child.children);
            }

            for (int start = 0; start < children.Count; start++)
            {
                UIEffectNode first = children[start];
                if (first == null || first.repeatCount > 1 ||
                    !TryGetRepeatName(first.name, out string baseName,
                        out int firstIndex, out int digits) ||
                    firstIndex != 1)
                {
                    continue;
                }

                var group = new List<UIEffectNode> { first };
                int end = start + 1;
                while (end < children.Count &&
                       TryGetRepeatName(children[end].name,
                           out string nextBase, out int nextIndex,
                           out int nextDigits) &&
                       string.Equals(baseName, nextBase,
                           StringComparison.Ordinal) &&
                       nextDigits == digits &&
                       nextIndex == group.Count + 1 &&
                       HaveRepeatShape(first, children[end],
                           firstIndex, nextIndex))
                {
                    group.Add(children[end]);
                    end++;
                }

                if (group.Count < 2 || !TryGetRepeatOffset(
                        group, out float offsetX, out float offsetY))
                {
                    continue;
                }

                CollectVariants(first, group, firstIndex);
                StripRepeatSuffix(first, firstIndex);
                first.repeatCount = group.Count;
                first.repeatOffsetX = offsetX;
                first.repeatOffsetY = offsetY;
                first.repeatNameDigits = digits;
                children.RemoveRange(start + 1, group.Count - 1);
                collapsedNodeCount += group.Count - 1;
            }

            return collapsedNodeCount;
        }

        private static bool HaveRepeatShape(
            UIEffectNode first,
            UIEffectNode candidate,
            int firstIndex,
            int candidateIndex)
        {
            if (first == null || candidate == null ||
                !string.Equals(first.type, candidate.type,
                    StringComparison.OrdinalIgnoreCase) ||
                first.children.Count != candidate.children.Count ||
                !Mathf.Approximately(first.width, candidate.width) ||
                !Mathf.Approximately(first.height, candidate.height) ||
                !string.Equals(first.anchor, candidate.anchor,
                    StringComparison.OrdinalIgnoreCase) ||
                !Mathf.Approximately(first.fontSize, candidate.fontSize) ||
                !string.Equals(first.alignment, candidate.alignment,
                    StringComparison.OrdinalIgnoreCase) ||
                first.bold != candidate.bold ||
                first.intentionalColor != candidate.intentionalColor ||
                first.preserveAspect != candidate.preserveAspect ||
                first.sliced != candidate.sliced ||
                first.raycastTarget != candidate.raycastTarget ||
                !string.Equals(first.scrollDirection,
                    candidate.scrollDirection,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(first.binding,
                    candidate.binding,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (int index = 0; index < first.children.Count; index++)
            {
                UIEffectNode firstChild = first.children[index];
                UIEffectNode candidateChild = candidate.children[index];
                if (!NamesMatchRepeat(
                        firstChild.name,
                        candidateChild.name,
                        firstIndex,
                        candidateIndex) ||
                    !Mathf.Approximately(firstChild.x, candidateChild.x) ||
                    !Mathf.Approximately(firstChild.y, candidateChild.y) ||
                    !HaveRepeatShape(
                        firstChild,
                        candidateChild,
                        firstIndex,
                        candidateIndex))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetRepeatOffset(
            List<UIEffectNode> group,
            out float offsetX,
            out float offsetY)
        {
            offsetX = (group[group.Count - 1].x - group[0].x) /
                      (group.Count - 1);
            offsetY = (group[group.Count - 1].y - group[0].y) /
                      (group.Count - 1);
            for (int index = 1; index < group.Count; index++)
            {
                float expectedX = group[0].x + offsetX * index;
                float expectedY = group[0].y + offsetY * index;
                if (Mathf.Abs(group[index].x - expectedX) > 3f ||
                    Mathf.Abs(group[index].y - expectedY) > 3f)
                {
                    return false;
                }
            }

            offsetX = Mathf.Round(offsetX * 1000f) / 1000f;
            offsetY = Mathf.Round(offsetY * 1000f) / 1000f;
            return true;
        }

        private static void CollectVariants(
            UIEffectNode template,
            List<UIEffectNode> instances,
            int templateIndex)
        {
            for (int index = 1; index < instances.Count; index++)
            {
                UIEffectNode candidate = instances[index];
                var variant = new UIEffectNodeVariant
                {
                    index = index + 1,
                };
                bool changed = false;
                changed |= SetVariant(
                    template.text, candidate.text,
                    value => variant.text = value);
                changed |= SetVariant(
                    template.color, candidate.color,
                    value => variant.color = value);
                changed |= SetVariant(
                    template.semantic, candidate.semantic,
                    value => variant.semantic = value);
                changed |= SetVariant(
                    template.resource, candidate.resource,
                    value => variant.resource = value);
                if (!ListsEqual(
                        template.resourceCandidates,
                        candidate.resourceCandidates))
                {
                    variant.resourceCandidates = new List<string>(
                        candidate.resourceCandidates);
                    changed = true;
                }

                if (changed)
                {
                    template.variants.Add(variant);
                }
            }

            for (int childIndex = 0;
                 childIndex < template.children.Count;
                 childIndex++)
            {
                var childInstances = new List<UIEffectNode>();
                foreach (UIEffectNode instance in instances)
                {
                    childInstances.Add(instance.children[childIndex]);
                }

                CollectVariants(
                    template.children[childIndex],
                    childInstances,
                    templateIndex);
            }
        }

        private static bool SetVariant(
            string template,
            string candidate,
            Action<string> setter)
        {
            if (string.Equals(template, candidate,
                    StringComparison.Ordinal))
            {
                return false;
            }

            setter(candidate ?? string.Empty);
            return true;
        }

        private static bool ListsEqual(
            List<string> first,
            List<string> second)
        {
            first ??= new List<string>();
            second ??= new List<string>();
            if (first.Count != second.Count)
            {
                return false;
            }

            for (int index = 0; index < first.Count; index++)
            {
                if (!string.Equals(first[index], second[index],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static void StripRepeatSuffix(
            UIEffectNode node,
            int repeatIndex)
        {
            if (TryGetRepeatName(node.name, out string baseName,
                    out int nodeIndex, out _) &&
                nodeIndex == repeatIndex)
            {
                node.name = baseName;
            }

            foreach (UIEffectNode child in node.children)
            {
                StripRepeatSuffix(child, repeatIndex);
            }
        }

        private static bool NamesMatchRepeat(
            string first,
            string second,
            int firstIndex,
            int secondIndex)
        {
            return TryGetRepeatName(first, out string firstBase,
                       out int parsedFirstIndex, out _) &&
                   TryGetRepeatName(second, out string secondBase,
                       out int parsedSecondIndex, out _) &&
                   parsedFirstIndex == firstIndex &&
                   parsedSecondIndex == secondIndex &&
                   string.Equals(firstBase, secondBase,
                       StringComparison.Ordinal);
        }

        private static bool TryGetRepeatName(
            string value,
            out string baseName,
            out int index,
            out int digits)
        {
            index = 0;
            Match match = Regex.Match(
                value ?? string.Empty,
                "^(.*?)([0-9]{2,})$");
            if (!match.Success ||
                !int.TryParse(match.Groups[2].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out index))
            {
                baseName = string.Empty;
                digits = 0;
                return false;
            }

            baseName = match.Groups[1].Value;
            digits = match.Groups[2].Value.Length;
            return baseName.Length > 0;
        }

        private static void ValidateNode(
            UIEffectNode node,
            string parentPath,
            HashSet<string> paths,
            bool legacyAutoBinding)
        {
            if (node == null)
            {
                throw new InvalidOperationException(
                    $"{parentPath} 包含空 UI 节点。");
            }

            node.name = node.name?.Trim() ?? string.Empty;
            node.type = node.type?.Trim() ?? string.Empty;
            node.semantic = node.semantic?.Trim() ?? string.Empty;
            node.anchor = string.IsNullOrWhiteSpace(node.anchor)
                ? "Auto"
                : node.anchor.Trim();
            node.text ??= string.Empty;
            node.alignment = string.IsNullOrWhiteSpace(node.alignment)
                ? "Center"
                : node.alignment.Trim();
            node.color = node.color?.Trim() ?? string.Empty;
            node.resource = node.resource?.Trim() ?? string.Empty;
            node.scrollDirection =
                string.IsNullOrWhiteSpace(node.scrollDirection)
                    ? "Vertical"
                    : node.scrollDirection.Trim();
            node.binding = string.IsNullOrWhiteSpace(node.binding)
                ? "Auto"
                : node.binding.Trim();
            if (legacyAutoBinding &&
                string.Equals(
                    node.binding,
                    "Auto",
                    StringComparison.OrdinalIgnoreCase))
            {
                node.binding = "Yes";
            }
            node.resourceCandidates ??= new List<string>();
            node.variants ??= new List<UIEffectNodeVariant>();
            node.children ??= new List<UIEffectNode>();

            if (node.repeatCount <= 0)
            {
                node.repeatCount = 1;
            }

            if (node.repeatNameDigits <= 0)
            {
                node.repeatNameDigits = 2;
            }

            if (node.fontSize <= 0f)
            {
                node.fontSize = 32f;
            }

            if (node.name.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{parentPath} 包含未命名 UI 节点。");
            }

            if (!SupportedTypes.Contains(node.type))
            {
                throw new InvalidOperationException(
                    $"节点 {parentPath}/{node.name} 使用了不支持的类型：" +
                    node.type);
            }

            if (!SupportedAnchors.Contains(node.anchor))
            {
                throw new InvalidOperationException(
                    $"节点 {parentPath}/{node.name} 使用了不支持的锚点：" +
                    node.anchor);
            }

            if (node.type.Equals(
                    "ScrollRect",
                    StringComparison.OrdinalIgnoreCase) &&
                !SupportedScrollDirections.Contains(
                    node.scrollDirection))
            {
                throw new InvalidOperationException(
                    $"节点 {parentPath}/{node.name} 使用了不支持的滚动方向：" +
                    node.scrollDirection);
            }

            if (!SupportedBindings.Contains(node.binding))
            {
                throw new InvalidOperationException(
                    $"节点 {parentPath}/{node.name} 使用了不支持的绑定策略：" +
                    node.binding);
            }

            if (node.width <= 0f || node.height <= 0f)
            {
                throw new InvalidOperationException(
                    $"节点 {parentPath}/{node.name} 的宽高必须大于 0。");
            }

            if (node.repeatCount > 100)
            {
                throw new InvalidOperationException(
                    $"节点 {parentPath}/{node.name} 的 repeatCount 不能超过 100。");
            }

            var variantIndices = new HashSet<int>();
            foreach (UIEffectNodeVariant variant in node.variants)
            {
                if (variant == null ||
                    variant.index <= 0 ||
                    variant.index > 100 ||
                    !variantIndices.Add(variant.index))
                {
                    throw new InvalidOperationException(
                        $"节点 {parentPath}/{node.name} 包含无效或重复的变体索引。");
                }

                if (variant.resourceCandidates != null &&
                    variant.resourceCandidates.Count == 0)
                {
                    variant.resourceCandidates = null;
                }
            }

            string path = parentPath + "/" + node.name;
            if (!paths.Add(path))
            {
                throw new InvalidOperationException(
                    $"UISchema 节点路径重复：{path}。");
            }

            for (int index = 0;
                 index < node.resourceCandidates.Count;
                 index++)
            {
                node.resourceCandidates[index] =
                    node.resourceCandidates[index]?.Trim() ??
                    string.Empty;
            }

            for (int index = 0;
                 index < node.children.Count;
                 index++)
            {
                ValidateNode(
                    node.children[index],
                    path,
                    paths,
                    legacyAutoBinding);
            }
        }

        private sealed class CompactJsonWriter
        {
            private readonly StringBuilder builder = new StringBuilder();

            public override string ToString()
            {
                return builder.ToString();
            }

            public void WriteSchema(UIEffectSchema schema)
            {
                builder.Append('{');
                bool first = true;
                WriteString(ref first, "version", schema.version);
                WriteString(ref first, "name", schema.name);
                WriteNumber(ref first, "designWidth", schema.designWidth);
                WriteNumber(ref first, "designHeight", schema.designHeight);
                if (!string.IsNullOrWhiteSpace(schema.referenceImage))
                {
                    WriteString(
                        ref first,
                        "referenceImage",
                        schema.referenceImage);
                }

                WriteNodes(ref first, "children", schema.children);
                builder.Append('}');
            }

            private void WriteNode(UIEffectNode node)
            {
                builder.Append('{');
                bool first = true;
                WriteString(ref first, "name", node.name);
                WriteString(ref first, "type", node.type);
                if (!string.IsNullOrWhiteSpace(node.semantic))
                {
                    WriteString(ref first, "semantic", node.semantic);
                }

                if (!string.Equals(
                        node.anchor,
                        "Auto",
                        StringComparison.OrdinalIgnoreCase))
                {
                    WriteString(ref first, "anchor", node.anchor);
                }

                if (!Mathf.Approximately(node.x, 0f))
                {
                    WriteNumber(ref first, "x", node.x);
                }

                if (!Mathf.Approximately(node.y, 0f))
                {
                    WriteNumber(ref first, "y", node.y);
                }

                WriteNumber(ref first, "width", node.width);
                WriteNumber(ref first, "height", node.height);
                if (!string.IsNullOrEmpty(node.text))
                {
                    WriteString(ref first, "text", node.text);
                }

                if (!Mathf.Approximately(node.fontSize, 32f))
                {
                    WriteNumber(ref first, "fontSize", node.fontSize);
                }

                if (!string.Equals(
                        node.alignment,
                        "Center",
                        StringComparison.OrdinalIgnoreCase))
                {
                    WriteString(ref first, "alignment", node.alignment);
                }

                WriteTrue(ref first, "bold", node.bold);
                if (!string.IsNullOrWhiteSpace(node.color))
                {
                    WriteString(ref first, "color", node.color);
                }

                if (!string.IsNullOrWhiteSpace(node.resource))
                {
                    WriteString(ref first, "resource", node.resource);
                }

                if (node.resourceCandidates != null &&
                    node.resourceCandidates.Count > 0)
                {
                    WriteStrings(
                        ref first,
                        "resourceCandidates",
                        node.resourceCandidates);
                }

                WriteTrue(
                    ref first,
                    "intentionalColor",
                    node.intentionalColor);
                WriteTrue(
                    ref first,
                    "preserveAspect",
                    node.preserveAspect);
                WriteTrue(ref first, "sliced", node.sliced);
                WriteTrue(
                    ref first,
                    "raycastTarget",
                    node.raycastTarget);
                if (!string.Equals(
                        node.scrollDirection,
                        "Vertical",
                        StringComparison.OrdinalIgnoreCase))
                {
                    WriteString(
                        ref first,
                        "scrollDirection",
                        node.scrollDirection);
                }

                if (!string.Equals(
                        node.binding,
                        "Auto",
                        StringComparison.OrdinalIgnoreCase))
                {
                    WriteString(ref first, "binding", node.binding);
                }

                if (node.repeatCount > 1)
                {
                    WriteInteger(
                        ref first,
                        "repeatCount",
                        node.repeatCount);
                    if (!Mathf.Approximately(node.repeatOffsetX, 0f))
                    {
                        WriteNumber(
                            ref first,
                            "repeatOffsetX",
                            node.repeatOffsetX);
                    }

                    if (!Mathf.Approximately(node.repeatOffsetY, 0f))
                    {
                        WriteNumber(
                            ref first,
                            "repeatOffsetY",
                            node.repeatOffsetY);
                    }

                    if (node.repeatNameDigits != 2)
                    {
                        WriteInteger(
                            ref first,
                            "repeatNameDigits",
                            node.repeatNameDigits);
                    }
                }

                if (node.variants != null && node.variants.Count > 0)
                {
                    WriteVariants(ref first, node.variants);
                }

                if (node.children != null && node.children.Count > 0)
                {
                    WriteNodes(ref first, "children", node.children);
                }

                builder.Append('}');
            }

            private void WriteVariants(
                ref bool first,
                List<UIEffectNodeVariant> variants)
            {
                WritePropertyName(ref first, "variants");
                builder.Append('[');
                for (int index = 0; index < variants.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }

                    UIEffectNodeVariant variant = variants[index];
                    builder.Append('{');
                    bool firstProperty = true;
                    WriteInteger(
                        ref firstProperty,
                        "index",
                        variant.index);
                    WriteNullableString(
                        ref firstProperty,
                        "text",
                        variant.text);
                    WriteNullableString(
                        ref firstProperty,
                        "color",
                        variant.color);
                    WriteNullableString(
                        ref firstProperty,
                        "semantic",
                        variant.semantic);
                    WriteNullableString(
                        ref firstProperty,
                        "resource",
                        variant.resource);
                    if (variant.resourceCandidates != null &&
                        variant.resourceCandidates.Count > 0)
                    {
                        WriteStrings(
                            ref firstProperty,
                            "resourceCandidates",
                            variant.resourceCandidates);
                    }

                    builder.Append('}');
                }

                builder.Append(']');
            }

            private void WriteNodes(
                ref bool first,
                string name,
                List<UIEffectNode> nodes)
            {
                WritePropertyName(ref first, name);
                builder.Append('[');
                for (int index = 0; index < nodes.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }

                    WriteNode(nodes[index]);
                }

                builder.Append(']');
            }

            private void WriteStrings(
                ref bool first,
                string name,
                List<string> values)
            {
                WritePropertyName(ref first, name);
                builder.Append('[');
                for (int index = 0; index < values.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }

                    WriteEscaped(values[index] ?? string.Empty);
                }

                builder.Append(']');
            }

            private void WriteString(
                ref bool first,
                string name,
                string value)
            {
                WritePropertyName(ref first, name);
                WriteEscaped(value ?? string.Empty);
            }

            private void WriteNullableString(
                ref bool first,
                string name,
                string value)
            {
                if (value != null)
                {
                    WriteString(ref first, name, value);
                }
            }

            private void WriteNumber(
                ref bool first,
                string name,
                float value)
            {
                WritePropertyName(ref first, name);
                builder.Append(value.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture));
            }

            private void WriteInteger(
                ref bool first,
                string name,
                int value)
            {
                WritePropertyName(ref first, name);
                builder.Append(value.ToString(
                    CultureInfo.InvariantCulture));
            }

            private void WriteTrue(
                ref bool first,
                string name,
                bool value)
            {
                if (!value)
                {
                    return;
                }

                WritePropertyName(ref first, name);
                builder.Append("true");
            }

            private void WritePropertyName(
                ref bool first,
                string name)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                WriteEscaped(name);
                builder.Append(':');
            }

            private void WriteEscaped(string value)
            {
                builder.Append('"');
                foreach (char character in value)
                {
                    switch (character)
                    {
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '\b':
                            builder.Append("\\b");
                            break;
                        case '\f':
                            builder.Append("\\f");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            if (character < 0x20)
                            {
                                builder.Append("\\u");
                                builder.Append(((int)character).ToString(
                                    "x4",
                                    CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                builder.Append(character);
                            }

                            break;
                    }
                }

                builder.Append('"');
            }
        }
    }
}
#endif
