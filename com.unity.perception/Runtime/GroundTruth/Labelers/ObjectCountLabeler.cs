﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine.UI;

namespace UnityEngine.Perception.GroundTruth
{
    /// <summary>
    /// Labeler which produces object counts for each label in the associated <see cref="IdLabelConfig"/> each frame.
    /// </summary>
    [Serializable]
    public sealed class ObjectCountLabeler : CameraLabeler
    {
        /// <summary>
        /// The ID to use for object count annotations in the resulting dataset
        /// </summary>
        public string objectCountMetricId = "51da3c27-369d-4929-aea6-d01614635ce2";

        /// <summary>
        /// The <see cref="IdLabelConfig"/> which associates objects with labels.
        /// </summary>
        public IdLabelConfig labelConfig => m_LabelConfig;

        /// <summary>
        /// Fired when the object counts are computed for a frame.
        /// </summary>
        public event Action<int, NativeSlice<uint>,IReadOnlyList<IdLabelEntry>> ObjectCountsComputed;

        [SerializeField]
        IdLabelConfig m_LabelConfig;

        static ProfilerMarker s_ClassCountCallback = new ProfilerMarker("OnClassLabelsReceived");

        ClassCountValue[] m_ClassCountValues;

        Dictionary<int, AsyncMetric> m_ObjectCountAsyncMetrics;
        MetricDefinition m_ObjectCountMetricDefinition;

        List<string> vizEntries = null;

        /// <summary>
        /// Creates a new ObjectCountLabeler. This constructor should only be used by serialization. For creation from
        /// user code, use <see cref="ObjectCountLabeler(IdLabelConfig)"/>.
        /// </summary>
        public ObjectCountLabeler()
        {
        }

        /// <summary>
        /// Creates a new ObjectCountLabeler with the given <see cref="IdLabelConfig"/>.
        /// </summary>
        /// <param name="labelConfig">The label config for resolving the label for each object.</param>
        public ObjectCountLabeler(IdLabelConfig labelConfig)
        {
            if (labelConfig == null)
                throw new ArgumentNullException(nameof(labelConfig));

            m_LabelConfig = labelConfig;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        struct ClassCountValue
        {
            public int label_id;
            public string label_name;
            public uint count;
        }

        /// <inheritdoc/>
        protected override bool supportsVisualization => true;

        /// <inheritdoc/>
        protected override void Setup()
        {
            if (labelConfig == null)
                throw new InvalidOperationException("The ObjectCountLabeler idLabelConfig field must be assigned");

            m_ObjectCountAsyncMetrics =  new Dictionary<int, AsyncMetric>();

            perceptionCamera.RenderedObjectInfosCalculated += (frameCount, objectInfo) =>
            {
                NativeArray<uint> objectCounts = ComputeObjectCounts(objectInfo);
                ObjectCountsComputed?.Invoke(frameCount, objectCounts, labelConfig.labelEntries);
                ProduceObjectCountMetric(objectCounts, m_LabelConfig.labelEntries, frameCount);
            };

            visualizationEnabled = supportsVisualization;
        }

        /// <inheritdoc/>
        protected override void OnBeginRendering()
        {
            if (m_ObjectCountMetricDefinition.Equals(default))
            {
                m_ObjectCountMetricDefinition = DatasetCapture.RegisterMetricDefinition("object count",
                    m_LabelConfig.GetAnnotationSpecification(),
                    "Counts of objects for each label in the sensor's view", id: new Guid(objectCountMetricId));
            }

            m_ObjectCountAsyncMetrics[Time.frameCount] = perceptionCamera.SensorHandle.ReportMetricAsync(m_ObjectCountMetricDefinition);
        }

        NativeArray<uint> ComputeObjectCounts(NativeArray<RenderedObjectInfo> objectInfo)
        {
            var objectCounts = new NativeArray<uint>(m_LabelConfig.labelEntries.Count, Allocator.Temp);
            foreach (var info in objectInfo)
            {
                if (!m_LabelConfig.TryGetLabelEntryFromInstanceId(info.instanceId, out _, out var labelIndex))
                    continue;

                objectCounts[labelIndex]++;
            }

            return objectCounts;
        }

        void ProduceObjectCountMetric(NativeSlice<uint> counts, IReadOnlyList<IdLabelEntry> entries, int frameCount)
        {
            using (s_ClassCountCallback.Auto())
            {
                if (!m_ObjectCountAsyncMetrics.TryGetValue(frameCount, out var classCountAsyncMetric))
                    return;

                m_ObjectCountAsyncMetrics.Remove(frameCount);

                if (m_ClassCountValues == null || m_ClassCountValues.Length != entries.Count)
                    m_ClassCountValues = new ClassCountValue[entries.Count];

                bool visualize = visualizationEnabled;
                if (visualize && vizEntries == null)
                {
                    vizEntries = new List<string>();
                }

                for (var i = 0; i < entries.Count; i++)
                {
                    m_ClassCountValues[i] = new ClassCountValue()
                    {
                        label_id = entries[i].id,
                        label_name = entries[i].label,
                        count = counts[i]
                    };

                    if (visualize)
                    {
                        var label = entries[i].label + " Counts";

                        hudPanel.UpdateEntry(label, counts[i].ToString());
                        vizEntries.Add(label);
                    }
                }

                classCountAsyncMetric.ReportValues(m_ClassCountValues);
            }
        }

        /// <inheritdoc/>
        protected override void PopulateVisualizationPanel(ControlPanel panel)
        {
            panel.AddToggleControl("Object Counts", enabled => { visualizationEnabled = enabled; });
        }

        /// <inheritdoc/>
        override protected void OnVisualizerEnabledChanged(bool enabled)
        {
            if (!enabled)
            {
                hudPanel.RemoveEntries(vizEntries);
                vizEntries.Clear();
            }
        }
    }
}
